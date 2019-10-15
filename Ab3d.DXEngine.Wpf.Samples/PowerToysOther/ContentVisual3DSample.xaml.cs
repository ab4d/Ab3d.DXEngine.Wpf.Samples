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
using System.Windows.Media.Media3D;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Ab3d.DXEngine.Wpf.Samples.PowerToysOther
{
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
