using System;
using System.Globalization;
using System.Windows.Data;

namespace Ab3d.DXEngine.Wpf.Samples.Common
{
    // Replaces all "\\n" to Environment.NewLine
    public class LineBreakableStringConverter : IValueConverter
    {
        public object Convert(
            object value,
            Type targetType,
            object parameter,
            CultureInfo culture)
        {
            string str;

            str = value as string;

            if (str != null)
                return str.Replace("\\n", Environment.NewLine);

            return null;
        }

        public object ConvertBack(
            object value,
            Type targetType,
            object parameter,
            CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}