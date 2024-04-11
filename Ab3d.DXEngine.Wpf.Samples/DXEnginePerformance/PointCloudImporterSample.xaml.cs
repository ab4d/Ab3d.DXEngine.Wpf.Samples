using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using Ab3d.Assimp;
using Ab3d.DirectX;
using Ab3d.DXEngine.Wpf.Samples.Common;
using Ab3d.DXEngine.Wpf.Samples.DXEngineHitTesting;
using Ab3d.Visuals;
using SharpDX;

namespace Ab3d.DXEngine.Wpf.Samples.DXEnginePerformance
{
    /// <summary>
    /// Interaction logic for PointCloudImporterSample.xaml
    /// </summary>
    public partial class PointCloudImporterSample : Page
    {
        private const bool ExportAsBinaryPly = true;

        private DisposeList _disposables;
        private string _fileName;

        private float _pixelSize;
        private PixelsVisual3D _pixelsVisual3D;
        private bool _isWorldSize;
        private bool _isCircularPixel;
        private double _boundsDiagonalLength;

        private double _pixelSizeSliderFactor;
        private AssimpWpfImporter _assimpWpfImporter;
        private Point3D _startMovePosition;
        private DXEventManager3DWrapper _eventManager;
        
        private Vector3 _startPos;
        private Vector3 _endPos;
        private Color4[] _savedPositionColors;
        private bool _isCropping;
        private ModelMoverVisual3D _modelMover1;
        private ModelMoverVisual3D _modelMover2;

        public PointCloudImporterSample()
        {
            InitializeComponent();

            var dragAndDropHelper = new DragAndDropHelper(this, ".*");
            dragAndDropHelper.FileDropped += (sender, args) => LoadPointCloud(args.FileName);

            _pixelSize = 2;
            _pixelSizeSliderFactor = 0.05; // set slider to go from 0 to 5
            _isWorldSize = false;
            _isCircularPixel = true;

            _disposables = new DisposeList();

            MainDXViewportView.DXSceneInitialized += delegate (object sender, EventArgs args)
            {
                // The initially loaded point-cloud is a cropped version of a bigger point-cloud created by maxch (see Resources/PointClouds/readme.txt for more info)
                string fileName = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"Resources\PointClouds\14 Ladybrook Road 10 - cropped.ply");
                LoadPointCloud(fileName);
            };

            this.Unloaded += delegate (object sender, RoutedEventArgs args)
            {
                _disposables.Dispose();
                MainDXViewportView.Dispose();
            };
        }

        private void LoadPointCloud(string fileName)
        {
            Mouse.OverrideCursor = Cursors.Wait;
            
            if (_disposables != null)
            {
                _disposables.Dispose();
                _disposables = null;
            }

            _disposables = new DisposeList();

            StopCropping();
            MainViewport.Children.Clear();


            try
            {
                Color4[] positionColors;
                var positions = LoadPositions(fileName, out positionColors);

                if (positions == null)
                {
                    PixelsCountTextBlock.Text = "";
                    return;
                }

                var positionsBounds = BoundingBox.FromPoints(positions);
                _boundsDiagonalLength = positionsBounds.ToRect3D().GetDiagonalLength();

                Camera1.TargetPosition = positionsBounds.Center.ToWpfPoint3D();
                Camera1.Distance = _boundsDiagonalLength * 1.8;


                // When using WorldSize pixel size, then set the Maximum size based on the size of the scene (max value is 1/200 of the _boundsDiagonalLength)
                if (IsWorldSizeCheckBox.IsChecked ?? false)
                    _pixelSizeSliderFactor = _boundsDiagonalLength / 20000;


                _pixelsVisual3D = new PixelsVisual3D(positions)
                {
                    IsCircularPixel = _isCircularPixel,
                    IsWorldSize = _isWorldSize
                };

                _disposables.Add(_pixelsVisual3D);

                UpdatePixelSize();


                if (positionColors != null)
                {
                    _pixelsVisual3D.PixelColor = Colors.White; // When using PixelColors, PixelColor is used as a mask (multiplied with each color)
                    _pixelsVisual3D.PixelColors = positionColors;
                }
                else
                {
                    _pixelsVisual3D.PixelColor = Colors.Black;
                }

                MainViewport.Children.Add(_pixelsVisual3D);

                _fileName = fileName;
                PixelsCountTextBlock.Text = string.Format(System.Globalization.CultureInfo.InvariantCulture, "Points count: {0:#,##0}", positions.Length);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error loading file", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                Mouse.OverrideCursor = null;
            }
        }

        private Vector3[] LoadPositions(string fileName, out Color4[] positionColors)
        {
            Vector3[] positions = null;
            positionColors = null;
            
            var fileExtension = System.IO.Path.GetExtension(fileName);
            bool swapYZCoordinates = ZUpAxisCheckBox.IsChecked ?? false;

            // Use PlyPointCloudReader to read .ply files
            // PlyPointCloudReader is available with full source code in the Common folder so you can change it to your needs.
            if (fileExtension.Equals(".ply", StringComparison.OrdinalIgnoreCase))
            {
                var plyPointCloudReader = new PlyPointCloudReader()
                {
                    SwapYZCoordinates = swapYZCoordinates
                };

                positions = plyPointCloudReader.ReadPointCloud(fileName);

                positionColors = plyPointCloudReader.PixelColors;
            }
            else
            {
                // Use AssimpImporter to read other files

                if (_assimpWpfImporter == null)
                {
                    // See Ab3d.PowerToys.Samples/AssimpSamples/AssimpWpfImporterSample.xaml.cs in Ab3d.PowerToys samples for more information.
                    AssimpLoader.LoadAssimpNativeLibrary();
                    _assimpWpfImporter = new AssimpWpfImporter();
                }

                var readModel3D = _assimpWpfImporter.ReadModel3D(fileName, texturesPath: null); // we can also define a textures path if the textures are located in some other directory (this is parameter can be skipped, but is defined here so you will know that you can use it)

                // First we count all the positions so we can allocate the positions and position color arrays
                var totalPositionsCount = CountPositions(readModel3D);

                if (totalPositionsCount > 0)
                {
                    positions = new Vector3[totalPositionsCount];
                    var newPositionColors = new Color4[totalPositionsCount]; 

                    int index = 0;
                    
                    // Go through all child GeometryModel3D objects and add their positions to the positions.
                    // We also add color of the positions from the material's color.
                    Ab3d.Utilities.ModelIterator.IterateGeometryModel3DObjects(readModel3D, parentTransform3D: null, callback: (childGeometryModel3D, parentTransform3D) =>
                    {
                        var meshGeometry3D = childGeometryModel3D.Geometry as MeshGeometry3D;
                        if (meshGeometry3D != null)
                        {
                            var point3DCollection = meshGeometry3D.Positions;

                            if (point3DCollection != null && point3DCollection.Count > 0)
                            {
                                // Convert Point3D to Vector3
                                var count = point3DCollection.Count;

                                var material = childGeometryModel3D.Material ?? childGeometryModel3D.BackMaterial;
                                var materialColor = Ab3d.Utilities.ModelUtils.GetMaterialDiffuseColor(material, Colors.Black);
                                var materialColor4 = materialColor.ToColor4();

                                var childTransform = Ab3d.Utilities.ModelUtils.CombineTransform(parentTransform3D, childGeometryModel3D.Transform);

                                if (childTransform == null || childTransform.Value.IsIdentity)
                                {
                                    if (swapYZCoordinates)
                                    {
                                        for (int i = 0; i < count; i++)
                                        {
                                            int newIndex = i + index;
                                            var point3D = point3DCollection[i];
                                            positions[newIndex] = new Vector3((float)point3D.X, (float)point3D.Z, (float)point3D.Y); // swap y and z
                                            newPositionColors[newIndex] = materialColor4;
                                        }
                                    }
                                    else
                                    {
                                        for (int i = 0; i < count; i++)
                                        {
                                            int newIndex = i + index;
                                            positions[newIndex] = point3DCollection[i].ToVector3();
                                            newPositionColors[newIndex] = materialColor4;
                                        }
                                    }
                                }
                                else
                                {
                                    var matrix = childTransform.Value.ToMatrix(); // Convert WPF matrix to SharpDX matrix that uses floats. This will be faster.

                                    if (swapYZCoordinates)
                                    {
                                        for (int i = 0; i < count; i++)
                                        {
                                            int newIndex = i + index;
                                            var vector3 = point3DCollection[i].ToVector3();

                                            Vector3.Transform(ref vector3, ref matrix, out vector3); // First transform, then swap y and z

                                            positions[newIndex] = new Vector3(vector3.X, vector3.Z, vector3.Y); // swap y and z
                                            newPositionColors[newIndex] = materialColor4;
                                        }
                                    }
                                    else
                                    {
                                        for (int i = 0; i < count; i++)
                                        {
                                            int newIndex = i + index;
                                            var vector3 = point3DCollection[i].ToVector3();

                                            Vector3.Transform(ref vector3, ref matrix, out vector3);

                                            positions[newIndex] = vector3;
                                            newPositionColors[newIndex] = materialColor4;
                                        }
                                    }
                                }

                                index += count;
                            }
                        }
                    });

                    positionColors = newPositionColors;
                }
            }

            return positions;
        }

        private int CountPositions(Model3D model3D)
        {
            var geometryModel3D = model3D as GeometryModel3D;
            if (geometryModel3D != null)
            {
                var meshGeometry3D = geometryModel3D.Geometry as MeshGeometry3D;
                if (meshGeometry3D != null && meshGeometry3D.Positions != null)
                    return meshGeometry3D.Positions.Count;
            }
            else
            {
                var model3DGroup = model3D as Model3DGroup;
                if (model3DGroup != null)
                {
                    var childPositionsCount = 0;
                    foreach (var childModel3D in model3DGroup.Children)
                        childPositionsCount += CountPositions(childModel3D);

                    return childPositionsCount;
                }
            }

            return 0; // For example for Light Model3D object
        }

        private void OnIsWorldSizeCheckedChanged(object sender, RoutedEventArgs e)
        {
            if (!this.IsLoaded)
                return;


            _isWorldSize = IsWorldSizeCheckBox.IsChecked ?? false;

            if (_pixelsVisual3D != null)
                _pixelsVisual3D.IsWorldSize = _isWorldSize;

            if (_isWorldSize)
                _pixelSizeSliderFactor = _boundsDiagonalLength / 20000; // When using WorldSize pixel size, then set the Maximum size based on the size of the scene (max value is 1/200 of the _boundsDiagonalLength)
            else
                _pixelSizeSliderFactor = 0.05; // set slider to go from 0 to 5

            UpdatePixelSize();

            MainDXViewportView.Refresh();
        }

        private void OnIsCircularPixelCheckedChanged(object sender, RoutedEventArgs e)
        {
            if (!this.IsLoaded)
                return;
            
            _isCircularPixel = IsCircularPixelCheckBox.IsChecked ?? false;

            if (_pixelsVisual3D != null)
                _pixelsVisual3D.IsCircularPixel = _isCircularPixel;
            
            MainDXViewportView.Refresh();
        }

        private void PixelSizeSlider_OnValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!this.IsLoaded)
                return;

            UpdatePixelSize();

            MainDXViewportView.Refresh();
        }

        private void UpdatePixelSize()
        {
            // PixelSizeSlider has a max value 100; use _pixelSizeSliderFactor to convert that to correct PixelSize
            _pixelSize = (float)(_pixelSizeSliderFactor * PixelSizeSlider.Value);

            PixelSizeTextBlock.Text = string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0:0.###}", _pixelSize);

            if (_pixelsVisual3D != null)
                _pixelsVisual3D.PixelSize = _pixelSize;
        }

        private void OnZUpAxisCheckBoxCheckedChanged(object sender, RoutedEventArgs e)
        {
            if (_fileName == null)
                return;

            // Reload the data again
            LoadPointCloud(_fileName);
        }

        private void StopCropping()
        {
            if (!_isCropping) 
                return;

            _pixelsVisual3D.PixelColors = _savedPositionColors;
            _savedPositionColors = null;

            CropButton.Content = "Crop";
            _isCropping = false;

            ExportButton.Visibility = Visibility.Collapsed;


            MainViewport.Children.Remove(_modelMover1);
            MainViewport.Children.Remove(_modelMover2);

            _modelMover1 = null;
            _modelMover2 = null;
        }

        private void StartCropping()
        {
            if (_isCropping)
                return;

            // Setup and show 2 ModelMovesVisual3D objects
            // See PowerToysOther/ModelMoverOverlaySample.xaml.cs for more info

            if (_eventManager == null)
            {
                if (MainDXViewportView.DXScene == null)
                    return; // WPF 3D rendering


                _eventManager = new DXEventManager3DWrapper(MainDXViewportView);
                _eventManager.UsePreviewEvents = true;
                _eventManager.CustomEventsSourceElement = MainDXViewportView;


                // Clear depth buffer before rendering objects in the OverlayRenderingQueue
                // This way the objects that are rendered before OverlayRenderingQueue will not obstruct the objects in OverlayRenderingQueue
                MainDXViewportView.DXScene.OverlayRenderingQueue.ClearDepthStencilBufferBeforeRendering = true;

                // To show _selectionWireBox on top of 3D boxes, uncomment the following line:
                //if (_selectionWireBox != null)
                //    _selectionWireBox.SetDXAttribute(DXAttributeType.CustomRenderingQueue, MainDXViewportView.DXScene.OverlayRenderingQueue);

                // When hit-testing consider objects in OverlayRenderingQueue before all other objects
                // (DXScene.GetClosestHitObject will return object from OverlayRenderingQueue event if it is farther away from camera than some other object)
                MainDXViewportView.DXScene.DXHitTestOptions.OverlayRenderingQueue = MainDXViewportView.DXScene.OverlayRenderingQueue;
            }

            var bounds = _pixelsVisual3D.Bounds;
            var position1 = bounds.Location + new Vector3D(bounds.SizeX * 0.2, bounds.SizeY * 1.1, bounds.SizeZ * 0.2);
            var position2 = bounds.Location + new Vector3D(bounds.SizeX * 0.8, bounds.SizeY * -0.1, bounds.SizeZ * 0.8);

            var axisLength = bounds.GetDiagonalLength() / 15;

            _modelMover1 = ShowModelMover(position1, axisLength, MoveStartPosition);
            _modelMover2 = ShowModelMover(position2, axisLength, MoveEndPosition);

            _startPos = position1.ToVector3();
            _endPos = position2.ToVector3();

            CropPositions();

            CropButton.Content = "Stop cropping";
            _isCropping = true;

            ExportButton.Visibility = Visibility.Visible;
        }

        private void MoveStartPosition(Point3D position)
        {
            _startPos = position.ToVector3();
            CropPositions();
        }
        
        private void MoveEndPosition(Point3D position)
        {
            _endPos = position.ToVector3();
            CropPositions();
        }
        
        private void CropPositions()
        {
            if (_pixelsVisual3D == null)
                return;

            var positions = _pixelsVisual3D.Positions;
            int count = positions.Length;

            Color4[] positionColors;

            if (_savedPositionColors == null)
            {
                if (_pixelsVisual3D.PixelColors == null)
                {
                    // We have only a single color
                    var color = _pixelsVisual3D.PixelColor.ToColor4();
                    _savedPositionColors = new Color4[count];
                    positionColors = new Color4[count];
                    for (int i = 0; i < count; i++)
                    {
                        _savedPositionColors[i] = color;
                        positionColors[i] = color;
                    }
                }
                else
                {
                    // Copy original colors
                    positionColors = _pixelsVisual3D.PixelColors;
                    _savedPositionColors = new Color4[positionColors.Length];
                    Array.Copy(positionColors, _savedPositionColors, positionColors.Length);
                }
            }
            else
            {
                positionColors = _pixelsVisual3D.PixelColors;
                if (positionColors == null)
                    return;
            }

            var croppedColor = Colors.LightGray.ToColor4();

            Vector3 startPos = new Vector3(Math.Min(_startPos.X, _endPos.X), Math.Min(_startPos.Y, _endPos.Y), Math.Min(_startPos.Z, _endPos.Z));
            Vector3 endPos   = new Vector3(Math.Max(_startPos.X, _endPos.X), Math.Max(_startPos.Y, _endPos.Y), Math.Max(_startPos.Z, _endPos.Z));

            int positionsCount = 0;

            for (int i = 0; i < count; i++)
            {
                var position = positions[i];

                bool isCropped = position.X < startPos.X || position.X > endPos.X ||
                                 position.Y < startPos.Y || position.Y > endPos.Y ||
                                 position.Z < startPos.Z || position.Z > endPos.Z;

                if (isCropped)
                {
                    positionColors[i] = croppedColor;
                }
                else
                {
                    positionColors[i] = _savedPositionColors[i];
                    positionsCount++;
                }
            }

            if (_pixelsVisual3D.PixelColors == null)
                _pixelsVisual3D.PixelColors = positionColors;
            else
                _pixelsVisual3D.UpdatePixelColors();

            PixelsCountTextBlock.Text = string.Format(System.Globalization.CultureInfo.InvariantCulture, "Points count: {0:#,##0}", positionsCount);
        }

        private ModelMoverVisual3D ShowModelMover(Point3D startPositions, double axisLength, Action<Point3D> modelMovedAction)
        {
            var modelMoverVisual3D = new ModelMoverVisual3D
            {
                Position = startPositions,

                AxisLength      = axisLength,
                AxisArrowRadius = axisLength * 0.13,
                AxisRadius      = axisLength * 0.05
            };

            // Put _modelMover and _selectionWireBox to OverlayRenderingQueue
            if (MainDXViewportView.DXScene != null)
                modelMoverVisual3D.SetDXAttribute(DXAttributeType.CustomRenderingQueue, MainDXViewportView.DXScene.OverlayRenderingQueue);

            // IMPORTANT !!!
            // When ModelMoverVisual3D is used with EventManager3D
            // we need to call SubscribeWithEventManager3D to use EventManager3D for mouse events processing
            modelMoverVisual3D.SubscribeWithEventManager3D(_eventManager);
 

            // Setup event handlers
            modelMoverVisual3D.ModelMoveStarted += delegate (object o, EventArgs eventArgs)
            {
                _startMovePosition = modelMoverVisual3D.Position;

                // Disable MouseCameraController while dragging
                MouseCameraController1.IsEnabled = false;
            };

            modelMoverVisual3D.ModelMoved += delegate (object o, Ab3d.Common.ModelMovedEventArgs e)
            {
                var newPosition = _startMovePosition + e.MoveVector3D;
                modelMoverVisual3D.Position = newPosition;

                if (modelMovedAction != null)
                    modelMovedAction(newPosition);
            };

            modelMoverVisual3D.ModelMoveEnded += delegate (object sender, EventArgs args)
            {
                // Enable MouseCameraController again
                MouseCameraController1.IsEnabled = true;
            };

            MainViewport.Children.Add(modelMoverVisual3D);

            return modelMoverVisual3D;
        }


        private void CropButton_OnClick(object sender, RoutedEventArgs e)
        {
            if (_isCropping)
                StopCropping();
            else
                StartCropping();
        }

        private void ExportButton_OnClick(object sender, RoutedEventArgs e)
        {
            if (_pixelsVisual3D == null)
                return;

            var positions = _pixelsVisual3D.Positions;
            int count = positions.Length;
            
            Vector3 startPos = _startPos;
            Vector3 endPos = _endPos;

            var croppedPositions = new List<Vector3>();
            var croppedPositionColors = new List<Color4>();

            var isZUpAxis = ZUpAxisCheckBox.IsChecked ?? false;

            for (int i = 0; i < count; i++)
            {
                var position = positions[i];

                bool isCropped = position.X < startPos.X || position.X > endPos.X ||
                                 position.Y < startPos.Y || position.Y > endPos.Y ||
                                 position.Z < startPos.Z || position.Z > endPos.Z;

                if (!isCropped)
                {
                    if (isZUpAxis)
                        croppedPositions.Add(new Vector3(position.X, position.Z, position.Y)); // swap z and y
                    else
                        croppedPositions.Add(position);

                    croppedPositionColors.Add(_savedPositionColors[i]);
                }
            }


            var saveFileDialog = new Microsoft.Win32.SaveFileDialog
            {
                OverwritePrompt = true,
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                FileName = System.IO.Path.ChangeExtension(System.IO.Path.GetFileName(_fileName), ".ply"),
                DefaultExt = "ply",
                Filter = "ply file (*.ply)|*.ply",
                Title = "Select file name to save the cropped positions"
            };

            if (saveFileDialog.ShowDialog() ?? false)
            {
                try
                {
                    PlyPointCloudWriter.ExportPointCloud(saveFileDialog.FileName, croppedPositions.ToArray(), croppedPositionColors.ToArray(), isBinaryFileFormat: ExportAsBinaryPly);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error saving ply:\r\n" + ex.Message);
                }
            }
        }
    }
}

