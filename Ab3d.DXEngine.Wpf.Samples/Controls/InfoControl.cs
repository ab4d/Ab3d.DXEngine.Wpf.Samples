using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Ab3d.DXEngine.Wpf.Samples.Controls
{
    public class InfoControl : Grid
    {
        private TextBlock _tooltipTextBlock;


        public static DependencyProperty ShowToolTipOnPreviousControlProperty = DependencyProperty.Register("ShowToolTipOnPreviousControl", typeof(bool), typeof(InfoControl),
                 new FrameworkPropertyMetadata(true));

        /// <summary>
        /// When true (by default) then the ToolTip will be also set to a control that in the parent's Children before this InfoControl.
        /// </summary>
        public bool ShowToolTipOnPreviousControl
        {
            get
            {
                return (bool)base.GetValue(ShowToolTipOnPreviousControlProperty);
            }
            set
            {
                base.SetValue(ShowToolTipOnPreviousControlProperty, value);
            }
        }
        
        
        public static DependencyProperty InfoTextProperty = DependencyProperty.Register("InfoText", typeof(object), typeof(InfoControl),
                 new FrameworkPropertyMetadata(null, InfoControl.OnTextChanged));

        /// <summary>
        /// Text that will be shown as ToolTip.
        /// </summary>
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
            var thisInfoControl = (InfoControl)d;

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
                 new FrameworkPropertyMetadata(0.0, InfoControl.OnInfoWidthChanged));

        /// <summary>
        /// Width of the ToolTip TextBlock. Longer text will be automatically wrapped.
        /// Default value is 0 that does not limit the TextBlock width.
        /// </summary>
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
            var thisInfoControl = (InfoControl)d;
            var newWidth = (double)e.NewValue;

            if (newWidth == 0)
                newWidth = double.NaN;

            thisInfoControl._tooltipTextBlock.Width = newWidth;
        }


        public static DependencyProperty ShowDurationProperty = DependencyProperty.Register("ShowDuration", typeof(int), typeof(InfoControl),
                 new FrameworkPropertyMetadata(120000, InfoControl.OnShowDurationChanged));


        /// <summary>
        /// Duration of showing ToolTip in milliseconds. Default value is 120000.
        /// </summary>
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
            var thisInfoControl = (InfoControl)d;
            ToolTipService.SetShowDuration(thisInfoControl, (int)e.NewValue);
        }


        /// <summary>
        /// Gets the Ellipse that is used to show background circle.
        /// </summary>
        public Ellipse BackGroundEllipse { get; private set; }

        /// <summary>
        /// Gets the TextBlock that is used to show the question character.
        /// </summary>
        public TextBlock QuestionTextBlock { get; private set; }


        /// <summary>
        /// Gets or sets the fill brush for the Ellipse shape. Default value is Gray.
        /// </summary>
        public Brush EllipseFillBrush
        {
            get { return BackGroundEllipse.Fill; }
            set { BackGroundEllipse.Fill = value; }
        }

        /// <summary>
        /// Gets or sets the foreground brush for the question character. Default value is White.
        /// </summary>
        public Brush QuestionCharacterForeground
        {
            get { return QuestionTextBlock.Foreground; }
            set { QuestionTextBlock.Foreground = value; }
        }

        /// <summary>
        /// Gets or sets the FontSize for the question character. Default value is 10.
        /// </summary>
        public double QuestionCharacterFontSize
        {
            get { return QuestionTextBlock.FontSize; }
            set { QuestionTextBlock.FontSize = value; }
        }




        //<Grid>
        //  <Ellipse Fill = "Gray" Width="12" Height="12" VerticalAlignment="Center" HorizontalAlignment="Center"/>
        //  <TextBlock Text = "?" Foreground="White" FontWeight="Bold" FontFamily="Tahoma" FontSize="10" VerticalAlignment="Center" HorizontalAlignment="Center" Margin="0 0 0 0" />
        //</Grid>

        public InfoControl()
        {
            this.Width  = 12;
            this.Height = 12;
            this.VerticalAlignment = VerticalAlignment.Center;

            BackGroundEllipse = new Ellipse()
            {
                VerticalAlignment   = VerticalAlignment.Stretch,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Fill                = Brushes.Gray
            };

            QuestionTextBlock = new TextBlock()
            {
                Text                = "?",
                FontFamily          = new FontFamily("Tahoma"),
                FontWeight          = FontWeights.Bold,
                FontSize            = 10,
                Foreground          = Brushes.White,
                VerticalAlignment   = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
            };

            this.Children.Add(BackGroundEllipse);
            this.Children.Add(QuestionTextBlock);


            _tooltipTextBlock = new TextBlock();
            _tooltipTextBlock.TextWrapping = TextWrapping.Wrap;

            this.Loaded += (sender, args) =>
            {
                ToolTipService.SetShowDuration(this, this.ShowDuration);

                if (ShowToolTipOnPreviousControl)
                    SetToolTipOnPreviousControl();
            };          
        }

        private void SetToolTipOnPreviousControl()
        {
            var parentPanel = this.Parent as Panel;

            if (parentPanel == null)
                return;

            int outIndex = parentPanel.Children.IndexOf(this);
            
            if (outIndex > 0)
            {
                var siblingElement = parentPanel.Children[outIndex - 1] as FrameworkElement;
                if (siblingElement != null && siblingElement.ToolTip == null)
                {
                    siblingElement.ToolTip = _tooltipTextBlock;
                    ToolTipService.SetShowDuration(siblingElement, this.ShowDuration);
                }
            }
        }
    }
}
