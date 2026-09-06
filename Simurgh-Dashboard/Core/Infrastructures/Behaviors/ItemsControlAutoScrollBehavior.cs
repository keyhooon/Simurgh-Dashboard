using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;

namespace SimurghDashboard.Behaviors;

/// <summary>
/// Attached behavior providing smooth, continuous auto-scrolling for ScrollViewer 
/// using a single WPF DoubleAnimation (SineEase) without CPU-heavy DispatcherTimers.
/// </summary>
public static class ScrollViewerAutoScrollBehavior
{
    private static readonly object AnimationLock = new();

    // ------------------------------------------------------------------
    // Dependency Properties
    // ------------------------------------------------------------------

    public static readonly DependencyProperty IsEnabledProperty =
        DependencyProperty.RegisterAttached(
            "IsEnabled",
            typeof(bool),
            typeof(ScrollViewerAutoScrollBehavior),
            new PropertyMetadata(false, OnIsEnabledChanged));

    public static readonly DependencyProperty DurationProperty =
        DependencyProperty.RegisterAttached(
            "Duration",
            typeof(double),
            typeof(ScrollViewerAutoScrollBehavior),
            new PropertyMetadata(15.0));

    public static bool GetIsEnabled(DependencyObject obj) => (bool)obj.GetValue(IsEnabledProperty);
    public static void SetIsEnabled(DependencyObject obj, bool value) => obj.SetValue(IsEnabledProperty, value);

    public static double GetDuration(DependencyObject obj) => (double)obj.GetValue(DurationProperty);
    public static void SetDuration(DependencyObject obj, double value) => obj.SetValue(DurationProperty, value);

    // ------------------------------------------------------------------
    // Lifecycle Event Handlers
    // ------------------------------------------------------------------

    private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not ScrollViewer sv) return;

        if ((bool)e.NewValue)
        {
            sv.Loaded += OnLoaded;
            sv.Unloaded += OnUnloaded;
            sv.ScrollChanged += OnScrollChanged;

            if (sv.IsLoaded)
            {
                StartAnimation(sv);
            }
        }
        else
        {
            sv.Loaded -= OnLoaded;
            sv.Unloaded -= OnUnloaded;
            sv.ScrollChanged -= OnScrollChanged;
            StopAnimation(sv);
        }
    }

    private static void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is ScrollViewer sv && GetIsEnabled(sv))
        {
            StartAnimation(sv);
        }
    }

    private static void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (sender is ScrollViewer sv)
        {
            StopAnimation(sv);
        }
    }

    private static void OnScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        // Re-evaluate animation when layout bounds shift (items added/removed or resized)
        if (sender is ScrollViewer sv && GetIsEnabled(sv) && e.ExtentHeightChange != 0)
        {
            StartAnimation(sv);
        }
    }

    // ------------------------------------------------------------------
    // Animation Controller Methods
    // ------------------------------------------------------------------

    private static void StopAnimation(ScrollViewer sv)
    {
        lock (AnimationLock)
        {
            sv.BeginAnimation(ScrollViewer.VerticalOffsetProperty, null);
        }
    }

    private static void StartAnimation(ScrollViewer sv)
    {
        if (!GetIsEnabled(sv)) return;

        // If content fits completely within viewport, stop scroll animation
        if (sv.ScrollableHeight <= 0)
        {
            StopAnimation(sv);
            return;
        }

        lock (AnimationLock)
        {
            sv.BeginAnimation(ScrollViewer.VerticalOffsetProperty, null);

            var animation = new DoubleAnimation
            {
                From = 0,
                To = sv.ScrollableHeight,
                Duration = new Duration(TimeSpan.FromSeconds(GetDuration(sv))),
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever,
                EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
            };

            sv.BeginAnimation(ScrollViewer.VerticalOffsetProperty, animation, HandoffBehavior.SnapshotAndReplace);
        }
    }
}
