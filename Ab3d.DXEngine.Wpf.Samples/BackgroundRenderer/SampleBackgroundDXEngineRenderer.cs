using System;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using System.Windows.Threading;
using Ab3d.Cameras;
using Ab3d.Common.Cameras;
using Ab3d.Visuals;
using SharpDX;

namespace Ab3d.DXEngine.Wpf.Samples.BackgroundRenderer
{
    public class SampleBackgroundDXEngineRenderer : BackgroundDXEngineRenderer
    {
        private Action<string> _logFpsStatsAction;

        protected TargetPositionCamera _targetPositionCamera;

        private float _backgroundHeadingChangeInSecond;
        private DateTime _lastCameraChangeTime;


        // FPS stats fields:
        private int _framesCount;
        private double _renderTime;
        private int _lastFpsMeterSecond = -1;
        private bool _isFirstSecond = true;


        private volatile int _renderingSleep = 0;

        public int RenderingSleep
        {
            get { return _renderingSleep; }
            set
            {
                _renderingSleep = value;
                isSceneDirty = true;
            }
        }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="hWnd">handle to the window (area of the screen) that will be used to render the 3D scene to - can be created with D3DHost control.</param>
        /// <param name="logFpsStatsAction">Action that will update the FPS statistics text - the method must use Main UI thread Dispatcer to do the change on the UI thread. When null, the statistics will not be generated.</param>
        public SampleBackgroundDXEngineRenderer(IntPtr hWnd,
                                                Action<string> logFpsStatsAction)
            : base(hWnd)
        {
            _logFpsStatsAction = logFpsStatsAction;
        }

        protected override void OnDXViewportViewInitialized()
        {
            dxViewportView.BackgroundColor = Colors.Aqua;

            dxViewportView.SceneRendered += delegate (object sender, EventArgs args)
            {
                // Collect statistics
                _framesCount++;

                if (dxViewportView.DXScene != null && dxViewportView.DXScene.Statistics != null)
                    _renderTime += dxViewportView.DXScene.Statistics.TotalRenderTimeMs;

                if (_renderingSleep > 0)
                    System.Threading.Thread.Sleep(_renderingSleep);
            };


            // Now everything is set up to create the sample 3D scene
            CreateScene(createExtremlyComplexScene: false);

            CreateCamera();

            // Do the initial scene render
            RenderScene();
        }

        // Returns true if rendering is needed because of the BackgroundUpdate
        protected override bool BackgroundUpdate()
        {
            UpdateBackgroundFpsMeter();


            // Rotate camera if needed
            if (MathUtil.IsZero(_backgroundHeadingChangeInSecond))
                return false;

            DateTime now = DateTime.Now;

            double elapsedSeconds = (now - _lastCameraChangeTime).TotalSeconds;

            _targetPositionCamera.Heading += _backgroundHeadingChangeInSecond * elapsedSeconds;

            _lastCameraChangeTime = now;
            isSceneDirty = true;

            return true;
        }


        private void CreateCamera()
        {
            _targetPositionCamera = new TargetPositionCamera()
            {
                TargetViewport3D = wpfViewport3D,
                TargetPosition = new Point3D(0, 0.5, 0),
                Distance = 8,
                Heading = 30,
                Attitude = -10,
                ShowCameraLight = ShowCameraLightType.Never
            };

            // Because this _targetPositionCamera is never added to the UI three, we need to manually call Refresh method
            _targetPositionCamera.Refresh();
        }

        public void StartCameraRotation(double headingChangeInSecond)
        {
            BackgroundRendererDispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(() =>
            {
                _backgroundHeadingChangeInSecond = (float)headingChangeInSecond;
                _lastCameraChangeTime = DateTime.Now;
            }));
        }

        public void StopCameraRotation()
        {
            BackgroundRendererDispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(() =>
            {
                _backgroundHeadingChangeInSecond = 0;
                _lastCameraChangeTime = DateTime.Now;
            }));
        }

        public void ChangeCamera(double heading, double attitude, double distance, Point3D targetPosition)
        {
            if (_targetPositionCamera == null)
                return;

            BackgroundRendererDispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(() =>
            {
                _targetPositionCamera.BeginInit();
                _targetPositionCamera.TargetPosition = targetPosition;
                _targetPositionCamera.Heading        = heading;
                _targetPositionCamera.Attitude       = attitude;
                _targetPositionCamera.Distance       = distance;
                _targetPositionCamera.EndInit();

                isSceneDirty = true;
            }));
        }

        public void GetCameraData(out double heading, out double attitude, out double distance, out Point3D targetPosition)
        {
            if (_targetPositionCamera == null)
            {
                targetPosition = new Point3D();
                heading = attitude = distance = 0;
                return;
            }

            targetPosition = _targetPositionCamera.TargetPosition;
            heading        = _targetPositionCamera.Heading;
            attitude       = _targetPositionCamera.Attitude;
            distance       = _targetPositionCamera.Distance;
        }

        public void CreateScene(bool createExtremlyComplexScene)
        {
            // This is called in background thread

            var rootVisual3D = new ModelVisual3D();

            var floorBox = new BoxVisual3D()
            {
                CenterPosition = new Point3D(0, -0.05, 0),
                Size = new Size3D(15, 0.1, 15),
                Material = new DiffuseMaterial(Brushes.Green)
            };

            rootVisual3D.Children.Add(floorBox);


            double centerX = 0;
            double centerZ = 0;

            double boxesHeight = 1.4;

            double bigCircleRadius = 2;
            double singleSphereRadius = 0.1;


            int circleElementsCount;
            double spheresStartRadius;
            double spheresEndRadius;
            int yObjectsCount;

            if (createExtremlyComplexScene)
            {
                // This will create a 3D scene with almost 6000 3D objects
                // This will put a big load on GPU and especially on the CPU (the DirectX API and driver).
                //
                // NOTE:
                // When rendering many instances of the same object, it is possible to render the same scene 
                // much much more efficiently with using InstancedMeshGeometryVisual3D.
                boxesHeight = 3;
                circleElementsCount = 40;
                spheresStartRadius = bigCircleRadius * 0.3;
                spheresEndRadius = bigCircleRadius;
                yObjectsCount = 30;
            }
            else
            {
                boxesHeight = 1.4;
                circleElementsCount = 18;
                spheresStartRadius = bigCircleRadius;
                spheresEndRadius = bigCircleRadius;
                yObjectsCount = 1;
            }


            var boxMaterial = new DiffuseMaterial(Brushes.Gray);
            var sphereMaterial = new MaterialGroup();
            sphereMaterial.Children.Add(new DiffuseMaterial(Brushes.Gold));
            sphereMaterial.Children.Add(new SpecularMaterial(Brushes.White, 16));

            // IMPORTANT:
            // Freezing materials speeds up object creation by a many times !!!
            boxMaterial.Freeze();
            sphereMaterial.Freeze();

            for (int y = 0; y < yObjectsCount; y++)
            {
                double oneBoxHeight;
                double oneBoxHeightSpace = boxesHeight / yObjectsCount;

                if (yObjectsCount == 1)
                    oneBoxHeight = oneBoxHeightSpace;
                else
                    oneBoxHeight = oneBoxHeightSpace * 0.7;

                for (int a = 0; a < 360; a += (int) (Math.Ceiling(360.0 / circleElementsCount)))
                {
                    double rad = SharpDX.MathUtil.DegreesToRadians(a);
                    double sin = Math.Sin(rad);
                    double cos = Math.Cos(rad);


                    double x = sin * bigCircleRadius + centerX;
                    double z = cos * bigCircleRadius + centerZ;

                    var boxVisual3D = new BoxVisual3D()
                    {
                        CenterPosition = new Point3D(x, (y + 0.5) * oneBoxHeightSpace, z),
                        Size = new Size3D(0.2, oneBoxHeight, 0.2),
                        Material = boxMaterial
                    };

                    rootVisual3D.Children.Add(boxVisual3D);

                    for (double radius = spheresStartRadius; radius <= spheresEndRadius; radius += singleSphereRadius * 2)
                    {
                        x = sin * radius + centerX;
                        z = cos * radius + centerZ;

                        var sphereVisual3D = new SphereVisual3D()
                        {
                            CenterPosition = new Point3D(x, boxesHeight + singleSphereRadius * (y + 0.5) * 2, z),
                            Radius = singleSphereRadius,
                            Material = sphereMaterial
                        };

                        rootVisual3D.Children.Add(sphereVisual3D);
                    }
                }
            }


            wpfViewport3D.Children.Clear();
            wpfViewport3D.Children.Add(rootVisual3D);


            // Add lights
            var lightsVisual3D = new ModelVisual3D();
            var lightsGroup = new Model3DGroup();

            var directionalLight = new DirectionalLight(Colors.White, new Vector3D(1, -0.3, 0));
            lightsGroup.Children.Add(directionalLight);

            var ambientLight = new AmbientLight(System.Windows.Media.Color.FromRgb(60, 60, 60));
            lightsGroup.Children.Add(ambientLight);

            lightsVisual3D.Content = lightsGroup;
            wpfViewport3D.Children.Add(lightsVisual3D);


            if (_targetPositionCamera != null)
            {
                if (createExtremlyComplexScene)
                {
                    _targetPositionCamera.TargetPosition = new Point3D(0, 2, 0);
                    _targetPositionCamera.Attitude = -30;
                    _targetPositionCamera.Distance = 15;
                }
                else
                {
                    _targetPositionCamera.TargetPosition = new Point3D(0, 0.5, 0);
                    _targetPositionCamera.Attitude = -10;
                    _targetPositionCamera.Distance = 8;
                }
            }

            isSceneDirty = true;
        }

        private void UpdateBackgroundFpsMeter()
        {
            if (_logFpsStatsAction == null)
                return;


            int currentSecond = DateTime.Now.Second;

            if (currentSecond == _lastFpsMeterSecond) // This is the most common case - exit early
                return;

            if (_lastFpsMeterSecond == -1)
            {
                _lastFpsMeterSecond = currentSecond;
                return;
            }


            // If we are here then currentSecond != _lastFpsMeterSecond

            // We start measuring in the middle of the first second so the result for the first second is not correct - do not show it
            if (_isFirstSecond)
            {
                _isFirstSecond = false;
            }
            else
            {
                string renderingStatsText;

                if (_framesCount == 0)
                {
                    renderingStatsText = "no frames rendered";
                }
                else
                {
                    renderingStatsText = string.Format("{0} FPS", _framesCount);
                    if (_renderTime > 0)
                    {
                        // Show average DXEngine renderin time - time to render on frame (includes the DXEngine's update process that updates the internal objects base on the changes in WPF 3D objects)
                        // From this time it is possible to calculate the theoretical FPS that could be achieved if all the CPU time on one thread would be spend only for rendering the frame
                        double averageRenderTime = _renderTime / _framesCount;
                        renderingStatsText += string.Format(";  DXEngine RenderTime: {0:0.00}ms => {1:0} FPS", averageRenderTime, 1000.0 / averageRenderTime); // Show theoretical FPS from render time
                    }
                }

                // Update the TextBlock on the Main UI Thread
                // The _logFpsStatsAction should use Dispatcher.BeginInvoke to do the update on the Main UI thread
                _logFpsStatsAction(renderingStatsText);
            }

            _framesCount = 0;
            _renderTime = 0;
            _lastFpsMeterSecond = currentSecond;
        }
    }
}