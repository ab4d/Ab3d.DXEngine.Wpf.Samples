using System;
using System.Collections.Generic;
using System.Data;
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
using SharpDX.Direct3D;
using Point = System.Windows.Point;

namespace Ab3d.DXEngine.Wpf.Samples.DXEngineVisuals
{
    /// <summary>
    /// Interaction logic for VertexColorRenderingSample.xaml
    /// </summary>
    public partial class VertexColorRenderingSample : Page
    {
        private VertexColorMaterial _vertexColorMaterial;

        public VertexColorRenderingSample()
        {
            InitializeComponent();

            AddTestModel();

            Camera1.StartRotation(45, 0);


            // Cleanup
            this.Unloaded += delegate(object sender, RoutedEventArgs args)
            {
                // We need to dispose all DXEngine objects that are created here - in this case _vertexColorMaterial
                if (_vertexColorMaterial != null)
                {
                    _vertexColorMaterial.Dispose();
                    _vertexColorMaterial = null;
                }

                MainDXViewportView.Dispose();
            };
        }

        private void AddTestModel()
        {
            var boxMesh3D = new Ab3d.Meshes.BoxMesh3D(new Point3D(0, 0, 0), new Size3D(1, 1, 1), 1, 1, 1).Geometry;

            int positionsCount = boxMesh3D.Positions.Count;
            var bounds         = boxMesh3D.Bounds;

            // Create positionColorsArray that will define colors for each position
            var positionColorsArray = new Color4[positionsCount];

            for (int i = 0; i < positionsCount; i++)
            {
                var position = boxMesh3D.Positions[i];

                // Get colors based on the relative position inside the Bounds - in range from (0, 0, 0) to (1, 1, 1)
                float red   = (float)((position.X - bounds.X) / bounds.SizeX);
                float green = (float)((position.Y - bounds.Y) / bounds.SizeY);
                float blue  = (float)((position.Z - bounds.Z) / bounds.SizeZ);

                // Set Color this position
                positionColorsArray[i] = new Color4(red, green, blue, alpha: 1.0f);
            }


            // Now create the VertexColorMaterial that will be used instead of standard material
            // and will make the model render with special effect where each vertex can have its own color.
            _vertexColorMaterial = new VertexColorMaterial()
            {
                PositionColors = positionColorsArray, // The PositionColors property is used to specify colors for each vertex

                // To show specular effect set the specular data here:
                //SpecularPower = 16,
                //SpecularColor = Color3.White,
                //HasSpecularColor = true

                // If we would update the positionColorsArray very often, then set CreateDynamicBuffer to true
                //CreateDynamicBuffer = true,
            };

            // Create standard WPF material and set the _vertexColorMaterial to be used when the model is rendered in DXEngine.
            var vertexColorDiffuseMaterial = new DiffuseMaterial();
            vertexColorDiffuseMaterial.SetUsedDXMaterial(_vertexColorMaterial);


            // Create a GeometryModel3D that will be rendered with _vertexColorMaterial
            var vertexColorGeometryModel3D = new GeometryModel3D(boxMesh3D, vertexColorDiffuseMaterial);

            // Also set BackMaterial - this is required when transparent colors are used - in this case the interior of the box needs to be visible
            vertexColorGeometryModel3D.BackMaterial = vertexColorDiffuseMaterial;

            vertexColorGeometryModel3D.Transform = new ScaleTransform3D(100, 50, 80); // Scale the box

            var vertexColorModelVisual3D = new ModelVisual3D()
            {
                Content = vertexColorGeometryModel3D
            };

            MainViewport.Children.Add(vertexColorModelVisual3D);
        }

        private void ChangeColorsButton_OnClick(object sender, RoutedEventArgs e)
        {
            ChangePositionColors(changeColors: true, changeAlpha: true);
        }

        private void OnTransparentCheckBoxCheckedChanged(object sender, RoutedEventArgs e)
        {
            if (!this.IsLoaded)
                return;

            ChangePositionColors(changeColors: false, changeAlpha: true);
        }

        private void ChangePositionColors(bool changeColors, bool changeAlpha)
        {
            var rnd = new Random();

            bool hasTransparency = TransparentCheckBox.IsChecked ?? false;

            for (var i = 0; i < _vertexColorMaterial.PositionColors.Length; i++)
            {
                float alpha;
                Color4 newColor;

                if (changeAlpha)
                    alpha = hasTransparency ? (float)rnd.NextDouble() : 1;
                else
                    alpha = _vertexColorMaterial.PositionColors[i].Alpha;

                if (changeColors)
                    newColor = new Color4((float)rnd.NextDouble(), (float)rnd.NextDouble(), (float)rnd.NextDouble(), alpha);
                else
                    newColor = new Color4(_vertexColorMaterial.PositionColors[i].Red, _vertexColorMaterial.PositionColors[i].Green, _vertexColorMaterial.PositionColors[i].Blue, alpha);

                _vertexColorMaterial.PositionColors[i] = newColor;
            }

            _vertexColorMaterial.HasTransparency = hasTransparency;

            _vertexColorMaterial.Update();

            // We need to notify the DXEngine about the change
            if (MainDXViewportView.DXScene != null)
                MainDXViewportView.DXScene.NotifyChange(DXScene.ChangeNotifications.MaterialPropertiesChanged);
        }
    }
}
