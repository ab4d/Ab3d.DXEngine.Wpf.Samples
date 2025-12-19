using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using Ab3d.Assimp;
using Ab3d.Common.Cameras;
using Ab3d.DirectX;
using Ab3d.DirectX.Materials;
using Ab3d.Visuals;

#if SHARPDX
using SharpDX;
#endif

namespace Ab3d.DXEngine.Wpf.Samples.DXEngineVisuals
{
    /// <summary>
    /// Interaction logic for PlanarShadowsCustomization.xaml
    /// </summary>
    public partial class PlanarShadowsCustomization : Page
    {
        private PointLight _shadowPointLight;
        private DirectionalLight _shadowDirectionalLight;
        private AmbientLight _ambientLight;

        private Light _currentShadowLight;

        private Model3D _loadedModel3D;
        
        private double _lightVerticalAngle;
        private double _lightHorizontalAngle;
        private double _lightDistance;

        private AxisAngleRotation3D _originalModelAxisAngleRotation3D;
        private AxisAngleRotation3D _simplifiedModelAxisAngleRotation3D;

        private RenderingQueue _simplifiedShadowObjectsRenderingQueue;
        private PlanarShadowRenderingProvider _planarShadowRenderingProvider;
        private ModelVisual3D _simplifiedVisual3D;

        private DisposeList _disposables;
        private GeometryModel3D _teapotGeometryModel3D;
        private StandardMaterial _shadowPlaneMaterial;
        private StandardMaterial _shadowPlaneBackMaterial;
        private WireGridVisual3D _wireGridVisual3D;

        public PlanarShadowsCustomization()
        {
            InitializeComponent();

            _disposables = new DisposeList();

            MainDXViewportView.DXSceneInitialized += delegate(object sender, EventArgs args)
            {
                if (MainDXViewportView.DXScene != null)
                {
                    // Load texture file into ShaderResourceView (in our case we load dds file; but we could also load png file)
                    string textureFileName = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"Resources/GrassTexture.jpg");


                    // The easiest way to load image file and in the same time create a material with the loaded texture is to use the CreateStandardTextureMaterial method.
                    _shadowPlaneMaterial = Ab3d.DirectX.TextureLoader.CreateStandardTextureMaterial(MainDXViewportView.DXScene.DXDevice, textureFileName);

                    // We need to manually dispose the created StandardMaterial and ShaderResourceView
                    _disposables.Add(_shadowPlaneMaterial);
                    _disposables.Add(_shadowPlaneMaterial.DiffuseTextures[0]);


                    // If we want more control over the material creation process, we can use the following code:

                    //// To load a texture from file, you can use the TextureLoader.LoadShaderResourceView (this supports loading standard image files and also loading dds files).
                    //// This method returns a ShaderResourceView and it can also set a textureInfo parameter that defines some of the properties of the loaded texture (bitmap size, dpi, format, hasTransparency).
                    //TextureInfo textureInfo;
                    //var loadedShaderResourceView = Ab3d.DirectX.TextureLoader.LoadShaderResourceView(MainDXViewportView.DXScene.Device,
                    //                                                                                 textureFileName,
                    //                                                                                 out textureInfo);

                    //_disposables.Add(loadedShaderResourceView);

                    //// Define DXEngine's materials for shadow plane
                    //_shadowPlaneMaterial = new StandardMaterial()
                    //{
                    //    DiffuseColor    = Color3.White, // When DiffuseTextures are set, then DiffuseColor is used as a color filter (White means no filter)
                    //    DiffuseTextures = new ShaderResourceView[] {loadedShaderResourceView},
                    //    TextureBlendState = recommendedBlendState,
                    //    HasTransparency = hasTransparency
                    //};

                    _shadowPlaneBackMaterial = new StandardMaterial()
                    {
                        DiffuseColor = Colors.DimGray.ToColor3()
                    };

                    _disposables.Add(_shadowPlaneMaterial);
                    _disposables.Add(_shadowPlaneBackMaterial);


                    // Define the PlanarShadowRenderingProvider
                    _planarShadowRenderingProvider = new PlanarShadowRenderingProvider()
                    {
                        // We need to provide information about the position of the plane in 3D space
                        ShadowPlaneCenterPosition = new Vector3(0, 0, 0),
                        ShadowPlaneSize            = new Vector2(400, 400),
                        ShadowPlaneNormalVector    = new Vector3(0, 1, 0),
                        ShadowPlaneHeightDirection = new Vector3(0, 0, -1),

                        // In case ShadowPlaneMaterial and/or ShadowPlaneBackMaterial are defined
                        // the PlanarShadowRenderingProvider will also render the 3D plane.
                        ShadowPlaneMaterial = _shadowPlaneMaterial,
                        ShadowPlaneBackMaterial = _shadowPlaneBackMaterial,

                        // Set shadow properties
                        ShadowColor = Color3.Black,
                        ShadowTransparency = (float)ShadowTransparencySlider.Value / 100.0f, // default value is 0.65f

                        // Because shadow is rendered as standard 3D object, we need to offset it from the shadow plane
                        // to prevent z-fighting problems that occur when two 3D objects are rendered to the same 3D position.
                        // This value need to be very small so that it is not seen that the shadow is above the plane.
                        // Default value is 0.01f.
                        ShadowOffsetFromPlane = 0.01f,

                        // When using PlanarShadowRenderingProvider we do not need PlanarShadowMeshCreator from Ab3d.PowerToys
                        // to prepare a special MeshGeometry3D for us. Also PlanarShadowMeshCreator does not need to manually (on the CPU)
                        // cut the shadow to the plane bounds but this can be done with using hardware accelerated algorithm (using stencil buffer).
                        // But if we still want to use PlanarShadowMeshCreator we can set the following two properties to false 
                        // (for example if we wanted to use PlanarShadowRenderingProvider just to provide proper transparent shadows).
                        ApplyShadowMatrix = true, 
                        CutShadowToPlaneBounds = true,

                        IsCheckingIsCastingShadow = false, // Initially do not check for IsCastingShadow values (this is also a default value). See comments in LoadModel for more info.

                        //CustomShadowLight = new Ab3d.DirectX.Lights.DirectionalLight(new Vector3(0, -1, 1))
                        //CustomShadowLight = new Ab3d.DirectX.Lights.PointLight(new Vector3(0, 500, 0), 300)
                    };

                    _disposables.Add(_planarShadowRenderingProvider);


                    MainDXViewportView.DXScene.InitializeShadowRendering(_planarShadowRenderingProvider);
                }


                _lightHorizontalAngle = 30;
                _lightVerticalAngle = 27;
                _lightDistance = 500;

                _ambientLight = new AmbientLight(System.Windows.Media.Color.FromRgb(40, 40, 40));

                _shadowPointLight = new PointLight();
                _shadowDirectionalLight = new DirectionalLight();

                Camera1.ShowCameraLight = ShowCameraLightType.Never; // prevent adding camera's light

                SetShadowLight(isDirectionalLight: true);

                UpdateLights();

                _loadedModel3D = LoadModel3D();
                MainViewport.Children.Add(_loadedModel3D.CreateModelVisual3D());
            };



            this.PreviewKeyDown += OnPreviewKeyDown;

            // This will allow receiving keyboard events
            this.Focusable = true;
            this.Focus();

            this.Unloaded += delegate(object sender, RoutedEventArgs args)
            {
                _disposables.Dispose();
                MainDXViewportView.Dispose();
            };
        }
        
        private Model3D LoadModel3D()
        {
            AssimpLoader.LoadAssimpNativeLibrary();

            string fileName = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"Resources\Models\robotarm-upper-part.3ds");

            var assimpWpfImporter = new AssimpWpfImporter();
            var robotModel3D = assimpWpfImporter.ReadModel3D(fileName);

            // To get object names execute the following in Visual Studio Immediate window:
            // robotModel3D.DumpHierarchy();

            var hand2Group = assimpWpfImporter.NamedObjects["Hand2"] as Model3DGroup;
            _originalModelAxisAngleRotation3D = new AxisAngleRotation3D(new Vector3D(0, 0, -1), 0);
            var rotateTransform3D = new RotateTransform3D(_originalModelAxisAngleRotation3D);

            Ab3d.Utilities.TransformationsHelper.AddTransformation(hand2Group, rotateTransform3D);


            // By default all objects cast shadow, so to prevent that we need to set the IsCastingShadow to false.
            // This can be done in two ways:
            //
            // 1) Set DXAttributeType.IsCastingShadow on GeometryModel3D
            //    (only GeometryModel3D support that attribute;
            //     this need to be set before the WpfGeometryModel3DNode object is created from GeometryModel3D)
            //
            // 2) After the WpfGeometryModel3DNode is created from GeometryModel3D,
            // then we can set the IsCastingShadow on that SceneNode object - see commented code below.
            //
            // When we change the value of IsCastingShadow, we also need to enable checking this property with
            // setting the PlanarShadowRenderingProvider.IsCheckingIsCastingShadow property to true.
            // By default this property is set to false so improve performance
            // (prevent using FilterObjectsFunction in RenderObjectsRenderingStep that render shadow).

            _teapotGeometryModel3D = assimpWpfImporter.NamedObjects["Teapot"] as GeometryModel3D;
            _teapotGeometryModel3D.SetDXAttribute(DXAttributeType.IsCastingShadow, false);

            // If we want to change the value of IsCastingShadow when the object is already shown, 
            // we need to change that in the WpfGeometryModel3DNode - for example:
            //var teapotSceneNode = MainDXViewportView.GetSceneNodeForWpfObject(_teapotGeometryModel3D) as WpfGeometryModel3DNode; // we can get the teapotSceneNode in MainDXViewportView.DXSceneInitialized
            //if (teapotSceneNode != null)
            //    teapotSceneNode.IsCastingShadow = false; 

            return robotModel3D;
        }

        private Model3D CreateSimplifiedModel3D(Model3D originalModel3D)
        {
            double simplifiedModelHeight = originalModel3D.Bounds.SizeY * 0.6;
            double simplifiedModelArmLength = originalModel3D.Bounds.SizeX * 0.55;
            double simplifiedModelArmWidth = 16;

            var model3DGroup = new Model3DGroup();

            GeometryModel3D baseBox = Ab3d.Models.Model3DFactory.CreateBox(new Point3D(0, simplifiedModelHeight / 2, 0), new Size3D(simplifiedModelArmWidth, simplifiedModelHeight, simplifiedModelArmWidth), new DiffuseMaterial(Brushes.Black));
            GeometryModel3D armBox = Ab3d.Models.Model3DFactory.CreateBox(new Point3D(-simplifiedModelArmLength / 2, 0, 0), new Size3D(simplifiedModelArmLength, simplifiedModelArmWidth, simplifiedModelArmWidth), new DiffuseMaterial(Brushes.Black));
            GeometryModel3D teapot = Ab3d.Models.Model3DFactory.CreateSphere(new Point3D(-simplifiedModelArmLength - simplifiedModelArmWidth / 2, 0, 0), simplifiedModelArmWidth * 1.3, 8, new DiffuseMaterial(Brushes.Black));
            teapot.SetDXAttribute(DXAttributeType.IsCastingShadow, false); // comments in LoadModel for more info; though in this case it would be better to just prevent adding teapot to parent Model3DGroup

            var armModel3DGroup = new Model3DGroup();

            var armTransformGroup = new Transform3DGroup();

            _simplifiedModelAxisAngleRotation3D = new AxisAngleRotation3D(new Vector3D(0, 0, -1), 0);
            armTransformGroup.Children.Add(new RotateTransform3D(_simplifiedModelAxisAngleRotation3D));
            armTransformGroup.Children.Add(new TranslateTransform3D(0, simplifiedModelHeight - simplifiedModelArmWidth / 2, 0));
            armModel3DGroup.Transform = armTransformGroup;

            armModel3DGroup.Children.Add(armBox);
            armModel3DGroup.Children.Add(teapot);

            model3DGroup.Children.Add(baseBox);
            model3DGroup.Children.Add(armModel3DGroup);

            return model3DGroup;
        }

        private void OnPreviewKeyDown(object sender, KeyEventArgs keyEventArgs)
        {
            bool isChanged = false;
            double stepSize = 5;

            switch (keyEventArgs.Key)
            {
                case Key.Down:
                    if (_lightVerticalAngle - stepSize > 15)
                        _lightVerticalAngle -= stepSize;

                    isChanged = true; // mark as changed even if there were actually no change - this keyEventArgs.Handled to true so that the Up key does not go to some other UI element (for example changing the selected sample)
                    break;

                case Key.Up:
                    if (_lightVerticalAngle + stepSize < 165)
                        _lightVerticalAngle += stepSize;

                    isChanged = true;
                    break;

                case Key.Left:
                    _lightHorizontalAngle += stepSize;
                    isChanged = true;
                    break;

                case Key.Right:
                    _lightHorizontalAngle -= stepSize;
                    isChanged = true;
                    break;


                case Key.PageUp:
                    _lightDistance += stepSize;
                    isChanged = true;
                    break;

                case Key.PageDown:
                    _lightDistance -= stepSize;
                    isChanged = true;
                    break;


                case Key.Enter:
                    ChangeModel();
                    isChanged = true;
                    break;
            }

            if (isChanged)
            {
                UpdateLights();
                keyEventArgs.Handled = true;
            }
            else
            {
                keyEventArgs.Handled = false;
            }
        }

        private void UpdateLights()
        {
            var position = CalculateLightPosition();

            // Create direction from position - target position = (0,0,0)
            var lightDirection = new Vector3D(-position.X, -position.Y, -position.Z);
            lightDirection.Normalize();

            _shadowPointLight.Position = position;
            _shadowDirectionalLight.Direction = lightDirection;
        }

        private Point3D CalculateLightPosition()
        {
            double xRad = _lightHorizontalAngle * Math.PI / 180.0;
            double yRad = _lightVerticalAngle * Math.PI / 180.0;

            double x = (Math.Sin(xRad) * Math.Cos(yRad)) * _lightDistance;
            double y = Math.Sin(yRad) * _lightDistance;
            double z = (Math.Cos(xRad) * Math.Cos(yRad)) * _lightDistance;

            return new Point3D(x, y, z);
        }

        private void SetShadowLight(bool isDirectionalLight)
        {
            if (isDirectionalLight)
            {
                if (_currentShadowLight == _shadowDirectionalLight)
                    return;

                _currentShadowLight = _shadowDirectionalLight;
            }
            else
            {
                if (_currentShadowLight == _shadowPointLight)
                    return;

                _currentShadowLight = _shadowPointLight;
            }


            LightsModel3DGroup.Children.Clear();

            if (_ambientLight != null)
                LightsModel3DGroup.Children.Add(_ambientLight);

            if (_currentShadowLight != null)
                LightsModel3DGroup.Children.Add(_currentShadowLight);
        }


        private void DirectionalLightRadioButton_OnChecked(object sender, RoutedEventArgs e)
        {
            if (!this.IsLoaded)
                return;

            SetShadowLight(isDirectionalLight: true);
        }

        private void PointLightRadioButton_OnChecked(object sender, RoutedEventArgs e)
        {
            if (!this.IsLoaded)
                return;

            SetShadowLight(isDirectionalLight: false);
        }

        private void ChangeModelButton_OnClick(object sender, RoutedEventArgs e)
        {
            ChangeModel();
        }

        private void ChangeModel()
        { 
            if (_originalModelAxisAngleRotation3D != null)
            {
                _originalModelAxisAngleRotation3D.Angle += 10;

                if (_simplifiedModelAxisAngleRotation3D != null)
                    _simplifiedModelAxisAngleRotation3D.Angle = _originalModelAxisAngleRotation3D.Angle;
            }
        }

        private void OnShowSimplifiedShadowCheckBoxCheckedChanged(object sender, RoutedEventArgs e)
        {
            if (!this.IsLoaded)
                return;

            if (ShowSimplifiedShadowCheckBox.IsChecked ?? false)
            {
                if (MainDXViewportView.DXScene != null)
                {
                    if (_simplifiedShadowObjectsRenderingQueue == null)
                    {
                        // Create a custom RenderingQueue that will contain our simplified Model3D.
                        // The objects in this RenderingQueue will not be rendered with standard rendering (IsRenderedWithCustomRenderingStep = true)
                        // but will be only rendered when shadow objects are rendered (setting _planarShadowRenderingProvider.FilterRenderingQueuesFunction)
                        _simplifiedShadowObjectsRenderingQueue = new RenderingQueue("SimplifiedShadowObjectsRenderingQueue")
                        {
                            IsRenderedWithCustomRenderingStep = true // prevent rendering objects in this queue with standard rendering
                        };

                        MainDXViewportView.DXScene.AddRenderingQueueBefore(_simplifiedShadowObjectsRenderingQueue, MainDXViewportView.DXScene.BackgroundRenderingQueue);
                    }

                    _planarShadowRenderingProvider.FilterRenderingQueuesFunction = delegate(RenderingQueue queue)
                    {
                        return ReferenceEquals(queue, _simplifiedShadowObjectsRenderingQueue);
                    };
                }

                if (_simplifiedVisual3D == null)
                {
                    var simplifiedModel3D = CreateSimplifiedModel3D(_loadedModel3D);
                    _simplifiedVisual3D = simplifiedModel3D.CreateModelVisual3D();

                    // This object will be put into our custom rendering queue
                    _simplifiedVisual3D.SetDXAttribute(DXAttributeType.CustomRenderingQueue, _simplifiedShadowObjectsRenderingQueue);
                }

                MainViewport.Children.Add(_simplifiedVisual3D);
            }
            else
            {
                if (_simplifiedVisual3D != null)
                    MainViewport.Children.Remove(_simplifiedVisual3D);

                if (_planarShadowRenderingProvider != null)
                    _planarShadowRenderingProvider.FilterRenderingQueuesFunction = null; // Reset the filter method - this will render all standard rendering queues again
            }
        }
        
        private void OnCustomShadowLightCheckBoxCheckedChanged(object sender, RoutedEventArgs e)
        {
            if (!this.IsLoaded || _planarShadowRenderingProvider == null)
                return;

            if (CustomShadowLightCheckBox.IsChecked ?? false)
            {
                _planarShadowRenderingProvider.CustomShadowLight = new Ab3d.DirectX.Lights.DirectionalLight(new Vector3(0, -1, 0));
                //_planarShadowRenderingProvider.CustomShadowLight = new Ab3d.DirectX.Lights.PointLight(new Vector3(0, 500, 0), 300);
            }
            else
            {
                _planarShadowRenderingProvider.CustomShadowLight = null;
            }

            MainDXViewportView.Refresh();
        }

        private void ShadowTransparencySlider_OnValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_planarShadowRenderingProvider != null)
            {
                _planarShadowRenderingProvider.ShadowTransparency = (float)ShadowTransparencySlider.Value / 100.0f;
                MainDXViewportView.Refresh();
            }
        }

        private void OnIsCheckingIsCastingShadowCheckBoxChanged(object sender, RoutedEventArgs e)
        {
            if (_planarShadowRenderingProvider != null)
            {
                // See comments in LoadModel for more info about the following commented lines:
                //var teapotSceneNode = MainDXViewportView.GetSceneNodeForWpfObject(_teapotGeometryModel3D) as WpfGeometryModel3DNode; // we can get the teapotSceneNode in MainDXViewportView.DXSceneInitialized
                //if (teapotSceneNode != null)
                //    teapotSceneNode.IsCastingShadow = IsCheckingIsCastingShadowCheckBox.IsChecked ?? false;

                _planarShadowRenderingProvider.IsCheckingIsCastingShadow = IsCheckingIsCastingShadowCheckBox.IsChecked ?? false;
                MainDXViewportView.Refresh();
            }
        }

        private void OnCustomPlaneCheckBoxChanged(object sender, RoutedEventArgs e)
        {
            if (_planarShadowRenderingProvider != null)
            {
                if (CustomPlaneCheckBox.IsChecked ?? false)
                {
                    // When we set ShadowPlaneMaterial and ShadowPlaneMaterial to null, then the 3D plane is not drawn any more.
                    // But shadow can be still clipped to the bounds of the "plane" defined by the ShadowPlaneCenterPosition and ShadowPlaneSize.
                    _planarShadowRenderingProvider.ShadowPlaneMaterial     = null;
                    _planarShadowRenderingProvider.ShadowPlaneBackMaterial = null;

                    _wireGridVisual3D = new WireGridVisual3D()
                    {
                        CenterPosition   = new Point3D(_planarShadowRenderingProvider.ShadowPlaneCenterPosition.X, _planarShadowRenderingProvider.ShadowPlaneCenterPosition.Y - 0.2, _planarShadowRenderingProvider.ShadowPlaneCenterPosition.Z),
                        Size             = new Size(_planarShadowRenderingProvider.ShadowPlaneSize.X, _planarShadowRenderingProvider.ShadowPlaneSize.Y),
                        WidthCellsCount  = 10,
                        HeightCellsCount = 10,
                        LineThickness    = 1,
                    };

                    MainViewport.Children.Add(_wireGridVisual3D);
                }
                else
                {
                    _planarShadowRenderingProvider.ShadowPlaneMaterial     = _shadowPlaneMaterial;
                    _planarShadowRenderingProvider.ShadowPlaneBackMaterial = _shadowPlaneBackMaterial;

                    if (_wireGridVisual3D != null)
                    {
                        MainViewport.Children.Remove(_wireGridVisual3D);
                        _wireGridVisual3D = null;
                    }
                }

                MainDXViewportView.Refresh();
            }
        }

        private void OnClipToBoundsCheckBoxChanged(object sender, RoutedEventArgs e)
        {
            if (_planarShadowRenderingProvider != null)
            {
                _planarShadowRenderingProvider.CutShadowToPlaneBounds = (ClipToBoundsCheckBox.IsChecked ?? false);
                MainDXViewportView.Refresh();
            }
        }
    }
}