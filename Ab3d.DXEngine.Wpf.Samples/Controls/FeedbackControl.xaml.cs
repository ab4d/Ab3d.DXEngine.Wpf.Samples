using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Ab3d.DXEngine.Wpf.Samples.Controls
{
    /// <summary>
    /// Interaction logic for FeedbackControl.xaml
    /// </summary>
    public partial class FeedbackControl : UserControl
    {
        private Brush _normalShapesBrush = Brushes.Silver;

        private bool _isOverSadSmiley;
        private bool _isOverHappySmiley;

        public FeedbackControl()
        {
            InitializeComponent();

            SadSmileyPath.Fill = _normalShapesBrush;
            HappySmileyPath.Fill = _normalShapesBrush;
        }

        private void SmileyPath_OnMouseEnter(object sender, MouseEventArgs e)
        {
            if (ReferenceEquals(sender, SadSmileyPanel))
            {
                SadSmileyEllipse.Fill = Brushes.OrangeRed;
                SadSmileyPath.Fill = Brushes.Black;

                _isOverSadSmiley = true;
                _isOverHappySmiley = false;
            }
            else
            {
                HappySmileyEllipse.Fill = Brushes.Wheat;
                HappySmileyPath.Fill = Brushes.Black;

                _isOverHappySmiley = true;
                _isOverSadSmiley = false;
            }
        }

        private void SmileyPath_OnMouseLeave(object sender, MouseEventArgs e)
        {
            SadSmileyEllipse.Fill = null;
            HappySmileyEllipse.Fill = null;

            SadSmileyPath.Fill = _normalShapesBrush;
            HappySmileyPath.Fill = _normalShapesBrush;

            _isOverSadSmiley = false;
            _isOverHappySmiley = false;
        }

        private void RootPanel_OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            string url = "https://www.ab4d.com/Feedback.aspx?Subject=Ab3d.PowerToys_samples_feedback";

            if (_isOverHappySmiley)
                url += "&Message=I_feel_happy_because";
            else if (_isOverSadSmiley)
                url += "&Message=I_feel_sad_because";


            System.Diagnostics.Process.Start(url);
        }
    }
}
