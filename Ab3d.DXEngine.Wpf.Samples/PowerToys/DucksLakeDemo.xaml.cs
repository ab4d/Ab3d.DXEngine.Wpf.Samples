using System;
using System.Collections.Generic;
using System.Linq;
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
using Ab3d.Assimp;
using Ab3d.Common;
using Ab3d.Common.Cameras;
using Ab3d.Common.EventManager3D;
using Ab3d.Controls;
using Ab3d.DirectX;
using Ab3d.DirectX.Models;
using Ab3d.Utilities;
using Ab3d.Visuals;

namespace Ab3d.DXEngine.Wpf.Samples.PowerToys
{
    /// <summary>
    /// Interaction logic for DucksLakeDemo.xaml
    /// </summary>
    public partial class DucksLakeDemo : Page
    {
        private const double DuckSize = 0.3; // 30 cm is the height of the duck

        private Model3D _duckModel3D;
        private StandardTransform3D _standardTransform3D;

        private ModelMoverVisual3D _modelMover;
        private ModelRotatorVisual3D _modelRotator;
        private ModelScalarVisual3D _modelScalar;

        private ModelVisual3D _selectedVisual3D;

        private Vector3D _startTranslateVector3D;
        private double _startRotateX, _startRotateY, _startRotateZ;
        private double _startScaleX, _startScaleY, _startScaleZ;

        private ModelVisual3D _mouseDownModelVisual3D;
        private CornerWireBoxVisual3D _cornerWireBoxVisual3D;

        private List<ModelVisual3D> _duckModels;

        private Ab3d.Utilities.EventManager3D _eventManager3D;
        private VarianceShadowRenderingProvider _varianceShadowRenderingProvider;

        public DucksLakeDemo()
        {
            InitializeComponent();

            MouseCameraControllerInfo1.AddCustomInfoLine(0, MouseCameraController.MouseAndKeyboardConditions.LeftMouseButtonPressed, "Select duck");


            _duckModel3D = LoadDuckModel();

            _eventManager3D = new Ab3d.Utilities.EventManager3D(MainViewport);
            _eventManager3D.CustomEventsSourceElement = ViewportBorder;


            MainDXViewportView.DXSceneInitialized += delegate (object sender, EventArgs args)
            {
                if (MainDXViewportView.DXScene == null)
                    return; // Probably WPF 3D rendering

                // Setup shadow rendering
                _varianceShadowRenderingProvider = new VarianceShadowRenderingProvider();

                // Because we have a big green plane, we need to increase the shadow map size (increase this more to get a more detailed shadow).
                _varianceShadowRenderingProvider.ShadowMapSize = 1024; 

                MainDXViewportView.DXScene.InitializeShadowRendering(_varianceShadowRenderingProvider);

                // Specify the light that cases shadow(note that point light is not supported - only DirectionalLight and SpotLight are supported).
                MainSceneLight.SetDXAttribute(DXAttributeType.IsCastingShadow, true);
            };


            this.Focusable =  true; // by default Page is not focusable and therefore does not receive keyDown event
            this.PreviewKeyDown += OnPreviewKeyDown;
            this.Focus();


            // We need to synchronize the Camera in OverlayViewport with the camera in the MainViewport
            Camera1.CameraChanged += delegate (object s, CameraChangedRoutedEventArgs args)
            {
                OverlayViewport.Camera = MainViewport.Camera;
            };

            this.Loaded += delegate(object sender, RoutedEventArgs args)
            {
                GenerateRandomDucks(10);

                ShowModelMover();
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

        private void OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.F1:
                    MoveButton.IsChecked = true;
                    e.Handled = true;
                    break;

                case Key.F2:
                    RotateButton.IsChecked = true;
                    e.Handled = true;
                    break;

                case Key.F3:
                    ScaleButton.IsChecked = true;
                    e.Handled = true;
                    break;
            }
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

        private void GenerateRandomDucks(int ducksCount)
        {
            var rnd = new Random();

            double lakeRadius = LakeCircleVisual3D.Radius;
            Point3D lakeCenter = LakeCircleVisual3D.CenterPosition;

            _duckModels = new List<ModelVisual3D>(ducksCount);
            RootDucksVisual3D.Children.Clear();

            for (int i = 0; i < ducksCount; i++)
            {
                bool findNewPosition;

                do
                {
                    var position = new Point3D(rnd.NextDouble() * lakeRadius * 2 - lakeRadius,
                                               0,
                                               rnd.NextDouble() * lakeRadius * 2 - lakeRadius);

                    // If position is outside of 80% of lake radius
                    double distanceToLakeCenter = (position - lakeCenter).Length;
                    findNewPosition = distanceToLakeCenter > (lakeRadius * 0.8);

                    if (!findNewPosition)
                    {
                        // Now check if too close to any other duck
                        foreach (var duckModel in _duckModels)
                        {
                            var standardTransform3D = StandardTransform3D.GetStandardTransform3D(duckModel);

                            var distanceToDuck = (position - standardTransform3D.GetTranslateVector3D().ToPoint3D()).Length;
                            if (distanceToDuck < DuckSize * 4)
                            {
                                findNewPosition = true;
                                break;
                            }
                        }
                    }

                    if (!findNewPosition)
                    {
                        // The position is ok

                        double scale = rnd.NextDouble() + 1;

                        var standardTransform3D = new StandardTransform3D()
                        {
                            TranslateX = position.X,
                            TranslateY = position.Y,
                            TranslateZ = position.Z,

                            RotateY = rnd.NextDouble() * 360,
                            
                            ScaleX = scale,
                            ScaleY = scale,
                            ScaleZ = scale
                        };
                        
                        var duckVisual3D = new ModelVisual3D()
                        {
                            Content = _duckModel3D,
                        };

                        StandardTransform3D.SetStandardTransform3D(duckVisual3D, standardTransform3D, updateTransform3D: true);


                        _duckModels.Add(duckVisual3D);
                        RootDucksVisual3D.Children.Add(duckVisual3D);


                        var visualEventSource3D = new VisualEventSource3D(duckVisual3D);
                        visualEventSource3D.MouseEnter += OnDuckMouseEnter;
                        visualEventSource3D.MouseLeave += OnDuckMouseLeave;
                        visualEventSource3D.MouseDown += OnDuckMouseDown;
                        visualEventSource3D.MouseUp += OnDuckMouseUp;

                        _eventManager3D.RegisterEventSource3D(visualEventSource3D);
                    }

                } while (findNewPosition);
            }
        }

        #region Duck 3D model mouse event handlers

        private void OnDuckMouseEnter(object sender, Mouse3DEventArgs e)
        {
            var hitVisual3D = e.HitObject as ModelVisual3D;

            if (_duckModels.Contains(hitVisual3D))
                HighlightDuckVisual3D(hitVisual3D);
        }

        private void OnDuckMouseLeave(object sender, Mouse3DEventArgs e)
        {
            ClearDuckHighlight();
        }

        private void OnDuckMouseDown(object sender, MouseButton3DEventArgs e)
        {
            var hitVisual3D = e.HitObject as ModelVisual3D;

            if (_duckModels.Contains(hitVisual3D))
                _mouseDownModelVisual3D = hitVisual3D;
        }

        private void OnDuckMouseUp(object sender, MouseButton3DEventArgs e)
        {
            if (_mouseDownModelVisual3D != null && _mouseDownModelVisual3D == e.HitObject)
            {
                // We have a click (mouse down + mouse up on the same duck Visual3D)
                SelectDuckVisual3D(_mouseDownModelVisual3D);
            }
            else
            {
                // If mouse was released on some other object or if mouse down was not on a duck object (_mouseDownModelVisual3D == null)
                ClearSelectedDuckVisual3D();
            }

            _mouseDownModelVisual3D = null;
        }

        #endregion

        #region Selection and highlighting

        private void HighlightDuckVisual3D(ModelVisual3D duckModelVisual3D)
        {
            if (_cornerWireBoxVisual3D != null)
                ClearDuckHighlight();

            var duckBounds = duckModelVisual3D.Content.Bounds;
            duckBounds = duckModelVisual3D.Transform.TransformBounds(duckBounds);

            _cornerWireBoxVisual3D = new CornerWireBoxVisual3D()
            {
                CenterPosition = duckBounds.GetCenterPosition(),
                Size           = duckBounds.Size,
                LineColor      = Colors.DimGray,
                LineThickness  = 1,
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

        private void SelectDuckVisual3D(ModelVisual3D duckModelVisual3D)
        {
            _selectedVisual3D = duckModelVisual3D;

            _standardTransform3D = StandardTransform3D.GetStandardTransform3D(duckModelVisual3D);
            TransformEditor.StandardTransform3D = _standardTransform3D;

            if (MoveButton.IsChecked ?? false)
                ShowModelMover();
            else if (RotateButton.IsChecked ?? false)
                ShowModelRotator();
            else if (ScaleButton.IsChecked ?? false)
                ShowModelScalar();
            else
                MoveButton.IsChecked = true; // Show model mover
        }

        private void ClearSelectedDuckVisual3D()
        {
            _selectedVisual3D = null;
            _standardTransform3D = null;

            TransformEditor.StandardTransform3D = null;

            HideModelMover();
        }

        private void ShowWireframeDuck()
        {
            _standardTransform3D = new StandardTransform3D();

            WireframeDuckVisual3D.OriginalModel = _duckModel3D.Clone();
            WireframeDuckVisual3D.IsVisible = true;

            StandardTransform3D.SetStandardTransform3D(WireframeDuckVisual3D, _standardTransform3D, updateTransform3D: true);

            TransformEditor.StandardTransform3D = _standardTransform3D;

            _selectedVisual3D = WireframeDuckVisual3D;
        }

        private void HideWireframeDuck()
        {
            WireframeDuckVisual3D.IsVisible = false;
        }

        #endregion

        #region ModelMover, Rotator and Scalar

        private void ShowModelMover()
        {
            HideModelRotator();
            HideModelScalar();

            if (_selectedVisual3D == null || _standardTransform3D == null)
                return;


            EnsureModelMover();

            _modelMover.Position = _standardTransform3D.GetTranslateVector3D().ToPoint3D();

            // If the 3D scene would not be shown yet, we would need to refresh the camera
            //Camera1.Refresh();

            // We need to set the size of the model mover's axes to an appropriate size so that it will look big enough
            // regardless of how far away the selected model is.
            // This can be done with calculating how big a 80 x 80 rectangle (in screen coordinates) would be in 3D space at the specified distance:
            double lookDirectionDistance = (_modelMover.Position - Camera1.GetCameraPosition()).Length;
            var worldSize = Ab3d.Utilities.CameraUtils.GetPerspectiveWorldSize(new Size(80, 80), lookDirectionDistance, Camera1.FieldOfView, new Size(MainViewport.ActualWidth, MainViewport.ActualHeight));

            _modelMover.AxisLength      = (worldSize.Width + worldSize.Height) / 2; // average the width and height
            _modelMover.AxisRadius      = _modelMover.AxisLength * 0.03;            // axis radius is 3% of its length
            _modelMover.AxisArrowRadius = _modelMover.AxisRadius * 3;               // arrow radius is 3 times the axis radius


            if (!OverlayViewport.Children.Contains(_modelMover))
                OverlayViewport.Children.Add(_modelMover);
        }

        private void HideModelMover()
        {
            if (_modelMover != null)
            {
                OverlayViewport.Children.Remove(_modelMover);
                _modelMover = null;
            }

            MoveButton.IsChecked = false;
        }

        private void EnsureModelMover()
        {
            if (_modelMover != null)
                return;


            _modelMover = new ModelMoverVisual3D();

            _modelMover.IsYAxisShown = false; // Prevent moving the duck up and down


            // Setup event handlers on ModelMoverVisual3D
            _modelMover.ModelMoveStarted += delegate (object o, EventArgs eventArgs)
            {
                if (_selectedVisual3D == null || _standardTransform3D == null)
                    return;

                //_standardTransform3D = StandardTransform3D.GetStandardTransform3D(_selectedVisual3D);

                _startTranslateVector3D = _standardTransform3D.GetTranslateVector3D();
            };

            _modelMover.ModelMoved += delegate (object o, Ab3d.Common.ModelMovedEventArgs e)
            {
                if (_selectedVisual3D == null || _standardTransform3D == null)
                    return;

                var newCenterPosition = _startTranslateVector3D + e.MoveVector3D;

                
                // If position is outside of 90% of lake radius, then do not make the move
                double distanceToLakeCenter = (newCenterPosition.ToPoint3D() - LakeCircleVisual3D.CenterPosition).Length;
                if (distanceToLakeCenter > (LakeCircleVisual3D.Radius * 0.90))
                {
                     // Move outside the lake
                    return;
                }

                _standardTransform3D.TranslateX = newCenterPosition.X;
                _standardTransform3D.TranslateY = newCenterPosition.Y;
                _standardTransform3D.TranslateZ = newCenterPosition.Z;

                _modelMover.Position = newCenterPosition.ToPoint3D();
            };

            // Nothing to do in ModelMoveEnded
            //_modelMover.ModelMoveEnded += delegate (object sender, EventArgs args)
            //{

            //};
        }


        private void ShowModelRotator()
        {
            HideModelMover();
            HideModelScalar();

            if (_selectedVisual3D == null || _standardTransform3D == null)
                return;


            EnsureModelRotator();

            _modelRotator.Position = _standardTransform3D.GetTranslateVector3D().ToPoint3D();

            // If the 3D scene would not be shown yet, we would need to refresh the camera
            //Camera1.Refresh();

            // We need to set the size of the model mover's axes to an appropriate size so that it will look big enough
            // regardless of how far away the selected model is.
            // This can be done with calculating how big a 80 x 80 rectangle (in screen coordinates) would be in 3D space at the specified distance:
            double lookDirectionDistance = (_modelRotator.Position - Camera1.GetCameraPosition()).Length;
            var    worldSize             = Ab3d.Utilities.CameraUtils.GetPerspectiveWorldSize(new Size(80, 80), lookDirectionDistance, Camera1.FieldOfView, new Size(MainViewport.ActualWidth, MainViewport.ActualHeight));


            _modelRotator.InnerRadius = ((worldSize.Width + worldSize.Height) / 2) * 0.8; // average the width and height
            _modelRotator.OuterRadius = _modelRotator.InnerRadius * 1.2;

            if (!OverlayViewport.Children.Contains(_modelRotator))
                OverlayViewport.Children.Add(_modelRotator);
        }

        private void HideModelRotator()
        {
            if (_modelRotator != null)
            {
                OverlayViewport.Children.Remove(_modelRotator);
                _modelRotator = null;
            }

            RotateButton.IsChecked = false;
        }

        private void EnsureModelRotator()
        {
            if (_modelRotator != null)
                return;


            _modelRotator = new ModelRotatorVisual3D();

            // Setup events on ModelRotatorVisual3D
            _modelRotator.ModelRotateStarted += delegate (object sender, ModelRotatedEventArgs args)
            {
                if (_selectedVisual3D == null || _standardTransform3D == null)
                    return;

                _startRotateX = _standardTransform3D.RotateX;
                _startRotateY = _standardTransform3D.RotateY;
                _startRotateZ = _standardTransform3D.RotateZ;
            };

            _modelRotator.ModelRotated += delegate (object sender, ModelRotatedEventArgs args)
            {
                if (_selectedVisual3D == null || _standardTransform3D == null)
                    return;

                if (args.RotationAxis == ModelRotatorVisual3D.XRotationAxis)
                    _standardTransform3D.RotateX = _startRotateX + args.RotationAngle;

                else if (args.RotationAxis == ModelRotatorVisual3D.YRotationAxis)
                    _standardTransform3D.RotateY = _startRotateY + args.RotationAngle;

                else if (args.RotationAxis == ModelRotatorVisual3D.ZRotationAxis)
                    _standardTransform3D.RotateZ = _startRotateZ + args.RotationAngle;
            };

            _modelRotator.ModelRotateEnded += delegate (object sender, ModelRotatedEventArgs args)
            {
                // Nothing to do here in this sample
                // The event handler is here only for description purposes
            };
        }


        private void ShowModelScalar()
        {
            HideModelMover();
            HideModelRotator();

            if (_selectedVisual3D == null || _standardTransform3D == null)
                return;


            EnsureModelScalar();

            _modelScalar.Position = _standardTransform3D.GetTranslateVector3D().ToPoint3D();

            // If the 3D scene would not be shown yet, we would need to refresh the camera
            //Camera1.Refresh();

            // We need to set the size of the model mover's axes to an appropriate size so that it will look big enough
            // regardless of how far away the selected model is.
            // This can be done with calculating how big a 80 x 80 rectangle (in screen coordinates) would be in 3D space at the specified distance:
            double lookDirectionDistance = (_modelScalar.Position - Camera1.GetCameraPosition()).Length;
            var worldSize = Ab3d.Utilities.CameraUtils.GetPerspectiveWorldSize(new Size(80, 80), lookDirectionDistance, Camera1.FieldOfView, new Size(MainViewport.ActualWidth, MainViewport.ActualHeight));


            _modelScalar.AxisLength = (worldSize.Width + worldSize.Height) / 2; // average the width and height
            _modelScalar.InnerBoxWidth = _modelScalar.AxisLength * 0.06;
            _modelScalar.OuterBoxWidth = _modelScalar.InnerBoxWidth * 3;
            _modelScalar.CenterBoxWidth = _modelScalar.InnerBoxWidth * 5;

            if (!OverlayViewport.Children.Contains(_modelScalar))
                OverlayViewport.Children.Add(_modelScalar);
        }

        private void HideModelScalar()
        {
            if (_modelScalar != null)
            {
                OverlayViewport.Children.Remove(_modelScalar);
                _modelScalar = null;
            }

            ScaleButton.IsChecked = false;
        }

        private void EnsureModelScalar()
        {
            if (_modelScalar != null)
                return;


            _modelScalar = new ModelScalarVisual3D();

            // Setup events on ModelScalarVisual3D
            _modelScalar.ModelScaleStarted += delegate (object sender, EventArgs args)
            {
                if (_selectedVisual3D == null || _standardTransform3D == null)
                    return;

                _startScaleX = _standardTransform3D.ScaleX;
                _startScaleY = _standardTransform3D.ScaleY;
                _startScaleZ = _standardTransform3D.ScaleZ;
            };

            _modelScalar.ModelScaled += delegate (object sender, ModelScaledEventArgs args)
            {
                if (_selectedVisual3D == null || _standardTransform3D == null)
                    return;

                _standardTransform3D.ScaleX = _startScaleX * args.ScaleX;
                _standardTransform3D.ScaleY = _startScaleY * args.ScaleY;
                _standardTransform3D.ScaleZ = _startScaleZ * args.ScaleZ;
            };

            _modelScalar.ModelScaleEnded += delegate (object sender, EventArgs args)
            {
                // Nothing to do here in this sample
                // The event handler is here only for description purposes
            };
        }

        #endregion

        #region ToggleButton event handler

        private void TransformEditor_OnChanged(object sender, EventArgs e)
        {
            if (_modelMover != null)
            {
                _modelMover.Position = _standardTransform3D.GetTranslateVector3D().ToPoint3D();
            }
        }

        private void MoveButton_OnChecked(object sender, RoutedEventArgs e)
        {
            if (!this.IsLoaded)
                return;

            ShowModelMover();
        }

        private void RotateButton_OnChecked(object sender, RoutedEventArgs e)
        {
            if (!this.IsLoaded)
                return;

            ShowModelRotator();
        }

        private void ScaleButton_OnChecked(object sender, RoutedEventArgs e)
        {
            if (!this.IsLoaded)
                return;

            ShowModelScalar();
        }

        private void MoveButton_OnUnchecked(object sender, RoutedEventArgs e)
        {
            if (!this.IsLoaded)
                return;

            HideModelMover();
        }

        private void RotateButton_OnUnchecked(object sender, RoutedEventArgs e)
        {
            if (!this.IsLoaded)
                return;

            HideModelRotator();
        }

        private void ScaleButton_OnUnchecked(object sender, RoutedEventArgs e)
        {
            if (!this.IsLoaded)
                return;

            HideModelScalar();
        }

        #endregion
    }
}
