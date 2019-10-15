using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Windows;
using System.Windows.Media;

namespace Ab3d.DXEngine.Wpf.Samples.Controls
{
    public class InfoControl : Image
    {
        private static BitmapImage _infoIcon;
        private TextBlock _tooltipTextBlock;

        public static DependencyProperty InfoTextProperty = DependencyProperty.Register("InfoText", typeof(object), typeof(InfoControl),
                 new FrameworkPropertyMetadata(null, new PropertyChangedCallback(InfoControl.OnTextChanged)));

        public object InfoText
        {
            get
            {
                return (string)base.GetValue(InfoTextProperty);
            }
            set
            {
                base.SetValue(InfoTextProperty, value);
            }
        }

        private static void OnTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            InfoControl thisInfoControl = null;

            thisInfoControl = (InfoControl)d;

            if (e.NewValue is string)
            {
                thisInfoControl._tooltipTextBlock.Text = ((string)e.NewValue).Replace("\\n", Environment.NewLine);
                thisInfoControl.ToolTip = thisInfoControl._tooltipTextBlock;
            }
            else
            {
                thisInfoControl.ToolTip = e.NewValue;
            }
        }



        public static DependencyProperty InfoWidthProperty = DependencyProperty.Register("InfoWidth", typeof(double), typeof(InfoControl),
                 new FrameworkPropertyMetadata(0.0, new PropertyChangedCallback(InfoControl.OnInfoWidthChanged)));

        public double InfoWidth
        {
            get
            {
                return (double)base.GetValue(InfoWidthProperty);
            }
            set
            {
                base.SetValue(InfoWidthProperty, value);
            }
        }

        private static void OnInfoWidthChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            double newWidth;

            InfoControl thisInfoControl = null;

            thisInfoControl = (InfoControl)d;
            newWidth = (double)e.NewValue;

            if (newWidth == 0)
                newWidth = double.NaN;

            thisInfoControl._tooltipTextBlock.Width = newWidth;
        }


        public static DependencyProperty ShowDurationProperty = DependencyProperty.Register("ShowDuration", typeof(int), typeof(InfoControl),
                 new FrameworkPropertyMetadata(30000, new PropertyChangedCallback(InfoControl.OnShowDurationChanged)));

        public int ShowDuration
        {
            get
            {
                return (int)base.GetValue(ShowDurationProperty);
            }
            set
            {
                base.SetValue(ShowDurationProperty, value);
            }
        }

        private static void OnShowDurationChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            InfoControl thisInfoControl = null;

            thisInfoControl = (InfoControl)d;

            ToolTipService.SetShowDuration(thisInfoControl, (int)e.NewValue);
        }


  //<Grid Width="12" Height="12">  
  //     <Ellipse HorizontalAlignment="Center" VerticalAlignment="Center" Width="12" Height="12" Fill="#6666FF"/>
  //     <Ellipse HorizontalAlignment="Center" VerticalAlignment="Center" Width="12" Height="12" Stroke="#AAAAFF" StrokeThickness="1"/>       
  //     <TextBlock HorizontalAlignment="Center" VerticalAlignment="Center" FontWeight="Bold" SnapsToDevicePixels="True" Margin="0 1 0 0" Foreground="White" FontSize="8" FontFamily="Times New Roman" Text="i"/>
  // </Grid>


        public InfoControl()
        {
            EnsureInfoIcon();

            this.Width = 12;
            this.Height = 12;
            this.Source = _infoIcon;

            _tooltipTextBlock = new TextBlock();
            _tooltipTextBlock.TextWrapping = TextWrapping.Wrap;

            this.Loaded += (sender, args) => ToolTipService.SetShowDuration(this, this.ShowDuration);          
        }

        public void ChangeIcon(ImageSource bitmap)
        {
            if (bitmap == null)
                this.Source = _infoIcon;
            else
                this.Source = bitmap;
        }

        private void EnsureInfoIcon()
        {
            if (DesignerProperties.GetIsInDesignMode(this))
                return;

            if (_infoIcon == null)
            {
                _infoIcon = new BitmapImage();

                _infoIcon.BeginInit();
                _infoIcon.StreamSource = Application.GetResourceStream(new Uri("pack://application:,,,/Resources/info_icon.png", UriKind.Absolute)).Stream;
                _infoIcon.EndInit();
            }
        }
    }
}
