using System.Collections;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using SimurghDashboard.RssFeed.Controls.Marquee;

namespace SimurghDashboard.RssFeed.Controls;

/// <summary>
/// High-performance DrawingVisual-backed marquee control.
/// Synchronizes seamlessly with INotifyCollectionChanged, renders RTL text correctly,
/// avoids UI allocations during animation, and provides manual hit-testing.
/// </summary>
public class DrawingMarqueeControl : FrameworkElement
{
    private const double MaxFrameDeltaSeconds = 0.25;
    private const double MinimumCycleLength = 0.5;

    private readonly DrawingVisual _stripVisual = new();
    private readonly VisualCollection _visualChildren;

    private readonly List<LayoutItem> _layoutItems = [];

    private INotifyCollectionChanged? _observedCollection;

    private bool _renderingSubscribed;
    private bool _layoutDirty = true;
    private bool _isLoaded;

    private double _cycleLength;
    private double _logicalOffset;
    private TimeSpan _lastFrameTime;

    private const string DiagnosticPrefix = "[DrawingMarqueeControl]";

    private int _renderFrameCount;
    private bool _hasLoggedFirstFrame;

    public DrawingMarqueeControl()
    {
        _visualChildren = new VisualCollection(this)
        {
            _stripVisual
        };

        ClipToBounds = true;
        SnapsToDevicePixels = true;
        UseLayoutRounding = true;

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        SizeChanged += OnSizeChanged;
        IsVisibleChanged += OnIsVisibleChanged;
        MouseLeftButtonUp += OnMouseLeftButtonUp;
    }

    #region Dependency Properties

    public static readonly DependencyProperty ItemsSourceProperty =
        DependencyProperty.Register(
            nameof(ItemsSource),
            typeof(IEnumerable),
            typeof(DrawingMarqueeControl),
            new FrameworkPropertyMetadata(null, OnVisualConfigurationChanged));

    public static readonly DependencyProperty DirectionProperty =
        DependencyProperty.Register(
            nameof(Direction),
            typeof(MarqueeDirection),
            typeof(DrawingMarqueeControl),
            new FrameworkPropertyMetadata(
                MarqueeDirection.RightToLeft,
                OnAnimationConfigurationChanged));

    public static readonly DependencyProperty ScrollSpeedProperty =
        DependencyProperty.Register(
            nameof(ScrollSpeed),
            typeof(double),
            typeof(DrawingMarqueeControl),
            new FrameworkPropertyMetadata(
                60.0,
                OnAnimationConfigurationChanged,
                CoerceScrollSpeed));

    public static readonly DependencyProperty FontFamilyProperty =
        TextElement.FontFamilyProperty.AddOwner(
            typeof(DrawingMarqueeControl),
            new FrameworkPropertyMetadata(
                SystemFonts.MessageFontFamily,
                OnVisualConfigurationChanged));

    public static readonly DependencyProperty FontSizeProperty =
        TextElement.FontSizeProperty.AddOwner(
            typeof(DrawingMarqueeControl),
            new FrameworkPropertyMetadata(
                16.0,
                OnVisualConfigurationChanged));

    public static readonly DependencyProperty FontWeightProperty =
        TextElement.FontWeightProperty.AddOwner(
            typeof(DrawingMarqueeControl),
            new FrameworkPropertyMetadata(
                FontWeights.Normal,
                OnVisualConfigurationChanged));

    public static readonly DependencyProperty FontStyleProperty =
        TextElement.FontStyleProperty.AddOwner(
            typeof(DrawingMarqueeControl),
            new FrameworkPropertyMetadata(
                FontStyles.Normal,
                OnVisualConfigurationChanged));

    public static readonly DependencyProperty ForegroundProperty =
        TextElement.ForegroundProperty.AddOwner(
            typeof(DrawingMarqueeControl),
            new FrameworkPropertyMetadata(
                Brushes.White,
                OnVisualConfigurationChanged));

    public static readonly DependencyProperty DefaultItemBackgroundProperty =
        DependencyProperty.Register(
            nameof(DefaultItemBackground),
            typeof(Brush),
            typeof(DrawingMarqueeControl),
            new FrameworkPropertyMetadata(
                Brushes.Transparent,
                OnVisualConfigurationChanged));

    public static readonly DependencyProperty ItemPaddingProperty =
        DependencyProperty.Register(
            nameof(ItemPadding),
            typeof(Thickness),
            typeof(DrawingMarqueeControl),
            new FrameworkPropertyMetadata(
                new Thickness(20, 0, 20, 0),
                OnVisualConfigurationChanged));

    public static readonly DependencyProperty ItemSpacingProperty =
        DependencyProperty.Register(
            nameof(ItemSpacing),
            typeof(double),
            typeof(DrawingMarqueeControl),
            new FrameworkPropertyMetadata(
                12.0,
                OnVisualConfigurationChanged,
                CoerceNonNegativeDouble));

    public static readonly DependencyProperty SeparatorBrushProperty =
        DependencyProperty.Register(
            nameof(SeparatorBrush),
            typeof(Brush),
            typeof(DrawingMarqueeControl),
            new FrameworkPropertyMetadata(
                new SolidColorBrush(Color.FromArgb(90, 255, 255, 255)),
                OnVisualConfigurationChanged));

    public static readonly DependencyProperty SeparatorWidthProperty =
        DependencyProperty.Register(
            nameof(SeparatorWidth),
            typeof(double),
            typeof(DrawingMarqueeControl),
            new FrameworkPropertyMetadata(
                1.0,
                OnVisualConfigurationChanged,
                CoerceNonNegativeDouble));

    public static readonly DependencyProperty SeparatorHeightRatioProperty =
        DependencyProperty.Register(
            nameof(SeparatorHeightRatio),
            typeof(double),
            typeof(DrawingMarqueeControl),
            new FrameworkPropertyMetadata(
                0.5,
                OnVisualConfigurationChanged,
                CoerceRatio));

    public static readonly RoutedEvent ItemClickedEvent =
        EventManager.RegisterRoutedEvent(
            nameof(ItemClicked),
            RoutingStrategy.Bubble,
            typeof(EventHandler<MarqueeItemClickedEventArgs>),
            typeof(DrawingMarqueeControl));

    public IEnumerable? ItemsSource
    {
        get => (IEnumerable?)GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
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

    public FontFamily FontFamily
    {
        get => (FontFamily)GetValue(FontFamilyProperty);
        set => SetValue(FontFamilyProperty, value);
    }

    public double FontSize
    {
        get => (double)GetValue(FontSizeProperty);
        set => SetValue(FontSizeProperty, value);
    }

    public FontWeight FontWeight
    {
        get => (FontWeight)GetValue(FontWeightProperty);
        set => SetValue(FontWeightProperty, value);
    }

    public FontStyle FontStyle
    {
        get => (FontStyle)GetValue(FontStyleProperty);
        set => SetValue(FontStyleProperty, value);
    }

    public Brush Foreground
    {
        get => (Brush)GetValue(ForegroundProperty);
        set => SetValue(ForegroundProperty, value);
    }

    public Brush DefaultItemBackground
    {
        get => (Brush)GetValue(DefaultItemBackgroundProperty);
        set => SetValue(DefaultItemBackgroundProperty, value);
    }

    public Thickness ItemPadding
    {
        get => (Thickness)GetValue(ItemPaddingProperty);
        set => SetValue(ItemPaddingProperty, value);
    }

    public double ItemSpacing
    {
        get => (double)GetValue(ItemSpacingProperty);
        set => SetValue(ItemSpacingProperty, value);
    }

    public Brush SeparatorBrush
    {
        get => (Brush)GetValue(SeparatorBrushProperty);
        set => SetValue(SeparatorBrushProperty, value);
    }

    public double SeparatorWidth
    {
        get => (double)GetValue(SeparatorWidthProperty);
        set => SetValue(SeparatorWidthProperty, value);
    }

    public double SeparatorHeightRatio
    {
        get => (double)GetValue(SeparatorHeightRatioProperty);
        set => SetValue(SeparatorHeightRatioProperty, value);
    }

    public event EventHandler<MarqueeItemClickedEventArgs> ItemClicked
    {
        add => AddHandler(ItemClickedEvent, value);
        remove => RemoveHandler(ItemClickedEvent, value);
    }

    #endregion

    #region Visual Tree Overrides

    protected override int VisualChildrenCount => _visualChildren.Count;

    protected override Visual GetVisualChild(int index)
    {
        if (index < 0 || index >= _visualChildren.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        return _visualChildren[index];
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var desiredHeight = Math.Max(
            1,
            FontSize * 1.8 + ItemPadding.Top + ItemPadding.Bottom);

        var desiredWidth = double.IsInfinity(availableSize.Width)
            ? 0
            : availableSize.Width;

        Log(
            $"MeasureOverride | Available={availableSize.Width:F1}x{availableSize.Height:F1}, " +
            $"Desired={desiredWidth:F1}x{desiredHeight:F1}");

        return new Size(desiredWidth, desiredHeight);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        Log(
            $"ArrangeOverride | Final={finalSize.Width:F1}x{finalSize.Height:F1}, " +
            $"Actual={ActualWidth:F1}x{ActualHeight:F1}, Dirty={_layoutDirty}");

        RebuildDrawingIfRequired();

        return finalSize;
    }

    #endregion

    #region Lifecycle

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _isLoaded = true;

        Log(
            $"Loaded | Actual={ActualWidth:F1}x{ActualHeight:F1}, " +
            $"Visible={IsVisible}, ItemsSource={ItemsSource?.GetType().FullName ?? "<null>"}");

        AttachCollection(ItemsSource);
        MarkLayoutDirty();
        RefreshRenderingSubscription();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        Log("Unloaded");

        _isLoaded = false;

        StopRendering();
        DetachCollection();
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        Log(
            $"SizeChanged | Previous={e.PreviousSize.Width:F1}x{e.PreviousSize.Height:F1}, " +
            $"New={e.NewSize.Width:F1}x{e.NewSize.Height:F1}");

        if (!e.WidthChanged && !e.HeightChanged)
        {
            return;
        }

        MarkLayoutDirty();
    }

    private void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        Log($"IsVisibleChanged | Old={e.OldValue}, New={e.NewValue}");

        _lastFrameTime = TimeSpan.Zero;
        RefreshRenderingSubscription();
    }

    #endregion

    #region Dependency Property Callbacks

    private static void OnVisualConfigurationChanged(
        DependencyObject d,
        DependencyPropertyChangedEventArgs e)
    {
        var control = (DrawingMarqueeControl)d;

        control.Log(
            $"VisualPropertyChanged | Property={e.Property.Name}, " +
            $"Old={e.OldValue ?? "<null>"}, New={e.NewValue ?? "<null>"}");

        if (e.Property == ItemsSourceProperty)
        {
            control.DetachCollection();
            control.AttachCollection(e.NewValue);
        }

        control.MarkLayoutDirty();
    }

    private static void OnAnimationConfigurationChanged(
        DependencyObject d,
        DependencyPropertyChangedEventArgs e)
    {
        var control = (DrawingMarqueeControl)d;

        if (e.Property == DirectionProperty)
        {
            control.ApplyTransform();
        }

        control.RefreshRenderingSubscription();
    }

    private static object CoerceScrollSpeed(
        DependencyObject d,
        object baseValue)
    {
        if (baseValue is not double value ||
            double.IsNaN(value) ||
            double.IsInfinity(value) ||
            value < 0)
        {
            return 0.0;
        }

        return value;
    }

    private static object CoerceNonNegativeDouble(
        DependencyObject d,
        object baseValue)
    {
        if (baseValue is not double value ||
            double.IsNaN(value) ||
            double.IsInfinity(value) ||
            value < 0)
        {
            return 0.0;
        }

        return value;
    }

    private static object CoerceRatio(
        DependencyObject d,
        object baseValue)
    {
        if (baseValue is not double value ||
            double.IsNaN(value) ||
            double.IsInfinity(value))
        {
            return 0.5;
        }

        return Math.Clamp(value, 0.0, 1.0);
    }

    #endregion

    #region Collection Handling

    private void AttachCollection(object? source)
    {
        if (source is not INotifyCollectionChanged collection)
        {
            Log(
                $"AttachCollection skipped | Source={source?.GetType().FullName ?? "<null>"}, " +
                $"ImplementsINotifyCollectionChanged=False");

            return;
        }

        if (ReferenceEquals(_observedCollection, collection))
        {
            Log("AttachCollection skipped | Already attached");
            return;
        }

        DetachCollection();

        _observedCollection = collection;
        _observedCollection.CollectionChanged += OnCollectionChanged;

        Log($"Collection attached | Type={source?.GetType().FullName}");
    }

    private void DetachCollection()
    {
        if (_observedCollection == null)
        {
            return;
        }

        _observedCollection.CollectionChanged -= OnCollectionChanged;
        _observedCollection = null;

        Log("Collection detached");
    }

    private void OnCollectionChanged(
        object? sender,
        NotifyCollectionChangedEventArgs e)
    {
        Log(
            $"CollectionChanged | Action={e.Action}, " +
            $"NewItems={e.NewItems?.Count ?? 0}, OldItems={e.OldItems?.Count ?? 0}, " +
            $"Thread={Environment.CurrentManagedThreadId}");

        if (!Dispatcher.CheckAccess())
        {
            Log("CollectionChanged marshaled to Dispatcher");

            Dispatcher.BeginInvoke(
                new Action(() => OnCollectionChanged(sender, e)));

            return;
        }

        MarkLayoutDirty();
    }

    #endregion

    #region Drawing Build Pipeline

    private void MarkLayoutDirty()
    {
        _layoutDirty = true;

        Log("Layout marked dirty");

        InvalidateMeasure();
        InvalidateArrange();
        InvalidateVisual();
    }

    private void RebuildDrawingIfRequired()
    {
        if (!_layoutDirty)
        {
            return;
        }

        _layoutDirty = false;
        _layoutItems.Clear();
        _cycleLength = 0;
        Log(
            $"Rebuild started | Actual={ActualWidth:F1}x{ActualHeight:F1}, " +
            $"ItemsSource={ItemsSource?.GetType().FullName ?? "<null>"}");

        if (ActualWidth <= 0 || ActualHeight <= 0)
        {
            ClearVisual();
            return;
        }

        // Extract items safely in case of background collection synchronization locks
        List<IMarqueeDrawItem> items;
        try
        {
            if (ItemsSource is ICollection<IMarqueeDrawItem> directCollection)
            {
                Log($"ItemsSource supports ICollection<IMarqueeDrawItem> | Count={directCollection.Count}");

                lock (directCollection)
                {
                    items = directCollection
                        .Where(static item => !string.IsNullOrWhiteSpace(item.Text))
                        .ToList();
                }
            }
            else
            {
                Log("ItemsSource does not support ICollection<IMarqueeDrawItem>; using OfType<IMarqueeDrawItem>");

                items = ItemsSource?
                    .OfType<IMarqueeDrawItem>()
                    .Where(static item => !string.IsNullOrWhiteSpace(item.Text))
                    .ToList() ?? [];
            }
        }
        catch (Exception exception)
        {
            Log($"ItemsSource enumeration failed | {exception}");

            ClearVisual();
            return;
        }

        Log($"Items snapshot created | ValidItemCount={items.Count}");

        if (items.Count == 0)
        {
            Log(
                "Rebuild aborted | No valid IMarqueeDrawItem found. " +
                "Verify that every model implements the exact SimurghDashboard.Controls.IMarqueeDrawItem interface.");

            ClearVisual();
            return;
        }

        var dpi = VisualTreeHelper.GetDpi(this);

        Log(
            $"DPI={dpi.DpiScaleX:F2}x{dpi.DpiScaleY:F2}, " +
            $"PixelsPerDip={dpi.PixelsPerDip:F2}, Font={FontFamily.Source}, FontSize={FontSize:F1}");

        var typeface = new Typeface(
            FontFamily,
            FontStyle,
            FontWeight,
            FontStretches.Normal);

        var itemX = 0.0;

        foreach (var item in items)
        {
            try
            {
                var text = new FormattedText(
                    item.Text,
                    CultureInfo.CurrentUICulture,
                    FlowDirection.RightToLeft,
                    typeface,
                    FontSize,
                    item.Foreground ?? Foreground,
                    dpi.PixelsPerDip);

                text.Trimming = TextTrimming.None;

                var itemWidth =
                    ItemPadding.Left +
                    text.WidthIncludingTrailingWhitespace +
                    ItemPadding.Right;

                _layoutItems.Add(new LayoutItem(
                    item,
                    text,
                    itemX,
                    itemWidth));

                Log(
                    $"Item added | Text=\"{item.Text}\", " +
                    $"Offset={itemX:F1}, TextWidth={text.WidthIncludingTrailingWhitespace:F1}, " +
                    $"ItemWidth={itemWidth:F1}");

                itemX += itemWidth + ItemSpacing + SeparatorWidth;
            }
            catch (Exception exception)
            {
                Log($"Item drawing layout failed | Text=\"{item.Text}\" | {exception}");
            }
        }

        _cycleLength = itemX;

        if (_cycleLength < MinimumCycleLength)
        {
            ClearVisual();
            return;
        }

        _logicalOffset %= _cycleLength;

        if (_logicalOffset < 0)
        {
            _logicalOffset += _cycleLength;
        }

        // Calculate cycle repeats required to cover visible viewport during scroll transitions
        var cycleCount = Math.Max(
            3,
            (int)Math.Ceiling(ActualWidth / _cycleLength) + 3);

        using var drawingContext = _stripVisual.RenderOpen();

        for (var cycleIndex = 0; cycleIndex < cycleCount; cycleIndex++)
        {
            var cycleX = cycleIndex * _cycleLength;

            foreach (var layoutItem in _layoutItems)
            {
                DrawItem(drawingContext, layoutItem, cycleX, null,null);
            }
        }

        ApplyTransform();
        RefreshRenderingSubscription();
    }

    private void DrawItem(
        DrawingContext drawingContext,
        LayoutItem layoutItem,
        double cycleX,
        Brush? defaultBackground,
        Brush? separatorBrush)
    {
        var item = layoutItem.Item;
        var x = cycleX + layoutItem.Offset;
        var itemRect = new Rect(x, 0, layoutItem.Width, ActualHeight);

        // 1. Draw item background rectangle
        var background = EnsureFrozenBrush(item.Background) ?? defaultBackground;
        if (background != null)
        {
            drawingContext.DrawRectangle(background, null, itemRect);
        }

        // 2. Draw border if specified
        var borderBrush = EnsureFrozenBrush(item.BorderBrush);
        if (borderBrush != null && item.BorderThickness > 0)
        {
            var pen = new Pen(borderBrush, item.BorderThickness);
            if (pen.CanFreeze)
            {
                pen.Freeze();
            }
            drawingContext.DrawRectangle(null, pen, itemRect);
        }

        // 3. Render RTL formatted text
        // In WPF FormattedText with FlowDirection.RightToLeft, drawing at (textRightX, textY)
        // anchors the origin precisely to the right padding boundary of the item box.
        var textY = Math.Max(0, (ActualHeight - layoutItem.Text.Height) / 2.0);
        var textRightX = x + layoutItem.Width - ItemPadding.Right;

        drawingContext.DrawText(layoutItem.Text, new Point(textRightX, textY));

        // 4. Draw separator between items
        if (SeparatorWidth > 0 && separatorBrush != null)
        {
            var separatorX = x + layoutItem.Width + (ItemSpacing / 2.0);
            var separatorHeight = ActualHeight * SeparatorHeightRatio;
            var separatorTop = (ActualHeight - separatorHeight) / 2.0;

            drawingContext.DrawRectangle(
                separatorBrush,
                null,
                new Rect(separatorX, separatorTop, SeparatorWidth, separatorHeight));
        }
    }


    private void ClearVisual()
    {
        using var drawingContext = _stripVisual.RenderOpen();

        _logicalOffset = 0;
        _stripVisual.Transform = Transform.Identity;

        RefreshRenderingSubscription();
    }

    #endregion

    #region Render Loop

    private bool CanAnimate()
    {
        return _isLoaded &&
               IsVisible &&
               ScrollSpeed > 0 &&
               _cycleLength >= MinimumCycleLength &&
               ActualWidth > 0 &&
               ActualHeight > 0;
    }

    private void RefreshRenderingSubscription()
    {
        if (CanAnimate())
        {
            StartRendering();
        }
        else
        {
            StopRendering();
        }
    }

    private void StartRendering()
    {
        if (_renderingSubscribed)
        {
            return;
        }

        _lastFrameTime = TimeSpan.Zero;

        CompositionTarget.Rendering += OnRendering;
        _renderingSubscribed = true;
    }

    private void StopRendering()
    {
        if (!_renderingSubscribed)
        {
            return;
        }

        CompositionTarget.Rendering -= OnRendering;

        _renderingSubscribed = false;
        _lastFrameTime = TimeSpan.Zero;
    }

    private void OnRendering(object? sender, EventArgs e)
    {
        if (!CanAnimate())
        {
            RefreshRenderingSubscription();
            return;
        }

        var renderingArgs = e as RenderingEventArgs;
        var currentFrameTime = renderingArgs?.RenderingTime ?? TimeSpan.Zero;

        if (_lastFrameTime == TimeSpan.Zero)
        {
            _lastFrameTime = currentFrameTime;
            return;
        }

        var deltaSeconds = (currentFrameTime - _lastFrameTime).TotalSeconds;
        _lastFrameTime = currentFrameTime;

        if (deltaSeconds <= 0)
        {
            return;
        }

        deltaSeconds = Math.Min(deltaSeconds, MaxFrameDeltaSeconds);

        _logicalOffset += ScrollSpeed * deltaSeconds;
        _logicalOffset %= _cycleLength;

        ApplyTransform();
    }

    private void ApplyTransform()
    {
        if (_cycleLength < MinimumCycleLength)
        {
            _stripVisual.Transform = Transform.Identity;
            return;
        }

        var normalizedOffset = _logicalOffset % _cycleLength;

        if (normalizedOffset < 0)
        {
            normalizedOffset += _cycleLength;
        }

        var transformX = Direction switch
        {
            MarqueeDirection.RightToLeft => -normalizedOffset,
            MarqueeDirection.LeftToRight => normalizedOffset - _cycleLength,
            _ => -normalizedOffset
        };

        _stripVisual.Transform = new TranslateTransform(transformX, 0);
    }

    #endregion

    #region Hit Testing

    private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_layoutItems.Count == 0 || _cycleLength < MinimumCycleLength)
        {
            return;
        }

        var clickedPoint = e.GetPosition(this);
        var logicalX = ResolveLogicalX(clickedPoint.X);

        var hitItem = _layoutItems.FirstOrDefault(
            item => logicalX >= item.Offset &&
                    logicalX <= item.Offset + item.Width);

        if (hitItem == null)
        {
            return;
        }

        RaiseEvent(new MarqueeItemClickedEventArgs(
            ItemClickedEvent,
            this,
            hitItem.Item));
    }

    private double ResolveLogicalX(double viewportX)
    {
        var normalizedOffset = _logicalOffset % _cycleLength;

        if (normalizedOffset < 0)
        {
            normalizedOffset += _cycleLength;
        }

        var logicalX = Direction switch
        {
            MarqueeDirection.RightToLeft => viewportX + normalizedOffset,
            MarqueeDirection.LeftToRight => viewportX + _cycleLength - normalizedOffset,
            _ => viewportX + normalizedOffset
        };

        logicalX %= _cycleLength;

        if (logicalX < 0)
        {
            logicalX += _cycleLength;
        }

        return logicalX;
    }

    #endregion



    private sealed record LayoutItem(
        IMarqueeDrawItem Item,
        FormattedText Text,
        double Offset,
        double Width);


    public static readonly DependencyProperty EnableDiagnosticsProperty =
        DependencyProperty.Register(
            nameof(EnableDiagnostics),
            typeof(bool),
            typeof(DrawingMarqueeControl),
            new FrameworkPropertyMetadata(false));

    public bool EnableDiagnostics
    {
        get => (bool)GetValue(EnableDiagnosticsProperty);
        set => SetValue(EnableDiagnosticsProperty, value);
    }

    private void Log(string message)
    {
        if (!EnableDiagnostics)
        {
            return;
        }

        Debug.WriteLine(
            $"{DiagnosticPrefix} [{DateTime.Now:HH:mm:ss.fff}] " +
            $"Control={GetHashCode():X8} | {message}");
    }

    /// <summary>
    /// Ensures the brush is frozen for thread safety and optimal Direct3D/MIL rendering pipeline performance.
    /// Prevents System.InvalidOperationException across UI and background threads.
    /// </summary>
    private static Brush? EnsureFrozenBrush(Brush? brush)
    {
        if (brush == null)
        {
            return null;
        }

        if (brush.IsFrozen)
        {
            return brush;
        }

        if (brush.CanFreeze)
        {
            // Safe to freeze original instance directly
            brush.Freeze();
            return brush;
        }

        // If original cannot freeze (e.g. data-bound or animated), clone and freeze the copy
        var clone = brush.Clone();
        if (clone.CanFreeze)
        {
            clone.Freeze();
        }

        return clone;
    }

    /// <summary>
    /// Overload for Pen resources to ensure zero-allocation thread safety in DrawingContext.
    /// </summary>
    private static Pen? EnsureFrozenPen(Pen? pen)
    {
        if (pen == null)
        {
            return null;
        }

        if (pen.IsFrozen)
        {
            return pen;
        }

        if (pen.CanFreeze)
        {
            pen.Freeze();
            return pen;
        }

        var clone = pen.Clone();
        if (clone.CanFreeze)
        {
            clone.Freeze();
        }

        return clone;
    }

}