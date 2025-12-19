using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;

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
            // For CORE3 project we need to set UseShellExecute to true,
            // otherwise a "The specified executable is not a valid application for this OS platform" exception is thrown.
            System.Diagnostics.Process.Start(new ProcessStartInfo("https://github.com/ab4d/Ab3d.OculusWrap/tree/master/Ab3d.OculusWrap") { UseShellExecute = true });
        }
    }
}
