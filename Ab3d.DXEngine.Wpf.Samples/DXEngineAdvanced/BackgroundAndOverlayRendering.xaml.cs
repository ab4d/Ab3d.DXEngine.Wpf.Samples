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
using Ab3d.Common;
using Ab3d.DirectX;
using Ab3d.DirectX.Materials;
using Ab3d.Visuals;
using SharpDX;
using SharpDX.Direct3D11;
using Color = System.Windows.Media.Color;
using RenderingEventArgs = Ab3d.DirectX.RenderingEventArgs;

namespace Ab3d.DXEngine.Wpf.Samples.DXEngineAdvanced
{
    /// <summary>
    /// Interaction logic for BackgroundAndOverlayRendering.xaml
    /// </summary>
    public partial class BackgroundAndOverlayRendering : Page
    {
        private DisposeList _disposables;
        private RenderObjectsRenderingStep _backgroundRenderObjectsRenderingStep;
        private RenderObjectsRenderingStep _alwaysOnTopRenderObjectsRenderingStep;

        public BackgroundAndOverlayRendering()
        {
            InitializeComponent();

            _disposables = new DisposeList();

            ClearDepthBufferInfoControl.InfoText =
@"The most easy to use and correct method to render objects in background and in overlay is to clear the depth buffer. Depth buffer is used to determine the distance of the object to the camera. This prevents rendering object that are farther away from the camera. When depth buffer is cleared, then the objects that are rendered after clearing the depth buffer will be always rendered and will not be hidden by the objects rendered before.

To render background objects, we set ClearDepthStencilBufferAfterRendering to true on the BackgroundRenderingQueue.
To render overlay objects, we set ClearDepthStencilBufferBeforeRendering to true on the OverlayRenderingQueue.

Note that 3D lines can rendered as background or overlay just by setting the ReadZBuffer and WriteZBuffer and do not require clearing depth buffer.";

            DisableDepthReadInfoControl.InfoText =
@"Another option to always render objects (overlay objects) is to prevent reading the depth values from depth buffer. Similarly, it is possible to render background objects by preventing writing their depth values to depth buffer. To achieve that new RenderingSteps need to be added. See the SetupDisabledDepthRead method for more information.

A drawback of this technique is that all triangles are always rendered - even the triangles that should be hidden (see the dragon model).
Therefor this method should be used only when rendering simple 3D objects.
This method is faster then clearing depth buffer because the clearing step is not needed.";

            OverlayHitTestingInfoControl.InfoText =
@"When getting the closest hit object (object under the mouse), the hit objects are sorted by their distance to the camera. When rendering overlay objects, it is possible to prioritize those objects, so that they will be always considered closer to the camera than other objects.

This is done by setting the DXScene.DXHitTestOptions.OverlayRenderingQueue to the OverlayRenderingQueue.

To demonstrate that, rotate the camera around the red dragon model - even if some yellow boxes are closer to the camera, the rotation will be done around the dragon model.";

            BackgroundHitTestingInfoControl.InfoText =
@"When getting the closest hit object (object under the mouse), the hit objects are sorted by their distance to the camera. When rendering background objects, it is possible to lower the priority of those objects, so that they will be always considered farther from to the camera than other objects.

This is done by setting the DXScene.DXHitTestOptions.BackgroundRenderingQueue to the BackgroundRenderingQueue.

To demonstrate that, rotate the camera around the yellow boxes that are in 3D space behind the blue dragon model - even if the blue dragon is closer to the camera, the rotation will be done around the yellow boxes.";


            MainDXViewportView.DXSceneInitialized += delegate (object sender, EventArgs args)
            {
                if (MainDXViewportView.DXScene == null)
                    return; // Probably WPF 3D rendering


                SetupClearingDepthBuffer();
                UpdateHitTestingOverlay();
                UpdateHitTestingBackground();

                AddStandardRenderedObjects();

                //PrepareAlwaysOnTopRendering();
                AddCustomRenderedObjects();

                AddCustomRenderedLines();
            };


            // IMPORTANT:
            // It is very important to call Dispose method on DXSceneView after the control is not used any more (see help file for more info)
            this.Unloaded += delegate
            {
                if (_disposables != null)
                {
                    _disposables.Dispose();
                    _disposables = null;
                }

                MainDXViewportView.Dispose();
            };
        }

        private void AddCustomRenderedObjects()
        {
            var readerObj = new Ab3d.ReaderObj();
            var originalDragonModel3D = readerObj.ReadModel3D(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"Resources\Models\dragon_vrip_res3.obj")) as GeometryModel3D;

            if (originalDragonModel3D == null)
                return;


            var meshGeometry3D = (MeshGeometry3D) originalDragonModel3D.Geometry;

            // Update positions in MeshGeometry3D to a desired size
            var transformedMeshGeometry3D = Ab3d.Utilities.MeshUtils.PositionAndScaleMeshGeometry3D(meshGeometry3D,
                                                                                                    position: new Point3D(0, 0, 0),
                                                                                                    positionType: PositionTypes.Bottom,
                                                                                                    finalSize: new Size3D(50, 50, 50),
                                                                                                    preserveAspectRatio: true,
                                                                                                    transformNormals: true);

            var backgroundDragonModel3D = new GeometryModel3D(transformedMeshGeometry3D, new DiffuseMaterial(Brushes.Blue));
            backgroundDragonModel3D.Transform = new TranslateTransform3D(-30, 0, 0);

            // We could also use:
            //var backgroundDragonModel3D = originalDragonModel3D.Clone();
            //Ab3d.Utilities.ModelUtils.ChangeMaterial(backgroundDragonModel3D, newMaterial: new DiffuseMaterial(Brushes.Blue), newBackMaterial: null);
            //Ab3d.Utilities.TransformationsHelper.AddTransformation(backgroundDragonModel3D, new TranslateTransform3D(-30, 0, 0));

            AddBackgroundObject(backgroundDragonModel3D);


            var overlayDragonModel3D = new GeometryModel3D(transformedMeshGeometry3D, new DiffuseMaterial(Brushes.Red));
            overlayDragonModel3D.Transform = new TranslateTransform3D(30, 0, 0);

            AddOverlayObject(overlayDragonModel3D);
        }

        private void AddCustomRenderedLines()
        {
            // First add 3D lines that are rendered without any special setting
            // readZBuffer and writeZBuffer will be set to true so they will "obey" the depth rules - will be hidden behind objects closer to the camera.
            AddLines(new Point3D(-40, 0, 30), 
                     positionsCount: 10, 
                     lineColor: Colors.Yellow, 
                     readZBuffer: true, 
                     writeZBuffer: true,
                     customRenderingQueue: null);


            // Add 3D lines to the background
            // The most important is to render those lines before any other objects are rendered.
            // This is done with setting CustomRenderingQueue property where we specify the BackgroundRenderingQueue.
            // We can also disable depth reading and writing (to render them regardless of any previously rendered objects).
            AddLines(new Point3D(-90, 0, 30), 
                     positionsCount: 10, 
                     lineColor: Colors.Blue, 
                     readZBuffer: false, 
                     writeZBuffer: false, 
                     customRenderingQueue: MainDXViewportView.DXScene.BackgroundRenderingQueue);


            // Add 3D lines that will be rendered on top of other 3D objects.
            // This is achieved with disabling depth reading and writing.
            // We also put that line into the OverlayRenderingQueue
            // (though this is not needed, because the ThickLineEffect that renders the 3D lines can use the ReadZBuffer and WriteZBuffer values from LineMaterial)
            AddLines(new Point3D(20, 0, 30), 
                     positionsCount: 10, 
                     lineColor: Colors.Red, 
                     readZBuffer: false, 
                     writeZBuffer: false, 
                     customRenderingQueue: MainDXViewportView.DXScene.OverlayRenderingQueue);


            //// Instead of using ScreenSpaceLineNode and LineMaterial that support ReadZBuffer and WriteZBuffer,
            //// we could also use out custom rendering steps and then define standard WPF lines
            //// and use SetDXAttribute to set CustomRenderingQueue to BackgroundRenderingQueue or OverlayRenderingQueue:
            //var lineVisual3D = new LineVisual3D()
            //{
            //    StartPosition = new Point3D(-100, 10, 20),
            //    EndPosition   = new Point3D(100,  10, 20),
            //    LineColor     = Colors.Orange,
            //    LineThickness = 10
            //};

            ////lineVisual3D.SetDXAttribute(DXAttributeType.CustomRenderingQueue, MainDXViewportView.DXScene.BackgroundRenderingQueue);
            //lineVisual3D.SetDXAttribute(DXAttributeType.CustomRenderingQueue, MainDXViewportView.DXScene.OverlayRenderingQueue);

            //MainViewport.Children.Add(lineVisual3D);


            // Using SetDXAttribute will not work on TextVisual3D, CenteredTextVisual3D and WireGridVisual3D
            // (the reason is that those objects create a more complex hierarchy and the DXAttribute are not propagated to the child objects).
            // To solve this you can use the following trick:

            //var textVisual3D = new TextVisual3D()
            //{
            //    Position = new Point3D(-90, 50, -20),
            //    Text = "TextVisual3D",
            //    FontSize = 30,
            //    LineThickness = 4,
            //    TextColor = Colors.Silver
            //};

            //// OnDXResourcesInitializedAction is called when the DXEngine creates the SceneNode from the and initializes it (so its children are created)
            //textVisual3D.SetDXAttribute(DXAttributeType.OnDXResourcesInitializedAction, new Action<object>(node =>
            //{
            //    var sceneNode = node as SceneNode;
            //    if (sceneNode == null)
            //        return;

            //    sceneNode.ForEachChildNode(new Action<SceneNode>(childSceneNode =>
            //    {
            //        var screenSpaceLineNode = childSceneNode as ScreenSpaceLineNode;
            //        if (screenSpaceLineNode != null)
            //            screenSpaceLineNode.CustomRenderingQueue = MainDXViewportView.DXScene.OverlayRenderingQueue;
            //    }));
            //}));
        }

        private void AddLines(Point3D startPosition, int positionsCount, Color lineColor, bool readZBuffer = true, bool writeZBuffer = true, RenderingQueue customRenderingQueue = null)
        {
            Vector3[] positions = new Vector3[positionsCount * 2];
            Vector3 position = startPosition.ToVector3();

            int index = 0;
            for (int i = 0; i < positionsCount; i++)
            {
                positions[index] = position;
                positions[index + 1] = position + new Vector3(40, 0, 0);

                index += 2;
                position += new Vector3(0, 0, 10);
            }

            // ThickLineEffect that renders the 3D lines can use the ReadZBuffer and WriteZBuffer values from LineMaterial.
            //
            // When ReadZBuffer is false (true by default), then line is rendered without checking the depth buffer -
            // so it is always rendered even it is is behind some other 3D object and should not be visible from the camera).
            //
            // When WriteZBuffer is false (true by default), then when rendering the 3D line, the depth of the line is not
            // written to the depth buffer. So No other object will be made hidden by the line even if that object is behind the line.
            var lineMaterial = new LineMaterial()
            {
                LineColor     = lineColor.ToColor4(),
                LineThickness = 2,
                ReadZBuffer   = readZBuffer, 
                WriteZBuffer  = writeZBuffer
            };

            _disposables.Add(lineMaterial);


            var screenSpaceLineNode = new ScreenSpaceLineNode(positions, isLineStrip: false, isLineClosed: false, lineMaterial: lineMaterial);
            
            // It is also needed that the 3D line is put to the Background or Overlay rendering queue so that it is rendered before or after other 3D objects.
            screenSpaceLineNode.CustomRenderingQueue = customRenderingQueue;

            var sceneNodeVisual3D = new SceneNodeVisual3D(screenSpaceLineNode);
            MainViewport.Children.Add(sceneNodeVisual3D);
        }

        private void AddBackgroundObject(Model3D geometryModel3D)
        {
            geometryModel3D.SetDXAttribute(DXAttributeType.CustomRenderingQueue, MainDXViewportView.DXScene.BackgroundRenderingQueue);

            var modelVisual3D = geometryModel3D.CreateModelVisual3D();
            MainViewport.Children.Add(modelVisual3D);
        }

        private void AddOverlayObject(Model3D geometryModel3D)
        {
            geometryModel3D.SetDXAttribute(DXAttributeType.CustomRenderingQueue, MainDXViewportView.DXScene.OverlayRenderingQueue);

            var modelVisual3D = geometryModel3D.CreateModelVisual3D();
            MainViewport.Children.Add(modelVisual3D);
        }

        private void AddOverlayObject(ModelVisual3D modelVisual3D)
        {
            modelVisual3D.SetDXAttribute(DXAttributeType.CustomRenderingQueue, MainDXViewportView.DXScene.OverlayRenderingQueue);
            MainViewport.Children.Add(modelVisual3D);
        }

        private void AddStandardRenderedObjects()
        {
            for (int x = -3; x <= 3; x++)
            {
                for (int y = -3; y <= 3; y++)
                {
                    var boxVisual3D = new BoxVisual3D()
                    {
                        CenterPosition = new Point3D(x * 30, 0, y * 30),
                        Size = new Size3D(10, 10, 10),
                        Material = new DiffuseMaterial(Brushes.Yellow)
                    };

                    MainViewport.Children.Add(boxVisual3D);
                }
            }
        }


        private void SetupClearingDepthBuffer()
        {
            var dxScene = MainDXViewportView.DXScene;

            dxScene.BackgroundRenderingQueue.ClearDepthStencilBufferAfterRendering = true;
            dxScene.OverlayRenderingQueue.ClearDepthStencilBufferBeforeRendering = true;

            // Instead of using ClearDepthStencilBufferAfterRendering and ClearDepthStencilBufferBeforeRendering
            // the same can be also achieved by adding new RenderingSteps (see SetupDisabledDepthRead)
            // and then using the following AfterRunningStep and BeforeRunningStep:

            //_backgroundRenderObjectsRenderingStep.AfterRunningStep += delegate (object sender, RenderingEventArgs args)
            //{
            //    var renderingContext = args.RenderingContext;

            //    if (renderingContext.DXScene.BackgroundRenderingQueue.Count > 0) // Only clear depth buffer when we have actually render any objects
            //        renderingContext.DeviceContext.ClearDepthStencilView(renderingContext.CurrentDepthStencilView, DepthStencilClearFlags.Depth, 1.0f, 0);
            //};

            //_alwaysOnTopRenderObjectsRenderingStep.BeforeRunningStep += delegate (object sender, RenderingEventArgs args)
            //{
            //    var renderingContext = args.RenderingContext;

            //    if (renderingContext.DXScene.OverlayRenderingQueue.Count > 0) // Only clear depth buffer when we will actually render any objects
            //        renderingContext.DeviceContext.ClearDepthStencilView(renderingContext.CurrentDepthStencilView, DepthStencilClearFlags.Depth, 1.0f, 0);
            //};

            DisableDisabledDepthRead();
        }
        
        private void DisableClearingDepthBuffer()
        {
            var dxScene = MainDXViewportView.DXScene;

            dxScene.BackgroundRenderingQueue.ClearDepthStencilBufferAfterRendering = false;
            dxScene.OverlayRenderingQueue.ClearDepthStencilBufferBeforeRendering = false;
        }

        private void SetupDisabledDepthRead()
        {
            var dxScene = MainDXViewportView.DXScene;

            if (_backgroundRenderObjectsRenderingStep == null)
            {
                _backgroundRenderObjectsRenderingStep = new RenderObjectsRenderingStep("BackgroundRenderingStep");
                _backgroundRenderObjectsRenderingStep.OverrideDepthStencilState = MainDXViewportView.DXScene.DXDevice.CommonStates.DepthNone; // Default state is DepthReadWrite

                // This RenderObjectsRenderingStep will render only objects inside ForegroundRenderingQueue
                _backgroundRenderObjectsRenderingStep.FilterRenderingQueuesFunction = queue => queue == dxScene.BackgroundRenderingQueue;

                dxScene.RenderingSteps.AddBefore(dxScene.DefaultRenderObjectsRenderingStep, _backgroundRenderObjectsRenderingStep);
            }

            if (_alwaysOnTopRenderObjectsRenderingStep == null)
            {
                _alwaysOnTopRenderObjectsRenderingStep = new RenderObjectsRenderingStep("AlwaysOnTopRenderingStep");
                _alwaysOnTopRenderObjectsRenderingStep.OverrideDepthStencilState = MainDXViewportView.DXScene.DXDevice.CommonStates.DepthNone; // Default state is DepthReadWrite

                // This RenderObjectsRenderingStep will render only objects inside ForegroundRenderingQueue
                _alwaysOnTopRenderObjectsRenderingStep.FilterRenderingQueuesFunction = queue => queue == dxScene.OverlayRenderingQueue;

                dxScene.RenderingSteps.AddAfter(dxScene.DefaultRenderObjectsRenderingStep, _alwaysOnTopRenderObjectsRenderingStep);
            }

            dxScene.DefaultRenderObjectsRenderingStep.FilterRenderingQueuesFunction = queue => queue != dxScene.BackgroundRenderingQueue &&
                                                                                               queue != dxScene.OverlayRenderingQueue;


            DisableClearingDepthBuffer();
        }
        
        private void DisableDisabledDepthRead()
        {
            var dxScene = MainDXViewportView.DXScene;

            if (_backgroundRenderObjectsRenderingStep != null)
            {
                dxScene.RenderingSteps.Remove(_backgroundRenderObjectsRenderingStep);
                _backgroundRenderObjectsRenderingStep = null;
            }

            if (_alwaysOnTopRenderObjectsRenderingStep != null)
            {
                dxScene.RenderingSteps.Remove(_alwaysOnTopRenderObjectsRenderingStep);
                _alwaysOnTopRenderObjectsRenderingStep = null;
            }

            dxScene.DefaultRenderObjectsRenderingStep.FilterRenderingQueuesFunction = null;
        }

        private void UpdateHitTestingOverlay()
        {
            if (OverlayHitTestingCheckBox.IsChecked ?? false)
                MainDXViewportView.DXScene.DXHitTestOptions.OverlayRenderingQueue = MainDXViewportView.DXScene.OverlayRenderingQueue;
            else
                MainDXViewportView.DXScene.DXHitTestOptions.OverlayRenderingQueue = null;
        }
        
        private void UpdateHitTestingBackground()
        {
            if (BackgroundHitTestingCheckBox.IsChecked ?? false)
                MainDXViewportView.DXScene.DXHitTestOptions.BackgroundRenderingQueue = MainDXViewportView.DXScene.BackgroundRenderingQueue;
            else
                MainDXViewportView.DXScene.DXHitTestOptions.BackgroundRenderingQueue = null;
        }


        private void ClearDepthBufferRadioButton_OnChecked(object sender, RoutedEventArgs e)
        {
            if (MainDXViewportView.DXScene == null)
                return;

            SetupClearingDepthBuffer();
            MainDXViewportView.Refresh();
        }
        
        private void DisableDepthReadRadioButton_OnChecked(object sender, RoutedEventArgs e)
        {
            if (MainDXViewportView.DXScene == null)
                return;

            SetupDisabledDepthRead();
            MainDXViewportView.Refresh();
        }

        private void OnOverlayHitTestingCheckBoxCheckedChanged(object sender, RoutedEventArgs e)
        {
            if (MainDXViewportView.DXScene == null)
                return;

            UpdateHitTestingOverlay();
        }
        
        private void OnBackgroundHitTestingCheckBoxCheckedChanged(object sender, RoutedEventArgs e)
        {
            if (MainDXViewportView.DXScene == null)
                return;

            UpdateHitTestingBackground();
        }
    }
}
