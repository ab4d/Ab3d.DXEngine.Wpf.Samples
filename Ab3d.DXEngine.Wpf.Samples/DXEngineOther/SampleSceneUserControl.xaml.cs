using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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
using Ab3d.Cameras;
using Ab3d.Common.Cameras;
using Ab3d.DirectX;
using Ab3d.DirectX.Controls;
using SharpDX;
using Ab3d.DirectX.Materials;

namespace Ab3d.DXEngine.Wpf.Samples.DXEngineOther
{
    // THIS CODE IS USED IN THE WinForms SAMPLE - USING WPF HOST

    /// <summary>
    /// Interaction logic for SampleSceneUserControl.xaml
    /// </summary>
    public partial class SampleSceneUserControl : Page
    {
        private DateTime _startTime;
        //private TargetPositionCamera _targetPositionCamera;
        private SpotLight _wpfSpotLight;
        private GeometryModel3D _spotLightModel;
        private TranslateTransform3D _spotLightTranslate;
        private PointLight _wpfPointLight1;
        private GeometryModel3D _pointLightModel;
        private TranslateTransform3D _pointLightTranslate;

        public SampleSceneUserControl()
        {
            InitializeComponent();

            //MainViewportView.GraphicsProfiles = DXEngineSettings.Current.GraphicsProfiles;
            //MainViewportView.PresentationType = DXEngineSettings.Current.PresentationType;

            DXDiagnostics.IsCollectingStatistics = true;

            CreateScene();

            this.Loaded += (sender, args) => _startTime = DateTime.Now;

            this.Unloaded += (sender, args) => MainDXViewportView.Dispose();
        }

        private void MainViewportView_OnSceneUpdating(object sender, EventArgs e)
        {
            var elapsed = (DateTime.Now - _startTime).TotalSeconds;

            //_targetPositionCamera.Heading = elapsed * 3;
            UpdateLightPositions(time: elapsed);            
        }

        private void CreateScene()
        {
            //_targetPositionCamera = new Ab3d.Cameras.TargetPositionCamera();

            //_targetPositionCamera.TargetPosition = new Point3D(0, 0, 0);
            //_targetPositionCamera.Heading = 30;
            //_targetPositionCamera.Attitude = -45;
            //_targetPositionCamera.Distance = 1200;

            //_targetPositionCamera.FarPlaneDistance = 20000000;
            //_targetPositionCamera.NearPlaneDistance = 0.125;

            //_targetPositionCamera.TargetViewport3D = MainViewport;
            //_targetPositionCamera.Refresh();

            //_targetPositionCamera.ShowCameraLight = ShowCameraLightType.Never;



            CreateLights();

            CreateTestScene(MainViewport);
        }

        private void CreateTestScene(Viewport3D wpfViewport3D)
        {
            var boxMaterial = new DiffuseMaterial(Brushes.Orange);
            
            var sphereMaterial = new MaterialGroup();
            sphereMaterial.Children.Add(new DiffuseMaterial(Brushes.SkyBlue));
            sphereMaterial.Children.Add(new SpecularMaterial(Brushes.White, 20));

            var lightMaterialGroup = new MaterialGroup();
            lightMaterialGroup.Children.Add(new DiffuseMaterial(Brushes.Black));
            lightMaterialGroup.Children.Add(new EmissiveMaterial(Brushes.Yellow));


            var objectsGroup = new Model3DGroup();

            //var boxMesh = new Ab3d.Meshes.BoxMesh3D(new Point3D(0, 0, 0), new Size3D(30, 30, 30), 1, 1, 1).Geometry;
            //var sphereMesh = new Ab3d.Meshes.SphereMesh3D(new Point3D(0, 0, 0), 15, 20, false).Geometry;


            //for (int y = 0; y < 5; y++)
            //{
            //    for (int x = -200; x <= 200; x += 100)
            //    {
            //        for (int z = -200; z <= 200; z += 100)
            //        {
            //            var geometryModel3D = new GeometryModel3D();

            //            if ((y % 2) == 0)
            //            {
            //                geometryModel3D.Geometry = boxMesh;
            //                geometryModel3D.Material = boxMaterial;
            //            }
            //            else
            //            {
            //                geometryModel3D.Geometry = sphereMesh;
            //                geometryModel3D.Material = sphereMaterial;
            //            }

            //            geometryModel3D.Transform = new TranslateTransform3D(x, y * 50, z);

            //            objectsGroup.Children.Add(geometryModel3D);
            //        }    
            //    }
            //}

            var boxMesh = new Ab3d.Meshes.BoxMesh3D(new Point3D(0, 0, 0), new Size3D(1, 1, 1), 1, 1, 1).Geometry;
            var sphereMesh = new Ab3d.Meshes.SphereMesh3D(new Point3D(0, 0, 0), 10, 20, false).Geometry;

            double yPos = 0;
            double firstSize = 50;

            for (int y = 0; y < 5; y++)
            {
                int samples = (y * 2) + 2;
                int halfSamples = samples / 2;
                double size = 400 / (samples * 1.5);

                if (y == 0)
                    firstSize = size;

                double xPos = -200;

                for (int x = -halfSamples; x <= halfSamples; x++)
                {
                    double zPos = -200;

                    for (int z = -halfSamples; z <= halfSamples; z++)
                    {
                        var geometryModel3D = new GeometryModel3D();

                        geometryModel3D.Geometry = boxMesh;
                        geometryModel3D.Material = boxMaterial;

                        var transform3DGroup = new Transform3DGroup();
                        transform3DGroup.Children.Add(new ScaleTransform3D(size, size, size));
                        transform3DGroup.Children.Add(new TranslateTransform3D(xPos, yPos, zPos));

                        geometryModel3D.Transform = transform3DGroup;

                        objectsGroup.Children.Add(geometryModel3D);


                        zPos += size * 1.5;
                    }

                    xPos += size * 1.5;
                }

                yPos += size + y * 7 + 10;
            }


            for (int a = 0; a < 360; a += 20)
            {
                double rad = MathUtil.DegreesToRadians(a);
                double x = Math.Sin(rad) * 600;
                double z = Math.Cos(rad) * 600;


                var geometryModel3D = new GeometryModel3D();

                geometryModel3D.Geometry = sphereMesh;
                geometryModel3D.Material = sphereMaterial;

                var transform3DGroup = new Transform3DGroup();
                transform3DGroup.Children.Add(new ScaleTransform3D(8, 8, 8));
                transform3DGroup.Children.Add(new TranslateTransform3D(x, 20, z));

                geometryModel3D.Transform = transform3DGroup;

                objectsGroup.Children.Add(geometryModel3D);
            }

            var bottomBoxModel3D = new GeometryModel3D();
            bottomBoxModel3D.Geometry = new Ab3d.Meshes.BoxMesh3D(new Point3D(0, -(firstSize / 2), 0), new Size3D(2000, 10, 2000), 1, 1, 1).Geometry;
            //bottomBoxModel3D.Geometry = new Ab3d.Meshes.BoxMesh3D(new Point3D(0, -(firstSize / 2), 0), new Size3D(2000, 10, 2000), 50, 1, 50).Geometry; // To see at least some spot light on bottom box in WPF, create the box from 50 x 50 mesh (or more)
            bottomBoxModel3D.Material = new DiffuseMaterial(Brushes.Silver);

            objectsGroup.Children.Add(bottomBoxModel3D);

            var wireGrid = new Ab3d.Visuals.WireGridVisual3D();
            wireGrid.CenterPosition = new Point3D(0, -(firstSize / 2) + 11, 0);
            wireGrid.Size = new Size(1800, 1800);
            wireGrid.WidthCellsCount = 9;
            wireGrid.HeightCellsCount = 9;
            wireGrid.LineColor = Colors.SkyBlue;
            wireGrid.LineThickness = 3;

            wpfViewport3D.Children.Add(wireGrid);



            var lightsGroup = new Model3DGroup();

            _spotLightModel = new GeometryModel3D();
            _spotLightModel.Geometry = sphereMesh;
            _spotLightModel.Material = lightMaterialGroup;

            _spotLightTranslate = new TranslateTransform3D(_wpfSpotLight.Position.X, _wpfSpotLight.Position.Y, _wpfSpotLight.Position.Z);
            _spotLightModel.Transform = _spotLightTranslate;

            lightsGroup.Children.Add(_spotLightModel);


            _pointLightModel = new GeometryModel3D();
            _pointLightModel.Geometry = sphereMesh;
            _pointLightModel.Material = lightMaterialGroup;

            _pointLightTranslate = new TranslateTransform3D(_wpfPointLight1.Position.X, _wpfPointLight1.Position.Y, _wpfPointLight1.Position.Z);
            _pointLightModel.Transform = _pointLightTranslate;

            lightsGroup.Children.Add(_pointLightModel);



            var modelVisual3D = new ModelVisual3D();
            modelVisual3D.Content = objectsGroup;

            wpfViewport3D.Children.Add(modelVisual3D);
            
            var lightsVisual3D = new ModelVisual3D();
            lightsVisual3D.Content = lightsGroup;

            wpfViewport3D.Children.Add(lightsVisual3D);
        }


        private void CreateLights()
        {
            var lightsVisual3D = new ModelVisual3D();
            MainViewport.Children.Add(lightsVisual3D);

            var lightsGroup = new Model3DGroup();
            lightsVisual3D.Content = lightsGroup;

            _wpfSpotLight = new System.Windows.Media.Media3D.SpotLight();
            _wpfSpotLight.Color = System.Windows.Media.Color.FromRgb(200, 200, 200);
            
            // Attenuation describes how the distance from the light affects the intensity
            // When ConstantAttenuation == 1 and LinearAttenuation == 0 and QuadraticAttenuation == 0 this means that distance does not affect the light intensity
            // The attenuation factor is calculated as:
            // attFactor = ConstantAttenuation + LinearAttenuation * d + QuadraticAttenuation * d * d;  // d is distance from the light
            // The final light color is get by divided the light color by attFactor.
            _wpfSpotLight.ConstantAttenuation = 1.0;
            _wpfSpotLight.LinearAttenuation = 0.0;
            _wpfSpotLight.QuadraticAttenuation = 0;
            _wpfSpotLight.OuterConeAngle = 40;
            _wpfSpotLight.InnerConeAngle = 35;
            _wpfSpotLight.Range = 4000;


            lightsGroup.Children.Add(_wpfSpotLight);

            _wpfPointLight1 = new PointLight();
            _wpfPointLight1.Range = 100;
            _wpfPointLight1.ConstantAttenuation = 0.0;
            _wpfPointLight1.LinearAttenuation = 1.0 / 50.0; // the light intensity decreased by distance from light
            _wpfPointLight1.QuadraticAttenuation = 0;
            lightsGroup.Children.Add(_wpfPointLight1);

            UpdateLightPositions(time: 0);


            //var directionalLight = new DirectionalLight();
            //directionalLight.Direction = new Vector3D(0, -0.1, -1);
            //lightsGroup.Children.Add(directionalLight);


            var wpfAmbientLight = new System.Windows.Media.Media3D.AmbientLight(System.Windows.Media.Color.FromRgb(10, 10, 10));
            lightsGroup.Children.Add(wpfAmbientLight);
        }

        private void UpdateLightPositions(double time)
        {
            // Animate spot light - position
            float angle = (float)time * 30.0f;
            float rad = MathUtil.DegreesToRadians(angle);

            float x = (float)Math.Sin(rad) * 800.0f;
            float y = (float)Math.Cos(MathUtil.DegreesToRadians((float)time * 90.0f)) * 300.0f + 400;
            float z = (float)Math.Cos(rad) * 800.0f;

            var lightPosition = new Point3D(x, y, z);

            // Animate spot light - target
            angle = (float)(time) * 45.0f;
            rad = MathUtil.DegreesToRadians(angle);

            x = (float)Math.Sin(rad) * 200.0f;
            z = (float)Math.Cos(rad) * 200.0f;

            var lightTarget = new Point3D(x, 0, z);
            var lightDirection = lightTarget - lightPosition;
            lightDirection.Normalize();

            _wpfSpotLight.Position = lightPosition;
            _wpfSpotLight.Direction = lightDirection;

            
            // Animate point light
            angle = (float)time * 60.0f;
            rad = MathUtil.DegreesToRadians(angle);
            x = (float)Math.Sin(rad) * 150.0f;
            y = (float)Math.Cos(rad) * 100.0f + 450;
            z = (float)Math.Cos(rad) * 150.0f;
            
            //x = (float)Math.Sin(rad) * 800.0f;
            //y = (float)Math.Cos(rad) * 50.0f + 80;
            //z = (float)Math.Cos(rad) * 800.0f;
            _wpfPointLight1.Position = new Point3D(x, y, z);

            if (_spotLightTranslate != null)
            {
                _spotLightTranslate.OffsetX = lightPosition.X;
                _spotLightTranslate.OffsetY = lightPosition.Y;
                _spotLightTranslate.OffsetZ = lightPosition.Z;

                _pointLightTranslate.OffsetX = _wpfPointLight1.Position.X;
                _pointLightTranslate.OffsetY = _wpfPointLight1.Position.Y;
                _pointLightTranslate.OffsetZ = _wpfPointLight1.Position.Z;
            }
        }
    }
}
