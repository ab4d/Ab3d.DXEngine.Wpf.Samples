using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Navigation;

namespace Ab3d.DXEngine.Wpf.Samples.PowerToys
{
    public partial class PowerToysIntroPage : Page
    {
        private string _sampleSolutionPath;

        public PowerToysIntroPage()
        {
            InitializeComponent();

            _sampleSolutionPath = System.IO.Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                @"..\..\..\..\Ab3d.PowerToys\Ab3d.PowerToys MAIN WPF Samples.sln");

            if (System.IO.File.Exists(_sampleSolutionPath))
                OpenSolutionTextBlock.Visibility = Visibility.Visible;
        }

        private void Hyperlink_OnClick(object sender, RoutedEventArgs e)
        {
            OpenSolutionHyperlink.IsEnabled = false;

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