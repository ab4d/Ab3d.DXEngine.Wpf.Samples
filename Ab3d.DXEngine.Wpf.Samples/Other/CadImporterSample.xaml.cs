using System;
using System.Collections.Generic;
using System.Diagnostics;
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
