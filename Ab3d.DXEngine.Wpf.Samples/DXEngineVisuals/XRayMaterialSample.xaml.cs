using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using Ab3d.Assimp;
using Ab3d.DirectX;
using Ab3d.DirectX.Materials;
using Ab3d.DXEngine.Wpf.Samples.Common;
using Ab3d.Utilities;
using Ab3d.Visuals;
using SharpDX;
using Material = Ab3d.DirectX.Material;

namespace Ab3d.DXEngine.Wpf.Samples.DXEngineVisuals
{
    /// <summary>
    /// Interaction logic for XRayMaterialSample.xaml
    /// </summary>
    public partial class XRayMaterialSample : Page
    {
        private Model3D _loadedModel3D;

        private DisposeList _disposables;
        private XRayMaterial _singleXRayMaterial;

        private Dictionary<GeometryModel3D, System.Windows.Media.Media3D.Material> _savedMaterials;

        public XRayMaterialSample()
        {
            InitializeComponent();

            if (System.ComponentModel.DesignerProperties.GetIsInDesignMode(this))
                return;

            
            AssimpLoader.LoadAssimpNativeLibrary();

            MainDXViewportView.IsTransparencySortingEnabled = true; // Start sorting of transparent objects


            string fileName = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"Resources\Models\robotarm-upper-part.3ds");

            var dragAndDropHelper = new DragAndDropHelper(this, ".*");
            dragAndDropHelper.FileDropped += (sender, args) => LoadModel3D(args.FileName);

            _disposables = new DisposeList();

            MainDXViewportView.DXSceneInitialized += delegate(object sender, EventArgs args)
            {
                LoadModel3D(fileName);
            };
            

            this.Unloaded += delegate(object sender, RoutedEventArgs args)
            {
                if (_disposables != null)
                {
                    _disposables.Dispose();
                    _disposables = null;
                }

                MainDXViewportView.Dispose();
            };
        }
        
        private void LoadModel3D(string fileName)
        {
            try
            {
                Mouse.OverrideCursor = Cursors.Wait;

                _savedMaterials = null;

                var assimpWpfImporter = new AssimpWpfImporter();
                var loadedModel3D = assimpWpfImporter.ReadModel3D(fileName);


                // Set XRay material to all read models
                bool useModelColor, useTwoSidedMaterial, readZBuffer, writeZBuffer;
                float falloff;
                Color3 singleColor;

                GetSelectedXRaySettings(out useModelColor, out singleColor, out falloff, out useTwoSidedMaterial, out readZBuffer, out writeZBuffer);


                if (useModelColor)
                    SetXRayMaterialFromMaterialColor(loadedModel3D, falloff, useTwoSidedMaterial, readZBuffer, writeZBuffer);
                else
                    SetSingleXRayMaterial(loadedModel3D, singleColor, falloff, useTwoSidedMaterial, readZBuffer, writeZBuffer);


                // Set both Distance and CameraWidth (this way we can change the CameraType with RadioButtons)
                // Distance is used when CameraType is PerspectiveCamera.
                // CameraWidth is used when CameraType is OrthographicCamera.
                Camera1.Distance    = loadedModel3D.Bounds.GetDiagonalLength() * 1.3;
                Camera1.CameraWidth = loadedModel3D.Bounds.SizeX * 2;

                Camera1.TargetPosition = loadedModel3D.Bounds.GetCenterPosition();
                Camera1.Offset = new Vector3D(0, 0, 0); // Reset offset

                // Clean the scene and add model to the scene
                MainViewport.Children.Clear();
                MainViewport.Children.Add(loadedModel3D.CreateModelVisual3D());


                _loadedModel3D = loadedModel3D;
            }
            finally
            {
                Mouse.OverrideCursor = null;
            }
        }

        private void SetSingleXRayMaterial(Model3D model3D, Color3 singleColor, float falloff, bool useTwoSidedMaterial, bool readZBuffer, bool writeZBuffer)
        {
            // First save original materials
            SaveMaterials(model3D);


            _singleXRayMaterial = new XRayMaterial()
            {
                DiffuseColor = singleColor,
                Alpha        = 1,
                Falloff      = falloff,
                IsTwoSided   = useTwoSidedMaterial,
                ReadZBuffer  = readZBuffer,
                WriteZBuffer = writeZBuffer
            };

            _disposables.Add(_singleXRayMaterial);

            var wpfXRayMaterial = new DiffuseMaterial();

            // To tell DXEngine to use the XRayMaterial instead of a material that is created from WPF's material,
            // we can use the SetUsedDXMaterial extension method.
            wpfXRayMaterial.SetUsedDXMaterial(_singleXRayMaterial);
            
            Ab3d.Utilities.ModelUtils.ChangeMaterial(model3D, wpfXRayMaterial, null);
        }

        private void SetXRayMaterialFromMaterialColor(Model3D model3D, float newFalloff, bool useTwoSidedMaterial, bool readZBuffer, bool writeZBuffer)
        {
            if (model3D == null)
                return;

            DXDevice dxDevice;

            if (MainDXViewportView.DXScene != null)
                dxDevice = MainDXViewportView.DXScene.DXDevice;
            else
                dxDevice = null;
            

            // Go through all GeometryModel3D and update the Falloff value for all XRayMaterials
            Ab3d.Utilities.ModelIterator.IterateGeometryModel3DObjects(model3D, null, delegate (GeometryModel3D geometryModel3D, Transform3D transform3D)
            {
                var material = geometryModel3D.Material;

                if (material != null)
                {
                    var xRayMaterial = material.GetUsedDXMaterial(dxDevice) as XRayMaterial;

                    if (xRayMaterial == null)
                    {
                        var materialColor = Ab3d.Utilities.ModelUtils.GetMaterialDiffuseColor(material, defaultColor: Colors.White, readBrushOpacity: true);

                        xRayMaterial = new XRayMaterial()
                        {
                            DiffuseColor = materialColor.ToColor3(),
                            Alpha        = materialColor.A / 255.0f,
                            Falloff      = newFalloff,
                            IsTwoSided   = useTwoSidedMaterial,
                            ReadZBuffer  = readZBuffer,
                            WriteZBuffer = writeZBuffer
                        };

                        _disposables.Add(xRayMaterial);

                        // To tell DXEngine to use the XRayMaterial instead of a material that is created from WPF's material,
                        // we can use the SetUsedDXMaterial extension method.
                        material.SetUsedDXMaterial(xRayMaterial);
                    }
                    else
                    {
                        // xRayMaterial already exists => just update the values
                        xRayMaterial.Falloff = newFalloff;
                        xRayMaterial.IsTwoSided = useTwoSidedMaterial;
                        xRayMaterial.ReadZBuffer = readZBuffer;
                        xRayMaterial.WriteZBuffer = writeZBuffer;
                    }
                }
            });

            _singleXRayMaterial = null;

            // We need to notify the DXEngine about the change
            if (MainDXViewportView.DXScene != null)
                MainDXViewportView.DXScene.NotifyChange(DXScene.ChangeNotifications.MaterialPropertiesChanged);
        }

        private void SaveMaterials(Model3D model3D)
        {
            _savedMaterials = new Dictionary<GeometryModel3D, System.Windows.Media.Media3D.Material>();

            Ab3d.Utilities.ModelIterator.IterateGeometryModel3DObjects(model3D, null, delegate (GeometryModel3D geometryModel3D, Transform3D transform3D)
            {
                _savedMaterials[geometryModel3D] = geometryModel3D.Material;
            });
        }

        private void RestoreMaterials(Model3D model3D)
        {
            if (_savedMaterials == null)
                return;

            Ab3d.Utilities.ModelIterator.IterateGeometryModel3DObjects(model3D, null, delegate (GeometryModel3D geometryModel3D, Transform3D transform3D)
            {
                System.Windows.Media.Media3D.Material material;
                if (_savedMaterials.TryGetValue(geometryModel3D, out material))
                    geometryModel3D.Material = material;
            });
        }

        private void GetSelectedXRaySettings(out bool useModelColor, out Color3 singleColor, out float newFalloff, out bool useTwoSidedMaterial, out bool readZBuffer, out bool writeZBuffer)
        {
            newFalloff = (float)FalloffSlider.Value;
            useTwoSidedMaterial = TwoSidedMaterialCheckBox.IsChecked ?? false;

            useModelColor = UseModelColorCheckBox.IsChecked ?? false;


            var comboBoxItem = ColorCombobox.SelectedItem as ComboBoxItem;
            if (comboBoxItem != null)
                singleColor = ((SolidColorBrush)comboBoxItem.Background).Color.ToColor3();
            else
                singleColor = Color3.White;

            readZBuffer = ReadZBufferCheckBox.IsChecked ?? false;
            writeZBuffer = WriteZBufferCheckBox.IsChecked ?? false;
        }

        private void OnXRaySettingsChanged(object sender, RoutedEventArgs e)
        {
            if (!this.IsLoaded)
                return;

            bool useModelColor, useTwoSidedMaterial, readZBuffer, writeZBuffer;
            float falloff;
            Color3 singleColor;

            GetSelectedXRaySettings(out useModelColor, out singleColor, out falloff, out useTwoSidedMaterial, out readZBuffer, out writeZBuffer);

            if (!useModelColor && _singleXRayMaterial != null)
            {
                _singleXRayMaterial.DiffuseColor = singleColor;
                _singleXRayMaterial.Falloff      = falloff;
                _singleXRayMaterial.IsTwoSided   = useTwoSidedMaterial;
                _singleXRayMaterial.ReadZBuffer  = readZBuffer;
                _singleXRayMaterial.WriteZBuffer = writeZBuffer;
            }
            else
            {
                if (useModelColor)
                {
                    if (_singleXRayMaterial != null)
                        RestoreMaterials(_loadedModel3D); // if before we were using single color xray, we need to restore original materials

                    SetXRayMaterialFromMaterialColor(_loadedModel3D, falloff, useTwoSidedMaterial, readZBuffer, writeZBuffer);
                }
                else
                {
                    SetSingleXRayMaterial(_loadedModel3D, singleColor, falloff, useTwoSidedMaterial, readZBuffer, writeZBuffer);
                }
            }

            // We need to notify the DXEngine about the change
            if (MainDXViewportView.DXScene != null)
                MainDXViewportView.DXScene.NotifyChange(DXScene.ChangeNotifications.MaterialPropertiesChanged);


            // Also update ColorCombobox.IsEnabled and SingleColorTextBlock.Foreground (this is much easier then binding)
            ColorCombobox.IsEnabled         = !useModelColor;
            SingleColorTextBlock.Foreground = useModelColor ? Brushes.DimGray : Brushes.Black;
        }
    }
}
