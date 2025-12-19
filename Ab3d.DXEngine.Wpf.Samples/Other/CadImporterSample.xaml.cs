using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Ab3d.DXEngine.Wpf.Samples.Other
{
    /// <summary>
    /// Interaction logic for CadImporterSample.xaml
    /// </summary>
    public partial class CadImporterSample : Page
    {
        public CadImporterSample()
        {
            InitializeComponent();

            SampleImage.Cursor = Cursors.Hand;
            SampleImage.MouseLeftButtonUp += SampleImage_OnMouseLeftButtonUp;
        }

        private void Hyperlink_OnClick(object sender, RoutedEventArgs e)
        {
            OpenCadImporterGitHub();
        }

        private void SampleImage_OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            OpenCadImporterGitHub();
        }

        private void OpenCadImporterGitHub()
        {
            System.Diagnostics.Process.Start(new ProcessStartInfo("https://github.com/ab4d/CadImporter") { UseShellExecute = true });
        }
    }
}
