// ============================================================================
// 3. THE "DUMB" UI CONTROL (Visuals & Animations Only)
// ============================================================================
using System;
using System.Collections;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace SimurghDashboard.Controls;

// Notice how all HTTP, RSS, Timer, and Error logic is completely gone.
// This is now a highly reusable marquee control that can animate ANY collection.
[TemplatePart(Name = "PART_Canvas", Type = typeof(Canvas))]
[TemplatePart(Name = "PART_ItemsHost", Type = typeof(ItemsControl))]
public class MarqueeItemsControl : Control
{
    private Canvas _canvas;
    private ItemsControl _itemsHost;
    private TranslateTransform _translateTransform;
    private Storyboard _marqueeStoryboard;

    static MarqueeItemsControl()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(MarqueeItemsControl),
            new FrameworkPropertyMetadata(typeof(MarqueeItemsControl)));
    }

    public MarqueeItemsControl()
    {
        Unloaded += OnUnloaded;
    }

    // ------------------------------------------------------------------------
    // Visual Dependency Properties
    // ------------------------------------------------------------------------

    // Standard ItemsSource property. We listen to changes so we can restart the animation.
    public IEnumerable ItemsSource
    {
        get => (IEnumerable)GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }
    public static readonly DependencyProperty ItemsSourceProperty =
        DependencyProperty.Register(nameof(ItemsSource), typeof(IEnumerable), typeof(MarqueeItemsControl),
            new PropertyMetadata(null, OnItemsSourceChanged));

    public double ScrollSpeed
    {
        get => (double)GetValue(ScrollSpeedProperty);
        set => SetValue(ScrollSpeedProperty, value);
    }
    public static readonly DependencyProperty ScrollSpeedProperty =
        DependencyProperty.Register(nameof(ScrollSpeed), typeof(double), typeof(MarqueeItemsControl),
            new PropertyMetadata(50.0, (d, e) => ((MarqueeItemsControl)d).RestartMarquee()));
    
    public static readonly DependencyProperty TitleProperty =
        DependencyProperty.Register(
            nameof(Title),
            typeof(string),
            typeof(MarqueeItemsControl),
            new PropertyMetadata("اخبار"));

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    // Add ItemTemplate to allow passing custom visual structure from the outside
    public static readonly DependencyProperty ItemTemplateProperty =
        DependencyProperty.Register(
            nameof(ItemTemplate),
            typeof(DataTemplate),
            typeof(MarqueeItemsControl),
            new PropertyMetadata(null));

    public DataTemplate ItemTemplate
    {
        get => (DataTemplate)GetValue(ItemTemplateProperty);
        set => SetValue(ItemTemplateProperty, value);
    }

    // Add ItemTemplateSelector in case there are multiple types (e.g., strings and RSS items)
    public static readonly DependencyProperty ItemTemplateSelectorProperty =
        DependencyProperty.Register(
            nameof(ItemTemplateSelector),
            typeof(DataTemplateSelector),
            typeof(MarqueeItemsControl),
            new PropertyMetadata(null));

    public DataTemplateSelector ItemTemplateSelector
    {
        get => (DataTemplateSelector)GetValue(ItemTemplateSelectorProperty);
        set => SetValue(ItemTemplateSelectorProperty, value);
    }
    // ------------------------------------------------------------------------
    // Event Handlers & Animation
    // ------------------------------------------------------------------------

    private static void OnItemsSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is MarqueeItemsControl control)
        {
            // Unsubscribe from old collection if it was observable
            if (e.OldValue is INotifyCollectionChanged oldCollection)
                oldCollection.CollectionChanged -= control.OnCollectionChanged;

            // Subscribe to new collection to auto-restart animation when items change (e.g. via ViewModel)
            if (e.NewValue is INotifyCollectionChanged newCollection)
                newCollection.CollectionChanged += control.OnCollectionChanged;

            control.QueueRestartMarquee();
        }
    }

    private void OnCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
    {
        QueueRestartMarquee();
    }

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();

        if (_canvas != null) _canvas.SizeChanged -= OnContainerSizeChanged;
        if (_itemsHost != null) _itemsHost.SizeChanged -= OnContainerSizeChanged;

        _canvas = GetTemplateChild("PART_Canvas") as Canvas;
        _itemsHost = GetTemplateChild("PART_ItemsHost") as ItemsControl;

        if (_itemsHost != null)
        {
            _translateTransform = new TranslateTransform();
            _itemsHost.RenderTransform = _translateTransform;
            _itemsHost.SizeChanged += OnContainerSizeChanged;
        }

        if (_canvas != null)
        {
            _canvas.SizeChanged += OnContainerSizeChanged;
        }
    }

    private void OnContainerSizeChanged(object sender, SizeChangedEventArgs e)
    {
        QueueRestartMarquee();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        StopMarquee();
    }

    // Using DispatcherPriority.Loaded ensures the UI has fully measured and arranged 
    // the new items before we try to calculate the ActualWidth for the animation.
    private void QueueRestartMarquee()
    {
        _ = Dispatcher.InvokeAsync(RestartMarquee, DispatcherPriority.Loaded);
    }

    private void RestartMarquee()
    {
        StopMarquee();

        if (_canvas == null || _itemsHost == null || _translateTransform == null ||
            _canvas.ActualWidth == 0 || _itemsHost.ActualWidth == 0 || ScrollSpeed <= 0)
            return;

        var startX = _canvas.ActualWidth;
        var endX = -_itemsHost.ActualWidth;
        var distance = startX - endX;
        var duration = TimeSpan.FromSeconds(distance / ScrollSpeed);

        var animation = new DoubleAnimation
        {
            From = startX,
            To = endX,
            Duration = new Duration(duration),
            RepeatBehavior = RepeatBehavior.Forever
        };

        _marqueeStoryboard = new Storyboard();
        Storyboard.SetTarget(animation, _itemsHost);
        Storyboard.SetTargetProperty(animation, new PropertyPath("(UIElement.RenderTransform).(TranslateTransform.X)"));

        _marqueeStoryboard.Children.Add(animation);
        _marqueeStoryboard.Begin();
    }

    private void StopMarquee()
    {
        if (_marqueeStoryboard != null)
        {
            _marqueeStoryboard.Stop();
            _marqueeStoryboard.Children.Clear();
            _marqueeStoryboard = null;
        }
    }
}
