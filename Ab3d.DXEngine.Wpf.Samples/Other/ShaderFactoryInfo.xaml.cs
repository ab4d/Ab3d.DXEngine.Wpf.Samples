using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Ab3d.DXEngine.Wpf.Samples.Other
{
    /// <summary>
    /// Interaction logic for ShaderFactoryInfo.xaml
    /// </summary>
    public partial class ShaderFactoryInfo : Page
    {
        private string _sampleSolutionPath;

        public ShaderFactoryInfo()
        {
            InitializeComponent();

            _sampleSolutionPath = System.IO.Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                @"..\..\..\Ab3d.DXEngine.ShaderFactory.sln");

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
                System.Diagnostics.Process.Start(new ProcessStartInfo(_sampleSolutionPath) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error opening solution", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
