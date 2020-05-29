using System;
using System.Collections.Generic;
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
using Ab3d.DirectX;
using Ab3d.DirectX.Effects;
using Ab3d.DirectX.Models;
using Ab3d.DirectX.PostProcessing;
using Ab3d.Visuals;
using SharpDX;
using SharpDX.Direct3D;
using SharpDX.Direct3D11;
using SharpDX.DXGI;

namespace Ab3d.DXEngine.Wpf.Samples.DXEngineAdvanced
{
    // This sample uses a few advanced rendering techniques to create object outlines that are visible through other objects.
    //
    // 1) Selected objects (using RenderObjectsRenderingStep.FilterObjectsFunction) are rendered to its 
    //    own render target with using a SolidColorEffect to render them with a single orange color.
    //    What is more, the objects are rendered with using stencil buffer - stencil value is set to 1 for each rendered pixel.
    //
    // 2) ExpandPostProcess post process effect is used to extend (make it bigger) the selected objects that were rendered with solid color.
    //
    // 3) RenderTextureRenderingStep is used to render the expanded solid color object.
    //    Rendering is done with using alpha bending (adding the expanded solid color object on top of the standard 3D scene).
    //    To add only the outline and not also the selected object rendered as solid color we use stencil buffer.
    //    We add the expanded solid color object to the existing scene only for those pixels that do not have stencil buffer set to 1
    //    (so only those that do not belong to the original solid color object but were added with using ExpandPostProcess).
    //
    // 
    // The following shows all the rendering steps after setting them up to also render outlines (new steps are marked with *)
    // - InitializeRenderingStep Name: 'Initialize'
    // * RenderingStepsGroup Name: 'Render outline group'
    // *   - RenderObjectsRenderingStep Name: 'Render outlined objects'  OverrideEffect set; OverrideDepthStencilState set; FilterObjectsFunction set;
    // *   - RenderPostProcessingRenderingStep Name: 'Expand objects rendering step'
    // - PrepareRenderTargetsRenderingStep Name: 'PrepareRenderTargets'
    // - RenderObjectsRenderingStep Name: 'StandardRenderObjects'
    // - ResolveMultisampledBackBufferRenderingStep Name: 'StandardResolveMultisampledBackBuffer'
    // - RenderingStepsGroup Name: 'RenderPostProcessingGroup' (DISABLED)
    // -    - PreparePostProcessingRenderingStep Name: 'PreparePostProcessing' (PARENT DISABLED)
    // -    - RenderPostProcessingRenderingStep Name: 'RenderPostProcessing' (PARENT DISABLED)
    // * RenderTextureRenderingStep   Name: 'Render outline over 3D scene'
    // - CompleteRenderingStep Name: 'Complete'


    /// <summary>
    /// Interaction logic for OutlinesSample.xaml
    /// </summary>
    public partial class OutlinesSample : Page
    {
        private SolidColorEffect _solidColorEffectWithOutline;
        private ExpandPostProcess _horizontalExpandPostProcess;
        private ExpandPostProcess _verticalExpandPostProcess;

        private Color4 _backgroundColor;

        private DisposeList _disposables;

        private List<string> _selectedObjectNames;

        public OutlinesSample()
        {
            InitializeComponent();

            _disposables = new DisposeList();

            CreateSceneObjects();

            MainDXViewportView.DXSceneInitialized += delegate(object sender, EventArgs args)
            {
                if (MainDXViewportView.DXScene == null) // When using WPF 3D rendering
                    return;

                SetupOutlineRenderingStep();
            };

            this.Unloaded += delegate(object sender, RoutedEventArgs args)
            {
                _disposables.Dispose();

                DisposeOutlineBackBuffers();
            };
        }


        private Texture2D _savedBackBuffer;
        private Texture2DDescription _savedBackBufferDescription;
        private RenderTargetView _savedRenderTargetView;
        private DepthStencilView _savedDepthStencilView;
        private ViewportF _savedViewport;

        private Texture2D _outlineBackBuffer;
        private Texture2DDescription _outlineBackBufferDescription;
        private RenderTargetView _outlineRenderTargetView;
        private ShaderResourceView _outlineShaderResourceView;
        private DepthStencilView _outlineDepthStencilView;

        private Texture2D _blurredOutlineBackBuffer;
        private RenderTargetView _blurredRenderTargetView;
        private ShaderResourceView _blurredShaderResourceView;

        private RenderPostProcessingRenderingStep _expandObjectsPostProcessesRenderingSteps;
        private DepthStencilState _stencilSetToOneDepthStencilState;
        private DepthStencilState _renderWhenStencilIsNotOneState;
        private RenderTextureRenderingStep _addOutlineRenderingStep;

        private ViewportF _outlineBufferViewport;

        private void SetupOutlineRenderingStep()
        {
            _solidColorEffectWithOutline = new SolidColorEffect();

            _solidColorEffectWithOutline.Color              = Colors.Orange.ToColor4();
            _solidColorEffectWithOutline.OverrideModelColor = true;
            _solidColorEffectWithOutline.OutlineThickness   = 0;
            _solidColorEffectWithOutline.WriteMaxDepthValue = false;

            _solidColorEffectWithOutline.InitializeResources(MainDXViewportView.DXScene.DXDevice);

            _disposables.Add(_solidColorEffectWithOutline);


            _backgroundColor = new Color4(1, 1, 1, 0);


            var stencilSetToOneDescription = new DepthStencilStateDescription()
            {
                IsDepthEnabled = true,
                DepthWriteMask = DepthWriteMask.All,

                DepthComparison  = Comparison.LessEqual,
                IsStencilEnabled = true,
                StencilReadMask  = 0xFF,
                StencilWriteMask = 0xFF,
                FrontFace = {
                    Comparison         = Comparison.Always,
                    DepthFailOperation = StencilOperation.Keep,
                    FailOperation      = StencilOperation.Keep,
                    PassOperation      = StencilOperation.Replace // The value that is set as reference is set in the ContextStateManger when _deviceContext.OutputMerger.SetDepthStencilState is called - the value is set to 1.
                }
            };

            stencilSetToOneDescription.BackFace = stencilSetToOneDescription.FrontFace;

            _stencilSetToOneDepthStencilState = new DepthStencilState(MainDXViewportView.DXScene.Device, stencilSetToOneDescription);

            if (MainDXViewportView.DXScene.DXDevice.IsDebugDevice)
                _stencilSetToOneDepthStencilState.DebugName = "StencilSetToOneDepthStencilState";

            _disposables.Add(_stencilSetToOneDepthStencilState);


            var renderWhenStencilIsNotOneDescription = new DepthStencilStateDescription()
            {
                IsDepthEnabled = true,
                DepthWriteMask = DepthWriteMask.All,

                DepthComparison  = Comparison.LessEqual,
                IsStencilEnabled = true,
                StencilReadMask  = 0xFF,
                StencilWriteMask = 0xFF,
                FrontFace = {
                    Comparison         = Comparison.Greater, // render only when 1 is greater then stencil value 
                    DepthFailOperation = StencilOperation.Keep,
                    FailOperation      = StencilOperation.Keep,
                    PassOperation      = StencilOperation.Keep
                }
            };

            renderWhenStencilIsNotOneDescription.BackFace = stencilSetToOneDescription.FrontFace;

            _renderWhenStencilIsNotOneState = new DepthStencilState(MainDXViewportView.DXScene.Device, renderWhenStencilIsNotOneDescription);

            if (MainDXViewportView.DXScene.DXDevice.IsDebugDevice)
                _renderWhenStencilIsNotOneState.DebugName = "RenderWhenStencilIsNotOneState";

            _disposables.Add(_renderWhenStencilIsNotOneState);


            var renderingStepsGroup = new RenderingStepsGroup("Render outline group", MainDXViewportView.DXScene.RenderingSteps);

            renderingStepsGroup.BeforeRunningStep += (object sender, DirectX.RenderingEventArgs args) =>
            {
                var renderingContext = args.RenderingContext;

                // Set new back buffer where we will render outline objects
                SetupOutlineBackBuffers(renderingContext);

                // Set new DepthStencilState that will also set stencil value to 1 for each rendered pixel
                renderingContext.ContextStatesManager.DepthStencilState = _stencilSetToOneDepthStencilState;

                _addOutlineRenderingStep.SourceTexture = _outlineShaderResourceView;
            };

            renderingStepsGroup.AfterRunningStep += (object sender, DirectX.RenderingEventArgs args) =>
            {
                var renderingContext = args.RenderingContext;

                // Reset the saved back buffer
                RestoreBackBuffers(renderingContext);
            };


            // The first step in rendering outlines is to render selected objects with the selected solid color.
            // This is done with creating a custom RenderObjectsRenderingStep
            // and overriding the OverrideEffect to use SolidColorEffect and _stencilSetToOneDepthStencilState.
            // We also render only the selected objects - this is done with using FilterObjectsFunction.
            var renderOutlinesObjectsRenderingStep = new RenderObjectsRenderingStep("Render outlined objects")
            {
                OverrideEffect = _solidColorEffectWithOutline,
                OverrideDepthStencilState = _stencilSetToOneDepthStencilState,

                FilterObjectsFunction = delegate (RenderablePrimitiveBase objectToRender)
                {
                    // IMPORTANT:
                    // This delegate is highly performance critical because it is called for each object in each frame.
                    // Therefore do not access any WPF's DependencyProperties there.

                    var wpfGeometryModel3DNode = objectToRender.OriginalObject as WpfGeometryModel3DNode;
                    
                    if (wpfGeometryModel3DNode == null)
                        return false;

                    // NOTE:
                    // Here we do a simple check for object name in a List<string>.
                    // If you have a lot of selected objects, use HashSet<string> instead.
                    //
                    // The reason why the check is done by the object name is that this way
                    // we can simply connect the WPF object (that is selected) and the SceneNode object that is created from the WPF object.
                    // So if we define the name for the WPF objects, then the SceneNode objects that are created from them will also have the same name.
                    //
                    // But if it is not possible to name WPF objects of if you have duplicate names,
                    // then you will need to store SceneNode objects in HashSet instead of object names.
                    //
                    // To use this we need to get the SceneNode instances that are created from WPF objects.
                    // One option is to call MainDXViewportView.GetSceneNodeForWpfObject(wpfObject) for each WPF object (this must be called after the SceneNodes are initialized - for example in DXSceneInitialized).
                    // Another option is to wait until SceneNodes are created from WPF objects (in DXSceneInitialized event handler or if the scene was already created after calling MainDXViewportView.Update()).
                    // Then go through all SceneNodes in the hierarchy (you can use MainDXViewportView.DXScene.RootNode.ForEachChildNode method) 
                    // and for each WpfGeometryModel3DNode gets its GeometryModel3D and build a Dictionary with WPF (GeometryModel3D) and SceneNode (WpfGeometryModel3DNode) objects.
                    // Then when you select for which WPF objects you want to draw outline, create a HashSet<WpfGeometryModel3DNode> with list of WpfGeometryModel3DNode objects.

                    return _selectedObjectNames.Contains(wpfGeometryModel3DNode.Name);
                }
            };

            renderingStepsGroup.Children.Add(renderOutlinesObjectsRenderingStep);


            // Add two ExpandPostProcess that will make the outline bigger
            int outlineWidth = (int)OutlineSizeSlider.Value;

            _horizontalExpandPostProcess = new Ab3d.DirectX.PostProcessing.ExpandPostProcess(isVerticalRenderingPass: false, expansionWidth: outlineWidth, backgroundColor: _backgroundColor);
            _verticalExpandPostProcess   = new Ab3d.DirectX.PostProcessing.ExpandPostProcess(isVerticalRenderingPass: true, expansionWidth: outlineWidth, backgroundColor: _backgroundColor);

            _disposables.Add(_horizontalExpandPostProcess);
            _disposables.Add(_verticalExpandPostProcess);

            var expandPostProcesses = new List<PostProcessBase>();
            expandPostProcesses.Add(_horizontalExpandPostProcess);
            expandPostProcesses.Add(_verticalExpandPostProcess);


            // We could also blur the outline to make it bigger, but Expand creates better results
            //var horizontalBlurPostProcess = new Ab3d.DirectX.PostProcessing.SimpleBlurPostProcess(isVerticalBlur: false, filterWidth: 5);
            //var verticalBlurPostProcess   = new Ab3d.DirectX.PostProcessing.SimpleBlurPostProcess(isVerticalBlur: true, filterWidth: 5);
            //horizontalBlurPostProcess.InitializeResources(MainDXViewportView.DXScene.DXDevice);
            //verticalBlurPostProcess.InitializeResources(MainDXViewportView.DXScene.DXDevice);

            //blurPostProcesses.Add(horizontalBlurPostProcess);
            //blurPostProcesses.Add(verticalBlurPostProcess);


            _expandObjectsPostProcessesRenderingSteps = new RenderPostProcessingRenderingStep("Expand objects rendering step", expandPostProcesses);
            renderingStepsGroup.Children.Add(_expandObjectsPostProcessesRenderingSteps);


            MainDXViewportView.DXScene.RenderingSteps.AddAfter(MainDXViewportView.DXScene.DefaultInitializeRenderingStep, renderingStepsGroup);


            _addOutlineRenderingStep = new RenderTextureRenderingStep(RenderTextureRenderingStep.TextureChannelsCount.FourChannels, "Render outline over 3D scene")
            {
                Offsets = new Vector4(0, 0, 0, 0), // preserve original colors
                Factors = new Vector4(1, 1, 1, 1),
                TargetViewport = new ViewportF(0, 0, 1f, 1f), // render to full screen
                CustomBlendState = MainDXViewportView.DXScene.DXDevice.CommonStates.NonPremultipliedAlphaBlend, // alpha blend
                CustomDepthStencilState = _renderWhenStencilIsNotOneState, // only render when stencil value is less then 1 (not where the objects are rendered)
            };

            _addOutlineRenderingStep.BeforeRunningStep += delegate(object sender, DirectX.RenderingEventArgs args)
            {
                var renderingContext = args.RenderingContext;

                renderingContext.SetBackBuffer(renderingContext.CurrentBackBuffer, renderingContext.CurrentBackBufferDescription, renderingContext.CurrentRenderTargetView, _outlineDepthStencilView, false);
                //renderingContext.DeviceContext.OutputMerger.SetTargets(_outlineDepthStencilView, renderingContext.CurrentRenderTargetView);
            };

            MainDXViewportView.DXScene.RenderingSteps.AddBefore(MainDXViewportView.DXScene.DefaultCompleteRenderingStep, _addOutlineRenderingStep);
        }

        private void SetupOutlineBackBuffers(RenderingContext renderingContext)
        {
            var dxDevice = renderingContext.DXDevice;

            // Save current back buffer
            _savedBackBuffer            = renderingContext.CurrentBackBuffer;
            _savedBackBufferDescription = renderingContext.CurrentBackBufferDescription;
            _savedRenderTargetView      = renderingContext.CurrentRenderTargetView;
            _savedDepthStencilView      = renderingContext.CurrentDepthStencilView;
            _savedViewport              = renderingContext.CurrentViewport;

            // If size is changed, we need to recreate the back buffer
            if (_outlineBackBuffer != null &&
                (renderingContext.CurrentBackBufferDescription.Width != _outlineBackBufferDescription.Width ||
                 renderingContext.CurrentBackBufferDescription.Height != _outlineBackBufferDescription.Height))
            {
                DisposeOutlineBackBuffers();
            }

            if (_outlineBackBuffer == null)
            {
                _outlineBackBufferDescription = dxDevice.CreateTexture2DDescription(renderingContext.CurrentBackBufferDescription.Width,
                                                                                    renderingContext.CurrentBackBufferDescription.Height,
                                                                                    new SampleDescription(1, 0),
                                                                                    isRenderTarget: true,
                                                                                    isSharedResource: false,
                                                                                    isStagingTexture: false,
                                                                                    isShaderResource: true,
                                                                                    format: DXDevice.StandardBufferFormat);

                _outlineBackBuffer = dxDevice.CreateTexture2D(_outlineBackBufferDescription);

                // Create RenderTargetView and DepthStencilView for LEFT eye - the scene will be rendered to that buffer
                _outlineRenderTargetView = new RenderTargetView(dxDevice.Device, _outlineBackBuffer);


                _outlineBufferViewport = new ViewportF(0, 0, _outlineBackBufferDescription.Width, _outlineBackBufferDescription.Height);


                var resourceViewDescription = new ShaderResourceViewDescription
                {
                    Format    = _outlineBackBufferDescription.Format,
                    Dimension = ShaderResourceViewDimension.Texture2D,
                    Texture2D = new ShaderResourceViewDescription.Texture2DResource
                    {
                        MipLevels       = 1,
                        MostDetailedMip = 0
                    }
                };

                _outlineShaderResourceView = new ShaderResourceView(dxDevice.Device, _outlineBackBuffer, resourceViewDescription);

                // IMPORTANT: We need to set up DepthStencilView with 8 bytes for stencil (by default DXEngine does not use stencil buffer and use all 32 bits for depth)
                _outlineDepthStencilView = dxDevice.CreateDepthStencilView(_outlineBackBufferDescription.Width,
                                                                           _outlineBackBufferDescription.Height,
                                                                           _outlineBackBufferDescription.SampleDescription,
                                                                           Format.D24_UNorm_S8_UInt,
                                                                           "OutlineDepthStencilBuffer");

                
                // Create the same back buffers for blurred outline
                _blurredOutlineBackBuffer = dxDevice.CreateTexture2D(_outlineBackBufferDescription);
                _blurredRenderTargetView = new RenderTargetView(dxDevice.Device, _blurredOutlineBackBuffer);
                _blurredShaderResourceView = new ShaderResourceView(dxDevice.Device, _blurredOutlineBackBuffer, resourceViewDescription);

                // Render horizontal blurring pass to _blurredOutlineBackBuffer
                // and then the vertical pass back to _outlineBackBuffer.
                _expandObjectsPostProcessesRenderingSteps.InitializeResourcesForMultiplePostProcesses(
                        /* source: */             _outlineBackBuffer, _outlineBackBufferDescription, _outlineRenderTargetView, _outlineShaderResourceView,
                        /* additional buffers: */ _blurredOutlineBackBuffer, _outlineBackBufferDescription, _blurredRenderTargetView, _blurredShaderResourceView,
                        /* destination: */        _outlineBackBuffer, _outlineBackBufferDescription, _outlineRenderTargetView);

                if (MainDXViewportView.DXScene.DXDevice.IsDebugDevice)
                {
                    _outlineBackBuffer.DebugName = "OutlineBackBuffer";
                    _outlineRenderTargetView.DebugName = "OutlineRenderTargetView";
                    _outlineShaderResourceView.DebugName = "OutlineShaderResourceView";

                    _blurredOutlineBackBuffer.DebugName = "BlurredOutlineBackBuffer";
                    _blurredRenderTargetView.DebugName = "BlurredRenderTargetView";
                    _blurredShaderResourceView.DebugName = "BlurredShaderResourceView";
                }
            }

            renderingContext.SetBackBuffer(_outlineBackBuffer, _outlineBackBufferDescription, _outlineRenderTargetView, _outlineDepthStencilView, bindNewRenderTargetsToDeviceContext: true);

            renderingContext.CurrentViewport = _outlineBufferViewport;
            renderingContext.DeviceContext.Rasterizer.SetViewport(_outlineBufferViewport);

            renderingContext.DeviceContext.ClearRenderTargetView(_outlineRenderTargetView, _backgroundColor); 
            renderingContext.DeviceContext.ClearDepthStencilView(_outlineDepthStencilView, DepthStencilClearFlags.Depth | DepthStencilClearFlags.Stencil, 1.0f, 0);
        }

        private void RestoreBackBuffers(RenderingContext renderingContext)
        {
            renderingContext.SetBackBuffer(_savedBackBuffer, _savedBackBufferDescription, _savedRenderTargetView, _savedDepthStencilView, bindNewRenderTargetsToDeviceContext: true);
            renderingContext.CurrentViewport = _savedViewport;

            _savedBackBuffer       = null;
            _savedRenderTargetView = null;
            _savedDepthStencilView = null;
        }

        private void DisposeOutlineBackBuffers()
        {
            DisposeHelper.DisposeAndNullify(ref _outlineBackBuffer);
            DisposeHelper.DisposeAndNullify(ref _outlineRenderTargetView);
            DisposeHelper.DisposeAndNullify(ref _outlineDepthStencilView);
            DisposeHelper.DisposeAndNullify(ref _outlineShaderResourceView);

            DisposeHelper.DisposeAndNullify(ref _blurredOutlineBackBuffer);
            DisposeHelper.DisposeAndNullify(ref _blurredRenderTargetView);
            DisposeHelper.DisposeAndNullify(ref _blurredShaderResourceView);

            // Reset structs
            _outlineBackBufferDescription = new Texture2DDescription();
        }


        private void CreateSceneObjects()
        {
            MainViewport.Children.Clear();

            _selectedObjectNames = new List<string>();

            var rnd = new Random();

            for (int i = 0; i < 4; i++)
            {
                var boxVisual3D = new BoxVisual3D()
                {
                    CenterPosition = new Point3D(i * 50 - 100, 0, -25),
                    Size           = new Size3D(20, 20, 20),
                    Material       = new DiffuseMaterial(Brushes.Silver)
                };

                string name = "Box_" + (i + 1).ToString();
                boxVisual3D.SetName(name);
                MainViewport.Children.Add(boxVisual3D);

                var checkBox = new CheckBox()
                {
                    Content = name,
                    Tag = boxVisual3D,
                    IsChecked = rnd.Next(2) > 0
                };

                checkBox.Checked += OnModelCheckBoxCheckedChanged;
                checkBox.Unchecked += OnModelCheckBoxCheckedChanged;

                OptionsPanel.Children.Add(checkBox);

                if (checkBox.IsChecked ?? false)
                    _selectedObjectNames.Add(name);


                var sphereVisual3D = new SphereVisual3D()
                {
                    CenterPosition = new Point3D(i * 50 - 100, 0, 25),
                    Radius         = 10,
                    Material       = new DiffuseMaterial(Brushes.Silver)
                };

                name = "Sphere_" + (i + 1).ToString();
                sphereVisual3D.SetName(name);
                MainViewport.Children.Add(sphereVisual3D);

                checkBox = new CheckBox()
                {
                    Content = name,
                    Tag = sphereVisual3D,
                    IsChecked = rnd.Next(2) > 0
                };

                checkBox.Checked   += OnModelCheckBoxCheckedChanged;
                checkBox.Unchecked += OnModelCheckBoxCheckedChanged;

                OptionsPanel.Children.Add(checkBox);

                if (checkBox.IsChecked ?? false)
                    _selectedObjectNames.Add(name);
            }
        }

        private void OnModelCheckBoxCheckedChanged(object sender, RoutedEventArgs e)
        {
            var checkBox = sender as CheckBox;
            var modelVisual3D = checkBox.Tag as ModelVisual3D;

            if (modelVisual3D != null)
            {
                var modelName = modelVisual3D.GetName();

                if (checkBox.IsChecked ?? false)
                    _selectedObjectNames.Add(modelName);
                else
                    _selectedObjectNames.Remove(modelName);
            }

            MainDXViewportView.Refresh(); // Render scene again
        }

        private void OnShowOutlineCheckBoxChanged(object sender, RoutedEventArgs e)
        {
            if (!this.IsLoaded || _expandObjectsPostProcessesRenderingSteps == null)
                return;

            if (ShowOutlineCheckBox.IsChecked ?? false)
            {
                if (!_expandObjectsPostProcessesRenderingSteps.PostProcesses.Contains(_horizontalExpandPostProcess))
                {
                    _expandObjectsPostProcessesRenderingSteps.PostProcesses.Add(_horizontalExpandPostProcess);
                    _expandObjectsPostProcessesRenderingSteps.PostProcesses.Add(_verticalExpandPostProcess);
                }
            }
            else
            {
                // Disable blur
                _expandObjectsPostProcessesRenderingSteps.PostProcesses.Remove(_horizontalExpandPostProcess);
                _expandObjectsPostProcessesRenderingSteps.PostProcesses.Remove(_verticalExpandPostProcess);
            }

            MainDXViewportView.Refresh(); // Render scene again
        }

        private void OutlineSizeSlider_OnValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!this.IsLoaded)
                return;

            if (_horizontalExpandPostProcess != null)
                _horizontalExpandPostProcess.ExpansionWidth = (int)OutlineSizeSlider.Value;

            if (_verticalExpandPostProcess != null)
                _verticalExpandPostProcess.ExpansionWidth = (int)OutlineSizeSlider.Value;

            MainDXViewportView.Refresh(); // Render scene again
        }
    }
}
