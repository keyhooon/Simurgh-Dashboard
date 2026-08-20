using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using SimurghDashboard.Services;

namespace SimurghDashboard.Controls;

[TemplatePart(Name = "PART_Canvas", Type = typeof(Canvas))]
[TemplatePart(Name = "PART_ItemsHost", Type = typeof(ItemsControl))]
public class RssTicker : Control
{
    private Canvas _canvas;
    private ItemsControl _itemsHost;
    private TranslateTransform _translateTransform;
    private Storyboard _marqueeStoryboard;
    private readonly DispatcherTimer _refreshTimer;
    private CancellationTokenSource _cts;

    static RssTicker()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(RssTicker), new FrameworkPropertyMetadata(typeof(RssTicker)));
    }

    public RssTicker()
    {
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;

        _refreshTimer = new DispatcherTimer();
        _refreshTimer.Tick += async (s, e) => await RefreshFeedAsync();
    }

    #region Dependency Properties

    // Property name changed to FeedUrls to support multiple links
    public string FeedUrls
    {
        get => (string)GetValue(FeedUrlsProperty);
        set => SetValue(FeedUrlsProperty, value);
    }
    public static readonly DependencyProperty FeedUrlsProperty =
        DependencyProperty.Register(nameof(FeedUrls), typeof(string), typeof(RssTicker),
            new PropertyMetadata(null, OnFeedConfigChanged));

    public IRssFeedService FeedService
    {
        get => (IRssFeedService)GetValue(FeedServiceProperty);
        set => SetValue(FeedServiceProperty, value);
    }
    public static readonly DependencyProperty FeedServiceProperty =
        DependencyProperty.Register(nameof(FeedService), typeof(IRssFeedService), typeof(RssTicker),
            new PropertyMetadata(null, OnFeedConfigChanged));

    public TimeSpan RefreshInterval
    {
        get => (TimeSpan)GetValue(RefreshIntervalProperty);
        set => SetValue(RefreshIntervalProperty, value);
    }
    public static readonly DependencyProperty RefreshIntervalProperty =
        DependencyProperty.Register(nameof(RefreshInterval), typeof(TimeSpan), typeof(RssTicker),
            new PropertyMetadata(TimeSpan.FromMinutes(10), OnIntervalChanged));

    public double ScrollSpeed
    {
        get => (double)GetValue(ScrollSpeedProperty);
        set => SetValue(ScrollSpeedProperty, value);
    }
    public static readonly DependencyProperty ScrollSpeedProperty =
        DependencyProperty.Register(nameof(ScrollSpeed), typeof(double), typeof(RssTicker),
            new PropertyMetadata(50.0, (d, e) => ((RssTicker)d).RestartMarquee()));

    public bool IsLoading
    {
        get => (bool)GetValue(IsLoadingProperty);
set => SetValue(IsLoadingPropertyKey, value);
    }
    private static readonly DependencyPropertyKey IsLoadingPropertyKey =
        DependencyProperty.RegisterReadOnly(nameof(IsLoading), typeof(bool), typeof(RssTicker), new PropertyMetadata(false));
    public static readonly DependencyProperty IsLoadingProperty = IsLoadingPropertyKey.DependencyProperty;

    public bool HasError
    {
        get => (bool)GetValue(HasErrorProperty);
        set => SetValue(HasErrorPropertyKey, value);
    }
    private static readonly DependencyPropertyKey HasErrorPropertyKey =
        DependencyProperty.RegisterReadOnly(nameof(HasError), typeof(bool), typeof(RssTicker), new PropertyMetadata(false));
    public static readonly DependencyProperty HasErrorProperty = HasErrorPropertyKey.DependencyProperty;

    #endregion

    private static void OnFeedConfigChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is RssTicker { IsLoaded: true } ticker)
        {
            _ = ticker.RefreshFeedAsync();
        }
    }

    private static void OnIntervalChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is RssTicker ticker)
        {
            ticker._refreshTimer.Interval = (TimeSpan)e.NewValue;
        }
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
        RestartMarquee();
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        _refreshTimer.Interval = RefreshInterval;
        _refreshTimer.Start();
        await RefreshFeedAsync();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _refreshTimer.Stop();
        CancelCurrentOperation();
        StopMarquee();
    }

    public async Task RefreshFeedAsync()
    {
        if (FeedService == null || string.IsNullOrWhiteSpace(FeedUrls))
            return;

        CancelCurrentOperation();
        _cts = new CancellationTokenSource();

        IsLoading = true;
        HasError = false;

        try
        {
            // Split URLs by comma or semicolon to create an array
            string[] urlsArray = FeedUrls.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                                         .Select(u => u.Trim())
                                         .ToArray();

            // Fetch multiple feeds concurrently
            var items = await FeedService.GetMultipleFeedsAsync(_cts.Token, urlsArray);

            if (_itemsHost != null)
            {
                _itemsHost.ItemsSource = items.ToList();
                await Dispatcher.InvokeAsync(RestartMarquee, DispatcherPriority.Loaded);
            }
        }
        catch (OperationCanceledException) { }
        catch
        {
            HasError = true;
            if (_itemsHost != null) _itemsHost.ItemsSource = null;
            StopMarquee();
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void RestartMarquee()
    {
        StopMarquee();

        if (_canvas == null || _itemsHost == null || _translateTransform == null ||
            _canvas.ActualWidth == 0 || _itemsHost.ActualWidth == 0 || ScrollSpeed <= 0)
            return;

        // Marquee animation settings for Right-to-Left (RTL) flow
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

    private void CancelCurrentOperation()
    {
        if (_cts != null && !_cts.IsCancellationRequested)
        {
            _cts.Cancel();
            _cts.Dispose();
            _cts = null;
        }
    }
}
