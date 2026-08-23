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
    // Direction enumeration for marquee movement
    public enum MarqueeDirection
    {
        // Items spawn on the right and scroll to the left
        RightToLeft,

        // Items spawn on the left and scroll to the right
        LeftToRight
    }

    public class MarqueeItemsControl : FrameworkElement
    {
        // --- Dependency Properties ---

        public static readonly DependencyProperty DirectionProperty =
            DependencyProperty.Register(
                nameof(Direction),
                typeof(MarqueeDirection),
                typeof(MarqueeItemsControl),
                new FrameworkPropertyMetadata(
                    MarqueeDirection.RightToLeft,
                    FrameworkPropertyMetadataOptions.AffectsRender,
                    OnDirectionChanged));

        public static readonly DependencyProperty ItemsSourceProperty =
            DependencyProperty.Register(
                nameof(ItemsSource),
                typeof(IEnumerable),
                typeof(MarqueeItemsControl),
                new PropertyMetadata(null, OnItemsSourceChanged));

        public static readonly DependencyProperty ItemTemplateProperty =
            DependencyProperty.Register(
                nameof(ItemTemplate),
                typeof(DataTemplate),
                typeof(MarqueeItemsControl),
                new PropertyMetadata(null, OnTemplateChanged));

        public static readonly DependencyProperty SeparatorTemplateProperty =
            DependencyProperty.Register(
                nameof(SeparatorTemplate),
                typeof(DataTemplate),
                typeof(MarqueeItemsControl),
                new PropertyMetadata(null, OnTemplateChanged));

        public static readonly DependencyProperty ItemFinishedCommandProperty =
            DependencyProperty.Register(
                nameof(ItemFinishedCommand),
                typeof(ICommand),
                typeof(MarqueeItemsControl),
                new PropertyMetadata(null));

        public static readonly DependencyProperty ScrollSpeedProperty =
            DependencyProperty.Register(
                nameof(ScrollSpeed),
                typeof(double),
                typeof(MarqueeItemsControl),
                new PropertyMetadata(60d));

        public static readonly DependencyProperty ItemSpacingProperty =
            DependencyProperty.Register(
                nameof(ItemSpacing),
                typeof(double),
                typeof(MarqueeItemsControl),
                new PropertyMetadata(20d));

        // --- Property Accessors ---

        public MarqueeDirection Direction
        {
            get => (MarqueeDirection)GetValue(DirectionProperty);
            set => SetValue(DirectionProperty, value);
        }

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

        // --- Core Internal Fields ---

        private readonly VisualCollection _visuals;
        private readonly List<TickerElementState> _activeElements = new();
        private readonly Queue<ContentPresenter> _presenterPool = new();

        private IEnumerator? _enumerator;
        private TimeSpan _lastRenderTime;
        private object? _currentData;
        private bool _expectingSeparator;
        private bool _isLoaded;

        private sealed class TickerElementState
        {
            public required ContentPresenter Presenter { get; init; }
            public required TranslateTransform Transform { get; init; }
            public object? DataItem { get; init; }
            public bool IsSeparator { get; init; }
            public double Width { get; init; }
        }

        public MarqueeItemsControl()
        {
            ClipToBounds = true;
            IsHitTestVisible = false;

            _visuals = new VisualCollection(this);

            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        private static void OnDirectionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (MarqueeItemsControl)d;
            // Purge and rebuild the visual pipeline when direction flips at runtime
            control.ClearActiveElements();
            control.ResetEnumeration();
        }

        private static void OnItemsSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (MarqueeItemsControl)d;

            if (e.OldValue is INotifyCollectionChanged oldCollection)
            {
                oldCollection.CollectionChanged -= control.OnCollectionChanged;
            }

            if (e.NewValue is INotifyCollectionChanged newCollection)
            {
                newCollection.CollectionChanged += control.OnCollectionChanged;
            }

            control.ResetEnumeration();
        }

        private static void OnTemplateChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (MarqueeItemsControl)d;
            control.ClearActiveElements();
        }

        private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.BeginInvoke(new Action(() => HandleCollectionChanged(e)));
                return;
            }

            HandleCollectionChanged(e);
        }

        private void HandleCollectionChanged(NotifyCollectionChangedEventArgs e)
        {
            ResetEnumeration();
            ClearActiveElements();
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
            ClearActiveElements();
        }

        private void OnRendering(object? sender, EventArgs e)
        {
            if (!_isLoaded || ActualWidth <= 0 || ActualHeight <= 0)
            {
                return;
            }

            var renderingArgs = (RenderingEventArgs)e;

            if (_lastRenderTime == renderingArgs.RenderingTime)
            {
                return;
            }

            var deltaTime = _lastRenderTime == TimeSpan.Zero
                ? 0
                : (renderingArgs.RenderingTime - _lastRenderTime).TotalSeconds;

            _lastRenderTime = renderingArgs.RenderingTime;
            var offset = ScrollSpeed * deltaTime;

            if (offset > 0)
            {
                MoveAndCleanupElements(offset);
            }

            ManageSpawning();
        }

        private void MoveAndCleanupElements(double offset)
        {
            var isRightToLeft = Direction == MarqueeDirection.RightToLeft;

            for (var index = _activeElements.Count - 1; index >= 0; index--)
            {
                var state = _activeElements[index];

                // Calculate displacement based on active direction vector
                if (isRightToLeft)
                {
                    state.Transform.X -= offset;
                }
                else
                {
                    state.Transform.X += offset;
                }

                // Check out-of-bounds threshold
                var isOffScreen = isRightToLeft
                    ? (state.Transform.X + state.Width < 0)
                    : (state.Transform.X > ActualWidth);

                if (isOffScreen)
                {
                    if (!state.IsSeparator &&
                        state.DataItem is not null &&
                        ItemFinishedCommand?.CanExecute(state.DataItem) == true)
                    {
                        ItemFinishedCommand.Execute(state.DataItem);
                    }

                    RecycleElement(state);
                    _activeElements.RemoveAt(index);
                }
            }
        }

        private void ManageSpawning()
        {
            System.Diagnostics.Debug.WriteLine(
                $"Width={ActualWidth}, " +
                $"Active={_activeElements.Count}, " +
                $"Enumerator={_enumerator is not null}, " +
                $"Current={_currentData}");
            var isRightToLeft =
                Direction == MarqueeDirection.RightToLeft;

            while (true)
            {
                double spawnX;

                if (_activeElements.Count == 0)
                {
                    // First item starts just outside the entering edge.
                    spawnX = isRightToLeft
                        ? ActualWidth
                        : -1;
                }
                else if (isRightToLeft)
                {
                    // Next item must be placed after the rightmost active item.
                    var rightmostElement = _activeElements[^1];

                    spawnX =
                        rightmostElement.Transform.X +
                        rightmostElement.Width +
                        ItemSpacing;
                }
                else
                {
                    // Next item must be placed before the leftmost active item.
                    var leftmostElement = _activeElements[0];

                    spawnX =
                        leftmostElement.Transform.X -
                        ItemSpacing;
                }

                object? itemToSpawn;
                DataTemplate? templateToUse;
                var isSeparator = false;

                if (_expectingSeparator &&
                    SeparatorTemplate is not null &&
                    _currentData is not null)
                {
                    itemToSpawn = _currentData;
                    templateToUse = SeparatorTemplate;
                    isSeparator = true;
                    _expectingSeparator = false;
                }
                else
                {
                    if (!AdvanceEnumerator())
                    {
                        break;
                    }

                    itemToSpawn = _currentData;

                    if (itemToSpawn is null)
                    {
                        break;
                    }

                    templateToUse = ResolveItemTemplate(itemToSpawn);

                    _expectingSeparator = SeparatorTemplate is not null;
                }

                if (itemToSpawn is null)
                {
                    break;
                }

                var presenter = GetOrCreatePresenter();

                presenter.Content = itemToSpawn;
                presenter.ContentTemplate = templateToUse;

                presenter.Measure(
                    new Size(
                        double.PositiveInfinity,
                        Math.Max(1, ActualHeight)));

                var width = presenter.DesiredSize.Width;

                if (width <= 0 ||
                    double.IsNaN(width) ||
                    double.IsInfinity(width))
                {
                    presenter.Content = null;
                    presenter.ContentTemplate = null;
                    _presenterPool.Enqueue(presenter);
                    break;
                }

                var transform =
                    presenter.RenderTransform as TranslateTransform
                    ?? new TranslateTransform();

                presenter.RenderTransform = transform;

                if (isRightToLeft)
                {
                    // spawnX is the left edge of the item.
                    transform.X = spawnX;
                }
                else
                {
                    // spawnX is the right edge of the item.
                    transform.X = spawnX - width;
                }

                transform.Y = 0;

                presenter.Arrange(
                    new Rect(
                        0,
                        0,
                        width,
                        Math.Max(1, ActualHeight)));

                _visuals.Add(presenter);

                _activeElements.Add(new TickerElementState
                {
                    Presenter = presenter,
                    Transform = transform,
                    DataItem = itemToSpawn,
                    IsSeparator = isSeparator,
                    Width = width
                });
            }
        }


        private DataTemplate? ResolveItemTemplate(object item)
        {
            if (ItemTemplate is not null)
            {
                return ItemTemplate;
            }

            // Implicit DataTemplate dynamic lookup using DataTemplateKey
            var templateKey = new DataTemplateKey(item.GetType());
            return TryFindResource(templateKey) as DataTemplate;
        }

        private bool AdvanceEnumerator()
        {
            if (ItemsSource is null)
            {
                return false;
            }

            // Create the enumerator on the first request.
            _enumerator ??= ItemsSource.GetEnumerator();

            // Read the next item from the current cycle.
            if (_enumerator.MoveNext() && _enumerator.Current is not null)
            {
                _currentData = _enumerator.Current;
                return true;
            }

            // The current cycle is finished. Dispose the old enumerator.
            if (_enumerator is IDisposable disposable)
            {
                disposable.Dispose();
            }

            // Start a new cycle from the first item.
            _enumerator = ItemsSource.GetEnumerator();

            // Do not keep the marquee alive for an empty source.
            if (!_enumerator.MoveNext() || _enumerator.Current is null)
            {
                if (_enumerator is IDisposable newDisposable)
                {
                    newDisposable.Dispose();
                }

                _enumerator = null;
                _currentData = null;
                _expectingSeparator = false;

                return false;
            }

            _currentData = _enumerator.Current;
            return true;
        }

        private void SpawnElement(
            object dataItem,
            DataTemplate? template,
            bool isSeparator,
            double xPosition,
            bool isRightToLeft)
        {
            var presenter = GetOrCreatePresenter();

            presenter.Content = dataItem;
            presenter.ContentTemplate = template;

            // Measure child with infinity width to calculate intrinsic bounds
            presenter.Measure(new Size(double.PositiveInfinity, ActualHeight));
            presenter.Arrange(new Rect(0, 0, presenter.DesiredSize.Width, ActualHeight));

            var transform = (TranslateTransform)presenter.RenderTransform;

            // In LeftToRight mode, if spawning at index 0, place the element completely behind the left edge
            if (!isRightToLeft && _activeElements.Count == 0)
            {
                transform.X = -presenter.DesiredSize.Width;
            }
            else if (!isRightToLeft)
            {
                transform.X = xPosition - presenter.DesiredSize.Width;
            }
            else
            {
                transform.X = xPosition;
            }

            transform.Y = 0;

            _visuals.Add(presenter);

            _activeElements.Add(new TickerElementState
            {
                Presenter = presenter,
                Transform = transform,
                DataItem = dataItem,
                IsSeparator = isSeparator,
                Width = presenter.DesiredSize.Width
            });
        }

        private ContentPresenter GetOrCreatePresenter()
        {
            if (_presenterPool.Count > 0)
            {
                var pooledPresenter = _presenterPool.Dequeue();
                pooledPresenter.RenderTransform ??= new TranslateTransform();
                return pooledPresenter;
            }

            var transform = new TranslateTransform();

            return new ContentPresenter
            {
                RenderTransform = transform,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Left,
                IsHitTestVisible = false,
                CacheMode = new BitmapCache
                {
                    EnableClearType = true,
                    RenderAtScale = 1.0
                }
            };
        }

        private void RecycleElement(TickerElementState state)
        {
            _visuals.Remove(state.Presenter);

            state.Presenter.Content = null;
            state.Presenter.ContentTemplate = null;

            if (state.Presenter.RenderTransform is TranslateTransform transform)
            {
                transform.X = 0;
                transform.Y = 0;
            }

            _presenterPool.Enqueue(state.Presenter);
        }

        private void ClearActiveElements()
        {
            foreach (var state in _activeElements)
            {
                RecycleElement(state);
            }

            _activeElements.Clear();
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

        protected override int VisualChildrenCount => _visuals?.Count??0;

        protected override Visual GetVisualChild(int index)
        {
            if (index < 0 || index >= _visuals.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            return _visuals[index];
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            var width = double.IsInfinity(availableSize.Width) ? 0 : availableSize.Width;
            var height = double.IsInfinity(availableSize.Height) ? 0 : availableSize.Height;
            return new Size(width, height);
        }
    }
}
