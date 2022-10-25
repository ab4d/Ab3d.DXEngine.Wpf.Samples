using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using Ab3d.Common.Cameras;
using Ab3d.Controls;
using Ab3d.DirectX;
using Ab3d.DirectX.Effects;
using Ab3d.DirectX.Materials;
using Ab3d.DirectX.Models;
using Ab3d.Visuals;
using SharpDX;

namespace Ab3d.DXEngine.Wpf.Samples.DXEngineVisuals
{
    /// <summary>
    /// Interaction logic for FaceColorRenderingSample.xaml
    /// </summary>
    public partial class FaceColorRenderingSample : Page
    {
        private FaceColorMaterial _faceColorMaterial;

        private int _lastHitTriangleIndex = -1;
        private GeometryModel3D _faceColorGeometryModel3D;
        private SceneNode _faceColorSceneNode;

        public FaceColorRenderingSample()
        {
            InitializeComponent();

            EmissiveAmountInfoControl.InfoText =
@"You can control how much the per-face color are shaded based on the light angles.
This is done by defining the DiffuseColor and EmissiveColor:
To show shaded per-face colors, set DiffuseColor to White and EmissiveColor to Black.
To show original per-face colors without any shading, set EmissiveColor to White and DiffuseColor to Black.

Because the per-face color is multiplied by the DiffuseColor and EmissiveColor, 
you can use those two colors to apply a color mask. 
To show only red colors, set DiffuseColor and EmissiveColor to Red.";


            AddTestModel();

            Camera1.StartRotation(45, 0);

            Camera1.CameraChanged += delegate { CheckHitTriangle(); };
            ViewportBorder.MouseMove += delegate { CheckHitTriangle(); };


            // Cleanup
            this.Unloaded += delegate(object sender, RoutedEventArgs args)
            {
                // We need to dispose all DXEngine objects that are created here - in this case _faceColorMaterial
                if (_faceColorMaterial != null)
                {
                    _faceColorMaterial.Dispose();
                    _faceColorMaterial = null;
                }

                MainDXViewportView.Dispose();
            };
        }

        private void AddTestModel()
        {
            var sphereMesh3D = new Ab3d.Meshes.SphereMesh3D(new Point3D(0, 0, 0), radius: 50, segments: 20).Geometry;

            int trianglesCount = sphereMesh3D.TriangleIndices.Count / 3;

            // Create positionColorsArray that will define colors for each position
            var faceColorsArray = new Color4[trianglesCount];

            var possibleColors = new Color4[] { Colors.Red.ToColor4(), Colors.Green.ToColor4(), Colors.Blue.ToColor4(), Colors.Gray.ToColor4() };

            for (int i = 0; i < trianglesCount; i++)
                faceColorsArray[i] = possibleColors[i % possibleColors.Length];


            // Now create the FaceColorMaterial that will be used instead of standard material
            // and will make the model render with special effect where each vertex can have its own color.
            _faceColorMaterial = new FaceColorMaterial()
            {
                FaceColors = faceColorsArray, // The FaceColors property is used to specify colors for each vertex

                IsTwoSided = true,

                // If the FaceColors array will be changed often, then it is recommended to set CreateDynamicBuffer to true
                CreateDynamicBuffer = true,

                // Set HasTransparency to true, when some colors are semi-transparent (this enables alpha-blending)
                // This can remain on false when some faces are discarded by setting alpha color value to 0.
                HasTransparency = false,

                // To show specular effect set the specular data here:
                //SpecularPower = 16,
                //SpecularColor = Color3.White,
                //HasSpecularColor = true
            };


            // Create standard WPF material and set the _faceColorMaterial to be used when the model is rendered in DXEngine.
            var faceColorDiffuseMaterial = new DiffuseMaterial();
            faceColorDiffuseMaterial.SetUsedDXMaterial(_faceColorMaterial);


            // Create a GeometryModel3D that will be rendered with _faceColorMaterial
            _faceColorGeometryModel3D = new GeometryModel3D(sphereMesh3D, faceColorDiffuseMaterial);
            
            var faceColorModelVisual3D = new ModelVisual3D()
            {
                Content = _faceColorGeometryModel3D
            };

            MainViewport.Children.Add(faceColorModelVisual3D);
        }

        private void CheckHitTriangle()
        {
            if (MainDXViewportView.DXScene == null)
                return; // not yet initialized

            if (!(HitTestingCheckBox.IsChecked ?? false))
                return;

            // Get SceneNode object that was created from _faceColorGeometryModel3D
            if (_faceColorSceneNode == null)
                _faceColorSceneNode = MainDXViewportView.GetSceneNodeForWpfObject(_faceColorGeometryModel3D);


            var mousePosition = Mouse.GetPosition(MainDXViewportView);
            var hitTestResult = MainDXViewportView.GetClosestHitObject(mousePosition);

            if (hitTestResult != null &&
                hitTestResult.HitSceneNode == _faceColorSceneNode &&
                hitTestResult.TriangleIndex != _lastHitTriangleIndex)
            {
                HideTriangle(hitTestResult.TriangleIndex);
                _lastHitTriangleIndex = hitTestResult.TriangleIndex;
            }
        }

        private void HideTriangle(int triangleIndex)
        {
            // When color's alpha value is below 0.01, then this face (triangle) is discarded and will not be rendered.
            _faceColorMaterial.FaceColors[triangleIndex] = new Color4(0, 0, 0, 0);

            // After changing the colors in the FaceColors array, we need to call Update method to update the DirectX buffer.
            // If the colors are changed frequently, then it is recommended to set FaceColorMaterial.CreateDynamicBuffer to true.
            _faceColorMaterial.Update();

            // We also need to inform the engine that a material was changed
            MainDXViewportView.DXScene.NotifyChange(DXScene.ChangeNotifications.MaterialPropertiesChanged);
        }


        private void EmissiveAmountSlider_OnValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!this.IsLoaded || MainDXViewportView.DXScene == null)
                return;

            // You can control how much the per-face color are shaded based on the light angles.
            // This is done by defining the DiffuseColor and EmissiveColor:
            // To show shaded per-face colors, set DiffuseColor to White and EmissiveColor to Black.
            // To show original per-face colors without any shading, set EmissiveColor to White and DiffuseColor to Black.
            //
            // Because the per-face color is multiplied by the DiffuseColor and EmissiveColor, 
            // you can use those two colors to apply a color mask. 
            // To show only red colors, set DiffuseColor and EmissiveColor to Red.

            float emissiveAmount = (float)EmissiveAmountSlider.Value;
            float diffuseAmount = 1 - emissiveAmount;

            Color3 diffuseColor = new Color3(diffuseAmount, diffuseAmount, diffuseAmount);
            Color3 emissiveColor = new Color3(emissiveAmount, emissiveAmount, emissiveAmount);

            _faceColorMaterial.DiffuseColor = diffuseColor;
            _faceColorMaterial.EmissiveColor = emissiveColor;

            MainDXViewportView.DXScene.NotifyChange(DXScene.ChangeNotifications.MaterialPropertiesChanged);
        }

        private void ChangeColors_OnClick(object sender, RoutedEventArgs e)
        {
            var rnd = new Random();

            var faceColorsArray = _faceColorMaterial.FaceColors;

            for (int i = 0; i < faceColorsArray.Length; i++)
                faceColorsArray[i] = new Color4(red: (float)rnd.NextDouble(), green: (float)rnd.NextDouble(), blue: (float)rnd.NextDouble(), alpha: 1);

            // Set HasTransparency to true, when some colors are semi-transparent (this enables alpha-blending)
            //_faceColorMaterial.HasTransparency = true;

            // After changing the colors in the FaceColors array, we need to call Update method to update the DirectX buffer.
            // If the colors are changed frequently, then it is recommended to set FaceColorMaterial.CreateDynamicBuffer to true.
            _faceColorMaterial.Update();

            // We also need to inform the engine that a material was changed
            MainDXViewportView.DXScene.NotifyChange(DXScene.ChangeNotifications.MaterialPropertiesChanged);
        }

        private void StartStopCameraRotationButton_OnClick(object sender, RoutedEventArgs e)
        {
            if (Camera1.IsRotating)
            {
                Camera1.StopRotation();
                StartStopCameraRotationButton.Content = "Start camera rotation";
            }
            else
            {
                Camera1.StartRotation(45, 0);
                StartStopCameraRotationButton.Content = "Stop camera rotation";
            }
        }
    }
}
