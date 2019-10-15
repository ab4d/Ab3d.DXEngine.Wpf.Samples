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

namespace Ab3d.DXEngine.Wpf.Samples.Other
{
    /// <summary>
    /// Interaction logic for OculusVRSample.xaml
    /// </summary>
    public partial class OculusVRSample : Page
    {
        public OculusVRSample()
        {
            InitializeComponent();
        }

        private void Hyperlink_OnClick(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Process.Start("https://github.com/ab4d/Ab3d.OculusWrap/tree/master/Ab3d.OculusWrap");
        }
    }
}
