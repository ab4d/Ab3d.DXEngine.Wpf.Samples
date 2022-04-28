//#define TEST

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
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
using Ab3d.Cameras;
using Ab3d.DirectX;
using Ab3d.DirectX.Controls;
using Ab3d.DirectX.Effects;
using Ab3d.DirectX.PostProcessing;
using Ab3d.DXEngine.Wpf.Samples.Common;
using Ab3d.Utilities;
using Ab3d.Visuals;
using SharpDX;
using Color = System.Windows.Media.Color;

namespace Ab3d.DXEngine.Wpf.Samples.DXEngineVisuals
{
    /// <summary>
    /// Interaction logic for AdvancedEdgeLinesSample.xaml
    /// </summary>
    public partial class AdvancedEdgeLinesSample : Page
    {
        private string _fileName;
        private MultiLineVisual3D _edgeLinesVisual3D;

        private SolidColorEffect _blackOutlineEffect;
        private RenderObjectsRenderingStep _renderObjectOutlinesRenderingStep;

        private DisposeList _disposables;
        
        private DXViewportView _mainDXViewportView;
        private Viewport3D _mainViewport3D;


        public AdvancedEdgeLinesSample()
        {
            InitializeComponent();


            PowerToysFeaturesInfoControl.InfoText =
@"With Ab3d.PowerToys library it is possible to show 3D lines (this is not possible when only WPF 3D is used). But the problem is that the geometry for 3D lines (2 triangles for each line) need to be generated on the CPU. Because 3D lines need to face the camera, the geometry needs to be regenerated on each camera change. This can slow the performance of the application when a lot of 3D lines need to be shown.

The Ab3d.PowerToys library can also generate edge lines based on the angle between triangles (if angle is bigger then the specified angle, then an edge line is created).";


            DXEngineFeaturesInfoControl.InfoText =
@"Ab3d.DXEngine can use hardware acceleration to create the geometry for 3D lines in the geometry shader. This can render millions on 3D lines on a modern GPU.

What is more, as shown in this sample, the following additional features are available when using Ab3d.DXEngine:
1) It is possible to set line depth bias that moves the lines closer to the camera so that they are rendered on top of the 3D shape. This way the lines are not partially occluded by the 3D shape because they occupy the same 3D space. The depth bias processing is done in the vertex shader.

2) It is possible to render object's outlines. Here a technique is used that first renders the scene with black color and with expanded geometry. Then the scene is rendered normally on top of the black scene. For other techniques to render object outlines see the 'Object outlines rendering' sample.

3) To reduce anti-aliasing the WPF 3D can use multi-sampling (MSAA). With Ab3d.DXEngine it is possible to further reduce the aliasing and produce super-smooth 3D lines with using super-sampling (SSAA). This renders the scene to a higher resolution (4 times higher when 4xSSAA is used). Then the rendered image is down-sampled to the final resolution with using a smart filter. This can be combined with multi-sampling to produce much better results that using multi-sampling alone." ;


            AssimpLoader.LoadAssimpNativeLibrary();

            var dragAndDropHelper = new DragAndDropHelper(this, ".*");
            dragAndDropHelper.FileDropped += (sender, args) => LoadModelWithEdgeLines(args.FileName);


            CreateDXViewportView();

            
            var startupFileName = AppDomain.CurrentDomain.BaseDirectory + @"Resources\Models\planetary-gear.fbx";
            LoadModelWithEdgeLines(startupFileName);


            this.Unloaded += delegate (object sender, RoutedEventArgs args)
            {
                Dispose();
            };
        }


        // We create DXViewport3D here and not in XAML because when the SSAA (super-sampling) CheckBox is changed,
        // then we need to recreate the DXViewport3D to change the SSAA setting.
        private void CreateDXViewportView()
        {
            Dispose();

            _disposables = new DisposeList(); // Start with a fresh list


            _mainViewport3D = new Viewport3D();

            Camera1.TargetViewport3D = _mainViewport3D;

            _mainDXViewportView = new DXViewportView(_mainViewport3D)
            {
                PresentationType = DXView.PresentationTypes.DirectXImage,
                BackgroundColor  = Colors.Transparent
            };

            GraphicsProfile usedGraphicsProfile;
            if (SuperSamplingCheckBox.IsChecked ?? false)
            {
                // HighQualityHardwareRendering: 4xMSAA (multi-sampling) + 4xSSAA (super-sampling)
                // Using super-sampling produces super-smooth lines because scene is render to 4 times bigger texture
                // (width *= 2; height *= 2) and then down-sampled to the final size with using smart filer
                usedGraphicsProfile = GraphicsProfile.HighQualityHardwareRendering;
            }
            else
            {
                // NormalQualityHardwareRendering: 4xMSAA (multi-sampling) and No super-sampling
                usedGraphicsProfile = GraphicsProfile.NormalQualityHardwareRendering;
            }

            _mainDXViewportView.GraphicsProfiles = new GraphicsProfile[]
            {
                usedGraphicsProfile,
                GraphicsProfile.Wpf3D // fallback
            };


            _mainDXViewportView.DXSceneDeviceCreated += delegate (object sender, EventArgs args)
            {
                // Wait until DXScene is created and then add edge detection post-process
                SetupObjectOutlinesRenderingStep();

                _renderObjectOutlinesRenderingStep.IsEnabled = ShowObjectOutlineCheckBox.IsChecked ?? false;


                // Update the information about shown graphics profile
                var parentWindow = Window.GetWindow(this) as MainWindow;
                if (parentWindow != null)
                    parentWindow.UpdateUsedGraphicsProfileDescription(_mainDXViewportView.UsedGraphicsProfile, _mainDXViewportView);
            };

            ViewportBorder.Child = _mainDXViewportView;

            
            // Reload existing file
            if (_fileName != null)
                LoadModelWithEdgeLines(_fileName);
        }
        
        private void LoadModelWithEdgeLines(string fileName)
        {
            _mainViewport3D.Children.Clear();

            // Create an instance of AssimpWpfImporter
            var assimpWpfImporter = new AssimpWpfImporter();
            var readModel3D = assimpWpfImporter.ReadModel3D(fileName, texturesPath: null); // we can also define a textures path if the textures are located in some other directory (this is parameter can be skipped, but is defined here so you will know that you can use it)


            if (readModel3D == null)
            {
                MessageBox.Show("Cannot read file");
                return;
            }

            _mainViewport3D.Children.Add(readModel3D.CreateModelVisual3D());


            // NOTE:
            // EdgeLinesFactory from Ab3d.PowerToys library will be used to create 3D lines that define the edges of the 3D models.  
            // The edges are created when the angle between two triangles is bigger then the specified edgeStartAngleInDegrees (set by EdgeStartAngleSlider).
            //
            // See Lines3D\StaticEdgeLinesCreationSample and Lines3D\DynamicEdgeLinesSample samples for more info.
            //
            //
            // ADDITIONAL NOTE: 
            // It is possible to store edge lines into wpf3d file format (this way it is not needed to generate the edge lines after the file is read).
            // The source code to read and write wpf3d file is available with Ab3d.PowerToys library.



            // With using AddEdgeLinePositions we will create STATIC lines from the current readModel3D.
            // If the readModel3D would be changed (any child transformation would be changed),
            // then the lines would not be correct any more.
            // See the DynamicEdgeLinesSample to see how to create dynamic edge lines.
            // If your object will not change, then it is better to create static edge lines for performance reasons
            // (having single MultiLineVisual3D for the whole instead of one MultiLineVisual3D for each GeometryModel3D).

            var edgeLinePositions = new Point3DCollection();
            EdgeLinesFactory.AddEdgeLinePositions(readModel3D, EdgeStartAngleSlider.Value, edgeLinePositions);


            _edgeLinesVisual3D = new MultiLineVisual3D()
            {
                Positions     = edgeLinePositions,
                LineColor     = Colors.Black,
                LineThickness = LineThicknessSlider.Value,
                IsVisible     = ShowEdgeLinesCheckBox.IsChecked ?? false
            };

            UpdateLineDepthBias();

            _mainViewport3D.Children.Add(_edgeLinesVisual3D);


            if (_fileName != fileName) // Reset camera only when the file is loaded for the first time
            {
                _fileName = fileName;

                // Set both Distance and CameraWidth (this way we can change the CameraType with RadioButtons)
                // Distance is used when CameraType is PerspectiveCamera.
                // CameraWidth is used when CameraType is OrthographicCamera.
                Camera1.Distance = readModel3D.Bounds.GetDiagonalLength() * 1.3; 
                Camera1.CameraWidth = readModel3D.Bounds.SizeX * 2;

                Camera1.Offset         = new Vector3D(0, 0, 0); // Reset offset
                Camera1.TargetPosition = readModel3D.Bounds.GetCenterPosition() + new Vector3D(0, readModel3D.Bounds.SizeY * 0.15, 0); // slightly move the object down so that the object is not shown over the title
            }

            // Add ambient light
            var ambientLight = new AmbientLight(Color.FromRgb(100, 100, 100));
            _mainViewport3D.Children.Add(ambientLight.CreateModelVisual3D());
        }


        private void SetupObjectOutlinesRenderingStep()
        {
            // One way to show object outlines is to render the whole scene with black color (using SolidColorEffect)
            // and with expanding the geometry of each object in the direction of triangle normals (setting SolidColorEffect.OutlineThickness property)
            // Then the standard 3D scene is rendered on top of the black 3D scene.

            _blackOutlineEffect = new SolidColorEffect()
            {
                Color              = Color4.Black,
                OverrideModelColor = true,  // This will render all objects with Black color; when false then object's color is used
                OutlineThickness   = 3,

                // Use the following 3 settings to show outline for the whole 3D scene:
                WriteMaxDepthValue      = true,                                                      // the black objects will be written to the back of all the objects (using max depth value).
                OverrideRasterizerState = _mainDXViewportView.DXScene.DXDevice.CommonStates.CullNone // render front and back triangles
            };

            _disposables.Add(_blackOutlineEffect);

            _blackOutlineEffect.InitializeResources(_mainDXViewportView.DXScene.DXDevice);

            // Add another RenderObjectsRenderingStep that will render black scene ...
            _renderObjectOutlinesRenderingStep = new RenderObjectsRenderingStep("RenderObjectOutlinesRenderingStep")
            {
                OverrideEffect = _blackOutlineEffect,
                FilterRenderingQueuesFunction = delegate(RenderingQueue queue)
                {
                    return (queue != _mainDXViewportView.DXScene.LineGeometryRenderingQueue); // Render all objects except 3D lines
                }
            };

            _disposables.Add(_blackOutlineEffect);

            // ... and add it before standard RenderObjectsRenderingStep
            _mainDXViewportView.DXScene.RenderingSteps.AddBefore(_mainDXViewportView.DXScene.DefaultRenderObjectsRenderingStep, _renderObjectOutlinesRenderingStep);


            // Uncomment the following line to prevent rendering standard 3D objects:
            //MainDXViewportView.DXScene.DefaultRenderObjectsRenderingStep.IsEnabled = false;
        }

        private void UpdateLineDepthBias()
        {
            if (_edgeLinesVisual3D == null)
                return;

            if (LineDepthBiasCheckBox.IsChecked ?? false)
            {
                // Use line depth bias to move the lines closer to the camera so the lines are rendered on top of solid model and are not partially occluded by it.
                // See DXEngineVisuals/LineDepthBiasSample for more info.
                _edgeLinesVisual3D.SetDXAttribute(DXAttributeType.LineDepthBias, 0.1);
                _edgeLinesVisual3D.SetDXAttribute(DXAttributeType.LineDynamicDepthBiasFactor, 0.02);
            }
            else
            {
                _edgeLinesVisual3D.ClearDXAttribute(DXAttributeType.LineDepthBias);
                _edgeLinesVisual3D.ClearDXAttribute(DXAttributeType.LineDynamicDepthBiasFactor);
            }
        }


        private void OnShowEdgeLinesCheckBoxCheckedChanged(object sender, RoutedEventArgs e)
        {
            if (!this.IsLoaded || _edgeLinesVisual3D == null)
                return;

            _edgeLinesVisual3D.IsVisible = ShowEdgeLinesCheckBox.IsChecked ?? false;
        }

        private void EdgeStartAngleSlider_OnValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!this.IsLoaded || _fileName == null)
                return;

            LoadModelWithEdgeLines(_fileName);
        }

        private void LineThicknessSlider_OnValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!this.IsLoaded || _edgeLinesVisual3D == null)
                return;

            _edgeLinesVisual3D.LineThickness = LineThicknessSlider.Value;
        }

        private void OnShowObjectOutlineCheckBoxCheckedChanged(object sender, RoutedEventArgs e)
        {
            if (!this.IsLoaded || _renderObjectOutlinesRenderingStep == null)
                return;

            _renderObjectOutlinesRenderingStep.IsEnabled = ShowObjectOutlineCheckBox.IsChecked ?? false;

            _mainDXViewportView.Refresh();
        }
        
        private void OnSuperSamplingCheckBoxCheckedChanged(object sender, RoutedEventArgs e)
        {
            if (!this.IsLoaded)
                return;

            // We cannot change SSAA (super-sampling) "on the fly".
            // We will need to dispose existing DXViewportView and create a new one.
            CreateDXViewportView();
        }

        private void OnLineDepthBiasSettingsChanged(object sender, RoutedEventArgs e)
        {
            if (!this.IsLoaded)
                return;

            UpdateLineDepthBias();
        }

        private void OnCameraTypeRadioButtonCheckedChanged(object sender, RoutedEventArgs e)
        {
            if (!this.IsLoaded)
                return;

            Camera1.CameraType = (OrthographicCameraRadioButton.IsChecked ?? false) ? BaseCamera.CameraTypes.OrthographicCamera
                : BaseCamera.CameraTypes.PerspectiveCamera;
        }

        private void Dispose()
        {
            if (_disposables != null)
            {
                _disposables.Dispose();
                _disposables = null;
            }

            if (_mainDXViewportView != null)
            {
                _mainDXViewportView.Dispose();
                _mainDXViewportView = null;
            }
        }
    }
}
