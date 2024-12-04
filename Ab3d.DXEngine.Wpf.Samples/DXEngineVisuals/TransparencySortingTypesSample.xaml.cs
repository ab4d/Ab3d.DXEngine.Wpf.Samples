using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
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
using Ab3d.Common;
using Ab3d.Common.Cameras;
using Ab3d.DirectX;
using Ab3d.DirectX.Materials;
using Ab3d.DXEngine.Wpf.Samples.DXEnginePerformance;
using Ab3d.Utilities;
using Ab3d.Visuals;
using SharpDX.Direct3D11;
using Material = Ab3d.DirectX.Material;

namespace Ab3d.DXEngine.Wpf.Samples.DXEngineVisuals
{
    /// <summary>
    /// Interaction logic for TransparencySortingTypesSample.xaml
    /// </summary>
    public partial class TransparencySortingTypesSample : Page
    {
        private DiffuseMaterial _semiTransparentMaterial;

        private List<AxisAngleRotation3D> _allRotateTransforms;

        private DateTime _startTime;
        private bool _isRotatingObjects;

        private BoxVisual3D _boxVisual3D;

        public TransparencySortingTypesSample()
        {
            InitializeComponent();

            NoSortingInfoControl.InfoText = 
@"By default the transparent objects are rendered in the same order as they are added to the scene.

Because of alpha blending and depth buffering (see internet for more info on those terms) the transparent images must be rendered in the order from the object that is the most distant from the camera to the closest to the camera. Otherwise the objects that are behind transparent objects may not be correctly seen through the transparent objects (notice in this sample that in this mode at some rotation angles objects are rendered correctly, but at other angles they are not).";
            
            CenterSortingInfoControl.InfoText = 
@"To solve transparency rendering problems, Ab3d.DXEngine supports a very efficient transparency sorting (Transparency sorting is supported also by Ab3d.PowerToy but it is much less efficient).

When enabled, then by default the transparent objects will be sorted based on the distance from the center of the object's bounding box to the camera.

This is very fast and in most cases this gives accurate results. But in case when the size of transparent objects is significantly different (middle objects in this sample) or when the object's center is at the same position (right objects in this sample), then using center of the bounding box may not produce accurate results.";

            AllCornersInfoControl.InfoText =
@"In case when using center of the bounding box to measure distance to the camera may not produce accurate results, it is recommended to measure distance to the camera from all corners of the bounding box. This is slower (because for each object 8 positions are compared instead of a single position) but can produce more accurate results (see middle and right objects in this sample). To see performance difference see the 'Transparency sorting performance' sample.";

            NoDepthInfoControl.InfoText =
@"By default when a transparent object is rendered it also write its depth to the depth buffer. This prevents rendering objects that are rendered after that object and are farther away from the camera to be rendered.

When this CheckBox is checked, then this will prevent writing to depth buffer for all transparent objects (set CommonStates.DefaultTransparentDepthStencilState to DepthRead instead of DepthReadWrite).

This can helps to prevent some transparency problems.

However, this does not work correctly in all of the cases because objects that are farther away from the camera can be rendered after closer objects, and this can affect the final color of the pixels.
You can observe that by first checking the 'No transparency sorting' check box. This will prevent showing blue boxes behind green plate in the middle group of objects. When checking 'Prevent writing to depth for transparent objects' the result will be much better, but the final color will stil not be correct because the blue boxes are rendered after the green plate and the result colors are too blue. If you check the 'Transparency sorting of all bounding box corners' check box the results will be correct.

Preventing depth write for transparent objects usually gives better results but this is not the default option to preserve the behavior of the engine with the previous version when this feature was introduced.";


            var semiTransparentBrush = new SolidColorBrush(Color.FromArgb(64, 26, 161, 226));
            _semiTransparentMaterial = new DiffuseMaterial(semiTransparentBrush);
            _semiTransparentMaterial.Freeze(); // This can significantly speed up the creation of objects if there are many objects that use the same material
            
            _allRotateTransforms = new List<AxisAngleRotation3D>();

            MainDXViewportView.IsTransparencySortingEnabled = true; // Enable transparency sorting

            CreateTestScene();

            _startTime = DateTime.Now;
            _isRotatingObjects = true;

            CompositionTarget.Rendering += delegate (object sender, EventArgs args)
            {
                if (!_isRotatingObjects)
                    return;
                
                var elapsedSeconds = (DateTime.Now - _startTime).TotalSeconds;

                foreach (var axisAngleRotation3D in _allRotateTransforms)
                    axisAngleRotation3D.Angle = elapsedSeconds * 30;
            };

            MainDXViewportView.DXSceneDeviceCreated += delegate(object sender, EventArgs args)
            {
                MainDXViewportView.IsTransparencySortingEnabled = (CenterSortingRadioButton.IsChecked ?? false) ||
                                                                  (AllCornersRadioButton.IsChecked ?? false);

                SetCheckAllBoundingBoxCorners(AllCornersRadioButton.IsChecked ?? false);

                if (NoDepthCheckBox.IsChecked ?? false)
                    MainDXViewportView.DXScene.DXDevice.CommonStates.DefaultTransparentDepthStencilState = MainDXViewportView.DXScene.DXDevice.CommonStates.DepthRead;
            };

            this.Unloaded += (sender, args) => MainDXViewportView.Dispose();
        }

        private void SetCheckAllBoundingBoxCorners(bool isEnabled)
        {
            if (MainDXViewportView == null || MainDXViewportView.DXScene == null)
                return; 

            var distanceSortedRenderingQueue = MainDXViewportView.DXScene.RenderingQueues.OfType<CameraDistanceSortedRenderingQueue>().FirstOrDefault();
            if (distanceSortedRenderingQueue != null)
                distanceSortedRenderingQueue.CheckAllBoundingBoxCorners = isEnabled;

            if (!_isRotatingObjects)
                MainDXViewportView.Refresh(); // render the scene again
        }

        private void CreateTestScene()
        {
            var baseVisual3D = CreateRotatedBaseVisual3D(new Vector3D(-500, 0, 0));
            CreateMultipleBoxes(new Point3D(0, 125, 0), 4, 4, modelSize: new Size3D(40, 200, 40), baseVisual3D);


            baseVisual3D = CreateRotatedBaseVisual3D(new Vector3D(0, 0, 0));

            var greenTransparentBrush = new SolidColorBrush(Color.FromArgb(64, 96, 226, 26));
            var greenTransparentMaterial = new DiffuseMaterial(greenTransparentBrush);
            _boxVisual3D = new BoxVisual3D()
            {
                CenterPosition = new Point3D(0, 70, 0),
                Size = new Size3D(250, 10, 250),
                Material = greenTransparentMaterial,
                //BackMaterial = greenTransparentMaterial // Here only single sided material is demonstrated (otherwise two RenderablePrimitives would need to be moved in DistanceSortedRenderingQueueOnSortingCompleted)
            };

            baseVisual3D.Children.Add(_boxVisual3D);

            CreateMultipleBoxes(new Point3D(0, 50, 0), 4, 4, modelSize: new Size3D(40, 40, 40), baseVisual3D);



            baseVisual3D = CreateRotatedBaseVisual3D(new Vector3D(500, 0, 0));
            CreateMultipleBoxesWithSameCenter(center: new Point3D(0, 100, 0), boxesCount: 5, modelStartSize: 100, modelEndSize: 200, baseVisual3D);
        }

        private ModelVisual3D CreateRotatedBaseVisual3D(Vector3D offset)
        {
            var axisAngleRotation3D = new AxisAngleRotation3D(new Vector3D(0, 1, 0), 0);
            _allRotateTransforms.Add(axisAngleRotation3D);

            var transform3DGroup = new Transform3DGroup();
            transform3DGroup.Children.Add(new RotateTransform3D(axisAngleRotation3D));
            transform3DGroup.Children.Add(new TranslateTransform3D(offset));

            var modelVisual3D = new ModelVisual3D();
            modelVisual3D.Transform = transform3DGroup;

            var boxVisual3D = new BoxVisual3D()
            {
                CenterPosition = new Point3D(0, 0, 0),
                Size = new Size3D(300, 5, 300),
                Material = new DiffuseMaterial(Brushes.LightGray),
            };

            modelVisual3D.Children.Add(boxVisual3D);

            SemiTransparentRootVisual3D.Children.Add(modelVisual3D);

            return modelVisual3D;
        }

        private void CreateMultipleBoxes(Point3D center, int xCount, int zCount, Size3D modelSize, ModelVisual3D baseVisual3D)
        {
            var parentModelVisual3D = new ModelVisual3D();

            double xMargin = modelSize.X * 0.5;
            double zMargin = modelSize.Z * 0.5;

            var startPosition = center - new Vector3D((modelSize.X + xMargin) * (xCount - 1) / 2, 0, (modelSize.Z + zMargin) * (zCount - 1) / 2);

            for (int x = 0; x < xCount; x++)
            {
                for (int z = 0; z < zCount; z++)
                {
                    var boxVisual3D = new BoxVisual3D()
                    {
                        CenterPosition = startPosition + new Vector3D((modelSize.X + xMargin) * x, 0, (modelSize.Z + zMargin) * z),
                        Size = modelSize,
                        Material = _semiTransparentMaterial,
                        BackMaterial = _semiTransparentMaterial
                    };

                    parentModelVisual3D.Children.Add(boxVisual3D);
                }
            }

            baseVisual3D.Children.Add(parentModelVisual3D);
        }

        private void CreateMultipleBoxesWithSameCenter(Point3D center, int boxesCount, float modelStartSize, float modelEndSize, ModelVisual3D baseVisual3D)
        {
            var parentModelVisual3D = new ModelVisual3D();

            for (int i = 0; i < boxesCount; i++)
            {
                double boxSize = modelStartSize + (modelEndSize - modelStartSize) * ((double)i / (double)boxesCount);
                var boxVisual3D = new BoxVisual3D()
                {
                    CenterPosition = center,
                    Size = new Size3D(boxSize, boxSize, boxSize),
                    Material = _semiTransparentMaterial,
                    BackMaterial = _semiTransparentMaterial
                };

                parentModelVisual3D.Children.Add(boxVisual3D);
            }

            baseVisual3D.Children.Add(parentModelVisual3D);
        }

        private void NoSortingRadioButton_OnChecked(object sender, RoutedEventArgs e)
        {
            MainDXViewportView.IsTransparencySortingEnabled = false;

            if (!_isRotatingObjects)
                MainDXViewportView.Refresh(); // render the scene again
        }

        private void CenterSortingRadioButton_OnChecked(object sender, RoutedEventArgs e)
        {
            MainDXViewportView.IsTransparencySortingEnabled = true;
            SetCheckAllBoundingBoxCorners(false);
        }

        private void AllCornersRadioButton_OnChecked(object sender, RoutedEventArgs e)
        {
            MainDXViewportView.IsTransparencySortingEnabled = true;
            SetCheckAllBoundingBoxCorners(true);
        }

        private void NoDepthCheckBoxCheckedChanged(object sender, RoutedEventArgs e)
        {
            if (MainDXViewportView.DXScene == null)
                return;

            MainDXViewportView.DXScene.DXDevice.CommonStates.DefaultTransparentDepthStencilState = (NoDepthCheckBox.IsChecked ?? false) 
                                                                                                     ? MainDXViewportView.DXScene.DXDevice.CommonStates.DepthRead
                                                                                                     : MainDXViewportView.DXScene.DXDevice.CommonStates.DepthReadWrite;

            if (!_isRotatingObjects)
                MainDXViewportView.Refresh(); // render the scene again
        }

        private void StartStopObjectRotationButton_OnClick(object sender, RoutedEventArgs e)
        {
            if (_isRotatingObjects)
            {
                _isRotatingObjects = false;
                StartStopObjectRotationButton.Content = "Start object rotation";
            }
            else
            {
                _startTime = DateTime.Now;
                _isRotatingObjects = true;
                StartStopObjectRotationButton.Content = "Stop object rotation";
            }
        }
    }
}
