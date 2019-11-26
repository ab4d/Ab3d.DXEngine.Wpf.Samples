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
    /// Interaction logic for WinFormsSample.xaml
    /// </summary>
    public partial class WinFormsSample : Page
    {
        private string _sampleSolutionPath;

        public WinFormsSample()
        {
            InitializeComponent();

            _sampleSolutionPath = System.IO.Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                @"..\..\..\Ab3d.DXEngine.WinForms.Sample.sln");

            if (System.IO.File.Exists(_sampleSolutionPath))
            {
                OpenSolutionTextBlock.Visibility = Visibility.Visible;

                SampleImage.Cursor            =  Cursors.Hand;
                SampleImage.MouseLeftButtonUp += SampleImage_OnMouseLeftButtonUp;
            }
        }

        private void Hyperlink_OnClick(object sender, RoutedEventArgs e)
        {
            OpenSolutionHyperlink.IsEnabled = false;
            OpenSamplesSolution();
        }

        private void SampleImage_OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            OpenSamplesSolution();
        }

        private void OpenSamplesSolution()
        {
            try
            {
                // For CORE3 project we need to set UseShellExecute to true,
                // otherwise a "The specified executable is not a valid application for this OS platform" exception is thrown.
                //Process.Start(_sampleSolutionPath);
                System.Diagnostics.Process.Start(new ProcessStartInfo(_sampleSolutionPath) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error opening solution", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
