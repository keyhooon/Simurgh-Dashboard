using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace SimurghDashboard.Controls
{
    public class MarqueeItemsControl : FrameworkElement
    {
        // --- Dependency Properties ---

        public static readonly DependencyProperty ItemsSourceProperty =
            DependencyProperty.Register(nameof(ItemsSource), typeof(IEnumerable), typeof(MarqueeItemsControl),
                new PropertyMetadata(null, OnItemsSourceChanged));

        public static readonly DependencyProperty ItemTemplateProperty =
            DependencyProperty.Register(nameof(ItemTemplate), typeof(DataTemplate), typeof(MarqueeItemsControl),
                new PropertyMetadata(null));

        public static readonly DependencyProperty SeparatorTemplateProperty =
            DependencyProperty.Register(nameof(SeparatorTemplate), typeof(DataTemplate), typeof(MarqueeItemsControl),
                new PropertyMetadata(null));

        public static readonly DependencyProperty ItemFinishedCommandProperty =
            DependencyProperty.Register(nameof(ItemFinishedCommand), typeof(ICommand), typeof(MarqueeItemsControl),
                new PropertyMetadata(null));

        public static readonly DependencyProperty ScrollSpeedProperty =
            DependencyProperty.Register(nameof(ScrollSpeed), typeof(double), typeof(MarqueeItemsControl),
                new PropertyMetadata(60.0));

        public static readonly DependencyProperty ItemSpacingProperty =
            DependencyProperty.Register(nameof(ItemSpacing), typeof(double), typeof(MarqueeItemsControl),
                new PropertyMetadata(20.0));

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

        public ICommand? ItemFinishedCommand
        {
            get => (ICommand?)GetValue(ItemFinishedCommandProperty);
            set => SetValue(ItemFinishedCommandProperty, value);
        }

        public double ScrollSpeed
        {
            get => (double)GetValue(ScrollSpeedProperty);
            set => SetValue(ScrollSpeedProperty, value);
        }

        public double ItemSpacing
        {
            get => (double)GetValue(ItemSpacingProperty);
            set => SetValue(ItemSpacingProperty, value);
        }

        // --- Core Fields ---

        private readonly VisualCollection _visuals;
        private readonly List<TickerElementState> _activeElements = new();
        private readonly Queue<ContentPresenter> _presenterPool = new();

        private TimeSpan _lastRenderTime;
        private IEnumerator? _enumerator;
        private bool _expectingSeparator;
        private object? _currentData;
        private bool _isLoaded;

        // --- Inner State Class ---

        private class TickerElementState
        {
            public ContentPresenter Presenter { get; set; } = null!;
            public TranslateTransform Transform { get; set; } = null!;
            public object? DataItem { get; set; }
            public bool IsSeparator { get; set; }
            public double Width { get; set; }
        }

        public MarqueeItemsControl()
        {
            ClipToBounds = true;
            IsHitTestVisible = false;
            _visuals = new VisualCollection(this);

            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        private static void OnItemsSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var view = (MarqueeItemsControl)d;

            // Unsubscribe from previous collection
            if (e.OldValue is INotifyCollectionChanged oldIncc)
            {
                oldIncc.CollectionChanged -= view.OnCollectionChanged;
            }

            // Subscribe to new collection
            if (e.NewValue is INotifyCollectionChanged newIncc)
            {
                newIncc.CollectionChanged += view.OnCollectionChanged;
            }

            view.ResetEnumeration();
        }

        private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            // Collection mutations might happen on background threads.
            // Safely dispatch reset to UI thread.
            Dispatcher.BeginInvoke(new Action(ResetEnumeration));
        }

        private void ResetEnumeration()
        {
            if (_enumerator is IDisposable disposable)
            {
                disposable.Dispose();
            }
            _enumerator = null;
            _currentData = null;
            _expectingSeparator = false;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            _isLoaded = true;
            _lastRenderTime = TimeSpan.Zero;
            CompositionTarget.Rendering += OnRendering;
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            _isLoaded = false;
            CompositionTarget.Rendering -= OnRendering;
            ResetEnumeration();

            // Clear active elements to prevent ghost bindings on reload
            foreach (var state in _activeElements)
            {
                RecycleElement(state);
            }
            _activeElements.Clear();
        }

        private void OnRendering(object? sender, EventArgs e)
        {
            if (!_isLoaded || ActualWidth <= 0 || ActualHeight <= 0) return;

            var args = (RenderingEventArgs)e;
            if (_lastRenderTime == args.RenderingTime) return;

            double deltaTime = _lastRenderTime == TimeSpan.Zero
                ? 0
                : (args.RenderingTime - _lastRenderTime).TotalSeconds;

            _lastRenderTime = args.RenderingTime;
            double offset = ScrollSpeed * deltaTime;

            if (offset > 0)
            {
                MoveAndCleanupElements(offset);
            }

            ManageSpawning();
        }

        private void MoveAndCleanupElements(double offset)
        {
            for (int i = _activeElements.Count - 1; i >= 0; i--)
            {
                var state = _activeElements[i];
                state.Transform.X -= offset;

                // Completely off-screen to the left?
                if (state.Transform.X + state.Width < 0)
                {
                    if (!state.IsSeparator && ItemFinishedCommand?.CanExecute(state.DataItem) == true)
                    {
                        ItemFinishedCommand.Execute(state.DataItem);
                    }

                    RecycleElement(state);
                    _activeElements.RemoveAt(i);
                }
            }
        }

        private void ManageSpawning()
        {
            // Keep filling the right side as long as there is empty space
            while (true)
            {
                double spawnX = ActualWidth;

                if (_activeElements.Count > 0)
                {
                    var last = _activeElements[^1];
                    double rightEdge = last.Transform.X + last.Width;

                    // If the last element hasn't fully entered + spacing, stop spawning
                    if (rightEdge + ItemSpacing > ActualWidth)
                        break;

                    spawnX = rightEdge + ItemSpacing;
                }

                object? itemToSpawn = null;
                DataTemplate? templateToUse = null;
                bool isSep = false;

                // Alternate between Separator and actual DataItem
                if (_expectingSeparator && SeparatorTemplate != null && _currentData != null)
                {
                    itemToSpawn = _currentData;
                    templateToUse = SeparatorTemplate;
                    isSep = true;
                    _expectingSeparator = false;
                }
                else
                {
                    if (!AdvanceEnumerator())
                        break; // Collection is empty

                    itemToSpawn = _currentData;
                    templateToUse = ItemTemplate;
                    isSep = false;
                    _expectingSeparator = true;
                }

                if (itemToSpawn != null)
                {
                    SpawnElement(itemToSpawn, templateToUse, isSep, spawnX);
                }
            }
        }

        private bool AdvanceEnumerator()
        {
            if (ItemsSource == null) return false;

            if (_enumerator == null)
            {
                _enumerator = ItemsSource.GetEnumerator();
            }

            if (!_enumerator.MoveNext())
            {
                // Reached the end. Dispose and create a fresh enumerator for looping.
                // (Safer than .Reset() which some IEnumerable implementations don't support).
                if (_enumerator is IDisposable disp) disp.Dispose();

                _enumerator = ItemsSource.GetEnumerator();

                if (!_enumerator.MoveNext())
                {
                    // The collection is completely empty
                    _enumerator = null;
                    return false;
                }
            }

            _currentData = _enumerator.Current;
            return true;
        }

        private void SpawnElement(object dataItem, DataTemplate? template, bool isSeparator, double xPosition)
        {
            var cp = GetOrCreatePresenter();
            cp.ContentTemplate = template;
            cp.Content = dataItem;

            // Force layout pass just for this specific new element
            cp.Measure(new Size(double.PositiveInfinity, ActualHeight));
            cp.Arrange(new Rect(0, 0, cp.DesiredSize.Width, ActualHeight));

            var transform = (TranslateTransform)cp.RenderTransform;
            transform.X = xPosition;

            _visuals.Add(cp);

            _activeElements.Add(new TickerElementState
            {
                Presenter = cp,
                Transform = transform,
                DataItem = dataItem,
                IsSeparator = isSeparator,
                Width = cp.DesiredSize.Width
            });
        }

        private ContentPresenter GetOrCreatePresenter()
        {
            if (_presenterPool.Count > 0)
            {
                return _presenterPool.Dequeue();
            }

            var transform = new TranslateTransform();
            var cp = new ContentPresenter
            {
                RenderTransform = transform,
                CacheMode = new BitmapCache
                {
                    EnableClearType = true,
                    RenderAtScale = 1.0,
                    SnapsToDevicePixels = true
                },
                // Vertical alignment center helps keep items nicely aligned horizontally
                VerticalAlignment = VerticalAlignment.Center
            };

            return cp;
        }

        private void RecycleElement(TickerElementState state)
        {
            _visuals.Remove(state.Presenter);

            // Clear bindings to free memory
            state.Presenter.Content = null;
            state.Presenter.ContentTemplate = null;

            _presenterPool.Enqueue(state.Presenter);
        }

        // --- Visual Tree & Layout Overrides ---

        protected override int VisualChildrenCount =>
            _visuals?.Count ?? 0;

        protected override Visual GetVisualChild(int index)
        {
            if (_visuals is null || index < 0 || index >= _visuals.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            return _visuals[index];
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            // Do not run measure logic on children here! 
            // We measure them manually on spawn.
            // Just request available width, and 0 height (meaning we rely on external Height constraints).
            double width = double.IsInfinity(availableSize.Width) ? 0 : availableSize.Width;
            double height = double.IsInfinity(availableSize.Height) ? 0 : availableSize.Height;
            return new Size(width, height);
        }
    }
}
