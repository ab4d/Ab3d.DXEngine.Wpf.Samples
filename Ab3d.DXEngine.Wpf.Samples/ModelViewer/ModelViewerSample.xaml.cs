using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using Ab3d.Animation;
using Ab3d.Cameras;
using Ab3d.Common;
using Ab3d.Common.Cameras;
using Ab3d.Common.Models;
using Ab3d.Controls;
using Ab3d.DirectX;
using Ab3d.DirectX.Materials;
using Ab3d.DirectX.Models;
using Ab3d.DXEngine.Wpf.Samples.Common;
using Ab3d.Models;
using Ab3d.Utilities;
using Ab3d.Visuals;
using Assimp;

namespace Ab3d.DXEngine.Wpf.Samples.ModelViewer
{
    /// <summary>
    /// Interaction logic for ModelViewerSample.xaml
    /// </summary>
    public partial class ModelViewerSample : Page
    {
        private string _fileName;

        private Model3D _loadedModel3D;

        private DisposeList _modelDisposeList;

        private Model3D _selectedModel3D;
        private Transform3D _selectedModelParentTransform3D;
        private Transform3D _selectedModelFullTransform3D;

        private TypeConverter _colorTypeConverter;
        private MeshGeometry3D _meshGeometryToInspect;

        private DirectionalLight _topDownLight;
        private DirectionalLight _sideLight;

        private DateTime _lastEscapeKeyPressedTime;
        
        private Dictionary<GeometryModel3D, System.Windows.Media.Media3D.Material> _savedMaterials;
        private Dictionary<GeometryModel3D, System.Windows.Media.Media3D.Material> _savedBackMaterials;

        private HashSet<GeometryModel3D> _transparentModels;

        private Point _mouseDownPosition;
        private bool _isLeftMouseDown;
        private bool _isDoubleClick;


        public ModelViewerSample()
        {
            InitializeComponent();

            LineThicknessComboBox.ItemsSource = new double[] { 0.1, 0.2, 0.5, 1, 2 };
            LineThicknessComboBox.SelectedIndex = 2;

            CameraControllerInfo1.AddCustomInfoLine(2, MouseCameraController.MouseAndKeyboardConditions.LeftMouseButtonPressed, "CLICK: Select object");
            CameraControllerInfo1.AddCustomInfoLine(3, MouseCameraController.MouseAndKeyboardConditions.LeftMouseButtonPressed, "DOUBLE CLICK: Zoom to");

            // Enable DXEngine's transparency sorter
            // This will sort transparent objects so that they are rendered in the correct order - from most distant objects to the closest objects
            MainDXViewportView.IsTransparencySortingEnabled = true;

            
            // Use helper class (defined in this sample project) to load the native Assimp libraries
            Ab3d.Assimp.AssimpLoader.LoadAssimpNativeLibrary();

            var dragAndDropHelper = new DragAndDropHelper(this, ".*");
            dragAndDropHelper.FileDropped += (sender, args) => LoadModel(args.FileName);


            string startUpFileName = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"Resources\Models\house with trees.3DS");
            LoadModel(startUpFileName);

            // To allow using left mouse button for camera rotation and for object selection, we need to subscribe to PreviewMouse events.
            // We also need to set the MouseMoveThreshold in the MouseCameraController to prevent starting a rotation on mouse down.
            ViewportBorder.PreviewMouseLeftButtonDown += OnMouseLeftButtonDown;
            ViewportBorder.PreviewMouseLeftButtonUp += OnMouseLeftButtonUp;

            // Check if ESCAPE key is pressed - on first press deselect the object, on second press show all objects
            this.Focusable = true; // by default Page is not focusable and therefore does not receive keyDown event
            this.PreviewKeyDown += OnPreviewKeyDown;
            this.Focus();


            this.Loaded += delegate (object sender, RoutedEventArgs args)
            {
                UpdateAmbientLight();
                UpdateNormalsAndMeshInspectorColors();
            };

            this.Unloaded += delegate (object sender, RoutedEventArgs args)
            {
                if (_modelDisposeList != null)
                {
                    _modelDisposeList.Dispose();
                    _modelDisposeList = null;
                }

                MainDXViewportView.Dispose();
            };
        }


        private void ShowAllObjects()
        {
            Camera1.FitIntoView(FitIntoViewType.CheckBounds);
        }

        private void LoadModel(string fileName)
        {
            bool isNewFile = false;

            // Create an instance of AssimpWpfImporter
            var assimpWpfImporter = new Ab3d.Assimp.AssimpWpfImporter();

            string fileExtension = System.IO.Path.GetExtension(fileName);
            if (!assimpWpfImporter.IsImportFormatSupported(fileExtension))
            {
                MessageBox.Show("Assimp does not support importing files file extension: " + fileExtension);
                return;
            }


            if (_modelDisposeList != null)
                _modelDisposeList.Dispose();

            _modelDisposeList = new DisposeList();


            try
            {
                Mouse.OverrideCursor = System.Windows.Input.Cursors.Wait;

                // Before reading the file we can set the default material (used when no other material is defined)
                assimpWpfImporter.DefaultMaterial = new DiffuseMaterial(Brushes.Silver);

                // After Assimp importer reads the file, it can execute many post processing steps to transform the geometry.
                // See the possible enum values to see what post processes are available.
                // Here we just set the AssimpPostProcessSteps to its default value - execute the Triangulate step to convert all polygons to triangles that are needed for WPF 3D.
                // Note that if ReadPolygonIndices is set to true in the next line, then the assimpWpfImporter will not use assimp's triangulation because it needs original polygon data.
                assimpWpfImporter.AssimpPostProcessSteps = PostProcessSteps.Triangulate;

                // When ReadPolygonIndices is true, assimpWpfImporter will read PolygonIndices collection that can be used to show polygons instead of triangles.
                assimpWpfImporter.ReadPolygonIndices = ReadPolygonIndicesCheckBox.IsChecked ?? false;

                
                Model3D readModel3D;

                try
                {
                    // Read model from file
                    readModel3D = assimpWpfImporter.ReadModel3D(fileName, texturesPath: null); // we can also define a textures path if the textures are located in some other directory (this is parameter can be skipped, but is defined here so you will know that you can use it)

                    isNewFile = (_fileName != fileName);
                    _fileName = fileName;
                }
                catch (Exception ex)
                {
                    readModel3D = null;
                    MessageBox.Show("Error importing file:\r\n" + ex.Message);
                }

                // After the model is read and if the object names are defined in the file,
                // you can get the model names by assimpWpfImporter.ObjectNames
                // or get object by name with assimpWpfImporter.NamedObjects

                // Note: to get the original Assimp's Scene object, check the assimpWpfImporter.ImportedAssimpScene


                if (TwoSidedMaterialsCheckBox.IsChecked ?? false)
                    SetTwoSidedMaterial(readModel3D);


                // Show the model
                ShowModel(readModel3D, updateCamera: isNewFile); // If we just reloaded the previous file, we preserve the current camera TargetPosition and Distance


                // Force garbage collection to clear the previously loaded objects from memory.
                // Note that sometimes when a lot of objects are created in large objects heap,
                // it may take two garbage collections to release the memory
                // (e.g. - after reading one large file, you will need to read two smaller files to clean the memory taken by the large file).
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
            }
            finally
            {
                // Dispose unmanaged resources
                assimpWpfImporter.Dispose();

                Mouse.OverrideCursor = null;
            }
        }

        private void SetTwoSidedMaterial(Model3D readModel3D)
        {
            Ab3d.Utilities.ModelIterator.IterateGeometryModel3DObjects(readModel3D, null, delegate(GeometryModel3D geometryModel3D, Transform3D transform3D)
            {
                if (geometryModel3D.BackMaterial == null)
                    geometryModel3D.BackMaterial = geometryModel3D.Material;
            });
        }

        private void ShowModel(Model3D model3D, bool updateCamera)
        {
            ClearCurrentModel();

            if (model3D == null)
                return;


            if (model3D.Bounds.GetDiagonalLength() > 10000 && (ScaleLargeModelsCheckBox.IsChecked ?? false))
            {
                // IMPORTANT:
                // Some imported files may define the models in actual units (meters or millimeters) and
                // this may make the objects very big (for example, objects bounds are bigger than 100000).
                // For such big models the camera rotation may become irregular (not smooth) because
                // of floating point precision errors on the graphics card.
                //
                // Therefore it is recommended to prevent such big models by scaling them to a more common size.
                // This can be done by the ModelUtils.CenterAndScaleModel3D method:
                // Put the model to the center of coordinate axis and scale it to 100 x 100 x 100.
                Ab3d.Utilities.ModelUtils.CenterAndScaleModel3D(model3D, centerPosition: new Point3D(0, 0, 0), finalSize: new Size3D(100, 100, 100));
            }

            ContentVisual.Content = model3D;

            _loadedModel3D = model3D;


            bool showPolygonLines = ReadPolygonIndicesCheckBox.IsChecked ?? false;

            var wireframeLinePositions = WireframeFactory.CreateWireframeLinePositions(model3D, null, showPolygonLines, removedDuplicates: true);
            
            AllWireframesLineVisual3D.Positions = wireframeLinePositions;

            UpdateWireframe();


            // If the read model already define some lights, then do not show the Camera's light
            if (ModelUtils.HasAnyLight(model3D))
                Camera1.ShowCameraLight = ShowCameraLightType.Never;
            else
                Camera1.ShowCameraLight = ShowCameraLightType.Always;


            if (updateCamera)
            {
                // Adjust the camera so the bounding box of the loaded model will fit into view
                // if camera is not valid (for example when the size of Viewport3D is not set yet), then wait until camera is valid and then automatically call FitIntoView
                Camera1.FitIntoView(model3D.Bounds, adjustTargetPosition: true, adjustmentFactor: 1, waitUntilCameraIsValid: true); 
            }


            FillTreeView(model3D);
        }

        private void ClearCurrentModel()
        {
            _loadedModel3D = null;
            _selectedModel3D = null;
            ContentVisual.Content = null;

            if (_transparentModels != null)
                _transparentModels.Clear();

            if (_savedMaterials != null)
                _savedMaterials.Clear();

            if (_savedBackMaterials != null)
                _savedBackMaterials.Clear();

            AllWireframesLineVisual3D.Positions = null;
            SelectedModelWireframeVisual3D.Positions = null;
            NormalLinesVisual3D.Positions = null;
        }


        private void LoadButton_OnClick(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new Microsoft.Win32.OpenFileDialog();
            openFileDialog.InitialDirectory = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources");

            openFileDialog.Filter = "3D model file (*.*)|*.*";
            openFileDialog.Title = "Open 3D model file";

            if ((openFileDialog.ShowDialog() ?? false) && !string.IsNullOrEmpty(openFileDialog.FileName))
                LoadModel(openFileDialog.FileName);
        }

        private void OnModelLoadingSettingsChanged(object sender, RoutedEventArgs e)
        {
            if (_fileName == null)
                return;

            // Read file again
            LoadModel(_fileName);
        }

        private void OnShowNormalsCheckBoxChanged(object sender, RoutedEventArgs e)
        {
            if (!this.IsLoaded)
                return;

            if (ShowNormalsCheckBox.IsChecked ?? false)
                ShowNormals(_selectedModel3D, _selectedModelFullTransform3D);
            else
                ClearNormals();
        }
        
        private void OnTransparentNonSelectedObjectsCheckBoxChanged(object sender, RoutedEventArgs e)
        {
            if (!this.IsLoaded)
                return;

            UpdateNonSelectedObjectsTransparency();
        }

        private void UpdateNonSelectedObjectsTransparency()
        {
            if (_selectedModel3D != null && (TransparentNonSelectedObjectsCheckBox.IsChecked ?? false))
            {
                var isTwoSidedTransparentMaterial = TwoSidedTransparentMaterialCheckBox.IsChecked ?? false;
                var isXRayMaterial = UseXRayMaterialCheckBox.IsChecked ?? false;

                UseTransparentMaterials(excludedModel3D: _selectedModel3D, isTwoSidedTransparentMaterial, isXRayMaterial);
                UseOriginalMaterials(selectedModel3D: _selectedModel3D);
            }
            else
            {
                UseAllOriginalMaterials();
            }
        }

        private void ClearNormals()
        {
            NormalLinesVisual3D.Positions = null;
        }

        private void ShowNormals(Model3D model3D, Transform3D modelTransform3D)
        {
            // Only show normals when a GeometryModel3D is selected

            var geometryModel3D = model3D as GeometryModel3D;

            if (geometryModel3D == null)
            {
                ClearNormals();
                return;
            }


            var meshGeometry3D = geometryModel3D.Geometry as MeshGeometry3D;

            if (meshGeometry3D != null)
            {
                double normalScale = _loadedModel3D.Bounds.GetDiagonalLength() * 0.02;
                var normalLinePositions = Ab3d.Models.WireframeFactory.GetNormalLinePositions(meshGeometry3D, normalScale, modelTransform3D);

                NormalLinesVisual3D.Positions = normalLinePositions;

                bool showArrows = meshGeometry3D.Positions.Count < 1000;
                NormalLinesVisual3D.EndLineCap = showArrows ? LineCap.ArrowAnchor : LineCap.Flat;
            }
        }

        public void FillTreeView(Model3D rootModel)
        {
            ElementsTreeView.BeginInit();
            ElementsTreeView.Items.Clear();

            var rootItem = new TreeViewItem
            {
                Header = GetTreeViewDisplayName(rootModel),
                Tag    = rootModel
            };

            ElementsTreeView.Items.Add(rootItem);

            if (rootModel is Model3DGroup)
                FillTreeView(rootItem, (Model3DGroup)rootModel);

            rootItem.IsExpanded = true;

            ElementsTreeView.EndInit();
        }

        private void FillTreeView(TreeViewItem currentItem, Model3DGroup currentModel)
        {
            foreach (Model3D oneModel in currentModel.Children)
            {
                var newItem = new TreeViewItem
                {
                    Header = GetTreeViewDisplayName(oneModel),
                    Tag = oneModel
                };

                currentItem.Items.Add(newItem);

                if (oneModel is Model3DGroup)
                    FillTreeView(newItem, (Model3DGroup)oneModel);
            }
        }

        private string GetTreeViewDisplayName(Model3D model3D)
        {
            var name = model3D.GetName();

            if (string.IsNullOrEmpty(name))
                name = '<' + model3D.GetType().Name + '>';

            return name;
        }

        
        void TreeViewItemDoubleClicked(object sender, MouseButtonEventArgs e)
        {
            bool isZoomed = ZoomToSelectedModel();

            if (isZoomed)
                e.Handled = true;
        }

        private bool ZoomToSelectedModel()
        {
            var selectedModel = GetSelectedModel3D();

            if (selectedModel != null)
            {
                ZoomToObject(selectedModel);
                return true;
            }

            return false;
        }

        private Model3D GetSelectedModel3D()
        {
            Model3D selectedModel;

            var selectedTreeViewItem = ElementsTreeView.SelectedItem as TreeViewItem;

            if (selectedTreeViewItem != null)
                selectedModel = selectedTreeViewItem.Tag as Model3D;
            else
                selectedModel = null;

            return selectedModel;
        }

        private void ZoomToObject(Model3D selectedModel)
        {
            if (selectedModel == null)
                return;

            var selectedModelTransform3D = Ab3d.Utilities.TransformationsHelper.GetModelTotalTransform(_loadedModel3D, selectedModel, addFinalModelTransformation: false);

            var worldBounds = selectedModel.Bounds;
            if (selectedModelTransform3D != null)
                worldBounds = selectedModelTransform3D.TransformBounds(worldBounds);

            var centerPosition = worldBounds.GetCenterPosition();
            var diagonalLength = worldBounds.GetDiagonalLength();


            // Animate camera change
            var newTargetPosition = centerPosition - Camera1.Offset;
            var newDistance = diagonalLength * Math.Tan(Camera1.FieldOfView * Math.PI / 180.0) * 1.5;
            var newCameraWidth = diagonalLength * 1.5; // for orthographic camera we assume 45 field of view (=> Tan == 1)

            Camera1.AnimateTo(newTargetPosition, newDistance, newCameraWidth, animationDurationInMilliseconds: 300, easingFunction: EasingFunctions.CubicEaseInOutFunction);

            // Use the following code to immediately change the camera without animation:
            //Camera1.Offset = new System.Windows.Media.Media3D.Vector3D(0, 0, 0); // reset offset so the target will actually be centerPosition
            //Camera1.TargetPosition = centerPosition;

            //Camera1.Distance = diagonalLength * Math.Tan(Camera1.FieldOfView * Math.PI / 180.0) * 1.5;
            //Camera1.CameraWidth = diagonalLength * 1.5; // for orthographic camera we assume 45 field of view (=> Tan == 1)
        }

        void TreeViewItemSelected(object sender, RoutedPropertyChangedEventArgs<object> args)
        {
            var selectedTreeViewItem = args.NewValue as TreeViewItem;

            if (selectedTreeViewItem != null)
            {
                var selectedModel3D = selectedTreeViewItem.Tag as Model3D;
                OnSelectedModelChanged(selectedModel3D);
            }
            else
            {
                OnSelectedModelChanged(null); // selection removed
            }
        }

        private void SelectTreeViewItem(Model3D selectedModel3D)
        {
            if (selectedModel3D == null)
            {
                var selectedTreeViewItem = ElementsTreeView.SelectedItem as TreeViewItem;

                if (selectedTreeViewItem != null)
                    selectedTreeViewItem.IsSelected = false;

                return;
            }

            foreach (var treeViewItem in ElementsTreeView.Items.OfType<TreeViewItem>())
            {
                bool isFound = SelectTreeViewItem(treeViewItem, selectedModel3D);

                if (isFound)
                    break;
            }
        }

        private bool SelectTreeViewItem(TreeViewItem treeViewItem, Model3D selectedModel3D)
        {
            if (ReferenceEquals(treeViewItem.Tag, selectedModel3D))
            {
                treeViewItem.IsSelected = true; // This will call TreeViewItemSelected
                treeViewItem.BringIntoView();
                return true;
            }

            foreach (var childTreeViewItem in treeViewItem.Items.OfType<TreeViewItem>())
            {
                bool isFound = SelectTreeViewItem(childTreeViewItem, selectedModel3D);

                if (isFound)
                    break;
            }

            return false;
        }

        private void SaveOriginalModelMaterials()
        {
            if (_savedMaterials == null)
            {
                _savedMaterials = new Dictionary<GeometryModel3D, System.Windows.Media.Media3D.Material>();
                _savedBackMaterials = new Dictionary<GeometryModel3D, System.Windows.Media.Media3D.Material>();
            }
            else
            {
                _savedMaterials.Clear();
                _savedBackMaterials.Clear();
            }

            Ab3d.Utilities.ModelIterator.IterateGeometryModel3DObjects(_loadedModel3D, null, (geometryModel3D, transform3D) =>
            {
                var material = geometryModel3D.Material;
                if (material != null)
                    _savedMaterials.Add(geometryModel3D, material);

                var backMaterial = geometryModel3D.BackMaterial;
                if (backMaterial != null)
                    _savedBackMaterials.Add(geometryModel3D, material);
            });
        }

        private void UseTransparentMaterials(Model3D excludedModel3D, bool isTwoSidedMaterial, bool isXRayMaterial)
        {
            if (_savedMaterials == null || _savedMaterials.Count == 0)
                SaveOriginalModelMaterials();

            if (_transparentModels == null)
                _transparentModels = new HashSet<GeometryModel3D>();
            else
                _transparentModels.Clear();


            Ab3d.Utilities.ModelIterator.IterateGeometryModel3DObjects(
                _loadedModel3D, 
                parentTransform3D: null, 
                callback: (geometryModel3D, transform3D) =>
                {
                    if (geometryModel3D == excludedModel3D)
                        return;

                    //if (_transparentModels.Contains(geometryModel3D))
                    //{
                    //    // Already set as transparent

                    //    if (isTwoSidedMaterial && geometryModel3D.BackMaterial == null)
                    //        geometryModel3D.BackMaterial = geometryModel3D.Material;
                    //    else if (!isTwoSidedMaterial && geometryModel3D.BackMaterial == geometryModel3D.Material)
                    //        geometryModel3D.BackMaterial = null;

                    //    return; 
                    //}

                    DiffuseMaterial newMaterial;

                    System.Windows.Media.Media3D.Material material;
                    if (_savedMaterials.TryGetValue(geometryModel3D, out material))
                    {
                        var materialColor = Ab3d.Utilities.ModelUtils.GetMaterialDiffuseColor(material, Colors.White);
                        newMaterial = new DiffuseMaterial(new SolidColorBrush(materialColor) { Opacity = 0.4 });

                        if (isXRayMaterial)
                        {
                            // Create XRayMaterial with the material's color and falloff set to 0.3 that will lower the amount of color xray will produce
                            var xRayMaterial = new XRayMaterial(materialColor.ToColor3(), falloff: 0.3f);
                            xRayMaterial.IsTwoSided = isTwoSidedMaterial;

                            // To tell DXEngine to use the XRayMaterial instead of a material that is created from WPF's material,
                            // we can use the SetUsedDXMaterial extension method.
                            newMaterial.SetUsedDXMaterial(xRayMaterial);


                            // We need to manually dispose all DXEngine's resources that are created by us
                            _modelDisposeList.Add(xRayMaterial);
                        }
                        
                        geometryModel3D.Material = newMaterial;
                    }
                    else
                    {
                        newMaterial = null;
                    }

                    if (isTwoSidedMaterial && newMaterial != null)
                    {
                        // When using two sided material then we always set the BackMaterial
                        geometryModel3D.BackMaterial = newMaterial;
                    }
                    else
                    {
                        if (_savedBackMaterials.TryGetValue(geometryModel3D, out material))
                        {
                            var materialColor = Ab3d.Utilities.ModelUtils.GetMaterialDiffuseColor(material, Colors.White);
                            var newBackMaterial = new DiffuseMaterial(new SolidColorBrush(materialColor) { Opacity = 0.4 });

                            if (isXRayMaterial)
                            {
                                // Create XRayMaterial with the material's color and falloff set to 0.3 that will lower the amount of color xray will produce
                                var xRayMaterial = new XRayMaterial(materialColor.ToColor3(), falloff: 0.3f);
                                xRayMaterial.IsTwoSided = isTwoSidedMaterial;

                                // To tell DXEngine to use the XRayMaterial instead of a material that is created from WPF's material,
                                // we can use the SetUsedDXMaterial extension method.
                                newBackMaterial.SetUsedDXMaterial(xRayMaterial);
                            }

                            geometryModel3D.BackMaterial = newBackMaterial;
                        }
                        else
                        {
                            geometryModel3D.BackMaterial = null; // reset BackMaterial in case before we used two-sided material
                        }
                    }

                    _transparentModels.Add(geometryModel3D);
                },
                model3Dfilter: model3D => model3D != excludedModel3D);
        }

        private void UseAllOriginalMaterials()
        {
            if (_savedMaterials == null)
                return;

            Ab3d.Utilities.ModelIterator.IterateGeometryModel3DObjects(_loadedModel3D, null, (geometryModel3D, transform3D) =>
            {
                if (!_transparentModels.Contains(geometryModel3D))
                    return; // model is not transparent

                System.Windows.Media.Media3D.Material material;
                if (_savedMaterials.TryGetValue(geometryModel3D, out material))
                    geometryModel3D.Material = material;

                if (_savedBackMaterials.TryGetValue(geometryModel3D, out material))
                    geometryModel3D.BackMaterial = material;
                else
                    geometryModel3D.BackMaterial = null; // BackMaterial may be set when using TwoSided materials
            });

            _transparentModels.Clear();
        }
        
        private void UseOriginalMaterials(Model3D selectedModel3D)
        {
            if (_savedMaterials == null)
                return;

            Ab3d.Utilities.ModelIterator.IterateGeometryModel3DObjects(selectedModel3D, null, (geometryModel3D, transform3D) =>
            {
                System.Windows.Media.Media3D.Material material;
                if (_savedMaterials.TryGetValue(geometryModel3D, out material))
                    geometryModel3D.Material = material;

                if (_savedBackMaterials.TryGetValue(geometryModel3D, out material))
                    geometryModel3D.BackMaterial = material;
                else
                    geometryModel3D.BackMaterial = null; // BackMaterial may be set when using TwoSided materials

                // remove moved from list of transparent models (if it was in the list)
                _transparentModels.Remove(geometryModel3D);
            });
        }

        protected void OnSelectedModelChanged(Model3D selectedModel3D)
        {
            Transform3D selectedModelParentTransform3D;
            Transform3D selectedModelFullTransform3D;

            if (selectedModel3D == _loadedModel3D) // when we select the root node, this is processed as deselecting any selected model
                selectedModel3D = null; 

            if (selectedModel3D == null)
            {
                _meshGeometryToInspect         = null;
                selectedModelParentTransform3D = null;
                selectedModelFullTransform3D   = null;
                ClearNormals();

                ZoomToObjectButton.IsEnabled = false;
                ObjectInfoButton.IsEnabled = false;
                DeselectButton.IsEnabled = false;

                MouseCameraController1.RotateAroundMousePosition = true;
                MouseCameraController1.ZoomMode = MouseCameraController.CameraZoomMode.MousePosition;
            }
            else
            {
                var geometryModel3D = selectedModel3D as GeometryModel3D;

                if (geometryModel3D != null)
                    _meshGeometryToInspect = geometryModel3D.Geometry as MeshGeometry3D;
                else
                    _meshGeometryToInspect = null;

                // We need two transformations for the selected model
                // 1) the transformation of all parent models. This is used to show wireframe lines.
                // 2) the full transformation of the model (= parent transform + model transform). This is used to show normals and mesh info
                selectedModelParentTransform3D = Ab3d.Utilities.TransformationsHelper.GetModelTotalTransform(_loadedModel3D, selectedModel3D, addFinalModelTransformation: false); // if the addFinalModelTransformation parameter would be true, we would get the full transformation of the model
                selectedModelFullTransform3D   = Ab3d.Utilities.TransformationsHelper.CombineTransform3D(selectedModel3D.Transform, selectedModelParentTransform3D);

                if (geometryModel3D != null && (ShowNormalsCheckBox.IsChecked ?? false))
                    ShowNormals(selectedModel3D, selectedModelFullTransform3D);
                else
                    ClearNormals();

                ZoomToObjectButton.IsEnabled = true;
                ObjectInfoButton.IsEnabled = true;
                DeselectButton.IsEnabled = true;


                var selectedModelCenterPosition = selectedModel3D.Bounds.GetCenterPosition();
                if (selectedModelParentTransform3D != null)
                    selectedModelCenterPosition = selectedModelParentTransform3D.Transform(selectedModelCenterPosition);

                Camera1.RotationCenterPosition = selectedModelCenterPosition;
                MouseCameraController1.RotateAroundMousePosition = false;
                MouseCameraController1.ZoomMode = MouseCameraController.CameraZoomMode.CameraRotationCenterPosition;
            }

            _selectedModel3D                = selectedModel3D;
            _selectedModelParentTransform3D = selectedModelParentTransform3D;
            _selectedModelFullTransform3D   = selectedModelFullTransform3D;

            UpdateWireframe();

            UpdateNonSelectedObjectsTransparency();

            UpdateMeshInspector();
        }

        private void ShowFileFormatsButton_OnClick(object sender, RoutedEventArgs e)
        {
            var assimpWpfImporter = new Ab3d.Assimp.AssimpWpfImporter();
            string[] supportedImportFormats = assimpWpfImporter.SupportedImportFormats;

            var assimpWpfExporter = new Ab3d.Assimp.AssimpWpfExporter();
            string[] supportedExportFormats = assimpWpfExporter.ExportFormatDescriptions.Select(f => f.FileExtension).ToArray();

            string fileFormatsInfo = string.Format("Using native Assimp library version {0}.\r\n\r\nSupported import formats:\r\n{1}\r\n\r\nSupported export formats:\r\n{2}",
                assimpWpfImporter.AssimpVersion,
                string.Join(", ", supportedImportFormats),
                string.Join(", ", supportedExportFormats));

            MessageBox.Show(fileFormatsInfo, "Supported Assimp file formats", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void ShowAllButton_OnClick(object sender, RoutedEventArgs e)
        {
            ShowAllObjects();
        }

        private void ZoomToObjectButton_OnClick(object sender, RoutedEventArgs e)
        {
            ZoomToSelectedModel();
        }

        private void ObjectInfoButton_OnClick(object sender, RoutedEventArgs e)
        {
            var selectedModel = GetSelectedModel3D();

            if (selectedModel == null)
                return;

            string objectInfoText = "Selected WPF object info:\r\n" + 
                                    Ab3d.Utilities.Dumper.GetObjectHierarchyString(selectedModel);

            var sceneNodeForWpfObject = MainDXViewportView.GetSceneNodeForWpfObject(selectedModel);
            if (sceneNodeForWpfObject != null)
            {
                objectInfoText += "\r\n\r\nSelected DXEngine's SceneNodes:\r\n";
                AddSceneNodeDetailsText(sceneNodeForWpfObject, "", ref objectInfoText);
            }

            var geometryModel3D = selectedModel as GeometryModel3D;
            if (geometryModel3D != null)
            {
                var meshGeometry3D = geometryModel3D.Geometry as MeshGeometry3D;

                if (meshGeometry3D != null)
                {
                    objectInfoText += "\r\n\r\nMeshGeometry3D info:\r\n" + Ab3d.Utilities.Dumper.GetDumpString(meshGeometry3D, maxLineCount: 100, "0.00");

                    if (meshGeometry3D.Positions.Count < 1000)
                        objectInfoText += "\r\n\r\nMeshInitializationCode:\r\n" + Ab3d.Utilities.Dumper.GetMeshInitializationCode(meshGeometry3D);
                    else
                        objectInfoText += string.Format("\r\n\r\nMeshInitializationCode skipped because mesh has more than 1000 positions ({0}).\r\n", meshGeometry3D.Positions.Count);
                }
            }

            ShowMessageWindow("3D Object info", objectInfoText);
        }

        private void AddSceneNodeDetailsText(SceneNode sceneNode, string indentString, ref string objectInfoText)
        {
            objectInfoText += indentString + sceneNode.GetDetailsText() + Environment.NewLine;

            foreach (var childNode in sceneNode.ChildNodes)
                AddSceneNodeDetailsText(childNode, indentString + "    ", ref objectInfoText);
        }

        private void ShowMessageWindow(string title, string message)
        {
            var textBox = new System.Windows.Controls.TextBox()
            {
                Margin = new Thickness(10, 10, 10, 10),
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                FontFamily = new FontFamily("Consolas"),
                Text = message
            };

            var window = new Window()
            {
                Title = title
            };

            window.Content = textBox;
            window.Show();
        }

        private void DeselectButton_OnClick(object sender, RoutedEventArgs e)
        {
            SelectTreeViewItem(null);
        }

        private void ShowSceneNodesButton_OnClick(object sender, RoutedEventArgs e)
        {
            if (MainDXViewportView.DXScene == null)
                return;

            var sceneNodesDumpString = MainDXViewportView.DXScene.GetSceneNodesDumpString(showBounds: true, showTransform: true, showDirtyFlags: false, showStatistics: true, showMaterialInfo: true, showMeshInfo: true);

            ShowMessageWindow("Scene nodes", sceneNodesDumpString);
        }
        

        private Color ParseColor(string colorText)
        {
            if (_colorTypeConverter == null)
                _colorTypeConverter = System.ComponentModel.TypeDescriptor.GetConverter(typeof(Color));

            var color = (Color)_colorTypeConverter.ConvertFromString(colorText);

            return color;
        }

        private Color GetColorFromComboBox(System.Windows.Controls.ComboBox comboBox)
        {
            var comboBoxItem = comboBox.SelectedItem as ComboBoxItem;

            if (comboBoxItem == null)
                return Colors.Black;

            string colorText = comboBoxItem.Content.ToString();

            var color = ParseColor(colorText);

            return color;
        }
        
        private double GetDoubleFromComboBox(System.Windows.Controls.ComboBox comboBox, double fallbackValue = 0)
        {
            if (comboBox.SelectedItem is double)
                return (double)comboBox.SelectedItem;
            
            if (comboBox.SelectedItem is float)
                return (float)comboBox.SelectedItem;

            if (comboBox.SelectedItem is Int32)
                return (int)comboBox.SelectedItem;


            var comboBoxItem = comboBox.SelectedItem as ComboBoxItem;

            if (comboBoxItem == null)
                return fallbackValue;

            string valueText = comboBoxItem.Content.ToString();
            double doubleValue;

            if (!double.TryParse(valueText, NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out doubleValue))
                doubleValue = fallbackValue;

            return doubleValue;
        }

        private void OnWireframeLineSettingsChanged(object sender, RoutedEventArgs e)
        {
            if (!this.IsLoaded)
                return;

            UpdateWireframe();
        }

        private void UpdateWireframe()
        {
            if (ShowWireframeCheckBox.IsChecked ?? false)
            {
                if (_selectedModel3D != null)
                {
                    AllWireframesLineVisual3D.IsVisible = false;
                    SelectedModelWireframeVisual3D.IsVisible = true;

                    bool showPolygonLines = ReadPolygonIndicesCheckBox.IsChecked ?? false;
                    var wireframeLinePositions = WireframeFactory.CreateWireframeLinePositions(_selectedModel3D, _selectedModelParentTransform3D, showPolygonLines, removedDuplicates: true);

                    SelectedModelWireframeVisual3D.Positions = wireframeLinePositions;
                }
                else
                {
                    AllWireframesLineVisual3D.IsVisible = true;
                    SelectedModelWireframeVisual3D.IsVisible = false;
                }

                AllWireframesLineVisual3D.LineColor = GetColorFromComboBox(LineColorComboBox);
                AllWireframesLineVisual3D.LineThickness = GetDoubleFromComboBox(LineThicknessComboBox, fallbackValue: 0.5);

                SelectedModelWireframeVisual3D.LineColor = AllWireframesLineVisual3D.LineColor;
                SelectedModelWireframeVisual3D.LineThickness = AllWireframesLineVisual3D.LineThickness;

                if (AddLineDepthBiasCheckBox.IsChecked ?? false)
                {
                    // Use line depth bias to move the lines closer to the camera so the lines are rendered on top of solid model and are not partially occluded by it.
                    // See DXEngineVisuals/LineDepthBiasSample for more info.
                    AllWireframesLineVisual3D.SetDXAttribute(DXAttributeType.LineDepthBias, 0.1);
                    AllWireframesLineVisual3D.SetDXAttribute(DXAttributeType.LineDynamicDepthBiasFactor, 0.02);

                    SelectedModelWireframeVisual3D.SetDXAttribute(DXAttributeType.LineDepthBias, 0.1);
                    SelectedModelWireframeVisual3D.SetDXAttribute(DXAttributeType.LineDynamicDepthBiasFactor, 0.02);
                }
                else
                {
                    AllWireframesLineVisual3D.ClearDXAttribute(DXAttributeType.LineDepthBias);
                    AllWireframesLineVisual3D.ClearDXAttribute(DXAttributeType.LineDynamicDepthBiasFactor);

                    SelectedModelWireframeVisual3D.ClearDXAttribute(DXAttributeType.LineDepthBias);
                    SelectedModelWireframeVisual3D.ClearDXAttribute(DXAttributeType.LineDynamicDepthBiasFactor);
                }
            }
            else
            {
                AllWireframesLineVisual3D.IsVisible = false;
                SelectedModelWireframeVisual3D.IsVisible = false;
            }
        }

        private void OnShowWireframeCheckBoxCheckedChanged(object sender, RoutedEventArgs e)
        {
            if (!this.IsLoaded)
                return;

            UpdateWireframe();
        }

        private void AmbientLightSlider_OnValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            UpdateAmbientLight();
        }

        private void UpdateAmbientLight()
        {
            if (AmbientLightSlider == null || SceneAmbientLight == null)
                return;

            var color = (byte)(2.55 * AmbientLightSlider.Value); // Minimum="0" Maximum="100"
            SceneAmbientLight.Color = Color.FromRgb(color, color, color);
        }

        private void OnShowMeshInspectorCheckBoxCheckedChanged(object sender, RoutedEventArgs e)
        {
            if (!this.IsLoaded)
                return;

            UpdateMeshInspector();
        }

        private void UpdateMeshInspector()
        {
            if (MeshInspector == null)
                return;

            if (_meshGeometryToInspect != null && (ShowMeshInspectorCheckBox.IsChecked ?? false))
            {
                if (_meshGeometryToInspect.Positions.Count > 500)
                {
                    var result = MessageBox.Show(string.Format("The selected MeshGeometry3D contains many positions ({0}).\r\nBecause MeshInspectorOverlay shows indexes of each position and triangle,\r\nthis may make the application unresponsive.\r\n\r\nAre you sure that you want to use MeshInspectorOverlay on selected model?", _meshGeometryToInspect.Positions.Count), "", MessageBoxButton.YesNo);

                    if (result != MessageBoxResult.Yes)
                        _meshGeometryToInspect = null;
                }

                MeshInspector.Transform = _selectedModelFullTransform3D;
                MeshInspector.MeshGeometry3D = _meshGeometryToInspect;
            }
            else
            {
                MeshInspector.MeshGeometry3D = null;
                MeshInspector.Transform = null;
            }
        }

        private void SelectedObjectColorSettingsComboBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateNormalsAndMeshInspectorColors();
        }

        private void UpdateNormalsAndMeshInspectorColors()
        {
            if (SelectedObjectColorSettingsComboBox == null || MeshInspector == null || NormalLinesVisual3D == null)
                return;

            switch (SelectedObjectColorSettingsComboBox.SelectedIndex)
            {
                case 1:
                    NormalLinesVisual3D.LineColor = Colors.Blue;
                    MeshInspector.PositionsTextColor = Colors.Aqua;
                    MeshInspector.TriangleIndexesTextColor = Colors.LightBlue;
                    break;
                
                case 2:
                    NormalLinesVisual3D.LineColor = Colors.White;
                    MeshInspector.PositionsTextColor = Colors.LightGray;
                    MeshInspector.TriangleIndexesTextColor = Colors.Gray;
                    break;
                
                case 3:
                    NormalLinesVisual3D.LineColor = Colors.Black;
                    MeshInspector.PositionsTextColor = Colors.Gray;
                    MeshInspector.TriangleIndexesTextColor = Colors.LightGray;
                    break;

                //case 0:
                default:
                    NormalLinesVisual3D.LineColor = Colors.Red;
                    MeshInspector.PositionsTextColor = Colors.Yellow;
                    MeshInspector.TriangleIndexesTextColor = Colors.Orange;
                    break;
            }
        }

        private void OnLightSettingsChanged(object sender, RoutedEventArgs e)
        {
            if (!this.IsLoaded)
                return;

            Camera1.ShowCameraLight = (CameraLightCheckBox.IsChecked ?? false) ? ShowCameraLightType.Always : ShowCameraLightType.Never;
            
            if (TopDownLightCheckBox.IsChecked ?? false)
            {
                if (_topDownLight == null)
                    _topDownLight = new DirectionalLight(Colors.White, new System.Windows.Media.Media3D.Vector3D(0, -1, 0));

                if (!LightsModel3DGroup.Children.Contains(_topDownLight))
                    LightsModel3DGroup.Children.Add(_topDownLight);
            }
            else
            {
                if (_topDownLight != null && LightsModel3DGroup.Children.Contains(_topDownLight))
                    LightsModel3DGroup.Children.Remove(_topDownLight);
            }
            
            if (SideLightCheckBox.IsChecked ?? false)
            {
                if (_sideLight == null)
                    _sideLight = new DirectionalLight(Colors.White, new System.Windows.Media.Media3D.Vector3D(1, 0, 0));

                if (!LightsModel3DGroup.Children.Contains(_sideLight))
                    LightsModel3DGroup.Children.Add(_sideLight);
            }
            else
            {
                if (_sideLight != null && LightsModel3DGroup.Children.Contains(_sideLight))
                    LightsModel3DGroup.Children.Remove(_sideLight);
            }
        }

        private void CameraTypeComboBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!this.IsLoaded)
                return;

            Camera1.CameraType = CameraTypeComboBox.SelectedIndex == 0 ? BaseCamera.CameraTypes.PerspectiveCamera : BaseCamera.CameraTypes.OrthographicCamera;
        }


        private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _mouseDownPosition = e.GetPosition(this);
            _isLeftMouseDown = true;
            _isDoubleClick = e.ClickCount == 2; // e.ClickCount in OnMouseLeftButtonUp event handler is always 1, so we need to read its value here
        }

        private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (!_isLeftMouseDown) // is mouse down was done outside of our view
                return;

            var mouseUpPosition = e.GetPosition(this);

            if ((_mouseDownPosition - mouseUpPosition).Length < MouseCameraController1.MouseMoveThreshold)
            {
                if (_isDoubleClick)
                    ProcessMouseClick(_mouseDownPosition, isDoubleClick: true);
                else
                    ProcessMouseClick(_mouseDownPosition, isDoubleClick: false);

                e.Handled = true;
            }

            _isLeftMouseDown = false;
        }

        private void ProcessMouseClick(Point mouseLocation, bool isDoubleClick)
        {
            // We are using hit testing from Ab3d.DXEngine.
            // This is the fastest option, but to get hit WPF object, this requires more work (casting to BaseWpfObjectNode and calling GetOriginalWpfObject).
            //
            // See samples in "Ab3d.DXEngine hit testing" section for more hit testing options and information.

            var dxRayHitTestResult = MainDXViewportView.GetClosestHitObject(mouseLocation);

            GeometryModel3D hitGeometryModel3D = null;

            if (dxRayHitTestResult != null)
            {
                var hitWpfObjectNode = dxRayHitTestResult.HitSceneNode as BaseWpfObjectNode;

                if (hitWpfObjectNode != null)
                {
                    var hitWpfObject = hitWpfObjectNode.GetOriginalWpfObject();
                    hitGeometryModel3D = hitWpfObject as GeometryModel3D;
                }
            }

            if (hitGeometryModel3D != null)
            {
                SelectTreeViewItem(hitGeometryModel3D);

                if (isDoubleClick)
                    ZoomToObject(hitGeometryModel3D);
            }
            else
            {
                SelectTreeViewItem(null);

                if (isDoubleClick)
                    ShowAllObjects();
            }
        }

        private void OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                var now = DateTime.Now;

                if (_lastEscapeKeyPressedTime != DateTime.MinValue && (now - _lastEscapeKeyPressedTime).TotalSeconds < 1)
                {
                    // On second escape key press, we show all objects
                    ShowAllObjects();
                    _lastEscapeKeyPressedTime = DateTime.MinValue;
                }
                else
                {
                    // On first escape key press we deselect selected object
                    SelectTreeViewItem(null);
                    _lastEscapeKeyPressedTime = now;
                }

                e.Handled = true;
            }
        }
    }
}
