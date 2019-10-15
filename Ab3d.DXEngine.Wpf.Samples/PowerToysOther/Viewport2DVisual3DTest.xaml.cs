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

namespace Ab3d.DXEngine.Wpf.Samples.PowerToysOther
{
    // When Viewport2DVisual3D is rendered with Ab3d.DXEngine, its content is shown but it is not interactive.
    // If you require interactive WPF controls that are rendered on top of 3D objects,
    // then you need to use pure WPF 3D objects without Ab3d.DXEngine.
    // An option is to add a Viewport3D on top of DXViewportView and render interactive objects on top for 3D scene that is rendered with DXEngine.

    /// <summary>
    /// Interaction logic for Viewport2DVisual3DTest.xaml
    /// </summary>
    public partial class Viewport2DVisual3DTest : Page
    {
        public Viewport2DVisual3DTest()
        {
            InitializeComponent();

            this.Unloaded += delegate (object sender, RoutedEventArgs e)
            {
                MainDXViewportView2.Dispose();
            };
        }

        private void WpfButton1_OnClick(object sender, RoutedEventArgs e)
        {
            if (ButtonParent1.Background != Brushes.Green)
                ButtonParent1.Background = Brushes.Green;
            else
                ButtonParent1.Background = Brushes.LightGray;
        }

        private void WpfButton2_OnClick(object sender, RoutedEventArgs e)
        {
            if (ButtonParent2.Background != Brushes.Green)
                ButtonParent2.Background = Brushes.Green;
            else            
                ButtonParent2.Background = Brushes.LightGray;
        }
    }
}
