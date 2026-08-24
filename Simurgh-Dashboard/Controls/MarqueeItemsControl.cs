// ============================================================================
// File: MarqueeInfrastructure.cs
// Target: .NET 8.0 / .NET 9.0 (WPF)
// Notes: Thread-safe batch store, high-performance sequence duplicator,
//        GPU-accelerated TranslateTransform with visual anchor preservation.
// ============================================================================

using System.Collections;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace SimurghDashboard.Controls
{
    #region Enums & Interfaces

    /// <summary>
    /// Defines horizontal scrolling direction for the marquee sequence.
    /// </summary>
    public enum MarqueeDirection
    {
        RightToLeft = 0,
        LeftToRight = 1
    }



    #endregion

    #region Internal Layout & Tracking Records

    /// <summary>
    /// Holds pre-calculated layout metrics for single cycle items.
    /// </summary>
    internal sealed record LogicalMetric(
        object Item,
        int Index,
        double StartOffset,
        double ItemWidth);

    /// <summary>
    /// Captures the relative position of the item visible on screen before rebuilding.
    /// </summary>
    internal sealed record VisualAnchor(
        object Item,
        int Index,
        double InternalItemOffset);

    #endregion


    #region MarqueeItemsControl Implementation

    /// <summary>
    /// High-performance marquee control using sequence duplication and GPU transform animation.
    /// Fully marshals collection changes and maintains screen positioning across dynamic mutations.
    /// </summary>
    [TemplatePart(Name = PartViewport, Type = typeof(FrameworkElement))]
    [TemplatePart(Name = PartHost, Type = typeof(StackPanel))]
    [TemplatePart(Name = PartAlertContainer, Type = typeof(ContentPresenter))]
    public class MarqueeItemsControl : Control
    {
        public const string PartViewport = "PART_Viewport";
        public const string PartHost = "PART_Host";
        public const string PartAlertContainer = "PART_AlertContainer";

        #region Dependency Properties

        public static readonly DependencyProperty ItemsSourceProperty =
            DependencyProperty.Register(
                nameof(ItemsSource),
                typeof(IEnumerable),
                typeof(MarqueeItemsControl),
                new FrameworkPropertyMetadata(null, OnItemsSourceChanged));

        public static readonly DependencyProperty ItemTemplateProperty =
            DependencyProperty.Register(
                nameof(ItemTemplate),
                typeof(DataTemplate),
                typeof(MarqueeItemsControl),
                new FrameworkPropertyMetadata(null, OnTemplateOrVisualPropertyChanged));

        public static readonly DependencyProperty SeparatorTemplateProperty =
            DependencyProperty.Register(
                nameof(SeparatorTemplate),
                typeof(DataTemplate),
                typeof(MarqueeItemsControl),
                new FrameworkPropertyMetadata(null, OnTemplateOrVisualPropertyChanged));

        public static readonly DependencyProperty DirectionProperty =
            DependencyProperty.Register(
                nameof(Direction),
                typeof(MarqueeDirection),
                typeof(MarqueeItemsControl),
                new FrameworkPropertyMetadata(MarqueeDirection.RightToLeft, OnSpeedOrDirectionChanged));

        public static readonly DependencyProperty ScrollSpeedProperty =
            DependencyProperty.Register(
                nameof(ScrollSpeed),
                typeof(double),
                typeof(MarqueeItemsControl),
                new FrameworkPropertyMetadata(60.0, OnSpeedOrDirectionChanged, CoerceScrollSpeed));

        public static readonly DependencyProperty ItemFinishedCommandProperty =
            DependencyProperty.Register(
                nameof(ItemFinishedCommand),
                typeof(ICommand),
                typeof(MarqueeItemsControl),
                new FrameworkPropertyMetadata(null));

        public IEnumerable? ItemsSource
        {
            get => (IEnumerable?)GetValue(ItemsSourceProperty);
            set => SetValue(ItemsSourceProperty, value);
        }

        public DataTemplate? ItemTemplate
        {
            get => (DataTemplate?)GetValue(ItemTemplateProperty);
            set => SetValue(ItemTemplateProperty, value);
        }

        public DataTemplate? SeparatorTemplate
        {
            get => (DataTemplate?)GetValue(SeparatorTemplateProperty);
            set => SetValue(SeparatorTemplateProperty, value);
        }

        public MarqueeDirection Direction
        {
            get => (MarqueeDirection)GetValue(DirectionProperty);
            set => SetValue(DirectionProperty, value);
        }

        public double ScrollSpeed
        {
            get => (double)GetValue(ScrollSpeedProperty);
            set => SetValue(ScrollSpeedProperty, value);
        }

        public ICommand? ItemFinishedCommand
        {
            get => (ICommand?)GetValue(ItemFinishedCommandProperty);
            set => SetValue(ItemFinishedCommandProperty, value);
        }

        #endregion

        #region Private Fields

        private readonly TranslateTransform _hostTransform = new();
        private readonly List<LogicalMetric> _logicalMetrics = [];

        private FrameworkElement? _viewport;
        private StackPanel? _host;
        private ContentPresenter? _alertContainer;

        private INotifyCollectionChanged? _observedCollection;
        private DispatcherOperation? _pendingRebuildOperation;

        private bool _isLoaded;
        private bool _isRebuilding;
        private bool _rebuildRequested;
        private bool _preserveAnchorRequested;

        private double _cycleLength;
        private double _currentLogicalOffset;

        #endregion

        static MarqueeItemsControl()
        {
            DefaultStyleKeyProperty.OverrideMetadata(
                typeof(MarqueeItemsControl),
                new FrameworkPropertyMetadata(typeof(MarqueeItemsControl)));
        }

        public MarqueeItemsControl()
        {
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
            SizeChanged += OnSizeChanged;
        }

        #region Template & Lifecycle Handlers

        public override void OnApplyTemplate()
        {
            // Halt running animations before tearing down visual components
            StopAnimation(captureCurrentOffset: true);

            _host = null;
            _viewport = null;
            _alertContainer = null;

            base.OnApplyTemplate();

            _viewport = GetTemplateChild(PartViewport) as FrameworkElement;
            _host = GetTemplateChild(PartHost) as StackPanel;
            _alertContainer = GetTemplateChild(PartAlertContainer) as ContentPresenter;

            if (_host != null)
            {
                // Ensure Transform is isolated on compositor thread
                _host.RenderTransform = _hostTransform;
                _host.RenderTransformOrigin = new Point(0, 0);
            }

            if (_isLoaded)
            {
                RequestRebuild(preserveAnchor: false);
            }
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            _isLoaded = true;

            // Wire notifications and trigger layout pipeline
            AttachCollection(ItemsSource);
            RequestRebuild(preserveAnchor: false);
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            _isLoaded = false;

            StopAnimation(captureCurrentOffset: true);

            if (_pendingRebuildOperation is { Status: DispatcherOperationStatus.Pending })
            {
                _pendingRebuildOperation.Abort();
            }

            _pendingRebuildOperation = null;
            _rebuildRequested = false;
            _preserveAnchorRequested = false;

            DetachCollection();
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (!_isLoaded || _host == null || _viewport == null)
            {
                return;
            }

            if (e.WidthChanged || e.HeightChanged)
            {
                RequestRebuild(preserveAnchor: true);
            }
        }

        #endregion

        #region Dependency Property Callbacks

        private static void OnItemsSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (MarqueeItemsControl)d;

            control.DetachCollection();
            control.AttachCollection(e.NewValue);
            control.RequestRebuild(preserveAnchor: false);
        }

        private static void OnTemplateOrVisualPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (MarqueeItemsControl)d;
            control.RequestRebuild(preserveAnchor: true);
        }

        private static void OnSpeedOrDirectionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (MarqueeItemsControl)d;
            if (!control._isLoaded)
            {
                return;
            }

            var currentOffset = control.GetCurrentLogicalOffset();
            control.StopAnimation(captureCurrentOffset: false);
            control.StartContinuousAnimation(currentOffset);
        }

        private static object CoerceScrollSpeed(DependencyObject d, object baseValue)
        {
            if (baseValue is double val)
            {
                if (double.IsNaN(val) || double.IsInfinity(val) || val < 0.0)
                {
                    return 0.0;
                }

                return val;
            }

            return 60.0;
        }

        #endregion

        #region Collection Binding & Rebuild Coalescing

        private void AttachCollection(object? source)
        {
            if (source is not INotifyCollectionChanged collection)
            {
                return;
            }

            if (ReferenceEquals(_observedCollection, collection))
            {
                return;
            }

            DetachCollection();

            _observedCollection = collection;
            _observedCollection.CollectionChanged += OnCollectionChanged;
        }

        private void DetachCollection()
        {
            if (_observedCollection == null)
            {
                return;
            }

            _observedCollection.CollectionChanged -= OnCollectionChanged;
            _observedCollection = null;
        }

        private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (!_isLoaded)
            {
                return;
            }

            // Always request UI marshal to handle background worker updates safely
            RequestRebuild(preserveAnchor: true);
        }

        private void RequestRebuild(bool preserveAnchor)
        {
            if (!_isLoaded)
            {
                return;
            }

            _rebuildRequested = true;
            _preserveAnchorRequested |= preserveAnchor;

            ScheduleDispatcherRebuild();
        }

        private void ScheduleDispatcherRebuild()
        {
            if (_pendingRebuildOperation is { Status: DispatcherOperationStatus.Pending })
            {
                return;
            }

            _pendingRebuildOperation = Dispatcher.BeginInvoke(
                DispatcherPriority.DataBind,
                new Action(ProcessPendingRebuild));
        }

        private void ProcessPendingRebuild()
        {
            _pendingRebuildOperation = null;

            if (!_isLoaded || !_rebuildRequested)
            {
                return;
            }

            var preserveAnchor = _preserveAnchorRequested;
            _rebuildRequested = false;
            _preserveAnchorRequested = false;

            RebuildSequenceAndAnimate(preserveAnchor);
        }

        #endregion

        #region Core Duplication Engine & Visual Anchoring

        private void RebuildSequenceAndAnimate(bool preserveAnchor)
        {
            if (_host == null || _viewport == null || _isRebuilding)
            {
                return;
            }

            _isRebuilding = true;

            try
            {
                // 1. Capture anchor from running GPU state before stopping
                StopAnimation(captureCurrentOffset: true);

                VisualAnchor? anchor = null;
                if (preserveAnchor)
                {
                    anchor = CaptureCurrentAnchor();
                }

                _host.Children.Clear();
                _logicalMetrics.Clear();
                _cycleLength = 0;

                var rawSource = ItemsSource?.Cast<object>().Where(x => x != null).ToList();
                if (rawSource == null || rawSource.Count == 0)
                {
                    _hostTransform.X = 0;
                    _currentLogicalOffset = 0;
                    return;
                }

                // 2. Measure Single Cycle to calculate cycle length
                var singleCyclePresenters = new List<FrameworkElement>();
                double accumulatedWidth = 0;

                for (var i = 0; i < rawSource.Count; i++)
                {
                    var item = rawSource[i];

                    var itemPresenter = CreateItemPresenter(item);
                    itemPresenter.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                    var itemWidth = Math.Max(0, itemPresenter.DesiredSize.Width);

                    _logicalMetrics.Add(new LogicalMetric(item, i, accumulatedWidth, itemWidth));
                    singleCyclePresenters.Add(itemPresenter);
                    accumulatedWidth += itemWidth;

                    // Append separator if template is supplied
                    if (SeparatorTemplate != null)
                    {
                        var separatorPresenter = CreateSeparatorPresenter(item);
                        separatorPresenter.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                        var separatorWidth = Math.Max(0, separatorPresenter.DesiredSize.Width);

                        accumulatedWidth += separatorWidth;
                        singleCyclePresenters.Add(separatorPresenter);
                    }
                }

                _cycleLength = accumulatedWidth;
                if (_cycleLength <= 0.01)
                {
                    _hostTransform.X = 0;
                    _currentLogicalOffset = 0;
                    return;
                }

                // 3. Mount single cycle elements
                foreach (var visual in singleCyclePresenters)
                {
                    _host.Children.Add(visual);
                }

                // 4. Duplicate sequence to cover viewport width and avoid white gaps
                var viewportWidth = Math.Max(_viewport.ActualWidth, ActualWidth);
                if (viewportWidth <= 0)
                {
                    viewportWidth = 1920; // Fallback metric during unrendered initial pass
                }

                var requiredDuplicates = (int)Math.Ceiling(viewportWidth / _cycleLength) + 1;
                for (var d = 0; d < requiredDuplicates; d++)
                {
                    for (var i = 0; i < rawSource.Count; i++)
                    {
                        var item = rawSource[i];
                        _host.Children.Add(CreateItemPresenter(item));

                        if (SeparatorTemplate != null)
                        {
                            _host.Children.Add(CreateSeparatorPresenter(item));
                        }
                    }
                }

                // 5. Calculate anchor and spin GPU animation
                var startOffset = 0.0;
                if (anchor != null)
                {
                    startOffset = RestoreAnchorOffset(anchor);
                }

                StartContinuousAnimation(startOffset);
            }
            finally
            {
                _isRebuilding = false;
            }
        }

        private ContentPresenter CreateItemPresenter(object item)
        {
            return new ContentPresenter
            {
                Content = item,
                ContentTemplate = ItemTemplate,
                VerticalAlignment = VerticalAlignment.Center
            };
        }

        private ContentPresenter CreateSeparatorPresenter(object? context)
        {
            return new ContentPresenter
            {
                Content = context,
                ContentTemplate = SeparatorTemplate,
                VerticalAlignment = VerticalAlignment.Center,
                IsHitTestVisible = false
            };
        }

        #endregion

        #region Animation & GPU Orchestration

        private double GetCurrentLogicalOffset()
        {
            if (_cycleLength <= 0)
            {
                return 0;
            }

            var currentX = _hostTransform.X;
            if (double.IsNaN(currentX) || double.IsInfinity(currentX))
            {
                return _currentLogicalOffset;
            }

            double logicalOffset;
            if (Direction == MarqueeDirection.RightToLeft)
            {
                logicalOffset = -currentX;
            }
            else
            {
                logicalOffset = currentX + _cycleLength;
            }

            logicalOffset %= _cycleLength;
            if (logicalOffset < 0)
            {
                logicalOffset += _cycleLength;
            }

            return logicalOffset;
        }

        private VisualAnchor? CaptureCurrentAnchor()
        {
            if (_logicalMetrics.Count == 0 || _cycleLength <= 0)
            {
                return null;
            }

            var normalizedOffset = GetCurrentLogicalOffset();
            var activeMetric = _logicalMetrics.LastOrDefault(m => m.StartOffset <= normalizedOffset)
                               ?? _logicalMetrics[0];

            var internalItemOffset = normalizedOffset - activeMetric.StartOffset;

            return new VisualAnchor(
                Item: activeMetric.Item,
                Index: activeMetric.Index,
                InternalItemOffset: internalItemOffset);
        }

        private double RestoreAnchorOffset(VisualAnchor anchor)
        {
            if (_logicalMetrics.Count == 0 || _cycleLength <= 0)
            {
                return 0;
            }

            var match = _logicalMetrics.FirstOrDefault(m => ReferenceEquals(m.Item, anchor.Item))
                        ?? _logicalMetrics.FirstOrDefault(m => Equals(m.Item, anchor.Item));

            if (match == null)
            {
                var safeIndex = Math.Clamp(anchor.Index, 0, _logicalMetrics.Count - 1);
                match = _logicalMetrics[safeIndex];
            }

            var internalOffset = Math.Clamp(anchor.InternalItemOffset, 0, Math.Max(0, match.ItemWidth));
            var restoredOffset = (match.StartOffset + internalOffset) % _cycleLength;

            if (restoredOffset < 0)
            {
                restoredOffset += _cycleLength;
            }

            return restoredOffset;
        }

        private void StopAnimation(bool captureCurrentOffset)
        {
            if (captureCurrentOffset && _cycleLength > 0)
            {
                _currentLogicalOffset = GetCurrentLogicalOffset();
            }

            _hostTransform.BeginAnimation(
                TranslateTransform.XProperty,
                null,
                HandoffBehavior.SnapshotAndReplace);
        }

        private void StartContinuousAnimation(double startLogicalOffset)
        {
            if (_cycleLength <= 0 || double.IsNaN(ScrollSpeed) || double.IsInfinity(ScrollSpeed) || ScrollSpeed <= 0)
            {
                _hostTransform.X = 0;
                return;
            }

            startLogicalOffset %= _cycleLength;
            if (startLogicalOffset < 0)
            {
                startLogicalOffset += _cycleLength;
            }

            _currentLogicalOffset = startLogicalOffset;

            var remainingDistance = Math.Max(0.001, _cycleLength - startLogicalOffset);
            var durationSeconds = remainingDistance / ScrollSpeed;

            var fromX = Direction == MarqueeDirection.RightToLeft
                ? -startLogicalOffset
                : startLogicalOffset - _cycleLength;

            var toX = Direction == MarqueeDirection.RightToLeft
                ? -_cycleLength
                : 0;

            var initialAnimation = new DoubleAnimation
            {
                From = fromX,
                To = toX,
                Duration = TimeSpan.FromSeconds(Math.Max(0.01, durationSeconds)),
                FillBehavior = FillBehavior.Stop
            };

            initialAnimation.Completed += OnInitialAnimationCompleted;

            _hostTransform.BeginAnimation(
                TranslateTransform.XProperty,
                initialAnimation,
                HandoffBehavior.SnapshotAndReplace);
        }

        private void OnInitialAnimationCompleted(object? sender, EventArgs e)
        {
            if (!_isLoaded || _isRebuilding || _cycleLength <= 0 || ScrollSpeed <= 0)
            {
                return;
            }

            var fullCycleDuration = _cycleLength / ScrollSpeed;

            var loopAnimation = new DoubleAnimation
            {
                From = Direction == MarqueeDirection.RightToLeft ? 0 : -_cycleLength,
                To = Direction == MarqueeDirection.RightToLeft ? -_cycleLength : 0,
                Duration = TimeSpan.FromSeconds(Math.Max(0.01, fullCycleDuration)),
                RepeatBehavior = RepeatBehavior.Forever,
                FillBehavior = FillBehavior.HoldEnd
            };

            _hostTransform.BeginAnimation(
                TranslateTransform.XProperty,
                loopAnimation,
                HandoffBehavior.SnapshotAndReplace);
        }

        #endregion
    }

    #endregion
}
