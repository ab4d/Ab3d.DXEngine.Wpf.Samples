using Ab3d.DirectX;
using Ab3d.DXEngine.glTF;
using Ab3d.DXEngine.Wpf.Samples.Common;
using Ab3d.Visuals;
using Openize.Drako;
using SharpDX;
using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace Ab3d.DXEngine.Wpf.Samples.DXEngineOther
{
    /// <summary>
    /// Interaction logic for GltfImporterSample.xaml
    /// </summary>
    public partial class GltfImporterSample : Page
    {
        private const string InitialFileName = @"Resources\Models\voyager.gltf";

        private glTFImporter _glTfImporter;

        public GltfImporterSample()
        {
            InitializeComponent();
            
            ConvertSimpleInfoControl.InfoText = 
@"When checked then simple glTF's PhysicallyBasedMaterials (PBR)
(have MetallicFactor set to 0 and do not have MetallicRoughness texture)
are converted into StandardMaterial objects.";

            ConvertAllInfoControl.InfoText = 
@"When checked then all glTF's PhysicallyBasedMaterials (PBR)
are converted into StandardMaterial objects.
To test this import a gltf file that use PBR material.";


            var dragAndDropHelper = new DragAndDropHelper(this, ".*");
            dragAndDropHelper.FileDropped += (sender, args) => LoadModel(args.FileName);


            MainDXViewportView.DXSceneInitialized += delegate (object sender, EventArgs args)
            {
                _glTfImporter = new glTFImporter(MainDXViewportView.DXScene.DXDevice);

                // To support Draco compressed meshes we need to set the DracoMeshReaderFactory.
                // The MyDracoReader class is defined below and uses Openize.Drako library to read Draco compressed meshes.
                _glTfImporter.DracoMeshReaderFactory = (dracoFileBytes) => new MyDracoReader(dracoFileBytes);
                
                var fileName = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, InitialFileName);
                LoadModel(fileName);
            };

            // IMPORTANT:
            // It is very important to call Dispose method on DXSceneView after the control is not used anymore (see help file for more info)
            this.Unloaded += (sender, args) => MainDXViewportView.Dispose();
        }

        private void LoadModel(string fileName)
        {
            if (_glTfImporter == null)
                return;


            MainViewport.Children.Clear();


            _glTfImporter.ConvertSimplePhysicallyBasedMaterialsToStandardMaterials = ConvertSimpleCheckBox.IsChecked ?? false;
            _glTfImporter.ConvertAllPhysicallyBasedMaterialsToStandardMaterials = ConvertAllCheckBox.IsChecked ?? false;

            if (LogCheckBox.IsChecked ?? false)
            {
                if (!_glTfImporter.LogInfoMessages)
                {
                    _glTfImporter.LogInfoMessages = true;
                    _glTfImporter.LoggerCallback = (logLevel, logMessage) => Debug.WriteLine(logLevel + ": " + logMessage);
                }
            }
            else
            {
                if (_glTfImporter.LogInfoMessages)
                {
                    _glTfImporter.LogInfoMessages = false;
                    _glTfImporter.LoggerCallback = null;
                }
            }
            

            var sceneNode = _glTfImporter.Import(fileName);

            var sceneNodeVisual3D = new SceneNodeVisual3D(sceneNode);

            MainViewport.Children.Add(sceneNodeVisual3D);

            if (sceneNode.Bounds != null)
            {
                Camera1.Distance = sceneNode.Bounds.GetDiagonalLength() * 1.5;
                Camera1.TargetPosition = sceneNode.Bounds.GetCenterPosition().ToWpfPoint3D();
            }
        }

        private void LoadButton_OnClick(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new Microsoft.Win32.OpenFileDialog();
            openFileDialog.InitialDirectory = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources\\Models");

            openFileDialog.Filter = "glTF file (*.gltf;*.glb)|*.gltf;*.glb";
            openFileDialog.Title = "Select glTF file";

            if ((openFileDialog.ShowDialog() ?? false) && !string.IsNullOrEmpty(openFileDialog.FileName))
                LoadModel(openFileDialog.FileName);
        }
    }
    

    // Custom DracoMeshReader that uses Openize.Drako library to read Draco compressed meshes.
    class MyDracoReader : DracoMeshReader
    {
        private readonly DracoMesh _dracoMesh;
        
        public MyDracoReader(byte[] dracoFileBytes)
        {
            _dracoMesh = Draco.Decode(dracoFileBytes) as DracoMesh;
        }
        
        public override Vector3[] GetPositions()
        {
            var attribute = _dracoMesh.GetNamedAttribute(Openize.Drako.AttributeType.Position);

            // IndicesMap define the mapping from the original indices to the new indices in the attribute buffer,
            // for example for box.gltf, the IndicesMap is [2, 2, 2, 0, 0, 0, ...
            // and this means that the first 3 positions in the final positions array are the same as the 2nd position in the attribute (compressed buffer)
            var positionsCount = attribute.IndicesMap.Length;
            var positions = new Vector3[positionsCount];
            for (int i = 0; i < positionsCount; i++)
            {
                int index = attribute.IndicesMap[i];
                var pos = attribute.GetValueAsVector3(index);
                positions[i] = new Vector3(pos.X, pos.Y, pos.Z);
            }
            
            return positions;
        }

        public override Vector3[] GetNormals()
        {
            var attribute = _dracoMesh.GetNamedAttribute(Openize.Drako.AttributeType.Normal);
            
            if (attribute == null)
                return null;

            // See comment in GetPositions
            var normalsCount = attribute.IndicesMap.Length;
            var normals = new Vector3[normalsCount];
            for (int i = 0; i < normalsCount; i++)
            {
                int index = attribute.IndicesMap[i];
                var n = attribute.GetValueAsVector3(index);
                normals[i] = new Vector3(n.X, n.Y, n.Z);
            }
            
            return normals;
        }
        
        public override Vector4[] GetTangents()
        {
            // Openize.Drako does not define Tangent attribute type.
            // When Tangent data is present, it stores that as Generic attribute.
            // In this cas we need to check that the DataType is FLOAT32 and ComponentsCount is 4 (Vector4)
            var attribute = _dracoMesh.GetNamedAttribute(AttributeType.Generic);

            if (attribute == null || attribute.ComponentsCount != 4 || attribute.DataType != DataType.FLOAT32)
                return null;
            
            var oneVector4 = new float[4];
            
            var tangentsCount = attribute.IndicesMap.Length;
            var tangents = new Vector4[tangentsCount];
        
            for (int i = 0; i < tangentsCount; i++)
            {
                int index = attribute.IndicesMap[i];
                attribute.GetValue(index, oneVector4);
                tangents[i] = new Vector4(oneVector4[0], oneVector4[1], oneVector4[2], oneVector4[3]);
            }
            
            return tangents;
        }
        
        public override Vector2[] GetTextureCoordinates()
        {
            var attribute = _dracoMesh.GetNamedAttribute(AttributeType.TexCoord);

            if (attribute == null)
                return null;
            
            var oneUV = new float[2];
            
            var textureCoordinatesCount = attribute.IndicesMap.Length;
            var textureCoordinates = new Vector2[textureCoordinatesCount];
        
            for (int i = 0; i < textureCoordinatesCount; i++)
            {
                int index = attribute.IndicesMap[i];
                attribute.GetValue(index, oneUV);
                textureCoordinates[i] = new Vector2(oneUV[0], oneUV[1]);
            }
            
            return textureCoordinates;
        }
        
        public override int[] GetTriangleIndices()
        {
            if (_dracoMesh.Indices == null || _dracoMesh.Indices.Count == 0)
                return null;
            
            var indicesCount = _dracoMesh.Indices.Count;
            var indices = new int[indicesCount];
            for (int i = 0; i < indicesCount; i++)
                indices[i] = _dracoMesh.Indices[i];

            return indices;
        }
    }    
}