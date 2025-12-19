using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Ab3d.DXEngine.Wpf.Samples.Other
{
    /// <summary>
    /// Interaction logic for OnlineHelp.xaml
    /// </summary>
    public partial class OnlineHelp : Page
    {
        public OnlineHelp()
        {
            InitializeComponent();
        }

        private void Hyperlink1_OnClick(object sender, RoutedEventArgs e)
        {
            ShowUsersGuide();
        }

        private void Hyperlink2_OnClick(object sender, RoutedEventArgs e)
        {
            ShowReferenceHelp();
        }

        private void DXEngineUsersGuideImage_OnMouseDown(object sender, MouseButtonEventArgs e)
        {
            ShowUsersGuide();
        }

        private void DXEngineReferenceHelpName_OnMouseDown(object sender, MouseButtonEventArgs e)
        {
            ShowReferenceHelp();
        }

        private void ShowUsersGuide()
        {
            // For CORE3 project we need to set UseShellExecute to true,
            // otherwise a "The specified executable is not a valid application for this OS platform" exception is thrown.
            System.Diagnostics.Process.Start(new ProcessStartInfo("https://www.ab4d.com/DirectX/3D/Introduction.aspx") { UseShellExecute = true });
        }

        private void ShowReferenceHelp()
        {
            // For CORE3 project we need to set UseShellExecute to true,
            // otherwise a "The specified executable is not a valid application for this OS platform" exception is thrown.
            System.Diagnostics.Process.Start(new ProcessStartInfo("https://www.ab4d.com/help/DXEngine/html/R_Project_Ab3d_DXEngine_Help.htm") { UseShellExecute = true });
        }
    }
}
