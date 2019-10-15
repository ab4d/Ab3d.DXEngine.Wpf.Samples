using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace Ab3d.DirectX.Client.Diagnostics
{
    /// <summary>
    /// Interaction logic for WpfPreviewWindow.xaml
    /// </summary>
    public partial class WpfPreviewWindow : Window
    {
        private string _xaml;

        public event EventHandler UpdateButtonClicked;

        public WpfPreviewWindow()
        {
            InitializeComponent();
        }

        public void SetXaml(string xaml)
        {
            if (xaml == null)
                return;

            _xaml = xaml;

            if (xaml.Length < 5000000) // show if text is shorter then 5 million chars
            {
                XamlTextBox.Text = xaml;
                ShowXamlButton.Visibility = Visibility.Collapsed;
            }
            else
            {
                XamlTextBox.Text = string.Format("XAML very large - Length: {0} characters\r\nClick \"Show xaml\" button to show xaml", xaml.Length);
                ShowXamlButton.Visibility = Visibility.Visible;
            }

            try
            {
                Mouse.OverrideCursor = Cursors.Wait;

                var frameworkElement = XamlReader.Parse(xaml) as FrameworkElement;
                ContentBorder.Child = frameworkElement;

                ErrorTextBlock.Visibility = Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                ErrorTextBlock.Text = ex.Message;
                ErrorTextBlock.Visibility = Visibility.Visible;
            }
            finally
            {
                Mouse.OverrideCursor = null;
            }
        }

        public void SetBackgroundColor(Color color)
        {
            ContentBorder.Background = new SolidColorBrush(color);
        }

        private void UpdateButton_OnClick(object sender, RoutedEventArgs e)
        {
            if (UpdateButtonClicked != null)
                UpdateButtonClicked(this, null);
        }

        private void ShowXamlButton_OnClick(object sender, RoutedEventArgs e)
        {
            XamlTextBox.Text = _xaml;

            ShowXamlButton.Visibility = Visibility.Collapsed;
        }
    }
}
