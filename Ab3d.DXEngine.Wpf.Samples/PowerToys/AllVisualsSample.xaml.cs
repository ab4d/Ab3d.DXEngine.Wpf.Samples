using System.Windows.Controls;

namespace Ab3d.DXEngine.Wpf.Samples.PowerToys
{
    /// <summary>
    /// Interaction logic for AllVisualsSample.xaml
    /// </summary>
    public partial class AllVisualsSample : Page
    {
        public AllVisualsSample()
        {
            InitializeComponent();

            // IMPORTANT:
            // It is very important to call Dispose method on DXSceneView after the control is not used any more (see help file for more info)
            this.Unloaded += (sender, args) => MainDXViewportView.Dispose();



            //// Code to add test button:
            //var testButton = new Button()
            //{
            //    Content = "TEST",
            //    HorizontalAlignment = HorizontalAlignment.Right,
            //    VerticalAlignment = VerticalAlignment.Bottom,
            //};

            //testButton.Click += delegate (object sender, RoutedEventArgs args)
            //{
            //    // Place to put test code
            //};

            //MainGrid.Children.Add(testButton);
        }
    }
}