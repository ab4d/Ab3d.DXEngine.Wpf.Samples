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
using Ab3d.Controls;
using Ab3d.DirectX;
using Ab3d.DirectX.Client.Settings;
using Ab3d.DirectX.Controls;
using Ab3d.DirectX.Materials;
using Ab3d.DirectX.Models;
using Ab3d.Models;
using Ab3d.Visuals;
using VerticalAlignment = System.Windows.VerticalAlignment;

namespace Ab3d.DXEngine.Wpf.Samples.DXEngineVisuals
{
    // This sample shows how to prevent z-fighting artifacts that may appear when 3D lines are rendered on top of 3D solid models.
    // In this case the lines appear disconnected and fuzzy because sometimes they are on top of solid objects and sometimes they are inside solid objects.
    // The standard way to solve this problem is by applying a so called depth bias that moves the 3D lines closer to the camera.
    //
    // This cannot be done with WPF 3D rendering, but line rendering in Ab3d.DXEngine supports that.
    //
    // The easiest way to set depth bias is to use the following code:
    // lineVisual3D.SetDXAttribute(DXAttributeType.LineDepthBias, depthBias);
    //
    // In this case the lineVisual3D will be moved for the depthBias amount (in world coordinates) closer to the camera.
    // This calculation is done in the vertex shader so this works for any camera orientation.
    //
    // The LineDepthBias attribute can be applied for all Visual3D objects from Ab3d.PowerToys library that show 3D lines
    // (LineVisual3D, MultiLineVisual3D, PolyLineVisual3D, WireframeVisual3D, etc.)
    //
    // If the specified depth bias value is too bix, then the lines that are behind the solid object and should be hidden, may become visible.
    // If on the other side, the specified depth bias value is too small, then the lines will still be disconnected.
    // Therefore the correct depth bias value need to be set.
    //
    // The correct depth bias value is based on the distance of the object from the camera. 
    // Objects that are close to the camera require small depth bias.
    // Objects that are far away from the camera require bigger bias.
    //
    // The Ab3d.DXEngine can automatically adjust the depth bias based on the distance to the camera.
    // This can be enabled by setting the LineDynamicDepthBiasFactor. 
    //
    // In case when LineDynamicDepthBiasFactor is bigger then 0, then the actually used depth bias is calculated in the vertex shader as:
    // usedDepthBias = LineDepthBias * toEyeDistance * LineDynamicDepthBiasFactor; // toEyeDistance is distance from the 3D position to the camera
    //
    // Recommended values are:
    // LineDepthBias: 0.1; LineDynamicDepthBiasFactor: 0.02 (LineDepthBias works well without dynamic depth bias for objects with size around 100) 
    // LineDepthBias: 0.002; LineDynamicDepthBiasFactor: 1
    //
    // 
    // NOTE:
    // The LineDynamicDepthBiasFactor was introduces in v4.5.
    // Before that it was recommended to set the LineDepthBias based on the size of the object.
    // When using LineDynamicDepthBiasFactor, then this is not needed anymore and you can set LineDepthBias to the same value for all objects.
    // For backwards compatibility the default value for LineDynamicDepthBiasFactor is 0 (disabling dynamic depth bias).


    /// <summary>
    /// Interaction logic for LineDepthBiasSample.xaml
    /// </summary>
    public partial class LineDepthBiasSample : Page
    {
        private double _previousCameraDistance;

        private MultiLineVisual3D _shownLineVisual3D;

        private TargetPositionCamera _customCamera;

        private DisposeList _disposables;

        public LineDepthBiasSample()
        {
            InitializeComponent();

            DepthBiasComboBox.ItemsSource = new double[] { 0, 0.001, 0.005, 0.01, 0.05, 0.1, 0.5, 1.0, 5.0, 10.0 };
            DepthBiasComboBox.SelectedItem = 0.1;

            LineThicknessComboBox.ItemsSource = new double[] { 0.1, 0.2, 0.5, 1, 2 };
            LineThicknessComboBox.SelectedItem = 0.5;

            _disposables = new DisposeList();

            CreateAllScenes();


            DynamicBiasInfoControl.InfoText =
@"When the scene is showing some objects that are very close to the camera and some objects that are far away to the camera, then
different depth bias would need to be set to those objects. The objects that are closer to the camera require a small depth bias.
The objects that are far away from the camera require a bigger bias.

This can be automatically achieved by checking this CheckBox. This will set the LineDynamicDepthBiasFactor to 0.02.

When DynamicDepthBiasFactor is bigger then 0 then this factor is multiplied by the distance of the position to the camera and
this is then multiplied by the DepthBias. This can be used to correctly set the depth bias for objects that are close 
to the camera (require small depth bias) and to the objects that are far away from the camera (require big depth bias).
A recommended value is 0.02. This works well for all distances of 3D objects when the DepthBias is set to 0.1.";


            Unloaded += delegate(object sender, RoutedEventArgs args)
            {
                if (_disposables != null)
                {
                    _disposables.Dispose();
                    _disposables = null;
                }
            };
        }

        private void CreateAllScenes()
        {
            double depthBias = (double)DepthBiasComboBox.SelectedItem;
            bool useDynamicBiasCheckBox = UseDynamicBiasCheckBox.IsChecked ?? false;

            // First add the main view on the left so the Diagnostics window will open data for this view
            Border createdBorder;
            Viewport3D customViewport3D;
            DXViewportView customDXViewportView;
            AddNewDXViewportView(ViewportsGrid, rowIndex: 0, columnIndex: 0, boxSize: 100, depthBias: depthBias, useDynamicBiasCheckBox: useDynamicBiasCheckBox, createDXEngine: true, title: null,
                                 createdBorder: out createdBorder, createdLineVisual3D: out _shownLineVisual3D, createdViewport3D: out customViewport3D, createdDXViewportView: out customDXViewportView, createdTargetPositionCamera: out _customCamera);

            _previousCameraDistance = _customCamera.Distance; 


            Grid.SetRowSpan(createdBorder, 3);

            _customCamera.CameraChanged += (sender, args) => CustomCamera_OnCameraChanged(sender, args);

            var mouseCameraController = new Ab3d.Controls.MouseCameraController()
            {
                RotateCameraConditions = MouseCameraController.MouseAndKeyboardConditions.LeftMouseButtonPressed,
                MoveCameraConditions = MouseCameraController.MouseAndKeyboardConditions.LeftMouseButtonPressed | MouseCameraController.MouseAndKeyboardConditions.ControlKey,
                EventsSourceElement = createdBorder,
                TargetCamera = _customCamera
            };

            ViewportsGrid.Children.Add(mouseCameraController);


            // Add other views
            AddNewDXViewportView(ViewportsGrid, rowIndex: 0, columnIndex: 1, boxSize: 100, depthBias: 0.0, useDynamicBiasCheckBox: false, createDXEngine: false, title: "WPF 3D rendering");
            AddNewDXViewportView(ViewportsGrid, rowIndex: 0, columnIndex: 2, boxSize: 100, depthBias: 0.0, useDynamicBiasCheckBox: false, createDXEngine: true,  title: "DXEngine rendering\r\nNo depth bias");
            AddNewDXViewportView(ViewportsGrid, rowIndex: 1, columnIndex: 1, boxSize: 100, depthBias: 0.1, useDynamicBiasCheckBox: true,  createDXEngine: true,  title: "DXEngine\r\nUsing dynamic depth bias");
            AddNewDXViewportView(ViewportsGrid, rowIndex: 1, columnIndex: 2, boxSize: 100, depthBias: 0.1, useDynamicBiasCheckBox: false, createDXEngine: true,  title: "DXEngine\r\nwithout dynamic depth bias");
            AddNewDXViewportView(ViewportsGrid, rowIndex: 2, columnIndex: 1, boxSize: 100, depthBias: 5.0, useDynamicBiasCheckBox: false, createDXEngine: true,  title: "DXEngine\r\nToo big depth bias\r\n(no dynamic depth bias)");
            AddNewDXViewportView(ViewportsGrid, rowIndex: 2, columnIndex: 2, boxSize: 100, depthBias: 1.0, useDynamicBiasCheckBox: true,  createDXEngine: true,  title: "DXEngine\r\nToo big depth bias\r\nwith dynamic depth bias");
        }

        private void AddNewDXViewportView(Grid parentGrid, int rowIndex, int columnIndex, double boxSize, double depthBias, bool useDynamicBiasCheckBox, bool createDXEngine, string title)
        {
            Border createdBorder;
            MultiLineVisual3D createdLineVisual3D;
            Viewport3D createdViewport3D;
            DXViewportView createdDXViewportView;
            TargetPositionCamera createdTargetPositionCamera;

            AddNewDXViewportView(parentGrid, rowIndex, columnIndex, boxSize, depthBias, useDynamicBiasCheckBox, createDXEngine, title,
                                 out createdBorder, out createdLineVisual3D, out createdViewport3D, out createdDXViewportView, out createdTargetPositionCamera);
        }

        private void AddNewDXViewportView(Grid parentGrid, int rowIndex, int columnIndex, double boxSize, double depthBias, bool useDynamicBiasCheckBox, bool createDXEngine, string title,
                                          out Border createdBorder, out MultiLineVisual3D createdLineVisual3D, out Viewport3D createdViewport3D, out DXViewportView createdDXViewportView, out TargetPositionCamera createdTargetPositionCamera)
        {
            createdBorder = new Border()
            {
                BorderBrush = Brushes.Black,
                BorderThickness = new Thickness(1, 1, 1, 1),
                SnapsToDevicePixels = true
            };


            createdViewport3D = new Viewport3D();

            createdTargetPositionCamera = new TargetPositionCamera()
            {
                Heading = 30,
                Attitude = -20,
                Distance = 200,
                TargetPosition = new Point3D(0, 0, 0),
                ShowCameraLight = ShowCameraLightType.Always,
                TargetViewport3D = createdViewport3D
            };

            var ambientLight = new AmbientLight(Color.FromRgb(40, 40, 40));
            createdViewport3D.Children.Add(ambientLight.CreateModelVisual3D());

            createdLineVisual3D = CreateScene(createdViewport3D, boxSize, depthBias, useDynamicBiasCheckBox, createdTargetPositionCamera);

            if (createDXEngine)
            {
                createdDXViewportView = new DXViewportView(createdViewport3D)
                {
                    SnapsToDevicePixels = true,
                    GraphicsProfiles = DXEngineSettings.Current.GraphicsProfiles // Use graphic profile that is defined in the user settings dialog
                };

                //if (columnIndex == 0)
                //    createdDXViewportView.PresentationType = DXView.PresentationTypes.DirectXOverlay;

                createdBorder.Child = createdDXViewportView;

                _disposables.Add(createdDXViewportView);
            }
            else
            {
                createdBorder.Child = createdViewport3D;
                createdDXViewportView = null;
            }

            Grid.SetRow(createdBorder, rowIndex);
            Grid.SetColumn(createdBorder, columnIndex);

            parentGrid.Children.Add(createdBorder);
            parentGrid.Children.Add(createdTargetPositionCamera);

            if (!string.IsNullOrEmpty(title))
            {
                var textBlock = new TextBlock()
                {
                    Text = title,
                    Foreground = Brushes.Black,
                    FontSize = 12,
                    Margin = new Thickness(10, 5, 5, 5),
                    HorizontalAlignment = HorizontalAlignment.Left,
                    VerticalAlignment = VerticalAlignment.Top,
                    Background = new SolidColorBrush(Color.FromArgb(160, 255, 255, 255))
                };

                Grid.SetRow(textBlock, rowIndex);
                Grid.SetColumn(textBlock, columnIndex);

                parentGrid.Children.Add(textBlock);
            }
        }

        private MultiLineVisual3D CreateScene(Viewport3D parentViewport3D, double boxSize, double depthBias, bool useDynamicBiasCheckBox, TargetPositionCamera targetPositionCamera)
        {
            parentViewport3D.Children.Clear();


            var box1Size = boxSize * 0.2;
            var box2Size = boxSize * 20;

            var boxMesh1 = new Ab3d.Meshes.BoxMesh3D(new Point3D(0, 0, 0), new Size3D(box1Size, box1Size, box1Size), 2, 2, 2).Geometry;
            var boxMesh2 = new Ab3d.Meshes.BoxMesh3D(new Point3D(boxSize * 10, -boxSize * 10, -boxSize * 10), new Size3D(box2Size, box2Size, box1Size), 20, 20, 1).Geometry;

            // Create a single MeshGeometry3D from both box meshes
            var meshGeometry3D = Ab3d.Utilities.MeshUtils.CombineMeshes(boxMesh1, boxMesh2);


            var boxesGeometry = new GeometryModel3D(meshGeometry3D, new DiffuseMaterial(Brushes.SkyBlue));

            parentViewport3D.Children.Add(boxesGeometry.CreateModelVisual3D());


            var sphereLinePositions = WireframeFactory.CreateWireframeLinePositions(meshGeometry3D);

            var multiLineVisual3D = new Ab3d.Visuals.MultiLineVisual3D()
            {
                Positions = sphereLinePositions,
                LineThickness = 0.5,
                LineColor = Colors.Black,
            };

            var createdLineVisual3D = multiLineVisual3D;


            // To specify line depth bias we use SetDXAttribute extension method and use LineDepthBias as DXAttributeType.
            // LineDepthBias attribute can be set to any object derived from BaseLineVisual3D type or to the WireframeVisual3D class.
            // Depth bias is a float value that specifies the depth offset for 3D lines. This can prevent drawing the 3D line and solid object at the same depth (preventing z-fighting).
            //
            // When using DXEngine's objects, you can set the depth bias by setting the DepthBias on the LineMaterial material
            // or DepthBias property on WpfWireframeVisual3DNode
            createdLineVisual3D.SetDXAttribute(DXAttributeType.LineDepthBias, depthBias);

            if (useDynamicBiasCheckBox)
                createdLineVisual3D.SetDXAttribute(DXAttributeType.LineDynamicDepthBiasFactor, 0.02); // 0.02 is the recommended value when using DepthBias 0.1

            parentViewport3D.Children.Add(createdLineVisual3D);


            // Set camera position and orientation
            targetPositionCamera.TargetPosition = new Point3D(0, 0, 0);
            targetPositionCamera.Offset = new Vector3D(0, 0, 0);

            targetPositionCamera.Distance = boxSize * 0.5;

            targetPositionCamera.Heading = 30;
            targetPositionCamera.Attitude = -30;


            return createdLineVisual3D;
        }

        private void DepthBiasComboBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!this.IsLoaded || _shownLineVisual3D == null)
                return;

            UpdateDepthBiasSettings();
        }

        private void OnUseDynamicBiasCheckBoxCheckedChanged(object sender, RoutedEventArgs e)
        {
            if (!this.IsLoaded || _shownLineVisual3D == null)
                return;

            UpdateDepthBiasSettings();
        }

        private void LineThicknessComboBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!this.IsLoaded)
                return;

            _shownLineVisual3D.LineThickness = (double)LineThicknessComboBox.SelectedItem;
        }

        private void UpdateDepthBiasSettings()
        {
            double selectedDepthBias = (double)DepthBiasComboBox.SelectedItem;

            // Set LineDepthBias DXAttribute on WPF object.
            // This will set the DXEngine's LineMaterial.DepthBias property
            
            _shownLineVisual3D.SetDXAttribute(DXAttributeType.LineDepthBias, selectedDepthBias);

            // It would be also possible to set the depth with changing the DepthBias on the WpfWireframeVisual3DNode.
            // This is done with using the SetDepthBias method (see commented method at the end of this file)
            //SetDepthBias(_customDXViewportView, _shownLineVisual3D, selectedDepthBias);


            // Set or clear the LineDynamicDepthBiasFactor to 0.02
            // This will set the DXEngine's LineMaterial.DynamicDepthBiasFactor property

            if (UseDynamicBiasCheckBox.IsChecked ?? false)
                _shownLineVisual3D.SetDXAttribute(DXAttributeType.LineDynamicDepthBiasFactor, 0.02); // 0.02 is the recommended value when using DepthBias 0.1
            else
                _shownLineVisual3D.ClearDXAttribute(DXAttributeType.LineDynamicDepthBiasFactor);
        }

        private void CustomCamera_OnCameraChanged(object sender, CameraChangedRoutedEventArgs e)
        {
            double newCameraDistance = _customCamera.Distance;

            double distanceChangeFactor = newCameraDistance / _previousCameraDistance;
            _previousCameraDistance = newCameraDistance;

            // When custom camera (controller by user) is changed, we also change other cameras
            // But do not change distance (because this is different based on the size of the sphere)
            foreach (var targetPositionCamera in ViewportsGrid.Children.OfType<TargetPositionCamera>())
            {
                if (ReferenceEquals(_customCamera, targetPositionCamera))
                    continue;

                targetPositionCamera.Heading  =  _customCamera.Heading;
                targetPositionCamera.Attitude =  _customCamera.Attitude;
                targetPositionCamera.Distance *= distanceChangeFactor;
            }
        }

        //// This method can be used to change the depth bias value after the Visual3D (line visual or WireframeVisual3D) has already been rendered by DXEngine (or at least the DXEngine has already initialized the 3D SceneNodes).
        //// To set initial line depth bias value, it is much easier to use the the following (see also the CreateScene method above):
        //// _multiLineVisual3D.SetDXAttribute(DXAttributeType.LineDepthBias, depthBias);
        //public static void SetDepthBias(DXViewportView parentDXViewportView, BaseVisual3D lineVisual3D, double depthBiasValue)
        //{
        //    var lineSceneNode = parentDXViewportView.GetSceneNodeForWpfObject(lineVisual3D);

        //    if (lineSceneNode == null)
        //        return;


        //    // First check if we got WpfWireframeVisual3DNode that is created from WireframeVisual3D
        //    // This is handled differently then LineVisual3D objects because WpfWireframeVisual3DNode defines the DepthBias property
        //    var wpfWireframeVisual3DNode = lineSceneNode as WpfWireframeVisual3DNode;
        //    if (wpfWireframeVisual3DNode != null)
        //    {
        //        wpfWireframeVisual3DNode.DepthBias = (float)depthBiasValue;
        //        return;
        //    }


        //    // Handle other 3D lines Visual3D objects:

        //    // To change the DepthBias we need to get to the used LineMaterial
        //    // LineMaterial is used on the ScreenSpaceLineNode (in the DXEngine's scene nodes hierarchy).
        //    // Currently the MultiLineVisual3D is converted into WpfModelVisual3DNode with one ScreenSpaceLineNode set as child.
        //    // But this might be optimized in the future so that WpfModelVisual3DNode would be converted directly into ScreenSpaceLineNode.
        //    // Thefore here we check both options:

        //    var screenSpaceLineNode = lineSceneNode as ScreenSpaceLineNode;

        //    if (screenSpaceLineNode == null && lineSceneNode.ChildNodesCount == 1)
        //        screenSpaceLineNode = lineSceneNode.ChildNodes[0] as ScreenSpaceLineNode;


        //    if (screenSpaceLineNode != null)
        //    {
        //        // Get line material
        //        // The LineMaterial is of type ILineMaterial that does not provide setters for properties
        //        // Therefore we need to cast that into the actual LineMaterial object
        //        var lineMaterial = screenSpaceLineNode.LineMaterial as LineMaterial;

        //        if (lineMaterial != null)
        //        {
        //            lineMaterial.DepthBias = (float)depthBiasValue; // Finally we can set DepthBias

        //            // When we change properties on the DXEngine objects, we need to manually notify the DXEngine about the changes
        //            // We do that with NotifySceneNodeChange method
        //            screenSpaceLineNode.NotifySceneNodeChange(SceneNode.SceneNodeDirtyFlags.MaterialChanged);
        //        }
        //    }
        //}
    }
}
