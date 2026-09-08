using System.Globalization;
using System.Windows.Data;
using System.Windows.Markup;

namespace SimurghDashboard.Clock.Services.Weather
{
    /// <summary>
    /// Complete, exhaustive 1:1 mapper for all 44 WWO condition codes to 
    /// Weather Icons webfont unicode glyphs (Day & Night variants).
    /// Reference: erikflowers/weather-icons (wwo mapping table)
    /// </summary>
    [ValueConversion(typeof(int), typeof(char))]
    public class WeatherCodeToFontGlyphConverter : MarkupExtension, IValueConverter, IMultiValueConverter
    {
        // Default fallback glyph: wi-na
        private const char WiNa = '\uf07b';

        // Full mapping table: Code -> (DayGlyph, NightGlyph)
        private static readonly Dictionary<int, (char Day, char Night)> WwoFullGlyphMap = new()
        {
            // 113: Clear/Sunny
            [113] = ('\uf00d', '\uf02e'), // wi-day-sunny / wi-night-clear

            // 116: Partly Cloudy
            [116] = ('\uf002', '\uf083'), // wi-day-cloudy / wi-night-alt-cloudy

            // 119: Cloudy
            [119] = ('\uf013', '\uf013'), // wi-cloudy

            // 122: Overcast
            [122] = ('\uf041', '\uf041'), // wi-day-sunny-overcast / wi-night-alt-cloudy-high (standard wi-cloudy / wi-day-cloudy-high)

            // 143: Mist
            [143] = ('\uf003', '\uf04a'), // wi-day-fog / wi-night-fog

            // 176: Patchy rain nearby
            [176] = ('\uf008', '\uf028'), // wi-day-rain / wi-night-alt-rain

            // 179: Patchy snow nearby
            [179] = ('\uf00a', '\uf02a'), // wi-day-snow / wi-night-alt-snow

            // 182: Patchy sleet nearby
            [182] = ('\uf0b2', '\uf0b4'), // wi-day-sleet / wi-night-alt-sleet

            // 185: Patchy freezing drizzle nearby
            [185] = ('\uf0b2', '\uf0b4'), // wi-day-sleet / wi-night-alt-sleet

            // 200: Thundery outbreaks in nearby
            [200] = ('\uf010', '\uf02d'), // wi-day-thunderstorm / wi-night-alt-thunderstorm

            // 227: Blowing snow
            [227] = ('\uf064', '\uf064'), // wi-snow-wind

            // 230: Blizzard
            [230] = ('\uf064', '\uf064'), // wi-snow-wind

            // 248: Fog
            [248] = ('\uf014', '\uf04a'), // wi-fog / wi-night-fog

            // 260: Freezing fog
            [260] = ('\uf014', '\uf04a'), // wi-fog / wi-night-fog

            // 263: Patchy light drizzle
            [263] = ('\uf009', '\uf029'), // wi-day-showers / wi-night-alt-showers

            // 266: Light drizzle
            [266] = ('\uf01c', '\uf01c'), // wi-sprinkle

            // 281: Freezing drizzle
            [281] = ('\uf0b2', '\uf0b4'), // wi-day-sleet / wi-night-alt-sleet

            // 284: Heavy freezing drizzle
            [284] = ('\uf0b2', '\uf0b4'), // wi-day-sleet / wi-night-alt-sleet

            // 293: Patchy light rain
            [293] = ('\uf008', '\uf028'), // wi-day-rain / wi-night-alt-rain

            // 296: Light rain
            [296] = ('\uf019', '\uf019'), // wi-rain

            // 299: Moderate rain at times
            [299] = ('\uf008', '\uf028'), // wi-day-rain / wi-night-alt-rain

            // 302: Moderate rain
            [302] = ('\uf019', '\uf019'), // wi-rain

            // 305: Heavy rain at times
            [305] = ('\uf008', '\uf028'), // wi-day-rain / wi-night-alt-rain

            // 308: Heavy rain
            [308] = ('\uf019', '\uf019'), // wi-rain

            // 311: Light freezing rain
            [311] = ('\uf006', '\uf026'), // wi-day-rain-mix / wi-night-alt-rain-mix

            // 314: Moderate or Heavy freezing rain
            [314] = ('\uf006', '\uf026'), // wi-day-rain-mix / wi-night-alt-rain-mix

            // 317: Light sleet
            [317] = ('\uf0b2', '\uf0b4'), // wi-day-sleet / wi-night-alt-sleet

            // 320: Moderate or heavy sleet
            [320] = ('\uf0b2', '\uf0b4'), // wi-day-sleet / wi-night-alt-sleet

            // 323: Patchy light snow
            [323] = ('\uf00a', '\uf02a'), // wi-day-snow / wi-night-alt-snow

            // 326: Light snow
            [326] = ('\uf01b', '\uf01b'), // wi-snow

            // 329: Patchy moderate snow
            [329] = ('\uf00a', '\uf02a'), // wi-day-snow / wi-night-alt-snow

            // 332: Moderate snow
            [332] = ('\uf01b', '\uf01b'), // wi-snow

            // 335: Patchy heavy snow
            [335] = ('\uf00a', '\uf02a'), // wi-day-snow / wi-night-alt-snow

            // 338: Heavy snow
            [338] = ('\uf01b', '\uf01b'), // wi-snow

            // 350: Ice pellets
            [350] = ('\uf015', '\uf015'), // wi-hail

            // 353: Light rain shower
            [353] = ('\uf009', '\uf029'), // wi-day-showers / wi-night-alt-showers

            // 356: Moderate or heavy rain shower
            [356] = ('\uf009', '\uf029'), // wi-day-showers / wi-night-alt-showers

            // 359: Torrential rain shower
            [359] = ('\uf009', '\uf029'), // wi-day-showers / wi-night-alt-showers

            // 362: Light sleet showers
            [362] = ('\uf0b2', '\uf0b4'), // wi-day-sleet / wi-night-alt-sleet

            // 365: Moderate or heavy sleet showers
            [365] = ('\uf0b2', '\uf0b4'), // wi-day-sleet / wi-night-alt-sleet

            // 368: Light snow showers
            [368] = ('\uf00a', '\uf02a'), // wi-day-snow / wi-night-alt-snow

            // 371: Moderate or heavy snow showers
            [371] = ('\uf00a', '\uf02a'), // wi-day-snow / wi-night-alt-snow

            // 374: Light showers of ice pellets
            [374] = ('\uf015', '\uf015'), // wi-hail

            // 377: Moderate or heavy showers of ice pellets
            [377] = ('\uf015', '\uf015'), // wi-hail

            // 386: Patchy light rain with thunder
            [386] = ('\uf00e', '\uf02c'), // wi-day-storm-showers / wi-night-alt-storm-showers

            // 389: Moderate or heavy rain with thunder
            [389] = ('\uf01e', '\uf01e'), // wi-thunderstorm

            // 392: Patchy light snow with thunder
            [392] = ('\uf010', '\uf02d'), // wi-day-snow-thunderstorm / wi-night-alt-snow-thunderstorm

            // 395: Moderate or heavy snow with thunder
            [395] = ('\uf010', '\uf02d')  // wi-day-snow-thunderstorm / wi-night-alt-snow-thunderstorm
        };

        public static string GetGlyph(int weatherCode, bool isDay = true)
        {
            if (WwoFullGlyphMap.TryGetValue(weatherCode, out var glyphPair))
            {
                return (isDay ? glyphPair.Day : glyphPair.Night).ToString();
            }

            return WiNa.ToString();
        }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int code || (value != null && int.TryParse(value.ToString(), out code)))
            {
                return GetGlyph(code, isDay: true);
            }
            return WiNa.ToString();
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            throw new NotSupportedException();

        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values != null && values.Length >= 1)
            {
                int code = 0;
                bool isDay = true;

                if (values[0] is int c) code = c;
                else if (values[0] != null) int.TryParse(values[0].ToString(), out code);

                if (values.Length > 1 && values[1] is bool d)
                {
                    isDay = d;
                }

                return GetGlyph(code, isDay);
            }
            return WiNa.ToString();
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) =>
            throw new NotSupportedException();

        public override object? ProvideValue(IServiceProvider serviceProvider)
        {
            return new WeatherCodeToFontGlyphConverter();
        }
    }
}
