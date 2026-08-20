using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace SimurghDashboard.Controls
{
    public class NineSliceBorder : FrameworkElement
    {
        // === Dependency Properties ===
        public static readonly DependencyProperty SourceProperty =
            DependencyProperty.Register(nameof(Source), typeof(ImageSource), typeof(NineSliceBorder),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender | FrameworkPropertyMetadataOptions.AffectsMeasure, OnSourceOrThicknessChanged));

        public static readonly DependencyProperty SliceThicknessProperty =
            DependencyProperty.Register(nameof(SliceThickness), typeof(Thickness), typeof(NineSliceBorder),
                new FrameworkPropertyMetadata(new Thickness(10), FrameworkPropertyMetadataOptions.AffectsRender | FrameworkPropertyMetadataOptions.AffectsMeasure, OnSourceOrThicknessChanged));

        public ImageSource Source { get => (ImageSource)GetValue(SourceProperty); set => SetValue(SourceProperty, value); }
        public Thickness SliceThickness { get => (Thickness)GetValue(SliceThicknessProperty); set => SetValue(SliceThicknessProperty, value); }

        private static void OnSourceOrThicknessChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((NineSliceBorder)d)._isSourceDirty = true;
        }

        // === Private Fields ===
        private readonly CroppedBitmap[] _slices = new CroppedBitmap[9];
        private bool _isSourceDirty = true;

        protected override Size ArrangeOverride(Size arrangeSize)
        {
            UpdateSlices();
            return base.ArrangeOverride(arrangeSize);
        }

        private void UpdateSlices()
        {
            if (!_isSourceDirty || Source is not BitmapSource bitmap) return;

            var sL = SliceThickness.Left;
            var sT = SliceThickness.Top;
            var sR = SliceThickness.Right;
            var sB = SliceThickness.Bottom;

            var imgW = bitmap.PixelWidth;
            var imgH = bitmap.PixelHeight;

            var cW = Math.Max(0, imgW - (int)sL - (int)sR);
            var cH = Math.Max(0, imgH - (int)sT - (int)sB);

            // Create 9 slices and cache them
            _slices[0] = new CroppedBitmap(bitmap, new Int32Rect(0, 0, (int)sL, (int)sT));           // TL
            _slices[1] = new CroppedBitmap(bitmap, new Int32Rect((int)sL, 0, cW, (int)sT));          // TC
            _slices[2] = new CroppedBitmap(bitmap, new Int32Rect(imgW - (int)sR, 0, (int)sR, (int)sT));// TR
            _slices[3] = new CroppedBitmap(bitmap, new Int32Rect(0, (int)sT, (int)sL, cH));          // ML
            _slices[4] = new CroppedBitmap(bitmap, new Int32Rect((int)sL, (int)sT, cW, cH));         // MC
            _slices[5] = new CroppedBitmap(bitmap, new Int32Rect(imgW - (int)sR, (int)sT, (int)sR, cH));// MR
            _slices[6] = new CroppedBitmap(bitmap, new Int32Rect(0, imgH - (int)sB, (int)sL, (int)sB));// BL
            _slices[7] = new CroppedBitmap(bitmap, new Int32Rect((int)sL, imgH - (int)sB, cW, (int)sB));// BC
            _slices[8] = new CroppedBitmap(bitmap, new Int32Rect(imgW - (int)sR, imgH - (int)sB, (int)sR, (int)sB));// BR

            _isSourceDirty = false;
        }

        protected override void OnRender(DrawingContext dc)
        {
            if (_isSourceDirty || _slices[0] == null) return;

            var dW = ActualWidth;
            var dH = ActualHeight;
            var midW = Math.Max(0, dW - SliceThickness.Left - SliceThickness.Right);
            var midH = Math.Max(0, dH - SliceThickness.Top - SliceThickness.Bottom);

            // Define destination rectangles (calculated on the fly for layout)
            var dst = new Rect[9]
            {
                new Rect(0, 0, SliceThickness.Left, SliceThickness.Top),                      // TL
                new Rect(SliceThickness.Left, 0, midW, SliceThickness.Top),                   // TC
                new Rect(dW - SliceThickness.Right, 0, SliceThickness.Right, SliceThickness.Top),// TR
                new Rect(0, SliceThickness.Top, SliceThickness.Left, midH),                   // ML
                new Rect(SliceThickness.Left, SliceThickness.Top, midW, midH),                // MC
                new Rect(dW - SliceThickness.Right, SliceThickness.Top, SliceThickness.Right, midH),// MR
                new Rect(0, dH - SliceThickness.Bottom, SliceThickness.Left, SliceThickness.Bottom),// BL
                new Rect(SliceThickness.Left, dH - SliceThickness.Bottom, midW, SliceThickness.Bottom),// BC
                new Rect(dW - SliceThickness.Right, dH - SliceThickness.Bottom, SliceThickness.Right, SliceThickness.Bottom)// BR
            };

            // High-speed rendering: just draw pre-cropped segments
            for (var i = 0; i < 9; i++)
            {
                if (dst[i].Width > 0 && dst[i].Height > 0)
                    dc.DrawImage(_slices[i], dst[i]);
            }
        }
    }
}
