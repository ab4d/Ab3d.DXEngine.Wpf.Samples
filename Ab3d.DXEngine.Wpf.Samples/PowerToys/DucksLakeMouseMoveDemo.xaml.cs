using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using Ab3d.Assimp;
using Ab3d.Common;
using Ab3d.Common.EventManager3D;
using Ab3d.Controls;
using Ab3d.DirectX;
using Ab3d.Utilities;
using Ab3d.Visuals;

namespace Ab3d.DXEngine.Wpf.Samples.PowerToys
{
    /// <summary>
    /// Interaction logic for DucksLakeMouseMoveDemo.xaml
    /// </summary>
    public partial class DucksLakeMouseMoveDemo : Page
    {
        private const double DuckSize = 0.3; // 30 cm is the height of the duck

        private ModelVisual3D _movedDuckVisual3D;
        private CornerWireBoxVisual3D _cornerWireBoxVisual3D;

        private List<ModelVisual3D> _duckModels;

        private Ab3d.Utilities.EventManager3D _eventManager3D;

        private Ab3d.Utilities.Plane _movementPlane;
        private Point3D _startMousePlaneIntersection;
        private Point3D _startDuckPosition;

        private VarianceShadowRenderingProvider _varianceShadowRenderingProvider;

        public DucksLakeMouseMoveDemo()
        {
            InitializeComponent();

            // Define a 3D plane where we will move the 3D objects (used for git testing of a ray from a mouse to the plane).
            // Here we define the plane by its normal (vector perpendicular to the plane) and a position on a plane.
            // Plane can be also defined by 3 positions or normal and a d value (x, y, z, d).
            _movementPlane = new Ab3d.Utilities.Plane(normal: new Vector3D(0, 1, 0), positionOnPlane: new Point3D(0, 0, 0));

            _eventManager3D = new Ab3d.Utilities.EventManager3D(MainViewport);
            _eventManager3D.CustomEventsSourceElement = ViewportBorder;

            ViewportBorder.MouseMove += ViewportBorderOnMouseMove;

            MouseCameraControllerInfo1.AddCustomInfoLine(0, MouseCameraController.MouseAndKeyboardConditions.LeftMouseButtonPressed, "Move selected duck");

            this.Loaded += delegate(object sender, RoutedEventArgs args)
            {
                var duckModel3D = LoadDuckModel();
                GenerateRandomDucks(10, duckModel3D);
            };

            MainDXViewportView.DXSceneInitialized += delegate (object sender, EventArgs args)
            {
                if (MainDXViewportView.DXScene == null)
                    return; // Probably WPF 3D rendering

                if (MainDXViewportView.DXScene.ShaderQuality == ShaderQuality.Low)
                    LowQualityInfoTextBlock.Visibility = Visibility.Visible; // Show info that shadow rendering is not supported with low quality rendering

                // Setup shadow rendering
                _varianceShadowRenderingProvider = new VarianceShadowRenderingProvider();

                // Because we have a big green plane, we need to increase the shadow map size (increase this more to get a more detailed shadow).
                _varianceShadowRenderingProvider.ShadowMapSize = 1024; 

                MainDXViewportView.DXScene.InitializeShadowRendering(_varianceShadowRenderingProvider);

                // Specify the light that cases shadow(note that point light is not supported - only DirectionalLight and SpotLight are supported).
                MainSceneLight.SetDXAttribute(DXAttributeType.IsCastingShadow, true);
            };

            // IMPORTANT:
            // It is very important to call Dispose method on DXSceneView after the control is not used any more (see help file for more info)
            this.Unloaded += delegate(object sender, RoutedEventArgs args)
            {
                if (_varianceShadowRenderingProvider != null)
                    _varianceShadowRenderingProvider.Dispose();

                MainDXViewportView.Dispose();
            };
        }

        private Model3D LoadDuckModel()
        {
            string fileName = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"Resources\Models\duck.dae");

            var assimpWpfImporter = new AssimpWpfImporter();
            var readModel3D = assimpWpfImporter.ReadModel3D(fileName, texturesPath: null); // we can also define a textures path if the textures are located in some other directory (this is parameter can be skipped, but is defined here so you will know that you can use it)

            // Move the model so that it has its bottom center position at (0,0,0) and
            // scale it so that its height is set to DuckSize (0.3m) - multiply xSize and zSize by 10 so that only y size will limit the final size (we preserve the aspect ratio)
            Ab3d.Utilities.ModelUtils.PositionAndScaleModel3D(readModel3D, new Point3D(0, 0, 0), PositionTypes.Bottom, new Size3D(DuckSize * 10, DuckSize, DuckSize * 10), preserveAspectRatio: true, preserveCurrentTransformation: false);

            return readModel3D;
        }

        private void GenerateRandomDucks(int ducksCount, Model3D duckModel3D)
        {
            var rnd = new Random();

            double lakeRadius = LakeCircleVisual3D.Radius;

            _duckModels = new List<ModelVisual3D>(ducksCount);
            RootDucksVisual3D.Children.Clear();

            for (int i = 0; i < ducksCount; i++)
            {
                double scale = rnd.NextDouble() + 1;

                var standardTransform3D = new StandardTransform3D()
                {
                    RotateY = rnd.NextDouble() * 360,
                            
                    ScaleX = scale,
                    ScaleY = scale,
                    ScaleZ = scale
                };
                        
                var duckVisual3D = new ModelVisual3D()
                {
                    Content = duckModel3D,
                };

                StandardTransform3D.SetStandardTransform3D(duckVisual3D, standardTransform3D, updateTransform3D: true);


                bool isCorrectPosition = false;
                while (!isCorrectPosition)
                {
                    var position = new Point3D(rnd.NextDouble() * lakeRadius * 2 - lakeRadius,
                                               0,
                                               rnd.NextDouble() * lakeRadius * 2 - lakeRadius);

                    // If position is outside of 80% of lake radius
                    isCorrectPosition = CheckIsPositionInsideLake(position);

                    if (isCorrectPosition)
                    {
                        standardTransform3D.TranslateX = position.X;
                        standardTransform3D.TranslateY = position.Y;
                        standardTransform3D.TranslateZ = position.Z;

                        // Now check if too close to any other duck
                        isCorrectPosition = !CheckDuckCollision(duckVisual3D);
                    }
                }

                _duckModels.Add(duckVisual3D);
                RootDucksVisual3D.Children.Add(duckVisual3D);


                var visualEventSource3D = new VisualEventSource3D(duckVisual3D);
                visualEventSource3D.MouseEnter += OnDuckMouseEnter;
                visualEventSource3D.MouseLeave += OnDuckMouseLeave;
                visualEventSource3D.MouseDown += OnDuckMouseDown;
                visualEventSource3D.MouseUp += OnDuckMouseUp;

                _eventManager3D.RegisterEventSource3D(visualEventSource3D);
            }
        }

        private void OnDuckMouseEnter(object sender, Mouse3DEventArgs e)
        {
            if (_movedDuckVisual3D != null)
                return; // Do not select another duck model as long as one duck is selected

            var hitVisual3D = e.HitObject as ModelVisual3D;

            if (_duckModels.Contains(hitVisual3D))
                HighlightDuckVisual3D(hitVisual3D);
        }

        private void OnDuckMouseLeave(object sender, Mouse3DEventArgs e)
        {
            if (_movedDuckVisual3D != null)
                return; // Do not clear selection as long as one duck is selected

            ClearDuckHighlight();
        }

        private void OnDuckMouseDown(object sender, MouseButton3DEventArgs e)
        {
            if (e.MouseData.LeftButton != MouseButtonState.Pressed) // if some other button is clicked (not left)
                return; 

            var hitVisual3D = e.HitObject as ModelVisual3D;

            // Get intersection of ray created from mouse position and the current plane
            Point3D intersectionPoint;
            bool hasIntersection = Camera1.GetMousePositionOnPlane(e.CurrentMousePosition, _movementPlane.GetPointOnPlane(), _movementPlane.Normal, out intersectionPoint);

            if (_duckModels.Contains(hitVisual3D) && hasIntersection)
            {
                _movedDuckVisual3D = hitVisual3D;

                _startMousePlaneIntersection = intersectionPoint;

                var standardTransform3D = StandardTransform3D.GetStandardTransform3D(_movedDuckVisual3D);
                _startDuckPosition = standardTransform3D.GetTranslateVector3D().ToPoint3D();

                if (_cornerWireBoxVisual3D == null)
                    HighlightDuckVisual3D(_movedDuckVisual3D);

                if (_cornerWireBoxVisual3D != null)
                    _cornerWireBoxVisual3D.LineThickness = 2; // Increase LineThickness on mouse down
            }
        }

        private void OnDuckMouseUp(object sender, MouseButton3DEventArgs e)
        {
            if (_movedDuckVisual3D != null)
            {
                ClearDuckHighlight();
                _movedDuckVisual3D = null;
            }
        }
        
        private void ViewportBorderOnMouseMove(object sender, MouseEventArgs e)
        {
            if (_movedDuckVisual3D == null)
                return; // No duck selected

            if (e.LeftButton != MouseButtonState.Pressed) 
            {
                // if left button was released
                // Because in this sample we do not capture the mouse, the button release can happen
                // outside of this control and we do not get the OnDuckMouseUp event.

                // Stop mouse move
                ClearDuckHighlight();
                _movedDuckVisual3D = null;
                return;
            } 

            var mousePosition = e.GetPosition(ViewportBorder);

            // Get intersection of ray created from mouse position and the current plane
            Point3D intersectionPoint;
            bool hasIntersection = Camera1.GetMousePositionOnPlane(mousePosition, _movementPlane.GetPointOnPlane(), _movementPlane.Normal, out intersectionPoint);

            if (hasIntersection)
            {
                var movementVector = intersectionPoint - _startMousePlaneIntersection;

                var standardTransform3D = StandardTransform3D.GetStandardTransform3D(_movedDuckVisual3D);

                var newDuckPosition = _startDuckPosition + movementVector;

                // If position is outside of 80% of lake radius
                var isCorrectPosition = CheckIsPositionInsideLake(newDuckPosition);

                if (isCorrectPosition)
                {
                    var savedTranslateVector = standardTransform3D.GetTranslateVector3D();

                    standardTransform3D.TranslateX = newDuckPosition.X;
                    standardTransform3D.TranslateY = newDuckPosition.Y;
                    standardTransform3D.TranslateZ = newDuckPosition.Z;

                    // Now check if too close to any other duck
                    isCorrectPosition = !CheckDuckCollision(_movedDuckVisual3D);

                    if (!isCorrectPosition)
                    {
                        // Revert translation
                        standardTransform3D.TranslateX = savedTranslateVector.X;
                        standardTransform3D.TranslateY = savedTranslateVector.Y;
                        standardTransform3D.TranslateZ = savedTranslateVector.Z;
                    }
                }

                if (_cornerWireBoxVisual3D != null)
                    _cornerWireBoxVisual3D.LineColor =isCorrectPosition ? Colors.DimGray : Colors.Red;
            }
        }

        private void HighlightDuckVisual3D(ModelVisual3D duckModelVisual3D)
        {
            if (_cornerWireBoxVisual3D != null)
                ClearDuckHighlight();

            var duckBounds = duckModelVisual3D.Content.Bounds;
            //duckBounds = duckModelVisual3D.Transform.TransformBounds(duckBounds);

            _cornerWireBoxVisual3D = new CornerWireBoxVisual3D()
            {
                CenterPosition = duckBounds.GetCenterPosition(),
                Size           = duckBounds.Size,
                LineColor      = Colors.DimGray,
                LineThickness  = 1,
                Transform      = duckModelVisual3D.Transform
            };

            MainViewport.Children.Add(_cornerWireBoxVisual3D);
        }

        private void ClearDuckHighlight()
        {
            if (_cornerWireBoxVisual3D != null)
            {
                MainViewport.Children.Remove(_cornerWireBoxVisual3D);
                _cornerWireBoxVisual3D = null;
            }
        }

        private void UpdateDuckHighlightPosition(ModelVisual3D duckModelVisual3D)
        {
            if (_cornerWireBoxVisual3D != null)
            {
                var duckBounds = GetTransformedDuckBounds(duckModelVisual3D);
                _cornerWireBoxVisual3D.CenterPosition = duckBounds.GetCenterPosition();
            }
        }

        private Rect3D GetTransformedDuckBounds(ModelVisual3D duckVisual3D)
        {
            // Because we know that duck's ModelVisual3D has only one GeometryModel3D set to its Content,
            // we can get the bounds by getting the Content.Bounds.
            //
            // A more genetic way to get bounds of a Visual3D is to use ModelUtils.GetBounds:
            //var duckBounds = Ab3d.Utilities.ModelUtils.GetBounds(duckVisual3D);

            var duckBounds = duckVisual3D.Content.Bounds;

            duckBounds = duckVisual3D.Transform.TransformBounds(duckBounds);

            return duckBounds;
        }

        // True if position is inside 80% of the lake's radius
        private bool CheckIsPositionInsideLake(Point3D position)
        {
            double lakeRadius = LakeCircleVisual3D.Radius;
            Point3D lakeCenter = LakeCircleVisual3D.CenterPosition;

            double distanceToLakeCenter = (position - lakeCenter).Length;
            return distanceToLakeCenter < (lakeRadius * 0.8);
        }

        // True if bounding box of duckVisual3D intersects with any other duck model's bounding box
        private bool CheckDuckCollision(ModelVisual3D duckVisual3D)
        {
            var duckBounds = GetTransformedDuckBounds(duckVisual3D);

            foreach (var duckModel in _duckModels)
            {
                if (ReferenceEquals(duckModel, duckVisual3D))
                    continue; // prevent collision with itself

                var otherDuckBounds = GetTransformedDuckBounds(duckModel);

                if (!Rect3D.Intersect(duckBounds, otherDuckBounds).IsEmpty)
                    return true;
            }

            return false;
        }
    }
}
