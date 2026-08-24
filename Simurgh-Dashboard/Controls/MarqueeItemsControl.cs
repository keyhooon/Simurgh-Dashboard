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
using System.Diagnostics;

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
        private int _rebuildSequence;

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
            Trace("OnApplyTemplate started.");

            StopAnimation(captureCurrentOffset: true);

            _host = null;
            _viewport = null;
            _alertContainer = null;

            base.OnApplyTemplate();

            _viewport = GetTemplateChild(PartViewport) as FrameworkElement;
            _host = GetTemplateChild(PartHost) as StackPanel;
            _alertContainer = GetTemplateChild(PartAlertContainer) as ContentPresenter;

            Trace(
                $"Template parts resolved. " +
                $"Viewport={_viewport != null}, " +
                $"Host={_host != null}, " +
                $"AlertContainer={_alertContainer != null}, " +
                $"ItemTemplate={ItemTemplate != null}, " +
                $"SeparatorTemplate={SeparatorTemplate != null}.");

            if (_viewport == null)
            {
                Trace($"ERROR: Template part '{PartViewport}' was not found.");
            }

            if (_host == null)
            {
                Trace($"ERROR: Template part '{PartHost}' was not found or is not a StackPanel.");
            }
            else
            {
                _host.RenderTransform = _hostTransform;
                _host.RenderTransformOrigin = new Point(0, 0);

                Trace(
                    $"Host configured. Orientation={_host.Orientation}, " +
                    $"Children={_host.Children.Count}.");
            }

            if (_isLoaded)
            {
                Trace("Control is already loaded. Requesting rebuild after template application.");
                RequestRebuild(preserveAnchor: false);
            }
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            _isLoaded = true;

            Trace(
                $"Loaded. ActualSize={ActualWidth:0.##}x{ActualHeight:0.##}, " +
                $"ItemsSource={Describe(ItemsSource)}.");

            AttachCollection(ItemsSource);
            RequestRebuild(preserveAnchor: false);
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            Trace("Unloaded.");

            _isLoaded = false;

            StopAnimation(captureCurrentOffset: true);

            if (_pendingRebuildOperation is { Status: DispatcherOperationStatus.Pending })
            {
                Trace("Aborting pending rebuild operation.");
                _pendingRebuildOperation.Abort();
            }

            _pendingRebuildOperation = null;
            _rebuildRequested = false;
            _preserveAnchorRequested = false;

            DetachCollection();
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            Trace(
                $"SizeChanged. Previous={e.PreviousSize.Width:0.##}x{e.PreviousSize.Height:0.##}, " +
                $"New={e.NewSize.Width:0.##}x{e.NewSize.Height:0.##}, " +
                $"Loaded={_isLoaded}, Host={_host != null}, Viewport={_viewport != null}.");

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

        private static void OnItemsSourceChanged(
            DependencyObject d,
            DependencyPropertyChangedEventArgs e)
        {
            var control = (MarqueeItemsControl)d;

            control.Trace(
                $"ItemsSource changed. Old={Describe(e.OldValue)}, " +
                $"New={Describe(e.NewValue)}.");

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
                Trace(
                    $"ItemsSource does not implement {nameof(INotifyCollectionChanged)}. " +
                    $"Source={Describe(source)}.");
                return;
            }

            if (ReferenceEquals(_observedCollection, collection))
            {
                Trace("Collection is already attached.");
                return;
            }

            DetachCollection();

            _observedCollection = collection;
            _observedCollection.CollectionChanged += OnCollectionChanged;

            Trace($"CollectionChanged handler attached. Collection={Describe(source)}.");
        }

        private void DetachCollection()
        {
            if (_observedCollection == null)
            {
                return;
            }

            _observedCollection.CollectionChanged -= OnCollectionChanged;
            _observedCollection = null;

            Trace("CollectionChanged handler detached.");
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
                Trace($"Rebuild ignored. Loaded=false, PreserveAnchor={preserveAnchor}.");
                return;
            }

            _rebuildRequested = true;
            _preserveAnchorRequested |= preserveAnchor;

            Trace(
                $"Rebuild requested. PreserveAnchor={preserveAnchor}, " +
                $"AggregatedPreserveAnchor={_preserveAnchorRequested}, " +
                $"Pending={_pendingRebuildOperation?.Status.ToString() ?? "<none>"}.");

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
                Trace(
                    $"Pending rebuild skipped. Loaded={_isLoaded}, " +
                    $"Requested={_rebuildRequested}.");
                return;
            }

            var preserveAnchor = _preserveAnchorRequested;
            _rebuildRequested = false;
            _preserveAnchorRequested = false;

            Trace($"Processing rebuild. PreserveAnchor={preserveAnchor}.");

            RebuildSequenceAndAnimate(preserveAnchor);
        }

        #endregion

        #region Core Duplication Engine & Visual Anchoring

        private void RebuildSequenceAndAnimate(bool preserveAnchor)
        {
            var rebuildId = ++_rebuildSequence;

            Trace(
                $"Rebuild #{rebuildId} started. " +
                $"PreserveAnchor={preserveAnchor}, " +
                $"Host={_host != null}, Viewport={_viewport != null}, " +
                $"AlreadyRebuilding={_isRebuilding}.");

            if (_host == null || _viewport == null || _isRebuilding)
            {
                Trace($"Rebuild #{rebuildId} aborted before layout.");
                return;
            }

            _isRebuilding = true;

            try
            {
                StopAnimation(captureCurrentOffset: true);

                VisualAnchor? anchor = null;
                if (preserveAnchor)
                {
                    anchor = CaptureCurrentAnchor();

                    Trace(
                        $"Rebuild #{rebuildId} anchor captured. " +
                        $"Anchor={anchor?.Item?.GetType().Name ?? "<none>"}, " +
                        $"Index={anchor?.Index}, " +
                        $"InternalOffset={anchor?.InternalItemOffset:0.##}.");
                }

                _host.Children.Clear();
                _logicalMetrics.Clear();
                _cycleLength = 0;

                var rawSource = ItemsSource?
                    .Cast<object>()
                    .Where(static x => x != null)
                    .ToList();

                Trace(
                    $"Rebuild #{rebuildId} source resolved. " +
                    $"Items={rawSource?.Count ?? 0}, " +
                    $"ItemTemplate={ItemTemplate != null}, " +
                    $"SeparatorTemplate={SeparatorTemplate != null}.");

                if (rawSource == null || rawSource.Count == 0)
                {
                    _hostTransform.X = 0;
                    _currentLogicalOffset = 0;

                    Trace($"Rebuild #{rebuildId} stopped: source is empty.");
                    return;
                }

                var singleCyclePresenters = new List<FrameworkElement>();
                double accumulatedWidth = 0;

                for (var i = 0; i < rawSource.Count; i++)
                {
                    var item = rawSource[i];

                    var itemPresenter = CreateItemPresenter(item);
                    itemPresenter.Measure(
                        new Size(
                            double.PositiveInfinity,
                            double.PositiveInfinity));

                    var itemWidth = Math.Max(0, itemPresenter.DesiredSize.Width);

                    Trace(
                        $"Rebuild #{rebuildId} item[{i}] measured. " +
                        $"Type={item.GetType().FullName}, " +
                        $"Width={itemWidth:0.##}, " +
                        $"Content={item}.");

                    _logicalMetrics.Add(
                        new LogicalMetric(
                            item,
                            i,
                            accumulatedWidth,
                            itemWidth));

                    singleCyclePresenters.Add(itemPresenter);
                    accumulatedWidth += itemWidth;

                    if (SeparatorTemplate == null)
                    {
                        continue;
                    }

                    var separatorPresenter = CreateSeparatorPresenter(item);

                    separatorPresenter.Measure(
                        new Size(
                            double.PositiveInfinity,
                            double.PositiveInfinity));

                    var separatorWidth = Math.Max(
                        0,
                        separatorPresenter.DesiredSize.Width);

                    Trace(
                        $"Rebuild #{rebuildId} separator after item[{i}] measured. " +
                        $"Width={separatorWidth:0.##}.");

                    accumulatedWidth += separatorWidth;
                    singleCyclePresenters.Add(separatorPresenter);
                }

                _cycleLength = accumulatedWidth;

                Trace(
                    $"Rebuild #{rebuildId} first cycle measured. " +
                    $"CycleLength={_cycleLength:0.##}, " +
                    $"FirstCycleVisuals={singleCyclePresenters.Count}.");

                if (_cycleLength <= 0.01)
                {
                    _hostTransform.X = 0;
                    _currentLogicalOffset = 0;

                    Trace(
                        $"ERROR: Rebuild #{rebuildId} stopped because CycleLength is zero. " +
                        "Usually ItemTemplate is missing, template content has zero width, " +
                        "or the item data is not rendered.");

                    return;
                }

                foreach (var visual in singleCyclePresenters)
                {
                    _host.Children.Add(visual);
                }

                var viewportWidth = Math.Max(
                    _viewport.ActualWidth,
                    ActualWidth);

                var usedFallbackViewportWidth = viewportWidth <= 0;

                if (usedFallbackViewportWidth)
                {
                    viewportWidth = 1920;
                }

                var totalCycleCount = Math.Max(
                    2,
                    (int)Math.Ceiling(viewportWidth / _cycleLength) + 1);

                var additionalCycleCount = totalCycleCount - 1;

                Trace(
                    $"Rebuild #{rebuildId} duplication calculation. " +
                    $"Viewport={viewportWidth:0.##}, " +
                    $"ViewportActual={_viewport.ActualWidth:0.##}, " +
                    $"ControlActual={ActualWidth:0.##}, " +
                    $"Fallback={usedFallbackViewportWidth}, " +
                    $"TotalCycles={totalCycleCount}, " +
                    $"AdditionalCycles={additionalCycleCount}.");

                for (var cycleIndex = 0;
                     cycleIndex < additionalCycleCount;
                     cycleIndex++)
                {
                    foreach (var item in rawSource)
                    {
                        _host.Children.Add(CreateItemPresenter(item));

                        if (SeparatorTemplate != null)
                        {
                            _host.Children.Add(CreateSeparatorPresenter(item));
                        }
                    }
                }

                Trace(
                    $"Rebuild #{rebuildId} visuals mounted. " +
                    $"HostChildren={_host.Children.Count}, " +
                    $"Expected={(singleCyclePresenters.Count * totalCycleCount)}.");

                var startOffset = anchor != null
                    ? RestoreAnchorOffset(anchor)
                    : 0.0;

                Trace(
                    $"Rebuild #{rebuildId} starting animation. " +
                    $"StartOffset={startOffset:0.##}, " +
                    $"Direction={Direction}, " +
                    $"ScrollSpeed={ScrollSpeed:0.##}.");

                StartContinuousAnimation(startOffset);
            }
            catch (Exception ex)
            {
                Trace(
                    $"ERROR: Rebuild #{rebuildId} failed. " +
                    $"{ex.GetType().FullName}: {ex.Message}\n{ex.StackTrace}");

                throw;
            }
            finally
            {
                _isRebuilding = false;
                Trace($"Rebuild #{rebuildId} finished.");
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
        private void Trace(string message)
        {
            Debug.WriteLine(
                $"[Marquee:{GetHashCode():X8}] " +
                $"[{DateTime.Now:HH:mm:ss.fff}] " +
                message);
        }

        private static string Describe(object? value)
        {
            return value == null
                ? "<null>"
                : $"{value.GetType().FullName}: {value}";
        }
    }

    #endregion
}
