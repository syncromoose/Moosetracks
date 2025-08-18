using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace MooseTracks
{
    public class ColorConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length == 3 &&
                values[0] is double r &&
                values[1] is double g &&
                values[2] is double b)
            {
                // Detect if the input is normalized (0–1) or in 0–255
                if (r <= 1 && g <= 1 && b <= 1)
                {
                    r *= 255;
                    g *= 255;
                    b *= 255;
                }

                byte rb = (byte)Math.Clamp(Math.Round(r), 0, 255);
                byte gb = (byte)Math.Clamp(Math.Round(g), 0, 255);
                byte bb = (byte)Math.Clamp(Math.Round(b), 0, 255);

                return new SolidColorBrush(Color.FromRgb(rb, gb, bb));
            }

            return new SolidColorBrush(Colors.Black);
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
