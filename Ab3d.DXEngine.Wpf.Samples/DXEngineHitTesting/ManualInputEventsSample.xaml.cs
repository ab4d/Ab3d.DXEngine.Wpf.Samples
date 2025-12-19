using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using Ab3d.Controls;
using Ab3d.DirectX.Models;

namespace Ab3d.DXEngine.Wpf.Samples.DXEngineHitTesting
{
    /// <summary>
    /// Interaction logic for ManualInputEventsSample.xaml
    /// </summary>
    public partial class ManualInputEventsSample : Page
    {
        private readonly float _dragMouseDistance = 3;

        // Define the dragging plane
        private readonly Vector3D _dragPlaneNormal = new Vector3D(0, 1, 0);
        private readonly Point3D _dragPlanePoint = new Point3D(0, 0, 0); // position on a plane

        private DiffuseMaterial _normalMaterial   = new DiffuseMaterial(Brushes.Silver);
        private DiffuseMaterial _selectedMaterial = new DiffuseMaterial(Brushes.Yellow);
        private DiffuseMaterial _clickedMaterial  = new DiffuseMaterial(Brushes.Green);
        private DiffuseMaterial _draggedMaterial  = new DiffuseMaterial(Brushes.Orange);
        private DiffuseMaterial _collidedMaterial = new DiffuseMaterial(Brushes.Red);


        private bool _isLeftMouseButtonPressed;
        private Point _pressedMouseLocation;

        private bool _isMouseDragging;
        private bool _isCollided;
        private Point3D _dragStartPosition;
        private Vector3D _startModelOffset;
        private TranslateTransform3D _modelTranslateTransform;

        private GeometryModel3D _lastHitModel;
        private GeometryModel3D _pressedHitModel;

        private Model3DGroup _boxesGroup;

        private HashSet<GeometryModel3D> _clickedModels = new HashSet<GeometryModel3D>();
        
        public ManualInputEventsSample()
        {
            InitializeComponent();

            CreateTestScene();
            
            SubscribeMouseEvents(eventsSourceElement: ViewportBorder);

            CameraControllerInfo1.AddCustomInfoLine(0, MouseCameraController.MouseAndKeyboardConditions.LeftMouseButtonPressed, "Click or select and drag");
        }


        #region Mouse events processing
        private void SubscribeMouseEvents(UIElement eventsSourceElement)
        {
            // NOTE:
            // Currently mouse rotation and movements are assigned to right mouse button (in MouseCameraController that is defined in XAML)
            // If you want to use left mouse button for mouse rotation and selection, then subscribe to PreviewMouseLeftButtonDown and other Preview events (and set args.Handled to true when needed)

            eventsSourceElement.MouseLeftButtonDown += delegate(object sender, MouseButtonEventArgs args)
            {
                // PointerPressed is called not only when the mouse button is pressed, but all the time until the button is pressed
                // But we would only like ot know when the left mouse button is pressed
                if (_isLeftMouseButtonPressed)
                    return;

                var currentPoint = args.GetPosition(eventsSourceElement);
                _isLeftMouseButtonPressed = true;

                if (_isLeftMouseButtonPressed)
                    ProcessMouseButtonPress(currentPoint);
            };

            eventsSourceElement.MouseLeftButtonUp += delegate (object sender, MouseButtonEventArgs args)
            {
                if (!_isLeftMouseButtonPressed) // is already released
                    return;

                var currentPoint = args.GetPosition(eventsSourceElement);
                _isLeftMouseButtonPressed = false;

                if (!_isLeftMouseButtonPressed)
                    ProcessMouseButtonRelease(currentPoint);
            };

            eventsSourceElement.MouseMove += delegate(object sender, MouseEventArgs args)
            {
                var currentPoint = args.GetPosition(eventsSourceElement);
                ProcessMouseMove(currentPoint);
            };
        }

        private void ProcessMouseButtonPress(Point mousePoint)
        {
            ShowMessage("Left mouse button pressed");

            _pressedHitModel = _lastHitModel;
            _pressedMouseLocation = mousePoint;
        }

        private void ProcessMouseButtonRelease(Point mousePoint)
        {
            ShowMessage("Left mouse button released");

            // Mouse click occurs when the mouse was pressed and released on the same object
            if (_pressedHitModel != null && _lastHitModel == _pressedHitModel && !_isMouseDragging)
            {
                ShowMessage($"{_pressedHitModel.GetName()} CLICKED");
                OnModelMouseClick(_pressedHitModel, mousePoint);
            }

            if (_isMouseDragging && _pressedHitModel != null)
            {
                ShowMessage($"{_pressedHitModel.GetName()} DRAGGING ENDED");
                OnModelEndMouseDrag(_pressedHitModel, mousePoint);

                _isMouseDragging = false;
            }

            _pressedHitModel = null;
        }

        private void ProcessMouseMove(Point mousePoint)
        {
            ShowMessage($"Mouse moved to {mousePoint.X:F1} {mousePoint.Y:F1}");

            if (_isMouseDragging)
            {
                if (_pressedHitModel != null)
                    OnModelMouseDrag(_pressedHitModel, mousePoint);

                return;
            }

            if (_isLeftMouseButtonPressed && _pressedHitModel != null)
            {
                var distance = (mousePoint - _pressedMouseLocation).Length;

                if (distance >= _dragMouseDistance && (EnableDragCheckBox.IsChecked ?? false))
                {
                    ShowMessage($"{_pressedHitModel.GetName()} DRAGGING STARTED");
                    OnModelBeginMouseDrag(_pressedHitModel, mousePoint);

                    _isMouseDragging = true;
                    return;
                }
            }

            var hitModel = GetHitModel(mousePoint);

            if (hitModel == _lastHitModel)
                return;

            if (_lastHitModel != null && _lastHitModel.Material == _selectedMaterial) // do not change _clickedMaterial 
            {
                ShowMessage($"{_lastHitModel.GetName()} MOUSE LEAVE");
                OnModelMouseLeave(_lastHitModel, mousePoint);
            }

            if (hitModel != null)
            {
                ShowMessage($"{hitModel.GetName()} MOUSE ENTER");
                OnModelMouseEnter(hitModel, mousePoint);
            }

            _lastHitModel = hitModel;
        }
        #endregion

        #region 3D Model mouse events
        private void OnModelMouseEnter(GeometryModel3D modelNode, Point mousePoint)
        {
            if (modelNode.Material == _normalMaterial) // do not change _clickedMaterial 
                modelNode.Material = _selectedMaterial;

            Mouse.OverrideCursor = Cursors.Hand;
        }

        private void OnModelMouseLeave(GeometryModel3D modelNode, Point mousePoint)
        {
            modelNode.Material = _normalMaterial;

            Mouse.OverrideCursor = null;
        }

        private void OnModelMouseClick(GeometryModel3D modelNode, Point mousePoint)
        {
            if (_clickedModels.Contains(modelNode))
            {
                // Remove model from the clicked models
                modelNode.Material = _selectedMaterial; // mouse is still over the element
                _clickedModels.Remove(modelNode);
            }
            else
            {
                // Add model to clicked models
                modelNode.Material = _clickedMaterial;
                _clickedModels.Add(modelNode);
            }
        }

        private void OnModelBeginMouseDrag(GeometryModel3D modelNode, Point mousePoint)
        {
            modelNode.Material = _draggedMaterial;

            // Gets the mouse position on the XZ plane - this is a start 3D position for dragging
            // This is calculated by getting an intersection of ray created from mouse position and the XZ plane (defined by pointOnPlane: new Point3D(0, 0, 0), planeNormal: new Vector3D(0, 1, 0))
            bool hasIntersection = Camera1.GetMousePositionOnPlane(mousePoint, _dragPlanePoint, _dragPlaneNormal, out _dragStartPosition);

            if (hasIntersection)
            {
                _modelTranslateTransform = modelNode.Transform as TranslateTransform3D;
                if (_modelTranslateTransform == null)
                {
                    _modelTranslateTransform = new TranslateTransform3D();
                    modelNode.Transform = _modelTranslateTransform;
                }

                _startModelOffset = new Vector3D(_modelTranslateTransform.OffsetX, _modelTranslateTransform.OffsetY, _modelTranslateTransform.OffsetZ);
            }
        }

        private void OnModelEndMouseDrag(GeometryModel3D modelNode, Point mousePoint)
        {
            if (_clickedModels.Contains(modelNode))
                modelNode.Material = _clickedMaterial;
            else
                modelNode.Material = _normalMaterial;

            _modelTranslateTransform = null;
        }

        private void OnModelMouseDrag(GeometryModel3D modelNode, Point mousePoint)
        {
            if (!_isMouseDragging || _modelTranslateTransform == null)
                return;


            Point3D currentDragPosition;

            // Gets the mouse position on the XZ plane - this is a start 3D position for dragging
            // This is calculated by getting an intersection of ray created from mouse position and the XZ plane (defined by pointOnPlane: new Point3D(0, 0, 0), planeNormal: new Vector3D(0, 1, 0))
            bool hasIntersection = Camera1.GetMousePositionOnPlane(mousePoint, _dragPlanePoint, _dragPlaneNormal, out currentDragPosition);

            if (hasIntersection)
            {
                var draggedVector = currentDragPosition - _dragStartPosition;

                var savedX = _modelTranslateTransform.OffsetX;
                var savedZ = _modelTranslateTransform.OffsetZ;

                _modelTranslateTransform.OffsetX = _startModelOffset.X + draggedVector.X;
                _modelTranslateTransform.OffsetZ = _startModelOffset.Z + draggedVector.Z;

                if (_pressedHitModel != null)
                {
                    List<GeometryModel3D> collidedModels;

                    if (EnableCollisionCheckBox.IsChecked ?? false)
                        collidedModels = GetCollidedModels(_pressedHitModel, _boxesGroup);
                    else
                        collidedModels = null;

                    if (collidedModels != null)
                    {
                        _modelTranslateTransform.OffsetX = savedX;
                        _modelTranslateTransform.OffsetZ = savedZ;

                        if (!_isCollided)
                        {
                            _pressedHitModel.Material = _collidedMaterial;
                            _isCollided = true;
                        }

                        ShowMessage("COLLIDED with " + string.Join(", ", collidedModels.Select(m => m.GetName())));
                    }
                    else
                    {
                        if (_isCollided)
                        {
                            _pressedHitModel.Material = _draggedMaterial;
                            _isCollided = false;
                        }

                        ShowMessage($"DRAGGED for {draggedVector.X:F1} {draggedVector.Z:F1}");
                    }
                }
            }
        }
        #endregion

        #region Setup methods
        private void CreateTestScene()
        {
            _boxesGroup = new Model3DGroup();

            var modelVisual3D = new ModelVisual3D();
            modelVisual3D.Content = _boxesGroup;

            MainViewport.Children.Add(modelVisual3D);

            // Create 7 x 7 boxes with different height
            for (int y = -3; y <= 3; y++)
            {
                for (int x = -3; x <= 3; x++)
                {
                    // Height is based on the distance from the center
                    double height = (5 - Math.Sqrt(x * x + y * y)) * 60;

                    // Create the 3D Box
                    var boxMesh = new Ab3d.Meshes.BoxMesh3D(centerPosition: new Point3D(x * 100, height / 2, y * 100), size: new Size3D(80, height, 80), xSegments: 1, ySegments: 1, zSegments: 1).Geometry;
                    var boxModel3D = new GeometryModel3D(boxMesh, _normalMaterial);

                    boxModel3D.SetName($"Box_{x + 4}_{y + 4}");

                    _boxesGroup.Children.Add(boxModel3D);
                }
            }
        }

        private void ShowMessage(string message)
        {
            var oldMessages = InfoTextBox.Text;
            if (oldMessages.Length > 2000)
                oldMessages = oldMessages.Substring(0, 2000); // prevent showing very large text

            InfoTextBox.Text = message + Environment.NewLine + oldMessages;
        }
        #endregion

        #region Collision detection
        // Gets models from groupNode that collide with modelNode
        // The method only checks 2D top-down collisions
        private List<GeometryModel3D> GetCollidedModels(GeometryModel3D model3D, Model3DGroup modelGroup)
        {
            // First get bounding box
            // Because all the tested object have the same GroupNode parent, 
            // we can use local BoundingBox. Otherwise WorldBoundingBox should be used.
            var boundingBox = model3D.Bounds;

            if (boundingBox.IsEmpty)
                return null;

            // The collision detection will be done in 2D (top down),
            // so convert 3D bounding box to 2D rect

            var rect1Min = new Point(boundingBox.X, boundingBox.Z);
            var rect1Max = new Point(boundingBox.X + boundingBox.SizeX, boundingBox.Z + boundingBox.SizeZ);


            // Now go through all child nodes in groupNode and check for intersections
            List<GeometryModel3D> collidedModels = null;

            foreach (var childGeometryModel3D in modelGroup.Children.OfType<GeometryModel3D>())
            {
                if (ReferenceEquals(model3D, childGeometryModel3D))
                    continue;

                boundingBox = childGeometryModel3D.Bounds;

                if (boundingBox.IsEmpty)
                    continue;

                var rect2Min = new Point(boundingBox.X, boundingBox.Z);
                var rect2Max = new Point(boundingBox.X + boundingBox.SizeX, boundingBox.Z + boundingBox.SizeZ);

                if (rect2Min.X <= rect1Max.X && rect2Max.X >= rect1Min.X &&
                    rect2Min.Y <= rect1Max.Y && rect2Max.Y >= rect1Min.Y)
                {
                    if (collidedModels == null)
                        collidedModels = new List<GeometryModel3D>();

                    collidedModels.Add(childGeometryModel3D);
                }
            }

            return collidedModels;
        }
        #endregion

        #region GetHitModel
        private GeometryModel3D GetHitModel(Point mousePosition)
        {
            var dxRayHitTestResult = MainDXViewportView.GetClosestHitObject(mousePosition);

            if (dxRayHitTestResult == null)
                return null;

            var wpfGeometryModel3DNode = dxRayHitTestResult.HitSceneNode as WpfGeometryModel3DNode;

            if (wpfGeometryModel3DNode != null)
                return wpfGeometryModel3DNode.GeometryModel3D;

            return null;
        }
        #endregion
    }
}
