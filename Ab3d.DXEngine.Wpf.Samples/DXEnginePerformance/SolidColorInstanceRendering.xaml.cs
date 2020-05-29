using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
using Ab3d.DirectX;
using Ab3d.DirectX.Effects;
using Ab3d.Visuals;
using SharpDX;
using Color = System.Windows.Media.Color;

namespace Ab3d.DXEngine.Wpf.Samples.DXEnginePerformance
{
    /// <summary>
    /// Interaction logic for SolidColorInstanceRendering.xaml
    /// </summary>
    public partial class SolidColorInstanceRendering : Page
    {
        private InstancedMeshGeometryVisual3D _instancedMeshGeometryVisual3D;

        private TypeConverter _colorTypeConverter;

        private enum RenderingTypes
        {
            Standard,
            SolidColorEffect,
            UseSingleObjectColor
        }

        private RenderingTypes _currentRenderingType;
        private SolidColorEffect _solidColorEffectWithOutline;


        public SolidColorInstanceRendering()
        {
            InitializeComponent();

            MainDXViewportView.DXSceneDeviceCreated += delegate(object sender, EventArgs args)
            {
                _currentRenderingType = GetCurrentRenderingType();

                CreateInstances();

                ChangeRenderingType();
            };

            // IMPORTANT:
            // It is very important to call Dispose method on DXSceneView after the control is not used any more (see help file for more info)
            this.Unloaded += (sender, args) =>
            {
                if (_solidColorEffectWithOutline != null)
                {
                    _solidColorEffectWithOutline.Dispose();
                    _solidColorEffectWithOutline = null;
                }

                MainDXViewportView.Dispose();
            };
        }

        private void CreateInstances()
        {
            // Load standard Stanford Bunny model (res3) with 11533 position
            var meshGeometry3D = LoadMeshFromObjFile("bun_zipper_res3.obj");

            var bounds = meshGeometry3D.Bounds;

            double diagonalSize = Math.Sqrt(bounds.SizeX * bounds.SizeX + bounds.SizeY * bounds.SizeY + bounds.SizeZ * bounds.SizeZ);
            var modelScaleFactor = 20 / diagonalSize; // Scale model so that its diagonal is 20 units big


            // The following method prepare InstanceData array with data for each instance (WorldMatrix and Color)
            InstanceData[] instancedData = CreateInstancesData(new Point3D(0, 25, 0), new Size3D(100, 60, 100), (float)modelScaleFactor, 6, 4, 6, useTransparency: false);


            // Create InstancedGeometryVisual3D with selected meshGeometry and InstancesData
            _instancedMeshGeometryVisual3D = new InstancedMeshGeometryVisual3D(meshGeometry3D);
            _instancedMeshGeometryVisual3D.InstancesData = instancedData;

            ObjectsPlaceholder.Children.Clear();
            ObjectsPlaceholder.Children.Add(_instancedMeshGeometryVisual3D);
        }

        public static InstanceData[] CreateInstancesData(Point3D center, Size3D size, float modelScaleFactor, int xCount, int yCount, int zCount, bool useTransparency)
        {
            int totalCount = xCount * yCount * zCount;
            var instancedData = new InstanceData[totalCount];

            float xStep = xCount <= 1 ? 0 : (float)(size.X / (xCount - 1));
            float yStep = yCount <= 1 ? 0 : (float)(size.Y / (yCount - 1));
            float zStep = zCount <= 1 ? 0 : (float)(size.Z / (zCount - 1));

            int i = 0;


            for (int y = 0; y < yCount; y++)
            {
                float yPos = (float)(center.Y - (size.Y / 2.0) + (y * yStep));

                for (int z = 0; z < zCount; z++)
                {
                    float zPos = (float)(center.Z - (size.Z / 2.0) + (z * zStep));

                    for (int x = 0; x < xCount; x++)
                    {
                        float xPos = (float)(center.X - (size.X / 2.0) + (x * xStep));

                        instancedData[i].World = new SharpDX.Matrix(modelScaleFactor, 0, 0, 0,
                                                                    0, modelScaleFactor, 0, 0,
                                                                    0, 0, modelScaleFactor, 0,
                                                                    xPos, yPos, zPos, 1);

                        // Start with yellow and move to white (multiplied by 1.4 so that white color appear before the top)
                        instancedData[i].DiffuseColor = new SharpDX.Color4(red: 1.0f - (float)i / (float)totalCount,
                                                                           green: 1.0f,
                                                                           blue: 1.0f - (float)x / (float)xCount,
                                                                           alpha: 1.0f);
                        i++;
                    }
                }
            }

            return instancedData;
        }

        private MeshGeometry3D LoadMeshFromObjFile(string fileName)
        {
            if (!System.IO.Path.IsPathRooted(fileName))
                fileName = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources\\Models\\" + fileName);

            var readerObj   = new Ab3d.ReaderObj();
            var readModel3D = readerObj.ReadModel3D(fileName) as GeometryModel3D;

            if (readModel3D == null)
                return null;

            return readModel3D.Geometry as MeshGeometry3D;
        }

        private Color GetColorFromComboBox(ComboBox comboBox)
        {
            var comboBoxItem = comboBox.SelectedItem as ComboBoxItem;

            if (comboBoxItem != null && comboBoxItem.Content != null)
            {
                var selectedSizeText = comboBoxItem.Content.ToString();

                if (_colorTypeConverter == null)
                    _colorTypeConverter = System.ComponentModel.TypeDescriptor.GetConverter(typeof(Color));

                return (Color)_colorTypeConverter.ConvertFromString(selectedSizeText);
            }

            return Colors.Red; // Invalid
        }

        private RenderingTypes GetCurrentRenderingType()
        {
            if (UseSolidColorEffectColorRadioButton.IsChecked ?? false)
                return RenderingTypes.SolidColorEffect;

            if (UseSingleObjectColorRadioButton.IsChecked ?? false)
                return RenderingTypes.UseSingleObjectColor;

            return RenderingTypes.Standard;
        }

        private void ChangeRenderingType()
        {
            if (MainDXViewportView.DXScene == null)
                return; // Probably WPF 3D rendering

            var color = GetColorFromComboBox(SolidModelColorComboBox);

            if (_currentRenderingType == RenderingTypes.SolidColorEffect)
                MainDXViewportView.DXScene.DefaultRenderObjectsRenderingStep.OverrideEffect = null;


            _currentRenderingType = GetCurrentRenderingType();

            if (_currentRenderingType == RenderingTypes.SolidColorEffect)
            {
                _solidColorEffectWithOutline = new Ab3d.DirectX.Effects.SolidColorEffect
                {
                    Color = color.ToColor4(),
                    OverrideModelColor = true
                };

                _solidColorEffectWithOutline.InitializeResources(MainDXViewportView.DXScene.DXDevice);

                MainDXViewportView.DXScene.DefaultRenderObjectsRenderingStep.OverrideEffect = _solidColorEffectWithOutline;
            }
            else if (_currentRenderingType == RenderingTypes.UseSingleObjectColor)
            {
                // Set IsSolidColorMaterial to render tube instances with solid color without any shading based on lighting
                _instancedMeshGeometryVisual3D.IsSolidColorMaterial = IsSolidColorMaterialCheckBox.IsChecked ?? false;

                var instancesData = _instancedMeshGeometryVisual3D.InstancesData;
                int instancesCount = instancesData.Length;

                for (var i = 0; i < instancesCount; i++)
                    instancesData[i].DiffuseColor = color.ToColor4();

                _instancedMeshGeometryVisual3D.Update(0, instancesCount, updateBounds: false);
            }
            else
            {
                CreateInstances();
            }
        }

        private void OnRenderingTypeRadioButtonCheckedChanged(object sender, RoutedEventArgs e)
        {
            if (!this.IsLoaded)
                return;

            ChangeRenderingType();
            MainDXViewportView.Refresh();
        }

        private void OnIsSolidColorMaterialCheckBoxCheckedChanged(object sender, RoutedEventArgs e)
        {
            if (!this.IsLoaded)
                return;

            _instancedMeshGeometryVisual3D.IsSolidColorMaterial = IsSolidColorMaterialCheckBox.IsChecked ?? false;
            MainDXViewportView.Refresh();
        }

        private void OnSolidModelColorComboBoxChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!this.IsLoaded)
                return;

            ChangeRenderingType();
            MainDXViewportView.Refresh();
        }
    }
}
