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
using Ab3d.DirectX;
using Ab3d.DirectX.Effects;
using Ab3d.DirectX.PostProcessing;
using Ab3d.DXEngine.Wpf.Samples.Common;
using Ab3d.Utilities;
using Ab3d.Visuals;
using SharpDX;
using SharpDX.Direct3D11;
using Color = System.Windows.Media.Color;
using RenderingEventArgs = Ab3d.DirectX.RenderingEventArgs;

namespace Ab3d.DXEngine.Wpf.Samples.DXEngineVisuals
{
    /// <summary>
    /// Interaction logic for ObjectOutlinesRenderingSample.xaml
    /// </summary>
    public partial class ObjectOutlinesRenderingSample : Page
    {
        private string _fileName;

        private DisposeList _disposables;

        private SoberEdgeDetectionPostProcess _edgeDetectionPostProcess;
        private SolidColorEffect _blackOutlineEffect;
        private RenderObjectsRenderingStep _renderObjectOutlinesRenderingStep;
        private DepthStencilView _savedDepthStencilView;
        private PreparePostProcessingRenderingStep _prepareExpandObjectsPostProcessingRenderingStep;
        private RenderPostProcessingRenderingStep _expandObjectsPostProcessesRenderingSteps;
        private ExpandPostProcess _horizontalExpandPostProcess;
        private ExpandPostProcess _verticalExpandPostProcess;

        public ObjectOutlinesRenderingSample()
        {
            InitializeComponent();


            OutlineThicknessComboBox.ItemsSource  = new float[] {0f, 0.1f, 0.5f, 1f, 2f, 3f, 4f, 5f, 10f, 20f};
            OutlineThicknessComboBox.SelectedItem = 3.0f;

            OutlineDepthBiasComboBox.ItemsSource  = new float[] {0f, 0.0001f, 0.0005f, 0.001f, 0.005f, 0.01f, 0.02f, 0.01f, 0.1f, 0.2f, 0.3f, 0.5f, 0.8f, 0.9f, 1f, 2f, 5f, 10f};
            OutlineDepthBiasComboBox.SelectedItem = 0.005f;

            EdgeThresholdComboBox.ItemsSource  = new float[] { 0f, 0.01f, 0.02f, 0.01f, 0.1f, 0.2f, 0.3f, 0.5f, 0.8f, 0.9f, 1f };
            EdgeThresholdComboBox.SelectedItem = 0.1f;

            OutlineWidthComboBox.ItemsSource  = new int[] { 1, 2, 3, 4, 5, 7, 10, 12, 16 };
            OutlineWidthComboBox.SelectedItem = 3;



            ShowObjectOutlineInfoControl.InfoText =
@"A new RenderObjectsRenderingStep is created that renders all objects with SolidColorEffect with black color. The SolidColorEffect has the OutlineThickness property set and this expands the object's geometry in the direction of triangle normals (this is done in vertex shader). Then the scene is rendered again with standard effects on top of the black expanded scene.

To render outline around each object uncheck the 'Outline CullNone' and 'WriteMaxDepthValue' CheckBoxes. But on simpler objects with less triangles this may not render outlines in all directions.

To render silhouette around the whole 3D scene and not around each object, then check the 'Outline CullNone' and 'WriteMaxDepthValue' CheckBoxes. The first CheckBox will render back and front triangles. The second will render the objects to the back of the 3D scene (after all other objects).

This technique works well on objects that have a lot of smooth shaded positions and do not have sharp edges with big angles - for example rendering box with bigger OutlineThickness can show that the black sides are rendered away from the actual 3D object.
When 3D scene has many 3D objects, then using the technique can affect performance because the scene needs to be rendered 2 times (once for black background and once for standard rendering). This technique works with multi-sampling and super-sampling.";


            ExpandPostProcessInfoControl.InfoText =
@"This technique is similar than the previous technique because it also first renders the scene with a black SolidColorEffect. But instead of expanding the object's geometry, the scene is rendered normally with black color. Then an ExpandPostProcess is used to expand the rendered black scene for the specified amount.

This technique can produce more accurate results then using SolidColorEffect with OutlineThickness (especially for objects with sharp edges). But it is the slowest because it requires to render the scene again and then perform a two pass post-processing. This technique is not support when multi-sampling is used.";


            EdgeDetectionPostProcessInfoControl.InfoText =
@"EdgeDetectionPostProcess is a 2D texture post process that uses Sobel edge detection algorithm to detect edges based on the changes of color in the rendered 3D scene - if colors are different enough then a black edge is added to the rendered image.

If the rendered scene has many 3D objects, then this algorithm is the fastest because it does not require to render the 3D scene again. This technique does not support multi-sampling and super-sampling (executed after back-buffer is resolved).";



            AssimpLoader.LoadAssimpNativeLibrary();

            var dragAndDropHelper = new DragAndDropHelper(this, ".*");
            dragAndDropHelper.FileDropped += (sender, args) => LoadModel(args.FileName);

            string startupFileName = AppDomain.CurrentDomain.BaseDirectory + @"Resources\Models\house with trees.3ds";
            LoadModel(startupFileName);


            _disposables = new DisposeList();

            MainDXViewportView.DXSceneInitialized += delegate (object sender, EventArgs args)
            {
                // Wait until DXScene is created and then setup custom rendering steps to render outlines
                if (MainDXViewportView.DXScene == null)
                    return; // WPF 3D rendering

                if (MainDXViewportView.DXScene.UsedMultisamplingDescription.Count > 1) // Expand post process is available only without multi-sampling (MSAA)
                {
                    ExpandPostProcessCheckBox.IsChecked = false;
                    ExpandPostProcessCheckBox.IsEnabled = false;
                    OutlineWidthComboBox.IsEnabled      = false;

                    ExpandNotSupportedTextBlock.Visibility = Visibility.Visible;
                }

                UpdateUsedOutlineTechnique();
            };

            this.Unloaded += delegate (object sender, RoutedEventArgs args)
            {
                if (_disposables != null)
                {
                    _disposables.Dispose();
                    _disposables = null;
                }

                MainDXViewportView.Dispose();
            };
        }

        private void LoadModel(string fileName)
        {
            MainViewport.Children.Clear();

            // Create an instance of AssimpWpfImporter
            var assimpWpfImporter = new AssimpWpfImporter();
            var readModel3D = assimpWpfImporter.ReadModel3D(fileName, texturesPath: null); // we can also define a textures path if the textures are located in some other directory (this is parameter can be skipped, but is defined here so you will know that you can use it)


            if (readModel3D == null)
            {
                MessageBox.Show("Cannot read file");
                return;
            }

            MainViewport.Children.Add(readModel3D.CreateModelVisual3D());


            if (_fileName != fileName) // Reset camera only when the file is loaded for the first time
            {
                _fileName = fileName;

                Camera1.TargetPosition = readModel3D.Bounds.GetCenterPosition();
                Camera1.Distance = readModel3D.Bounds.GetDiagonalLength() * 1.2;
            }

            // Add ambient light
            var ambientLight = new AmbientLight(Color.FromRgb(100, 100, 100));
            MainViewport.Children.Add(ambientLight.CreateModelVisual3D());
        }


        private void SetupSolidColorEffectWithOutlines()
        {
            // One way to show object outlines is to render the whole scene with black color (using SolidColorEffect)
            // and with expanding the geometry of each object in the direction of triangle normals (setting SolidColorEffect.OutlineThickness property)
            // Then the standard 3D scene is rendered on top of the black 3D scene.
            var dxScene = MainDXViewportView.DXScene;

            _blackOutlineEffect = EnsureSolidColorEffect();

            // Set _blackOutlineEffect properties based on the user controls
            UpdateBlackOutlineEffectForOutlineThickness();

            // Add another RenderObjectsRenderingStep that will render black scene ...
            _renderObjectOutlinesRenderingStep = EnsureRenderObjectsRenderingStep(dxScene);

            // ... and add it before standard RenderObjectsRenderingStep
            if (dxScene != null && !dxScene.RenderingSteps.Contains(_renderObjectOutlinesRenderingStep))
                dxScene.RenderingSteps.AddBefore(dxScene.DefaultRenderObjectsRenderingStep, _renderObjectOutlinesRenderingStep);
        }

        private void DisableSolidColorEffectWithOutlines()
        {
            var dxScene = MainDXViewportView.DXScene;
            if (_renderObjectOutlinesRenderingStep != null && dxScene != null && dxScene.RenderingSteps.Contains(_renderObjectOutlinesRenderingStep))
                dxScene.RenderingSteps.Remove(_renderObjectOutlinesRenderingStep);
        }


        private void SetupExpandPostProcessOutlines()
        {
            var dxScene = MainDXViewportView.DXScene;

            _blackOutlineEffect = EnsureSolidColorEffect();

            if (_blackOutlineEffect == null)
                return;

            // Reset values that may be changed when using SolidColorEffectWithOutlines
            _blackOutlineEffect.DepthBias = 0;
            _blackOutlineEffect.OverrideRasterizerState = null;
            _blackOutlineEffect.OutlineThickness = 0;
            _blackOutlineEffect.WriteMaxDepthValue = true;

            _renderObjectOutlinesRenderingStep = EnsureRenderObjectsRenderingStep(dxScene);

            if (!dxScene.RenderingSteps.Contains(_renderObjectOutlinesRenderingStep))
                dxScene.RenderingSteps.AddBefore(dxScene.DefaultRenderObjectsRenderingStep, _renderObjectOutlinesRenderingStep);


            int outlineWidth = (int)OutlineWidthComboBox.SelectedItem;

            if (_prepareExpandObjectsPostProcessingRenderingStep == null)
            {
                // Expand post process is done in two passes (one horizontal and one vertical)
                _horizontalExpandPostProcess = new Ab3d.DirectX.PostProcessing.ExpandPostProcess(isVerticalRenderingPass: false, expansionWidth: outlineWidth, backgroundColor: dxScene.BackgroundColor);
                _verticalExpandPostProcess   = new Ab3d.DirectX.PostProcessing.ExpandPostProcess(isVerticalRenderingPass: true,  expansionWidth: outlineWidth, backgroundColor: dxScene.BackgroundColor);

                _disposables.Add(_horizontalExpandPostProcess);
                _disposables.Add(_verticalExpandPostProcess);

                var expandPostProcesses = new PostProcessBase[]
                {
                    _horizontalExpandPostProcess, 
                    _verticalExpandPostProcess
                };

                // To execute the post processes we need to rendering steps:
                // 1) PreparePostProcessingRenderingStep that creates required RenderTargets and ShaderResourceViews and sets that to the RenderPostProcessingRenderingStep
                // 2) RenderPostProcessingRenderingStep that actually executed all the post-processes

                // Because we will execute post-processes before the standard scene rendering, 
                // we also need to make sure that the Destination buffer is correctly set (see _prepareExandObjectsPostProcessingRenderingStep.BeforeRunningStep)
                // and that the DepthStencilView is reset after the post-processes are rendered (see _expandObjectsPostProcessesRenderingSteps.AfterRunningStep).

                // First create the RenderPostProcessingRenderingStep because it is needed in the constructor of the PreparePostProcessingRenderingStep
                _expandObjectsPostProcessesRenderingSteps = new RenderPostProcessingRenderingStep("Expand objects rendering step", expandPostProcesses);
                _expandObjectsPostProcessesRenderingSteps.AfterRunningStep += delegate(object sender, RenderingEventArgs args)
                {
                    // Post-processes are usually executed at the end of rendering process and work on 2D textures so they do not require DepthStencil.
                    // The CurrentBackBuffer / Description and RenderTargetView are already correct because they are sent by PreparePostProcessingRenderingStep,
                    // but we need to set the _savedDepthStencilView and SupersamplingCount.
                    args.RenderingContext.SetBackBuffer(args.RenderingContext.CurrentBackBuffer,
                                                        args.RenderingContext.CurrentBackBufferDescription,
                                                        args.RenderingContext.CurrentRenderTargetView,
                                                        _savedDepthStencilView,
                                                        dxScene.SupersamplingCount, 
                                                        bindNewRenderTargetsToDeviceContext: true);
                };

                _disposables.Add(_expandObjectsPostProcessesRenderingSteps);


                _prepareExpandObjectsPostProcessingRenderingStep = new PreparePostProcessingRenderingStep(_expandObjectsPostProcessesRenderingSteps, "Prepare expand post process");
                _prepareExpandObjectsPostProcessingRenderingStep.BeforeRunningStep += delegate(object sender, RenderingEventArgs args)
                {
                    // Because after the post-processes are executed we will continue with rendering the scene, 
                    // we need to set the DestinationBackBuffer (there will be the final result of the post-processes)
                    // to the currently used BackBuffer. If this is not done, then RenderingContext.FinalBackBuffer is used as destination back buffer.
                    _prepareExpandObjectsPostProcessingRenderingStep.SetCustomDestinationBackBuffer(args.RenderingContext.CurrentBackBuffer, 
                                                                                                    args.RenderingContext.CurrentBackBufferDescription, 
                                                                                                    args.RenderingContext.CurrentRenderTargetView);

                    // Save CurrentDepthStencilView
                    _savedDepthStencilView = args.RenderingContext.CurrentDepthStencilView;
                };

                _disposables.Add(_prepareExpandObjectsPostProcessingRenderingStep);
            }
            else
            {
                _horizontalExpandPostProcess.ExpansionWidth = outlineWidth;
                _verticalExpandPostProcess.ExpansionWidth   = outlineWidth;
            }


            if (!dxScene.RenderingSteps.Contains(_prepareExpandObjectsPostProcessingRenderingStep))
            {
                dxScene.RenderingSteps.AddAfter(_renderObjectOutlinesRenderingStep,  _prepareExpandObjectsPostProcessingRenderingStep);
                dxScene.RenderingSteps.AddAfter(_prepareExpandObjectsPostProcessingRenderingStep, _expandObjectsPostProcessesRenderingSteps);
            }
        }

        private void DisableExpandPostProcessOutlines()
        {
            var dxScene = MainDXViewportView.DXScene;

            if (_prepareExpandObjectsPostProcessingRenderingStep != null && dxScene.RenderingSteps.Contains(_prepareExpandObjectsPostProcessingRenderingStep))
                dxScene.RenderingSteps.Remove(_prepareExpandObjectsPostProcessingRenderingStep);

            if (_expandObjectsPostProcessesRenderingSteps != null && dxScene.RenderingSteps.Contains(_expandObjectsPostProcessesRenderingSteps))
                dxScene.RenderingSteps.Remove(_expandObjectsPostProcessesRenderingSteps);
        }


        private void SetupEdgeDetectionPostProcess()
        {
            if (_edgeDetectionPostProcess == null)
            {
                _edgeDetectionPostProcess = new Ab3d.DirectX.PostProcessing.SoberEdgeDetectionPostProcess
                {
                    // EdgeThreshold defines how big the color difference need to be to show na edge.
                    // 0.5f requires quite big change and this is ok because we already show edge lines
                    // and we only want to add lines for silhouettes of curved objects were no edge lines are defined.
                    EdgeThreshold = (float)EdgeThresholdComboBox.SelectedItem,

                    // Show edges on top of the existing rendering
                    MultiplyWithCurrentColor = true
                };
                
                _disposables.Add(_edgeDetectionPostProcess);
            }

            var dxScene = MainDXViewportView.DXScene;
            if (dxScene != null && !dxScene.PostProcesses.Contains(_edgeDetectionPostProcess))
                dxScene.PostProcesses.Add(_edgeDetectionPostProcess);
        }

        private void DisableEdgeDetectionPostProcess()
        {
            var dxScene = MainDXViewportView.DXScene;
            if (_edgeDetectionPostProcess != null && dxScene != null && dxScene.PostProcesses.Contains(_edgeDetectionPostProcess))
                dxScene.PostProcesses.Remove(_edgeDetectionPostProcess);
        }


        private SolidColorEffect EnsureSolidColorEffect()
        {
            if (MainDXViewportView.DXScene == null)
                return null;

            if (_blackOutlineEffect == null)
            {
                _blackOutlineEffect = new SolidColorEffect()
                {
                    Color              = Color4.Black,
                    OverrideModelColor = true, // This will render all objects with Black color; when false then object's color is used
                };

                _disposables.Add(_blackOutlineEffect);
                _blackOutlineEffect.InitializeResources(MainDXViewportView.DXScene.DXDevice);
            }

            return _blackOutlineEffect;
        }

        private void UpdateBlackOutlineEffectForOutlineThickness()
        {
            if (_blackOutlineEffect == null)
                return;

            _blackOutlineEffect.OutlineThickness        = (float)OutlineThicknessComboBox.SelectedItem;
            _blackOutlineEffect.DepthBias               = -((float)OutlineDepthBiasComboBox.SelectedItem); // Negate DepthBias to render solid color objects farther away from camera
            _blackOutlineEffect.WriteMaxDepthValue      = WriteMaxDepthValueCheckBox.IsChecked ?? false;
            _blackOutlineEffect.OverrideRasterizerState = (OutlineCullNoneCheckBox.IsChecked ?? false) ? MainDXViewportView.DXScene.DXDevice.CommonStates.CullNone : null;
        }

        private RenderObjectsRenderingStep EnsureRenderObjectsRenderingStep(DXScene dxScene)
        {
            if (_renderObjectOutlinesRenderingStep == null)
            {
                _renderObjectOutlinesRenderingStep = new RenderObjectsRenderingStep("RenderObjectOutlinesRenderingStep")
                {
                    OverrideEffect = _blackOutlineEffect,
                    FilterRenderingQueuesFunction = delegate (RenderingQueue queue)
                    {
                        // For test we will not render 3D lines (only all other objects)
                        return (queue != dxScene.LineGeometryRenderingQueue);
                    }
                };

                _disposables.Add(_renderObjectOutlinesRenderingStep);
            }

            return _renderObjectOutlinesRenderingStep;
        }


        private void OnOutlineSettingsChanged(object sender, RoutedEventArgs e)
        {
            if (!this.IsLoaded)
                return;

            UpdateBlackOutlineEffectForOutlineThickness();

            MainDXViewportView.Refresh();
        }
        
        private void OnShowSolidObjectCheckBoxCheckedChanged(object sender, RoutedEventArgs e)
        {
            if (!this.IsLoaded || MainDXViewportView.DXScene == null)
                return;

            // Enable / disable standard rendering of 3D objects
            MainDXViewportView.DXScene.DefaultRenderObjectsRenderingStep.IsEnabled = ShowSolidObjectCheckBox.IsChecked ?? false;

            MainDXViewportView.Refresh();
        }

        private void EdgeThresholdComboBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!this.IsLoaded)
                return;

            _edgeDetectionPostProcess.EdgeThreshold = (float) EdgeThresholdComboBox.SelectedItem;

            MainDXViewportView.Refresh(); // Render the scene again
        }

        private void OnOutlineTypeChanged(object sender, RoutedEventArgs e)
        {
            if (!this.IsLoaded)
                return;

            UpdateUsedOutlineTechnique();

            MainDXViewportView.Refresh(); // Render the scene again
        }

        private void UpdateUsedOutlineTechnique()
        {
            DisableSolidColorEffectWithOutlines();
            DisableExpandPostProcessOutlines();
            DisableEdgeDetectionPostProcess();

            if (EdgeDetectionPostProcessCheckBox.IsChecked ?? false)
                SetupEdgeDetectionPostProcess();

            if (ExpandPostProcessCheckBox.IsChecked ?? false)
                SetupExpandPostProcessOutlines();

            if (SolidColorEffectWithOutlinesCheckBox.IsChecked ?? false)
                SetupSolidColorEffectWithOutlines();
        }

        private void OutlineWidthComboBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!this.IsLoaded || _horizontalExpandPostProcess == null)
                return;

            int outlineWidth = (int)OutlineWidthComboBox.SelectedItem;

            _horizontalExpandPostProcess.ExpansionWidth = outlineWidth;
            _verticalExpandPostProcess.ExpansionWidth   = outlineWidth;

            MainDXViewportView.Refresh(); // Render the scene again
        }
    }
}
