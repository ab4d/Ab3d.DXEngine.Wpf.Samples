using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
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
using Ab3d.Visuals;
using SharpDX;
using Ab3d.DirectX;
using Ab3d.DirectX.Models;
using Ab3d.DXEngine.Wpf.Samples.DXEnginePerformance;
using Ab3d.Meshes;

namespace Ab3d.DXEngine.Wpf.Samples.DXEngineAdvanced
{
    /// <summary>
    /// Interaction logic for InstancedMeshNodeSample.xaml
    /// </summary>
    public partial class InstancedMeshNodeSample : Page
    {
        private DisposeList _disposables;

        public InstancedMeshNodeSample()
        {
            InitializeComponent();

            if (DesignerProperties.GetIsInDesignMode(this))
                return;


            _disposables = new DisposeList();


            // Create SimpleMesh that uses vertex buffer with PositionNormalTexture
            // To see more options on how to create DXEngine's mesh, see the ManuallyCreatedSceneNodes sample
            PositionNormalTexture[] vertexBuffer;
            int[] indexBuffer;
            ManuallyCreatedSceneNodes.GetVertexAndIndexBuffer(out vertexBuffer, out indexBuffer);

            var simpleMesh = new SimpleMesh<PositionNormalTexture>(vertexBuffer,
                                                                   indexBuffer,
                                                                   inputLayoutType: InputLayoutType.Position | InputLayoutType.Normal | InputLayoutType.TextureCoordinate,
                                                                   name: "SimpleMesh-from-PositionNormalTexture-array");


            // Create InstancedMeshNode
            var instancedMeshNode = new InstancedMeshNode(simpleMesh, "InstancedMeshNode");

            // Set instance data (use generator method from InstancedMeshGeometry3D sample)
            InstanceData[] instancedData = InstancedMeshGeometry3DTest.CreateInstancesData(center: new Point3D(0, 200, 0), 
                                                                                           size: new Size3D(400, 400, 400), 
                                                                                           modelScaleFactor: 0.2f, 
                                                                                           xCount: 20, yCount: 20, zCount: 20, 
                                                                                           useTransparency: false);

            instancedMeshNode.SetInstanceData(instancedData, true);

            // Instead of using SetInstanceData that takes an array of InstanceData,
            // you can also use SetInstanceBuffer to set the instance buffer directly
            //instancedMeshNode.SetInstanceBuffer();

            // When we use transparency, we also need to set UseAlphaBlend to true
            //instancedMeshNode.UseAlphaBlend = true;


            _disposables.Add(simpleMesh);
            _disposables.Add(instancedMeshNode);
            

            var sceneNodeVisual3D = new SceneNodeVisual3D(instancedMeshNode);
            MainViewport.Children.Add(sceneNodeVisual3D);


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
    }
}
