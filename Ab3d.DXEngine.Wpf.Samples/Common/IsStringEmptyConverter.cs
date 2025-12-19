using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Xml;

namespace Ab3d.DXEngine.Wpf.Samples.Common
{
    // From: Kevin Moore's Bag-o-Tricks (http://j832.com/bagotricks)
    public class IsStringEmptyConverter : IValueConverter
    {
        public object Convert(
            object value,
            Type targetType,
            object parameter,
            CultureInfo culture)
        {
            var xmlElement = value as XmlElement;
            if (xmlElement != null)
            {
                var descriptionAttribute = xmlElement.Attributes["Description"];
                if (descriptionAttribute != null)
                {
                    if (!string.IsNullOrEmpty(descriptionAttribute.Value))
                    {
                        if (targetType == typeof(Visibility))
                            return Visibility.Visible;

                        return true;
                    }
                }
            }

            if (targetType == typeof(Visibility))
                return Visibility.Collapsed;

            return false;
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