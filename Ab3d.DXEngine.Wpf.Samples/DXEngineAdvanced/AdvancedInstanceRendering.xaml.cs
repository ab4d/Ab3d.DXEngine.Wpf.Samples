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
using Ab3d.DirectX;
using Ab3d.DirectX.Models;
using Ab3d.DXEngine.Wpf.Samples.DXEnginePerformance;
using Ab3d.Visuals;
using SharpDX;

namespace Ab3d.DXEngine.Wpf.Samples.DXEngineAdvanced
{
    // This sample creates 3 InstancedMeshGeometry3DNode objects.
    // The first one is used to create the DirectX instance buffer.
    // This instance buffer is then reused by the other 2 InstancedMeshGeometry3DNode objects.
    // Each InstancedMeshGeometry3DNode object renders different part of the instances.
    // This is defined by setting StartInstanceIndex and InstancesCount properties.
    // This way it is possible to hide some instances or render only a fraction of instances
    // without removing the items from instances data - this would take some time because 
    // the instances data array must be first updated in the main memory and then
    // a new DirectX instance buffer must be created. 
    // With preserving the instances buffer and just changing which part is rendered,
    // there are no performance hits.
    // This way it is possible to support also more complex scenarios where many parts of
    // the instances data need to be hidden.
    // Event with having hundred InstancedMeshGeometry3DNode the performance should still be better 
    // then with updating instance buffer (except when the change is done rarely)
    //
    // This sample also demonstrates another advanced feature of a InstancedMeshGeometry3DNode -
    // it is possible to override the color data that is defined in the instances data 
    // and instead render all the instanced (between StartInstanceIndex and InstancesCount)
    // with some other color. 
    // This can be useful for selection or some other use cases.

    /// <summary>
    /// Interaction logic for AdvancedInstanceRendering.xaml
    /// </summary>
    public partial class AdvancedInstanceRendering : Page
    {
        private const int XInstancesCount = 100;
        private const int YInstancesCount = 4;
        private const int ZInstancesCount = 100;

        private InstancedMeshGeometry3DNode _instancedMeshGeometry3DNode1;
        private InstancedMeshGeometry3DNode _instancedMeshGeometry3DNode2;
        private InstancedMeshGeometry3DNode _instancedMeshGeometry3DNode3;

        private int _lastStartRowIndex;

        private DateTime _startTime;

        private DisposeList _disposables;
        

        public AdvancedInstanceRendering()
        {
            InitializeComponent();

            _disposables = new DisposeList();

            // Wait until DXScene and DirectX device are initialized. 
            // Then we can create the instance buffers in the InstancedMeshGeometry3DNode object.
            MainDXViewportView.DXSceneDeviceCreated += MainDXViewportViewOnDXSceneDeviceCreated;

            // Start animating...
            CompositionTarget.Rendering += CompositionTargetOnRendering;

            this.Unloaded += delegate(object sender, RoutedEventArgs args)
            {
                CompositionTarget.Rendering -= CompositionTargetOnRendering;

                _disposables.Dispose();
                MainDXViewportView.Dispose();
            };
        }

        private void CompositionTargetOnRendering(object sender, EventArgs e)
        {
            UpdateHiddenInstancesPositions();
        }

        private void MainDXViewportViewOnDXSceneDeviceCreated(object sender, EventArgs e)
        {
            var instancedData = InstancedMeshGeometry3DTest.CreateInstancesData(center: new Point3D(0, 0, 0), 
                                                                                size: new Size3D(4 * XInstancesCount, 4 * YInstancesCount, 4 * ZInstancesCount),
                                                                                modelScaleFactor: 1, 
                                                                                xCount: XInstancesCount, yCount: YInstancesCount, zCount: ZInstancesCount,
                                                                                useTransparency: false);

            // Update colors
            int dataCount = instancedData.Length;
            for (int i = 0; i < dataCount; i++)
            {
                float percentage = 1.0f - (float) i / (float)dataCount;
                instancedData[i].DiffuseColor = new Color4(red: percentage, green: 1, blue: percentage, alpha: 1);
            }


            var boxMeshGeometry = new Ab3d.Meshes.BoxMesh3D(centerPosition: new Point3D(0, 0, 0), size: new Size3D(3, 3, 3), xSegments: 1, ySegments: 1, zSegments: 1).Geometry;


            // The first InstancedMeshGeometry3DNode will get the instancedData and
            // will also create the DirectX instance buffer.
            _instancedMeshGeometry3DNode1 = new InstancedMeshGeometry3DNode(boxMeshGeometry);
            _instancedMeshGeometry3DNode1.SetInstanceData(instancedData);

            // Manually call InitializeResources.
            // For this to work, the dxViewportView.DXScene must be set.
            // This is the reason why this method is called inside a DXViewportView.DXSceneDeviceCreated event handler.
            _instancedMeshGeometry3DNode1.InitializeResources(MainDXViewportView.DXScene);

            _disposables.Add(_instancedMeshGeometry3DNode1);


            var instanceBuffer = _instancedMeshGeometry3DNode1.GetInstanceBuffer();

            if (instanceBuffer == null)
                throw new Exception("GetInstanceBuffer returned null"); // Probably DXScene is not initialized


            // Now create another 2 InstancedMeshGeometry3DNode objects
            // and initialize it with already created instanceBuffer

            // The next InstancedMeshGeometry3DNode will be also initialized so 
            // that all instances will be rendered with red color instead of the color defined in the instances data.
            _instancedMeshGeometry3DNode2 = new InstancedMeshGeometry3DNode(boxMeshGeometry);
            _instancedMeshGeometry3DNode2.SetInstanceBuffer(instanceBuffer, InstanceData.SizeInBytes, instancedData.Length, instancedData);
            _instancedMeshGeometry3DNode2.UseSingleObjectColor(Colors.Red.ToColor4());
            _disposables.Add(_instancedMeshGeometry3DNode2);

            // The last InstancedMeshGeometry3DNode will render last part of the instances with the color defined in the instance data.
            _instancedMeshGeometry3DNode3 = new InstancedMeshGeometry3DNode(boxMeshGeometry);
            _instancedMeshGeometry3DNode3.SetInstanceBuffer(instanceBuffer, InstanceData.SizeInBytes, instancedData.Length, instancedData);
            _disposables.Add(_instancedMeshGeometry3DNode3);


            // Set StartInstanceIndex and InstancesCount
            _startTime = DateTime.Now;
            _lastStartRowIndex = int.MinValue;
            UpdateHiddenInstancesPositions();


            var rootSceneNode = new SceneNode();
            rootSceneNode.AddChild(_instancedMeshGeometry3DNode1);
            rootSceneNode.AddChild(_instancedMeshGeometry3DNode2);
            rootSceneNode.AddChild(_instancedMeshGeometry3DNode3);

            var sceneNodeVisual3D = new SceneNodeVisual3D(rootSceneNode);
            MainViewport.Children.Add(sceneNodeVisual3D);
        }

        private void UpdateHiddenInstancesPositions()
        {
            var instancesData = _instancedMeshGeometry3DNode1.GetInstanceData();
            int instancesDataLength = instancesData.Length;


            var elapsedSeconds = (DateTime.Now - _startTime).TotalSeconds;

            // Use sin to animate the position - get a value from 0.1 to 0.9
            double positionPercent = (Math.Sin(elapsedSeconds) + 1) * 0.40 + 0.1;

            // convert the relative value to instance index
            int selectedPosition = (int)(positionPercent * XInstancesCount * YInstancesCount * ZInstancesCount);

            // Convert to index to be an index at the start of the row
            int oneRowInstancesCount = XInstancesCount * YInstancesCount; // number of instances in one horizontal row
            int startRowIndex = selectedPosition - (selectedPosition % oneRowInstancesCount);

            if (startRowIndex == _lastStartRowIndex)
                return; // Nothing to change


            int row1 = startRowIndex - 2 * oneRowInstancesCount;
            int row2 = startRowIndex + oneRowInstancesCount;
            int row3 = startRowIndex + 2 * oneRowInstancesCount;

            _lastStartRowIndex = startRowIndex;


            // Set StartInstanceIndex and InstancesCount so that 10% of instances will not be rendered (around _hiddenInstancesPosition)
            // Note here we do not need to call NotifySceneNodeChange because this is already done in the property setters.
            _instancedMeshGeometry3DNode1.StartInstanceIndex = 0;
            _instancedMeshGeometry3DNode1.InstancesCount        = row1;

            // The next _instancedMeshGeometry3DNode2 is showing all instances with red color (using 
            _instancedMeshGeometry3DNode2.StartInstanceIndex = row2;
            _instancedMeshGeometry3DNode2.InstancesCount        = row3 - row2;

            _instancedMeshGeometry3DNode3.StartInstanceIndex = row3;
            _instancedMeshGeometry3DNode3.InstancesCount        = instancesDataLength - row3;


            // For the changes to take effect, we need to notify the SceneNode about the changes.
            _instancedMeshGeometry3DNode1.NotifySceneNodeChange(SceneNode.SceneNodeDirtyFlags.MeshIndexBufferDataChanged | SceneNode.SceneNodeDirtyFlags.BoundsChanged);
            _instancedMeshGeometry3DNode2.NotifySceneNodeChange(SceneNode.SceneNodeDirtyFlags.MeshIndexBufferDataChanged | SceneNode.SceneNodeDirtyFlags.BoundsChanged);
            _instancedMeshGeometry3DNode3.NotifySceneNodeChange(SceneNode.SceneNodeDirtyFlags.MeshIndexBufferDataChanged | SceneNode.SceneNodeDirtyFlags.BoundsChanged);


            // NOTE:
            // To show all instances, we just set the StartInstanceIndex to 0 and InstancesCount to number of all instances.
            // To hide InstancedMeshGeometry3DNode just set InstancesCount to 0 (this way you can quickly show instances again - quicker then removing the InstancedMeshGeometry3DNode from its parent)
            // For example:
            //_instancedMeshGeometry3DNode1.StartInstanceIndex = 0;
            //_instancedMeshGeometry3DNode1.InstancesCount        = instancesDataLength;

            //// Hide other InstancedMeshGeometry3DNode:
            //_instancedMeshGeometry3DNode2.StartInstanceIndex = 0;
            //_instancedMeshGeometry3DNode2.InstancesCount        = 0;

            //_instancedMeshGeometry3DNode3.StartInstanceIndex = 0;
            //_instancedMeshGeometry3DNode3.InstancesCount        = 0;
        }
    }
}
