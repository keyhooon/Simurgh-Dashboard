using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using SimurghDashboard.Clock.Controls;

namespace SimurghDashboard.Core.Controls
{
    public class WidgetFrame : ContentControl
    {
        static WidgetFrame()
        {
            DefaultStyleKeyProperty.OverrideMetadata(
                typeof(WidgetFrame),
                new FrameworkPropertyMetadata(typeof(WidgetFrame)));
            BackgroundProperty.OverrideMetadata(typeof(WidgetFrame),
                new FrameworkPropertyMetadata(Brushes.Transparent));

            BorderBrushProperty.OverrideMetadata(typeof(WidgetFrame),
                new FrameworkPropertyMetadata(Brushes.Transparent));

            BorderThicknessProperty.OverrideMetadata(typeof(WidgetFrame),
                new FrameworkPropertyMetadata(new Thickness(0)));
        }

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            // Debug: template parts
            var rootBorder = GetTemplateChild("PART_RootBorder") as Border;
            var headerBorder = GetTemplateChild("PART_HeaderBorder") as Border;
            var contentPresenter = GetTemplateChild("PART_ContentPresenter") as ContentPresenter;
            var footerBorder = GetTemplateChild("PART_FooterBorder") as Border;
            var headerIcon = GetTemplateChild("PART_HeaderIcon") as Path;

            Debug.WriteLine(
                $"[WidgetFrame] Template applied. Root={rootBorder != null}, Header={headerBorder != null}, Content={contentPresenter != null}, Footer={footerBorder != null}, Icon={headerIcon != null}");
        }
        #region FrameVariant

        public WidgetFrameVariant FrameVariant
        {
            get => (WidgetFrameVariant)GetValue(FrameVariantProperty);
            set => SetValue(FrameVariantProperty, value);
        }

        public static readonly DependencyProperty FrameVariantProperty =
            DependencyProperty.Register(
                nameof(FrameVariant),
                typeof(WidgetFrameVariant),
                typeof(WidgetFrame),
                new FrameworkPropertyMetadata(
                    WidgetFrameVariant.RoundedRaised,
                    FrameworkPropertyMetadataOptions.AffectsRender | FrameworkPropertyMetadataOptions.AffectsMeasure));

        #endregion

        #region Header

        public object? Header
        {
            get => GetValue(HeaderProperty);
            set => SetValue(HeaderProperty, value);
        }

        public static readonly DependencyProperty HeaderProperty =
            DependencyProperty.Register(
                nameof(Header),
                typeof(object),
                typeof(WidgetFrame),
                new PropertyMetadata(null));

        #endregion

        #region HeaderTemplate

        public DataTemplate? HeaderTemplate
        {
            get => (DataTemplate?)GetValue(HeaderTemplateProperty);
            set => SetValue(HeaderTemplateProperty, value);
        }

        public static readonly DependencyProperty HeaderTemplateProperty =
            DependencyProperty.Register(
                nameof(HeaderTemplate),
                typeof(DataTemplate),
                typeof(WidgetFrame),
                new PropertyMetadata(null));

        #endregion

        #region HeaderTemplateSelector

        public DataTemplateSelector? HeaderTemplateSelector
        {
            get => (DataTemplateSelector?)GetValue(HeaderTemplateSelectorProperty);
            set => SetValue(HeaderTemplateSelectorProperty, value);
        }

        public static readonly DependencyProperty HeaderTemplateSelectorProperty =
            DependencyProperty.Register(
                nameof(HeaderTemplateSelector),
                typeof(DataTemplateSelector),
                typeof(WidgetFrame),
                new PropertyMetadata(null));

        #endregion

        #region HeaderIconGeometry

        public Geometry? HeaderIconGeometry
        {
            get => (Geometry?)GetValue(HeaderIconGeometryProperty);
            set => SetValue(HeaderIconGeometryProperty, value);
        }

        public static readonly DependencyProperty HeaderIconGeometryProperty =
            DependencyProperty.Register(
                nameof(HeaderIconGeometry),
                typeof(Geometry),
                typeof(WidgetFrame),
                new PropertyMetadata(null));

        #endregion

        #region HeaderIconBrush

        public Brush HeaderIconBrush
        {
            get => (Brush)GetValue(HeaderIconBrushProperty);
            set => SetValue(HeaderIconBrushProperty, value);
        }

        public static readonly DependencyProperty HeaderIconBrushProperty =
            DependencyProperty.Register(
                nameof(HeaderIconBrush),
                typeof(Brush),
                typeof(WidgetFrame),
                new PropertyMetadata(Brushes.White));

        #endregion

        #region HeaderIconSize

        public double HeaderIconSize
        {
            get => (double)GetValue(HeaderIconSizeProperty);
            set => SetValue(HeaderIconSizeProperty, value);
        }

        public static readonly DependencyProperty HeaderIconSizeProperty =
            DependencyProperty.Register(
                nameof(HeaderIconSize),
                typeof(double),
                typeof(WidgetFrame),
                new PropertyMetadata(16d));

        #endregion

        #region HeaderBadgeContent

        public object? HeaderBadgeContent
        {
            get => GetValue(HeaderBadgeContentProperty);
            set => SetValue(HeaderBadgeContentProperty, value);
        }

        public static readonly DependencyProperty HeaderBadgeContentProperty =
            DependencyProperty.Register(
                nameof(HeaderBadgeContent),
                typeof(object),
                typeof(WidgetFrame),
                new PropertyMetadata(null));

        #endregion

        #region HeaderBadgeTemplate

        public DataTemplate? HeaderBadgeTemplate
        {
            get => (DataTemplate?)GetValue(HeaderBadgeTemplateProperty);
            set => SetValue(HeaderBadgeTemplateProperty, value);
        }

        public static readonly DependencyProperty HeaderBadgeTemplateProperty =
            DependencyProperty.Register(
                nameof(HeaderBadgeTemplate),
                typeof(DataTemplate),
                typeof(WidgetFrame),
                new PropertyMetadata(null));

        #endregion

        #region HeaderBadgeTemplateSelector

        public DataTemplateSelector? HeaderBadgeTemplateSelector
        {
            get => (DataTemplateSelector?)GetValue(HeaderBadgeTemplateSelectorProperty);
            set => SetValue(HeaderBadgeTemplateSelectorProperty, value);
        }

        public static readonly DependencyProperty HeaderBadgeTemplateSelectorProperty =
            DependencyProperty.Register(
                nameof(HeaderBadgeTemplateSelector),
                typeof(DataTemplateSelector),
                typeof(WidgetFrame),
                new PropertyMetadata(null));

        #endregion

        #region FooterContent

        public object? FooterContent
        {
            get => GetValue(FooterContentProperty);
            set => SetValue(FooterContentProperty, value);
        }

        public static readonly DependencyProperty FooterContentProperty =
            DependencyProperty.Register(
                nameof(FooterContent),
                typeof(object),
                typeof(WidgetFrame),
                new PropertyMetadata(null));

        #endregion

        #region FooterTemplate

        public DataTemplate? FooterTemplate
        {
            get => (DataTemplate?)GetValue(FooterTemplateProperty);
            set => SetValue(FooterTemplateProperty, value);
        }

        public static readonly DependencyProperty FooterTemplateProperty =
            DependencyProperty.Register(
                nameof(FooterTemplate),
                typeof(DataTemplate),
                typeof(WidgetFrame),
                new PropertyMetadata(null));

        #endregion

        #region FooterTemplateSelector

        public DataTemplateSelector? FooterTemplateSelector
        {
            get => (DataTemplateSelector?)GetValue(FooterTemplateSelectorProperty);
            set => SetValue(FooterTemplateSelectorProperty, value);
        }

        public static readonly DependencyProperty FooterTemplateSelectorProperty =
            DependencyProperty.Register(
                nameof(FooterTemplateSelector),
                typeof(DataTemplateSelector),
                typeof(WidgetFrame),
                new PropertyMetadata(null));

        #endregion

        #region ShowFooter

        public bool ShowFooter
        {
            get => (bool)GetValue(ShowFooterProperty);
            set => SetValue(ShowFooterProperty, value);
        }

        public static readonly DependencyProperty ShowFooterProperty =
            DependencyProperty.Register(
                nameof(ShowFooter),
                typeof(bool),
                typeof(WidgetFrame),
                new PropertyMetadata(false));

        #endregion

        #region ShowHeaderSeparator

        public bool ShowHeaderSeparator
        {
            get => (bool)GetValue(ShowHeaderSeparatorProperty);
            set => SetValue(ShowHeaderSeparatorProperty, value);
        }

        public static readonly DependencyProperty ShowHeaderSeparatorProperty =
            DependencyProperty.Register(
                nameof(ShowHeaderSeparator),
                typeof(bool),
                typeof(WidgetFrame),
                new PropertyMetadata(true));

        #endregion

        #region ShowFooterSeparator

        public bool ShowFooterSeparator
        {
            get => (bool)GetValue(ShowFooterSeparatorProperty);
            set => SetValue(ShowFooterSeparatorProperty, value);
        }

        public static readonly DependencyProperty ShowFooterSeparatorProperty =
            DependencyProperty.Register(
                nameof(ShowFooterSeparator),
                typeof(bool),
                typeof(WidgetFrame),
                new PropertyMetadata(true));

        #endregion

        #region CornerRadius

        public CornerRadius CornerRadius
        {
            get => (CornerRadius)GetValue(CornerRadiusProperty);
            set => SetValue(CornerRadiusProperty, value);
        }

        public static readonly DependencyProperty CornerRadiusProperty =
            DependencyProperty.Register(
                nameof(CornerRadius),
                typeof(CornerRadius),
                typeof(WidgetFrame),
                new PropertyMetadata(new CornerRadius(8)));

        #endregion

        #region Padding Overrides

        public Thickness HeaderPadding
        {
            get => (Thickness)GetValue(HeaderPaddingProperty);
            set => SetValue(HeaderPaddingProperty, value);
        }

        public static readonly DependencyProperty HeaderPaddingProperty =
            DependencyProperty.Register(
                nameof(HeaderPadding),
                typeof(Thickness),
                typeof(WidgetFrame),
                new PropertyMetadata(new Thickness(12, 8, 12, 8)));

        public Thickness ContentPadding
        {
            get => (Thickness)GetValue(ContentPaddingProperty);
            set => SetValue(ContentPaddingProperty, value);
        }

        public static readonly DependencyProperty ContentPaddingProperty =
            DependencyProperty.Register(
                nameof(ContentPadding),
                typeof(Thickness),
                typeof(WidgetFrame),
                new PropertyMetadata(new Thickness(16)));

        public Thickness FooterPadding
        {
            get => (Thickness)GetValue(FooterPaddingProperty);
            set => SetValue(FooterPaddingProperty, value);
        }

        public static readonly DependencyProperty FooterPaddingProperty =
            DependencyProperty.Register(
                nameof(FooterPadding),
                typeof(Thickness),
                typeof(WidgetFrame),
                new PropertyMetadata(new Thickness(12, 4, 12, 4)));

        #endregion
    }
}
