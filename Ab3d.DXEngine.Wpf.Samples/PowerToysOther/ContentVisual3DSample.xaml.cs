using System;
using System.Windows.Controls;
using System.Windows.Media.Media3D;

namespace Ab3d.DXEngine.Wpf.Samples.PowerToysOther
{
    // ContentVisual3D class is very similar to the standard WPF's ModelVisual3D class.
    // A difference is that ContentVisual3D also supports IsVisible property.
    // This means that when rendered with Ab3d.DXEngine the processing of IsVisible property can be optimized.
    // In this case DirectX resources stay im memory when IsVisible is set to false; this way the object can be shown quickly because all the DirectX resources are still ready.
    // When using ModelVisual3D you need to remove the model from the Content or Children collection and this disposes the DirectX resources.

    /// <summary>
    /// Interaction logic for ContentVisual3DSample.xaml
    /// </summary>
    public partial class ContentVisual3DSample : Page
    {
        public ContentVisual3DSample()
        {
            InitializeComponent();

            LoadFile(AppDomain.CurrentDomain.BaseDirectory + @"Resources\Models\ship_boat.obj");

            // IMPORTANT:
            // It is very important to call Dispose method on DXSceneView after the control is not used any more (see help file for more info)
            this.Unloaded += (sender, args) => MainDXViewportView.Dispose();
        }

        private void LoadFile(string fileName)
        {
            var readerObj = new Ab3d.ReaderObj();

            // Read the model
            var loadedModel3D = readerObj.ReadModel3D(fileName);

            Ab3d.Utilities.ModelUtils.CenterAndScaleModel3D(loadedModel3D, centerPosition: new Point3D(0, 0, 0), finalSize: new Size3D(100, 100, 100));

            MainContentVisual3D.Content = loadedModel3D;
        }
    }
}
