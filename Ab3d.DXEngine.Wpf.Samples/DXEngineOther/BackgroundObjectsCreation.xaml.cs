using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using System.Windows.Threading;
using Ab3d.Common;
using Ab3d.Visuals;

namespace Ab3d.DXEngine.Wpf.Samples.DXEngineOther
{
    /// <summary>
    /// Interaction logic for BackgroundObjectsCreation.xaml
    /// </summary>
    public partial class BackgroundObjectsCreation : Page
    {
        private Random _rnd = new Random();

        private Stopwatch _stopwatch;

        private string _objFileName;

        private volatile float _backgroundThreadTime;

        public BackgroundObjectsCreation()
        {
            InitializeComponent();

            _objFileName = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources\\Models\\dragon_vrip_res3.obj");

            // Load the test file before the actual test so that the file gets into File System cache
            var readerObj = new Ab3d.ReaderObj();
            readerObj.ReadModel3D(_objFileName);

            Camera1.StartRotation(30, 0);
        }

        private void CreateObjectsUIButton_OnClick(object sender, RoutedEventArgs e)
        {
            RootModelVisual3D.Children.Clear();
            StartStopwatch();

            var model3DGroup = CreateManyObjects();

            // Create ModelVisual3D from modelVisual3D and add it to the scene
            RootModelVisual3D.Children.Add(model3DGroup.CreateModelVisual3D());


            MainDXViewportView.Refresh(); // Manually render next frame

            StopStopwatch(UIThreadTime1TextBlock);
        }

        private void CreateObjectsBackgroundButton_OnClick(object sender, RoutedEventArgs e)
        {
            RootModelVisual3D.Children.Clear();
            GC.Collect();

            if (MainDXViewportView.DXScene == null)
            {
                MessageBox.Show("This sample cannot run with WPF 3D rendering");
                return;
            }

            var dxScene = MainDXViewportView.DXScene;

            Task.Factory.StartNew(() =>
            {
                var stopwatch = new Stopwatch();
                stopwatch.Start();

                var model3DGroup = CreateManyObjects();

                var createdSceneNode = Ab3d.DirectX.Models.SceneNodeFactory.CreateFromModel3D(model3DGroup, null, dxScene);

                // Call InitializeResources to create all required DirectX resources from the background thread
                createdSceneNode.InitializeResources(dxScene);

                stopwatch.Stop();
                _backgroundThreadTime = (float)stopwatch.Elapsed.TotalMilliseconds;

                // Now go to UI thread and create the Visual3D objects and update the scene there.
                // There are two reasons to do that:
                // 1) We cannot create objects that are derived from Visual3D on the background thread (we also cannot freeze them with calling Freeze method - this is possible on MeshGeometry, Model3D and Material objects).
                // 2) We should not update the scene from the backgrond thread because we do not know when the UI thread is reading the scene.
                Dispatcher.Invoke(DispatcherPriority.Normal, new Action(() =>
                {
                    StartStopwatch();

                    // Create SceneNodeVisual3D that will show the created SceneNode
                    var sceneNodeVisual3D = new SceneNodeVisual3D(createdSceneNode);
                    RootModelVisual3D.Children.Add(sceneNodeVisual3D);

                    MainDXViewportView.Refresh(); // Manually render next frame

                    StopStopwatch(UIThreadTime2TextBlock);
                    BackgroundThreadTimeTextBlock.Text = string.Format("Background Thread time: {0:#,##0.00}ms", _backgroundThreadTime);
                }));
            });
        }

        private void LoadObjFileUIButton_OnClick(object sender, RoutedEventArgs e)
        {
            RootModelVisual3D.Children.Clear();
            StartStopwatch();

            // Load model from obj file
            var readerObj = new Ab3d.ReaderObj();
            var readModel3D = readerObj.ReadModel3D(_objFileName);

            // Scale and position the read model so that its bottom center is at (0,-100,0) and it can fit into 200 x 200 x 200 Rect3D
            Ab3d.Utilities.ModelUtils.PositionAndScaleModel3D(readModel3D, new Point3D(0, -100, 0), PositionTypes.Bottom, new Size3D(200, 200, 200));
            readModel3D.Freeze();

            // Create ModelVisual3D from readModel3D and add it to the scene
            RootModelVisual3D.Children.Add(readModel3D.CreateModelVisual3D());

            MainDXViewportView.Refresh(); // Manually render next frame

            StopStopwatch(UIThreadTime1TextBlock);
        }

        private void LoadObjFileBackgroundButton_OnClick(object sender, RoutedEventArgs e)
        {
            RootModelVisual3D.Children.Clear();
            GC.Collect();

            if (MainDXViewportView.DXScene == null)
            {
                MessageBox.Show("This sample cannot run with WPF 3D rendering");
                return;
            }

            var dxScene = MainDXViewportView.DXScene;

            Task.Factory.StartNew(() =>
            {
                var stopwatch = new Stopwatch();
                stopwatch.Start();

                // Load model from obj file
                var readerObj = new Ab3d.ReaderObj();
                var readModel3D = readerObj.ReadModel3D(_objFileName);

                // Scale and position the read model so that its bottom center is at (0,-100,0) and it can fit into 200 x 200 x 200 Rect3D
                Ab3d.Utilities.ModelUtils.PositionAndScaleModel3D(readModel3D, new Point3D(0, -100, 0), PositionTypes.Bottom, new Size3D(200, 200, 200));
                readModel3D.Freeze();

                var createdSceneNode = Ab3d.DirectX.Models.SceneNodeFactory.CreateFromModel3D(readModel3D, null, dxScene);

                // Call InitializeResources to create all required DirectX resources from the background thread
                createdSceneNode.InitializeResources(dxScene);

                stopwatch.Stop();
                _backgroundThreadTime = (float)stopwatch.Elapsed.TotalMilliseconds;

                // Now go to UI thread and create the Visual3D objects and update the scene there.
                // There are two reasons to do that:
                // 1) We cannot create objects that are derived from Visual3D on the background thread (we also cannot freeze them with calling Freeze method - this is possible on MeshGeometry, Model3D and Material objects).
                // 2) We should not update the scene from the backgrond thread because we do not know when the UI thread is reading the scene.
                Dispatcher.Invoke(DispatcherPriority.Normal, new Action(() =>
                {
                    StartStopwatch();

                    // Create SceneNodeVisual3D that will show the created SceneNode
                    var sceneNodeVisual3D = new SceneNodeVisual3D(createdSceneNode);
                    RootModelVisual3D.Children.Add(sceneNodeVisual3D);

                    MainDXViewportView.Refresh(); // Manually render next frame

                    StopStopwatch(UIThreadTime2TextBlock);
                    BackgroundThreadTimeTextBlock.Text = string.Format("Background Thread time: {0:#,##0.00}ms", _backgroundThreadTime);
                }));
            });


            StartStopwatch();
            StopStopwatch(UIThreadTime2TextBlock);
        }

        private Model3DGroup CreateManyObjects()
        {
            // NOTE:
            // When creatinig many instances of the same mesh, it is many many times faster
            // to use DirectX object instaning then creating individual objects as it is done here.
            // See InstancedMeshGeometry3DTest and InstanceModelGroupVisual3DTest for more info.
            // But in this sample we want to simulate a creation of a complex scene.

            var model3DGroup = new Model3DGroup();

            for (int i = 0; i < 1000; i++)
            {
                var sphereModel3D = Ab3d.Models.Model3DFactory.CreateSphere(centerPosition: GetRandomPosition(200), 
                                                                            radius: 5, 
                                                                            segments: 30,
                                                                            material: new DiffuseMaterial(new SolidColorBrush(GetRandomColor())));

                model3DGroup.Children.Add(sphereModel3D);
            }

            model3DGroup.Freeze(); // Freeze the Model3DGroup so the objects can be read on different thread

            return model3DGroup;
        }

        private void StartStopwatch()
        {
            GC.Collect();
            GC.WaitForFullGCComplete();

            _stopwatch = new Stopwatch();
            _stopwatch.Start();
        }

        private void StopStopwatch(TextBlock textBlock)
        {
            _stopwatch.Stop();

            string elapsedText = string.Format("{0:#,##0.00}ms", _stopwatch.Elapsed.TotalMilliseconds);
            string prefixText = textBlock.Text.Substring(0, textBlock.Text.IndexOf(":") + 1);

            textBlock.Text = prefixText + " " + elapsedText;
        }

        private Point3D GetRandomPosition(double scale)
        {
            double halfScale = scale * 0.5;

            return new Point3D(_rnd.NextDouble() * scale - halfScale, _rnd.NextDouble() * scale - halfScale, _rnd.NextDouble() * scale - halfScale);
        }

        private Color GetRandomColor()
        {
            return Color.FromRgb((byte) _rnd.Next(255), (byte) _rnd.Next(255), (byte) _rnd.Next(255));
        }
    }
}
