using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using System.Xml;
using SimurghDashboard.Services.Ticker;

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

    // State fields for managing items independently of the UI
    private IEnumerable<object> _lastFetchedItems = Array.Empty<object>();
    private string _currentErrorMessage;

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

    // Defines custom notifications to be dynamically prepended to the ticker.
    // Changing this collection updates the marquee immediately.
    public IEnumerable<object> Notifications
    {
        get => (IEnumerable<object>)GetValue(NotificationsProperty);
        set => SetValue(NotificationsProperty, value);
    }
    public static readonly DependencyProperty NotificationsProperty =
        DependencyProperty.Register(nameof(Notifications), typeof(IEnumerable<object>), typeof(RssTicker),
            new PropertyMetadata(null, OnNotificationsChanged));

    #endregion

    #region Message Dependency Properties

    public string LoadingMessage
    {
        get => (string)GetValue(LoadingMessageProperty);
        set => SetValue(LoadingMessageProperty, value);
    }
    public static readonly DependencyProperty LoadingMessageProperty =
        DependencyProperty.Register(nameof(LoadingMessage), typeof(string), typeof(RssTicker),
            new PropertyMetadata("درحال دریافت اطلاعات..."));

    public string GeneralErrorMessage
    {
        get => (string)GetValue(GeneralErrorMessageProperty);
        set => SetValue(GeneralErrorMessageProperty, value);
    }
    public static readonly DependencyProperty GeneralErrorMessageProperty =
        DependencyProperty.Register(nameof(GeneralErrorMessage), typeof(string), typeof(RssTicker),
            new PropertyMetadata("خطای ناشناخته در دریافت اخبار رخ داده است."));

    public string NetworkErrorMessage
    {
        get => (string)GetValue(NetworkErrorMessageProperty);
        set => SetValue(NetworkErrorMessageProperty, value);
    }
    public static readonly DependencyProperty NetworkErrorMessageProperty =
        DependencyProperty.Register(nameof(NetworkErrorMessage), typeof(string), typeof(RssTicker),
            new PropertyMetadata("خطا در ارتباط با سرور. لطفاً اتصال شبکه را بررسی کنید."));

    public string ParsingErrorMessage
    {
        get => (string)GetValue(ParsingErrorMessageProperty);
        set => SetValue(ParsingErrorMessageProperty, value);
    }
    public static readonly DependencyProperty ParsingErrorMessageProperty =
        DependencyProperty.Register(nameof(ParsingErrorMessage), typeof(string), typeof(RssTicker),
            new PropertyMetadata("ساختار فایل خبری (RSS) نامعتبر است."));

    public string TimeoutErrorMessage
    {
        get => (string)GetValue(TimeoutErrorMessageProperty);
        set => SetValue(TimeoutErrorMessageProperty, value);
    }
    public static readonly DependencyProperty TimeoutErrorMessageProperty =
        DependencyProperty.Register(nameof(TimeoutErrorMessage), typeof(string), typeof(RssTicker),
            new PropertyMetadata("مهلت زمان درخواست به پایان رسید (Timeout)."));

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

    private static void OnNotificationsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is RssTicker ticker)
        {
            // Re-render items immediately when notifications update
            ticker.UpdateDisplayItems();
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
        _currentErrorMessage = null;

        // Render loading state (combined with any active notifications)
        UpdateDisplayItems();

        try
        {
            var urlsArray = FeedUrls.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries)
                                    .Select(u => u.Trim())
                                    .ToArray();

            var items = await FeedService.GetMultipleFeedsAsync(_cts.Token, urlsArray);

            // Cache the successfully fetched items
            _lastFetchedItems = items.Cast<object>().ToList();
        }
        catch (OperationCanceledException) when (_cts.IsCancellationRequested)
        {
            // Ignore explicit task cancellation safely
        }
        catch (Exception ex)
        {
            HasError = true;

            if (ex is HttpRequestException or SocketException or System.Net.WebException)
            {
                _currentErrorMessage = NetworkErrorMessage;
            }
            else if (ex is XmlException or FormatException)
            {
                _currentErrorMessage = ParsingErrorMessage;
            }
            else if (ex is TimeoutException or TaskCanceledException)
            {
                _currentErrorMessage = TimeoutErrorMessage;
            }
            else
            {
                _currentErrorMessage = GeneralErrorMessage;
            }
        }
        finally
        {
            IsLoading = false;

            // Re-render final state (items or error) alongside notifications
            UpdateDisplayItems();
        }
    }

    /// <summary>
    /// Aggregates Notifications, active state messages (Loading/Error), and fetched RSS items.
    /// Ensures Notifications are always displayed even if RSS feed fails or is loading.
    /// </summary>
    private void UpdateDisplayItems()
    {
        if (_itemsHost == null) return;

        var displayItems = new List<object>();

        // 1. Add active notifications first
        if (Notifications != null)
        {
            displayItems.AddRange(Notifications);
        }

        // 2. Add current state (Loading vs Error vs Fetched RSS Items)
        if (IsLoading && !string.IsNullOrWhiteSpace(LoadingMessage))
        {
            displayItems.Add(LoadingMessage);
        }
        else if (HasError && !string.IsNullOrWhiteSpace(_currentErrorMessage))
        {
            displayItems.Add(_currentErrorMessage);
        }
        else if (_lastFetchedItems != null && _lastFetchedItems.Any())
        {
            displayItems.AddRange(_lastFetchedItems);
        }

        // Update the control and restart the animation
        _itemsHost.ItemsSource = displayItems;
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
