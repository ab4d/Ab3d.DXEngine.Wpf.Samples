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
using Ab3d.Assimp;
using Ab3d.Common.Cameras;
using Ab3d.DirectX;
using Ab3d.DXEngine.Wpf.Samples.Common;
using Ab3d.DXEngine.Wpf.Samples.DXEngineVisuals;
using Ab3d.Utilities;
using Ab3d.Visuals;
using Assimp;

namespace Ab3d.DXEngine.Wpf.Samples.PowerToys
{
    // This sample is almost the same as the sample in the Ab3d.PowerToys samples project but because
    // it uses Ab3d.DXEngine it can be used to load much more complex 3D objects.
    // The only change in this sample is that it support setting line's depth bias so that the wireframe lines
    // are much better visible (this is possible only with Ab3d.DXEngine).

    /// <summary>
    /// Interaction logic for AssimpWpfImporterSample.xaml
    /// </summary>
    public partial class AssimpWpfImporterSample : Page
    {
        private string _fileName;

        public AssimpWpfImporterSample()
        {
            InitializeComponent();


            // Use helper class (defined in this sample project) to load the native assimp libraries
            AssimpLoader.LoadAssimpNativeLibrary();


            var assimpWpfImporter = new AssimpWpfImporter();
            string[] supportedImportFormats = assimpWpfImporter.SupportedImportFormats;

            var assimpWpfExporter = new AssimpWpfExporter();
            string[] supportedExportFormats = assimpWpfExporter.ExportFormatDescriptions.Select(f => f.FileExtension).ToArray();

            FileFormatsTextBlock.Text = string.Format("Using native Assimp library version {0}.\r\n\r\nSupported import formats:\r\n{1}\r\n\r\nSupported export formats:\r\n{2}",
                                            assimpWpfImporter.AssimpVersion,
                                            string.Join(", ", supportedImportFormats),
                                            string.Join(", ", supportedExportFormats));


            var dragAndDropHelper = new DragAndDropHelper(this, ".*");
            dragAndDropHelper.FileDroped += (sender, args) => LoadModel(args.FileName);


            string startUpFileName = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"Resources\Models\duck.dae");
            LoadModel(startUpFileName);
        }

        private void LoadModel(string fileName)
        {
            bool isNewFile = false;

            // Create an instance of AssimpWpfImporter
            var assimpWpfImporter = new AssimpWpfImporter();

            string fileExtension = System.IO.Path.GetExtension(fileName);
            if (!assimpWpfImporter.IsImportFormatSupported(fileExtension))
            {
                MessageBox.Show("Assimp does not support importing files file extension: " + fileExtension);
                return;
            }

            try
            {
                Mouse.OverrideCursor = Cursors.Wait;

                // Before readin the file we can set the default material (used when no other materila is defined - here we set the default value again)
                assimpWpfImporter.DefaultMaterial = new DiffuseMaterial(Brushes.Silver);

                // After assimp importer reads the file, it can execute many post processing steps to transform the geometry.
                // See the possible enum values to see what post processes are available.
                // Here we just set the AssimpPostProcessSteps to its default value - execute the Triangulate step to convert all polygons to triangles that are needed for WPF 3D.
                // Note that if ReadPolygonIndices is set to true in the next line, then the assimpWpfImporter will not use assimp's triangulation because it needs original polygon data.
                assimpWpfImporter.AssimpPostProcessSteps = PostProcessSteps.Triangulate;

                // When ReadPolygonIndices is true, assimpWpfImporter will read PolygonIndices collection that can be used to show polygons instead of triangles.
                assimpWpfImporter.ReadPolygonIndices = ReadPolygonIndicesCheckBox.IsChecked ?? false;

                // Read model from file
                Model3D readModel3D;

                try
                {
                    readModel3D = assimpWpfImporter.ReadModel3D(fileName, texturesPath: null); // we can also define a textures path if the textures are located in some other directory (this is parameter can be skipped, but is defined here so you will know that you can use it)

                    isNewFile = (_fileName != fileName);
                    _fileName = fileName;
                }
                catch (Exception ex)
                {
                    readModel3D = null;
                    MessageBox.Show("Error importing file:\r\n" + ex.Message);
                }

                // After the model is read and if the object names are defined in the file,
                // you can get the model names by assimpWpfImporter.ObjectNames
                // or get object by name with assimpWpfImporter.NamedObjects

                // To get the  object model of the assimp importer, you can observe the assimpWpfImporter.ImportedAssimpScene

                // Show the model
                ShowModel(readModel3D, updateCamera: isNewFile); // If we just reloaded the previous file, we preserve the current camera TargetPosition and Distance
            }
            finally
            {
                // Dispose unmanaged resources
                assimpWpfImporter.Dispose();

                Mouse.OverrideCursor = null;
            }
        }

        private void ShowModel(Model3D model3D, bool updateCamera)
        {
            ContentVisual.Content = model3D;

            // NOTE:
            // We could show both solid model and wireframe in WireframeVisual3D (ContentWireframeVisual) with using WireframeWithOriginalSolidModel for WireframeType.
            // But in this sample we show solid frame is separate ModelVisual3D and therefore we show only wireframe in WireframeVisual3D.
            ContentWireframeVisual.BeginInit();
            ContentWireframeVisual.ShowPolygonLines = ReadPolygonIndicesCheckBox.IsChecked ?? false;
            ContentWireframeVisual.OriginalModel = model3D;
            ContentWireframeVisual.EndInit();

            if (AddLineDepthBiasCheckBox.IsChecked ?? false)
            {
                // To specify line depth bias to the Ab3d.PowerToys line Visual3D objects,
                // we use SetDXAttribute extension method and use LineDepthBias as DXAttributeType
                // NOTE: This can be used only before the Visual3D is created by DXEngine.
                // If you want to change the line bias after the object has been rendered, use the SetDepthBias method (see OnAddLineDepthBiasCheckBoxCheckedChanged)
                //
                // See DXEngineVisuals/LineDepthBiasSample for more info.
                ContentWireframeVisual.SetDXAttribute(DXAttributeType.LineDepthBias, model3D.Bounds.GetDiagonalLength() * 0.001);
            }

            // Calculate the center of the model and its size
            // This will be used to position the camera

            if (updateCamera)
            {
                var bounds = model3D.Bounds;

                var modelCenter = new Point3D(bounds.X + bounds.SizeX / 2,
                    bounds.Y + bounds.SizeY / 2,
                    bounds.Z + bounds.SizeZ / 2);

                var modelSize = Math.Sqrt(bounds.SizeX * bounds.SizeX +
                                          bounds.SizeY * bounds.SizeY +
                                          bounds.SizeZ * bounds.SizeZ);


                Camera1.TargetPosition = modelCenter;
                Camera1.Distance = modelSize * 2;
            }

            // If the read model already define some lights, then do not show the Camera's light
            if (ModelUtils.HasAnyLight(model3D))
                Camera1.ShowCameraLight = ShowCameraLightType.Never;
            else
                Camera1.ShowCameraLight = ShowCameraLightType.Always;

            ShowInfoButton.IsEnabled = true;
        }


        private void LoadButton_OnClick(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new Microsoft.Win32.OpenFileDialog();
            openFileDialog.InitialDirectory = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources");

            openFileDialog.Filter = "3D model file (*.*)|*.*";
            openFileDialog.Title = "Open 3D model file file";

            if ((openFileDialog.ShowDialog() ?? false) && !string.IsNullOrEmpty(openFileDialog.FileName))
                LoadModel(openFileDialog.FileName);
        }

        private void OnShowWireframeCheckBoxCheckedChanged(object sender, RoutedEventArgs e)
        {
            if (!this.IsLoaded)
                return;

            if (ShowWireframeCheckBox.IsChecked ?? false)
                ContentWireframeVisual.WireframeType = WireframeVisual3D.WireframeTypes.Wireframe;
            else
                ContentWireframeVisual.WireframeType = WireframeVisual3D.WireframeTypes.None;
        }

        private void OnAddLineDepthBiasCheckBoxCheckedChanged(object sender, RoutedEventArgs e)
        {
            if (ContentWireframeVisual == null || ContentWireframeVisual.OriginalModel == null)
                return;

            double depthBiasValue;
            if (AddLineDepthBiasCheckBox.IsChecked ?? false)
            {
                depthBiasValue = ContentWireframeVisual.OriginalModel.Bounds.GetDiagonalLength() * 0.001;
                

                // To specify line depth bias to the Ab3d.PowerToys line Visual3D objects,
                // we use SetDXAttribute extension method and use LineDepthBias as DXAttributeType
                // NOTE: This can be used only before the Visual3D is created by DXEngine.
                // If you want to change the line bias after the object has been rendered, use the SetDepthBias method (defined below)
                //
                // See DXEngineVisuals/LineDepthBiasSample for more info.
                
                //ContentWireframeVisual.SetDXAttribute(DXAttributeType.LineDepthBias, depthBiasValue);
            }
            else
            {
                depthBiasValue = 0;
                //ContentWireframeVisual.ClearDXAttribute(DXAttributeType.LineDepthBias);
            }

            LineDepthBiasSample.SetDepthBias(MainDXViewportView, ContentWireframeVisual, depthBiasValue);
        }

        private void OnReadPolygonIndicesCheckBoxCheckedChanged(object sender, RoutedEventArgs e)
        {
            if (_fileName == null)
                return;

            if ((ReadPolygonIndicesCheckBox.IsChecked ?? false) && !(ShowWireframeCheckBox.IsChecked ?? false))
                ShowWireframeCheckBox.IsChecked = true; // Turn on showing wireframe if it was off and if ReadPolygonIndicesCheckBox is checked

            // Read file again
            LoadModel(_fileName);
        }

        private void ShowInfoButton_OnClick(object sender, RoutedEventArgs e)
        {
            var shownModel = ContentVisual.Content;

            if (shownModel == null)
                return;

            string objectInfo = Ab3d.Utilities.Dumper.GetObjectHierarchyString(shownModel);


            var textBox = new TextBox()
            {
                Margin = new Thickness(10, 10, 10, 10),
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                FontFamily = new FontFamily("Consolas"),
                Text = objectInfo
            };

            var window = new Window()
            {
                Title = "3D Object info"
            };

            window.Content = textBox;
            window.Show();
        }
    }
}
