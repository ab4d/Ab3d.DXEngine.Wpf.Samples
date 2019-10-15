using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security;
using System.Windows;
using System.Windows.Data;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
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
            XmlElement str = value as XmlElement;
            if (str != null)
            {
                XmlAttribute attribute = str.Attributes["Description"];
                if (attribute != null)
                {
                    string foo = attribute.Value;
                    if (foo != null && foo.Length > 0)
                    {
                        if (targetType == typeof(Visibility))
                        {
                            return Visibility.Visible;
                        }
                        else
                        {
                            return true;
                        }
                    }
                }
            }
            if (targetType == typeof(Visibility))
            {
                return Visibility.Collapsed;
            }
            else
            {
                return false;
            }
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