using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using Ab3d.DirectX;
using Ab3d.DirectX.Controls;
using Ab3d.DirectX.Materials;

#if SHARPDX
using SharpDX.Direct3D11;
#endif

namespace Ab3d.DXEngine.Wpf.Samples.DXEngineVisuals
{
    /// <summary>
    /// Interaction logic for TwoSidedMaterialsSample.xaml
    /// </summary>
    public partial class TwoSidedMaterialsSample : Page
    {
        private bool _setFrontMaterial;
        private bool _setBackMaterial;

        public TwoSidedMaterialsSample()
        {
            InitializeComponent();

            MainDXViewportView.IsTransparencySortingEnabled = true;


            FrontAndBackSideInfoControl.InfoText = 
@"Rendering two-sided materials by first rendering back side and then front side. This requires two renderable objects in RenderingQueues and two draw calls to render one object - see info text in the TextBox below.";

            TwoSidedSolidInfoControl.InfoText = 
@"Rendering solid two-sided materials in one render pass.

Solid two-sided objects are rendered with one renderable objects and one draw call - see info text in the TextBox below.

Transparent two-sided materials are rendered by two renderable objects and in two draw calls: first back material and then front material is rendered. This is the default setting and preserves the results of rendering transparent objects when the default DepthReadWrite depth stencil state for transparent objects is used.";
            
            TwoSidedSolidTransparentInfoControl.InfoText = 
@"Rendering all two-sided materials in one render pass (required one renderable objects and on draw calls to render one object - see info text in the TextBox below).

Note that when rendering transparent objects this requires changing the depth stencil state for transparent objects to DepthRead (without write) for the inner side of the objects to be rendered correctly.";
            
            DepthReadWriteInfoControl.InfoText = 
@"When DepthStencilState is set to DepthReadWrite (by default), then the pixel will be rendered only if it is closer to the camera (its depth value is smaller) then the already rendered pixel (at the same x,y location). In this case the pixel's depth value will be written to the depth buffer so pixels that will be rendered afterwards will need to be closer to the camera to be rendered.

This requires that the objects (and also triangles) are sorted by their distance to the camera and rendered in that order.";
            
            DepthReadSideInfoControl.InfoText = 
@"The difference between DepthRead and DepthReadWrite mode is that by DepthRead the rendered pixels do not prevent rendering pixels that are farther away from the camera to be rendered (do not write to depth buffer). When this is used by transparent objects, this will not block rendering other objects even if they are farther away from the camera and rendered after the transparent objects.

This mode is useful when rendering two-sided transparent objects by one draw call because it does not require that the back material is rendered first.";


            this.Loaded += delegate(object sender, RoutedEventArgs args)
            {
                UpdateRenderingSettings();
                CreateScene();
            };
        }

        private void UpdateRenderingSettings()
        {
            if (OnlyFrontSideRadioButton.IsChecked ?? false)
            {
                _setFrontMaterial = true;
                _setBackMaterial = false;

                // UseTwoSidedMaterialsForSolidObjects is true by default.
                // In this case TwoSided material will be used when Material and BackMaterial properties are set to the same instance and when material is not transparent.
                DXViewportView.UseTwoSidedMaterialsForSolidObjects = false;

                // UseTwoSidedMaterialsForTransparentObjects is set to FALSE by default.
                // The reason for this is that in this case the back material is always rendered before front material and this can produce more accurate results
                // then when both back and front sides are rendered at the same time.
                DXViewportView.UseTwoSidedMaterialsForTransparentObjects = false;
            }
            else if (OnlyBackSideRadioButton.IsChecked ?? false)
            {
                _setFrontMaterial = false;
                _setBackMaterial = true;

                DXViewportView.UseTwoSidedMaterialsForSolidObjects = false;
                DXViewportView.UseTwoSidedMaterialsForTransparentObjects = false;
            }
            else if (FrontAndBackSideRadioButton.IsChecked ?? false)
            {
                _setFrontMaterial = true;
                _setBackMaterial = true;

                DXViewportView.UseTwoSidedMaterialsForSolidObjects = false;
                DXViewportView.UseTwoSidedMaterialsForTransparentObjects = false;
            }
            else if (TwoSidedSolidRadioButton.IsChecked ?? false)
            {
                _setFrontMaterial = true;
                _setBackMaterial = true;

                DXViewportView.UseTwoSidedMaterialsForSolidObjects = true;
                DXViewportView.UseTwoSidedMaterialsForTransparentObjects = false;
            }           
            else if (TwoSidedSolidTransparentRadioButton.IsChecked ?? false)
            {
                _setFrontMaterial = true;
                _setBackMaterial = true;

                DXViewportView.UseTwoSidedMaterialsForSolidObjects = true;
                DXViewportView.UseTwoSidedMaterialsForTransparentObjects = true;
            }
        }

        private void CreateScene()
        {
            MainViewport.Children.Clear();


            var solidMaterial = new DiffuseMaterial(System.Windows.Media.Brushes.LightSkyBlue);
            var transparentMaterial = new DiffuseMaterial(new SolidColorBrush(Colors.LightGreen) { Opacity = 0.5 });

            //
            // Add solid cylinder (we use TubeLineVisual3D because it can be opened on top and bottom)
            // 
            var tubeLineVisual1 = new Ab3d.Visuals.TubeLineVisual3D()
            {
                StartPosition = new Point3D(-150, 50, 0),
                EndPosition = new Point3D(-150, -50, 0),
                Radius = 40,
                Segments = 20,
                IsStartPositionClosed = false,
                IsEndPositionClosed = false,
                Material = _setFrontMaterial ? solidMaterial : null,
                BackMaterial = _setBackMaterial ? solidMaterial : null // To use TwoSided material set Material and BackMaterial to the same material instance (and when DXViewportView.UseTwoSidedMaterialsForSolidObjects and DXViewportView.UseTwoSidedMaterialsForTransparentObjects are true) 
            };

            tubeLineVisual1.SetName("SolidCylinder");

            MainViewport.Children.Add(tubeLineVisual1);


            //
            // Add transparent cylinder
            // 
            var tubeLineVisual2 = new Ab3d.Visuals.TubeLineVisual3D()
            {
                StartPosition = new Point3D(-50, 50, 0),
                EndPosition = new Point3D(-50, -50, 0),
                Radius = 40,
                Segments = 20,
                IsStartPositionClosed = false,
                IsEndPositionClosed = false,
                Material = _setFrontMaterial ? transparentMaterial : null,
                BackMaterial = _setBackMaterial ? transparentMaterial : null
            };

            tubeLineVisual2.SetName("TransparentCylinder");

            MainViewport.Children.Add(tubeLineVisual2);


            //
            // Add solid box
            // Create DXEngine's StandardMaterial and set IsTwoSided there
            //
            var boxVisual1 = new Ab3d.Visuals.BoxVisual3D()
            {
                CenterPosition = new Point3D(50, 0, 0),
                Size = new Size3D(80, 80, 60),
            };

            boxVisual1.SetName("SoildBox");
            
            if (DXViewportView.UseTwoSidedMaterialsForSolidObjects)
            {
                var standardMaterial1 = new StandardMaterial(Colors.LightSkyBlue.ToColor3())
                {
                    IsTwoSided = true, // DXEngine's StandardMaterial, VertexColorMaterial, WpfMaterial and PhysicallyBasedMaterial all support IsTwoSided property (they all implement ITwoSidedMaterial interface)
                };

                // To assign DXEngine material to WPF object, create a DiffuseMaterial and the call SetUsedDXMaterial on it
                var material1 = new DiffuseMaterial(System.Windows.Media.Brushes.Red);
                material1.SetUsedDXMaterial(standardMaterial1);

                boxVisual1.Material = material1;
            }
            else
            {
                boxVisual1.Material = _setFrontMaterial ? solidMaterial : null;
                boxVisual1.BackMaterial = _setBackMaterial ? solidMaterial : null;
            }

            MainViewport.Children.Add(boxVisual1);


            //
            // Add transparent box
            // Create DXEngine's StandardMaterial and set IsTwoSided there
            //
            var boxVisual2 = new Ab3d.Visuals.BoxVisual3D()
            {
                CenterPosition = new Point3D(150, 0, 0),
                Size = new Size3D(80, 80, 60),
            };

            boxVisual2.SetName("TransparentBox");
            
            if (DXViewportView.UseTwoSidedMaterialsForTransparentObjects)
            {
                var standardMaterial2 = new StandardMaterial(Colors.LightGreen.ToColor3())
                {
                    IsTwoSided = true, // DXEngine's StandardMaterial, VertexColorMaterial, WpfMaterial and PhysicallyBasedMaterial all support IsTwoSided property (they all implement ITwoSidedMaterial interface)
                    Alpha = 0.5f,
                    HasTransparency = true
                };

                // To assign DXEngine material to WPF object, create a DiffuseMaterial and the call SetUsedDXMaterial on it
                var material2 = new DiffuseMaterial(System.Windows.Media.Brushes.Red);
                material2.SetUsedDXMaterial(standardMaterial2);

                boxVisual2.Material = material2;
            }
            else
            {
                boxVisual2.Material = _setFrontMaterial ? transparentMaterial : null;
                boxVisual2.BackMaterial = _setBackMaterial ? transparentMaterial : null;
            }

            MainViewport.Children.Add(boxVisual2);


            // Show rendering queues after the scene is rendered
            MainDXViewportView.SceneRendered += MainDXViewportViewOnSceneRendered;
        }

        private void MainDXViewportViewOnSceneRendered(object sender, EventArgs e)
        {
            MainDXViewportView.SceneRendered -= MainDXViewportViewOnSceneRendered;

            if (MainDXViewportView.DXScene == null) // WPF 3D rendering?
                return;

            var renderingQueuesDumpString = MainDXViewportView.DXScene.GetRenderingQueuesDumpString(dumpEmptyRenderingQueues: false, showOverview: false, showItemsInQueue: true);
            InfoTextBox.Text = renderingQueuesDumpString;
        }

        private void OnRenderingTypeChanged(object sender, RoutedEventArgs e)
        {
            if (!this.IsLoaded)
                return;

            UpdateRenderingSettings();
            CreateScene();
        }

        private void OnDepthStencilStateChanged(object sender, RoutedEventArgs e)
        {
            if (!this.IsLoaded || MainDXViewportView.DXScene == null)
                return;


            DepthStencilState transparentDepthStencilState;
            if (DepthReadWriteRadioButton.IsChecked ?? false)
                transparentDepthStencilState = MainDXViewportView.DXScene.DXDevice.CommonStates.DepthReadWrite;
            else
                transparentDepthStencilState = MainDXViewportView.DXScene.DXDevice.CommonStates.DepthRead;

            MainDXViewportView.DXScene.DXDevice.CommonStates.DefaultTransparentDepthStencilState = transparentDepthStencilState;

            CreateScene();
        }
    }
}