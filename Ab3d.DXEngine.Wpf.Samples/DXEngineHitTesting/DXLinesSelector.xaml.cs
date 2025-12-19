using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using Ab3d.Common.Cameras;
using Ab3d.DirectX;
using Ab3d.DirectX.Materials;
using Ab3d.Utilities;
using Ab3d.Visuals;
using Color = System.Windows.Media.Color;
using Point = System.Windows.Point;

#if SHARPDX
using SharpDX;
using Matrix = SharpDX.Matrix;
#endif

// NOTE:
// This sample is the similar to the LinesSelector sample in the Ab3d.PowerToys samples project.
// The difference is that here we use DXLineSelectorData that can work with Vector3 data (use float) instead of LineSelectorData that use Point3D data (use double).
// This makes the DXLineSelectorData faster.
// This also allows creating DXEngine's ScreenSpaceLineNode objects from the line positions and does not require to crete line objects that are derived from Visual3D objects.
// This is much faster because it does not require to create slow WPF objects that are than converted to ScreenSpaceLineNode by the DXEngine.
// 
// If you still want to show the 3D lines in this sample by using Visual3D objects, you can set the UseDXScreenSpaceLineNode const to false.

namespace Ab3d.DXEngine.Wpf.Samples.DXEngineHitTesting
{
    /// <summary>
    /// Interaction logic for DXLinesSelector.xaml
    /// </summary>
    public partial class DXLinesSelector : Page
    {
        private static readonly bool UseDXScreenSpaceLineNode = true; 
        private static readonly bool AddTranslate = false;      // Set to true to test adding transformation to positions
        private static readonly float LinePositionsRange = 100; // defines the length of the generated lines

        private Random _rnd = new Random();
        
        private List<DXLineSelectorData> _allLineSelectorData;
        private List<DXLineSelectorData> _selectedLineSelectorData;

        private DXLineSelectorData _lastSelectedLineSelector;
        private Color _savedLineColor;

        private bool _isCameraChanged;

        private DisposeList _disposables;

        private Ab3d.Visuals.SphereVisual3D _closestPositionSphereVisual3D;
        private Vector2 _lastMousePosition;
        private Vector2 _usedMousePosition;

        private float _maxSelectionDistance;

        private Stopwatch _stopwatch;

        public DXLinesSelector()
        {
            InitializeComponent();

            // DXLineSelectorData is a utility class that can be used to get the closest distance 
            // of a specified screen position to the 3D line.
            _allLineSelectorData = new List<DXLineSelectorData>();
            _selectedLineSelectorData = new List<DXLineSelectorData>();

            _stopwatch = new Stopwatch();

            
            CreateSampleLines();


            _isCameraChanged = true; // When true, the CalculateScreenSpacePositions method is called before calculating line distances

            Camera1.CameraChanged += delegate(object sender, CameraChangedRoutedEventArgs e)
            {
                _isCameraChanged = true; // This will call CalculateScreenSpacePositions
            };

            this.MouseMove += delegate(object sender, MouseEventArgs e)
            {
                var position = e.GetPosition(MainBorder);
                _lastMousePosition = new Vector2((float)position.X, (float)position.Y);
            };

            MainDXViewportView.SceneUpdating += (sender, args) =>
            {
                if (_usedMousePosition != _lastMousePosition || _isCameraChanged)
                    UpdateClosestLine();
            };

            Camera1.StartRotation(20, 0);


            Unloaded += (sender, args) => Dispose();
        }

        private void Dispose()
        {
            if (_disposables != null)
            {
                _disposables.Dispose();
                _disposables = null;
            }
        }

        private void CreateSampleLines()
        {
            int simpleLinesCount;
            int polyLinesCount;
            int multiLinesCount;

            if (LinesCountComboBox.SelectedIndex == 0)
            {
                simpleLinesCount = 10;
                polyLinesCount   = 20;
                multiLinesCount  = 0;
            }
            else if (LinesCountComboBox.SelectedIndex == 1)
            {
                simpleLinesCount = 10;
                polyLinesCount   = 0;
                multiLinesCount  = 20;
            }
            else
            {
                simpleLinesCount = 0;
                multiLinesCount  = 0;

                var comboBoxItem1 = (ComboBoxItem)LinesCountComboBox.SelectedItem;
                var selectedText1 = (string)comboBoxItem1.Content;

                var selectedTextParts = selectedText1.Split(' ');

                polyLinesCount = Int32.Parse(selectedTextParts[0]);
            }


            var comboBoxItem2 = (ComboBoxItem)LinesSegmentsComboBox.SelectedItem;
            var selectedText2 = (string)comboBoxItem2.Content;

            int lineSegmentsCount = int.Parse(selectedText2);


            CreateSampleLines(simpleLinesCount, polyLinesCount, multiLinesCount, lineSegmentsCount);
        }

        private void CreateSampleLines(int simpleLinesCount, int polyLinesCount, int multiLinesCount, int lineSegmentsCount)
        {
            Dispose();
            _disposables = new DisposeList();

            _allLineSelectorData.Clear();
            _selectedLineSelectorData.Clear();

            MainViewport.Children.Clear();
            _closestPositionSphereVisual3D = null;


            bool checkBoundingBox = CheckBoundingBoxCheckBox.IsChecked ?? false;


            for (int i = 0; i < simpleLinesCount; i++)
                AddRandomLineWithLineSelectorData(lineLength: 2, checkBoundingBox, isPolyLine: false);

            for (int i = 0; i < polyLinesCount; i++)
                AddRandomLineWithLineSelectorData(lineLength: lineSegmentsCount, checkBoundingBox, isPolyLine: true);

            for (int i = 0; i < multiLinesCount; i++)
                AddRandomLineWithLineSelectorData(lineLength: lineSegmentsCount, checkBoundingBox, isPolyLine: false);


            _isCameraChanged = true; // This will force calling CalculateScreenSpacePositions again
        }

        private void AddRandomLineWithLineSelectorData(int lineLength, bool checkBoundingBox, bool isPolyLine)
        {
            var lineColor = GetRandomColor();
            float lineThickness = (float)_rnd.NextDouble() * 5 + 1;

            var linePositions = CreateRandomPositions(lineLength);


            object createdLineObject;

            if (UseDXScreenSpaceLineNode)
            {
                // Create ScreenSpaceLineNode directly from the line positions
                // See also DXEngineAdvanced/ScreenSpaceLineNodeSample.xaml.cs for more info on usage of ScreenSpaceLineNodeSample

                var lineMaterial = new LineMaterial()
                {
                    LineColor = lineColor.ToColor4(),
                    LineThickness = lineThickness,
                    IsPolyLine = isPolyLine
                };
                
                var screenSpaceLineNode = new ScreenSpaceLineNode(linePositions, isLineStrip: isPolyLine, isLineClosed: false, lineMaterial);
                
                if (AddTranslate)
                    screenSpaceLineNode.Transform = new Transformation(Matrix.Translation(0, 50, 0));

                // To show ScreenSpaceLineNode in DXViewportView we need to put it inside a SceneNodeVisual3D
                var sceneNodeVisual3D = new SceneNodeVisual3D(screenSpaceLineNode);
                MainViewport.Children.Add(sceneNodeVisual3D);

                _disposables.Add(lineMaterial);
                _disposables.Add(screenSpaceLineNode);

                createdLineObject = screenSpaceLineNode;
            }
            else
            {
                // Create LineVisual3D or PolyLineVisual3D from Ab3d.PowerToys library.
                // Those two objects will be converted into ScreenSpaceLineNode by the Ab3d.DXEngine, 
                // so if a lot of lines are created or they have a lot of positions, it is worth creating ScreenSpaceLineNode directly.
                if (lineLength == 2)
                {
                    var lineVisual3D = new Ab3d.Visuals.LineVisual3D()
                    {
                        StartPosition = linePositions[0].ToWpfPoint3D(),
                        EndPosition = linePositions[linePositions.Length - 1].ToWpfPoint3D(),
                        LineColor = lineColor,
                        LineThickness = lineThickness,
                    };

                    MainViewport.Children.Add(lineVisual3D);

                    createdLineObject = lineVisual3D;
                }
                else
                {
                    var positionsCollection = new Point3DCollection(linePositions.Length);
                    for (int i = 0; i < linePositions.Length; i++)
                        positionsCollection.Add(linePositions[i].ToWpfPoint3D());

                    BaseLineVisual3D lineVisual3D;
                    if (isPolyLine)
                    {
                        lineVisual3D = new Ab3d.Visuals.PolyLineVisual3D()
                        {
                            LineColor = lineColor,
                            LineThickness = lineThickness,
                            Positions = positionsCollection
                        };
                    }
                    else
                    {
                        lineVisual3D = new Ab3d.Visuals.MultiLineVisual3D()
                        {
                            LineColor = lineColor,
                            LineThickness = lineThickness,
                            Positions = positionsCollection
                        };
                    }

                    if (AddTranslate)
                        lineVisual3D.Transform = new TranslateTransform3D(0, 50, 0);

                    MainViewport.Children.Add(lineVisual3D);

                    createdLineObject = lineVisual3D;
                }
            }


            DXLineSelectorData lineSelectorData;

            if (AddTranslate)
            {
                var translation = Matrix.Translation(0, 50, 0);

                // Create one DXLineSelectorData for each line
                // When we specify the lineThickness, then the GetClosestDistance method will report the distance from the line's edge.
                // If we want to get the distance from the center of the line, set lineThickness to 0.
                lineSelectorData = new DXLineSelectorData(linePositions, lineThickness, isLineStrip: true, positionsTransformMatrix: ref translation);
            }
            else
            {
                // Create one DXLineSelectorData for each line
                // When we specify the lineThickness, then the GetClosestDistance method will report the distance from the line's edge.
                // If we want to get the distance from the center of the line, set lineThickness to 0.
                lineSelectorData = new DXLineSelectorData(linePositions, lineThickness, isLineStrip: true);
            }

            // If the positions are transformed, then use the DXLineSelectorData constructor that also takes positionsTransformMatrix as a parameter.
            // if you do not know the transformation of the parent line objects, then you can use the following:
            //bool isVisualConnected;
            //var lineTransform = Ab3d.Utilities.TransformationsHelper.GetVisual3DTotalTransform(lineVisual3D, true, out isVisualConnected);
            //Matrix positionsTransformMatrix = lineTransform.Value.ToMatrix();
            //var lineSelectorData = new DXLineSelectorData(linePositions, lineThickness, isLineStrip: true, ref positionsTransformMatrix);


            // Store LineVisual3D or PolyLineVisual3D to Tag so we can easily retrieve that when we need to change the color
            lineSelectorData.Tag = createdLineObject;

            // We can manually set the CheckBoundingBox
            // When it is not set manually, then it is true when the number of positions in the line is more the 30
            lineSelectorData.CheckBoundingBox = checkBoundingBox;

            // PERFORMANCE NOTE:
            // When CheckBoundingBox is true and when we have bounding box of linePositions,
            // then pass that to the DXLineSelectorData to prevent going through all linePositions and calculating bounding box in the DXLineSelectorData, for example:
            //var positionsBoundingBox = new BoundingBox(minimum, maximum);
            //lineSelectorData = new DXLineSelectorData(linePositions, positionsBoundingBox, lineThickness, isLineStrip: true);
            //lineSelectorData = new DXLineSelectorData(linePositions, positionsBoundingBox, lineThickness, isLineStrip: true, positionsTransformMatrix: ref translation);

            _allLineSelectorData.Add(lineSelectorData);
        }

        private void UpdateClosestLine()
        {
            if (_allLineSelectorData == null || _allLineSelectorData.Count == 0)
                return;


            bool isMultiThreaded = MultiThreadedCheckBox.IsChecked ?? false;


            double calculateScreenSpacePositionsTime;
            double getClosestDistanceTime;

            _stopwatch.Restart();


            if (_isCameraChanged)
            {
                // Each time camera is changed, we need to call CalculateScreenSpacePositions method.
                // This will update the 2D screen positions of the 3D lines.

                // IMPORTANT:
                // Before calling CalculateScreenSpacePositions it is highly recommended to call Refresh method on the camera.
                Camera1.Refresh();

                // When calling CalculateScreenSpacePositions we need worldToViewportMatrix

                // You can calculate the worldToViewportMatrix manually by the following code:
                //
                // Matrix3D viewMatrix, projectionMatrix;
                // Camera1.GetCameraMatrices(out viewMatrix, out projectionMatrix);
                //
                // var worldToViewportMatrix = viewMatrix * projectionMatrix * GetViewportMatrix(new Size(MainViewport.ActualWidth, MainViewport.ActualHeight));
                //
                // public static Matrix3D GetViewportMatrix(Size viewportSize)
                // {
                //     if (viewportSize.IsEmpty)
                //         return Ab3d.Common.Constants.ZeroMatrix;

                //     return new Matrix3D(viewportSize.Width / 2, 0, 0, 0,
                //                         0, -viewportSize.Height / 2, 0, 0,
                //                         0, 0, 1, 0,
                //                         viewportSize.Width / 2,
                //                         viewportSize.Height / 2, 0, 1);
                // }

                var worldToViewportMatrix3D = new Matrix3D();
                bool isWorldToViewportMatrixValid = Camera1.GetWorldToViewportMatrix(ref worldToViewportMatrix3D, forceMatrixRefresh: false);

                if (!isWorldToViewportMatrixValid)
                    return;

                var worldToViewportMatrix = worldToViewportMatrix3D.ToMatrix(); // Convert WPF's Matrix to SharpDX's Matrix


                if (isMultiThreaded)
                {
                    Parallel.For(0, _allLineSelectorData.Count, 
                                 i => _allLineSelectorData[i].CalculateScreenSpacePositions(ref worldToViewportMatrix));
                }
                else
                {
                    for (var i = 0; i < _allLineSelectorData.Count; i++)
                        _allLineSelectorData[i].CalculateScreenSpacePositions(ref worldToViewportMatrix);
                }

                _isCameraChanged = false;

                calculateScreenSpacePositionsTime = _stopwatch.Elapsed.TotalMilliseconds;
                _stopwatch.Restart();
            }
            else
            {
                calculateScreenSpacePositionsTime = 0;
            }


            // Now we can call the GetClosestDistance method.
            // This method calculates the closest distance from the _lastMousePosition to the line that was used to create the LineSelectorData.
            // GetClosestDistance also sets the LastDistance, LastLinePositionIndex properties on the LineSelectorData.

            if (isMultiThreaded)
            {
                Parallel.For(0, _allLineSelectorData.Count, 
                             i => _allLineSelectorData[i].GetClosestDistance(_lastMousePosition, _maxSelectionDistance));
            }
            else
            {
                for (var i = 0; i < _allLineSelectorData.Count; i++)
                    _allLineSelectorData[i].GetClosestDistance(_lastMousePosition, _maxSelectionDistance);
            }


            // Get the lines that are within _maxSelectionDistance and add them to _selectedLineSelectorData
            // We are reusing _selectedLineSelectorData list to prevent new allocations on each call of UpdateClosestLine
            
            foreach (var lineSelectorData in _allLineSelectorData)
            {
                if (lineSelectorData.LastDistance <= _maxSelectionDistance)
                    _selectedLineSelectorData.Add(lineSelectorData);
            }

            
            DXLineSelectorData closestLineSelector = null;
            Vector3 closestPositionOnLine = new Vector3();

            if (_selectedLineSelectorData.Count > 0)
            {
                // We need mouse ray from the mouse position to get the closest position on the line
                Point3D rayOrigin3D;
                Vector3D rayDirection3D;
                bool rayExist = Camera1.CreateMouseRay3D(new Point(_lastMousePosition.X, _lastMousePosition.Y), out rayOrigin3D, out rayDirection3D);

                if (!rayExist)
                    return;

                Vector3 rayOrigin = rayOrigin3D.ToVector3();
                Vector3 rayDirection = rayDirection3D.ToVector3();

                float closestDistance = float.MaxValue;

                if (OrderByDistanceCheckBox.IsChecked ?? false)
                {
                    // Order by camera distance (line that is closest to the camera is selected)

                    var cameraPosition = Camera1.GetCameraPosition().ToVector3();

                    foreach (var oneLineSelectorData in _selectedLineSelectorData)
                    {
                        var oneClosestPositionOnLine = oneLineSelectorData.GetClosestPositionOnLine(rayOrigin, rayDirection);

                        if (float.IsNaN(oneClosestPositionOnLine.X)) // if we cannot calculate the closest position, then NaN is returned
                            continue;

                        var distanceToCamera = (cameraPosition - oneClosestPositionOnLine).LengthSquared(); // We just use length for getting the closest item, so we can use squared values

                        if (distanceToCamera < closestDistance)
                        {
                            closestDistance = distanceToCamera;
                            closestLineSelector = oneLineSelectorData;
                            closestPositionOnLine = oneClosestPositionOnLine;
                        }
                    }
                }
                else
                {
                    // Order by distance to the specified position (line that is closes to the specified position is selected)

                    foreach (var lineSelectorData in _selectedLineSelectorData)
                    {
                        if (lineSelectorData.LastDistance < closestDistance)
                        {
                            closestDistance = lineSelectorData.LastDistance;
                            closestLineSelector = lineSelectorData;
                        }
                    }

                    if (closestLineSelector != null)
                        closestPositionOnLine = closestLineSelector.GetClosestPositionOnLine(rayOrigin, rayDirection);
                }
            }

            getClosestDistanceTime = _stopwatch.Elapsed.TotalMilliseconds;


            // The closest position on the line is shown with a SphereVisual3D
            if (_closestPositionSphereVisual3D == null)
            {
                _closestPositionSphereVisual3D = new SphereVisual3D()
                {
                    Radius = 2,
                    Material = new DiffuseMaterial(Brushes.Red)
                };

                MainViewport.Children.Add(_closestPositionSphereVisual3D);
            }

            string newClosestDistanceText;
            string newLineSegmentIndexText;
            if (closestLineSelector == null)
            {
                newClosestDistanceText = "";
                newLineSegmentIndexText = "";
                _closestPositionSphereVisual3D.IsVisible = false;
            }
            else
            {
                newClosestDistanceText = string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0:0.0}", closestLineSelector.LastDistance);
                newLineSegmentIndexText = closestLineSelector.LastLinePositionIndex.ToString();

                _closestPositionSphereVisual3D.CenterPosition = closestPositionOnLine.ToWpfPoint3D();
                _closestPositionSphereVisual3D.IsVisible = true;
            }
            
            if (ClosestDistanceValue.Text != newClosestDistanceText)
                ClosestDistanceValue.Text = newClosestDistanceText;
            
            if (LineSegmentIndexValue.Text != newLineSegmentIndexText)
                LineSegmentIndexValue.Text = newLineSegmentIndexText;

            string newUpdateTimeText = string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0:0.##} + {1:0.##}", calculateScreenSpacePositionsTime, getClosestDistanceTime);

            if (UpdateTimeValue.Text != newUpdateTimeText)
                UpdateTimeValue.Text = newUpdateTimeText;



            // Show the closest line as red
            if (!ReferenceEquals(_lastSelectedLineSelector, closestLineSelector))
            {
                if (_lastSelectedLineSelector != null)
                    SetLineColor(_lastSelectedLineSelector, _savedLineColor); // Reset color to the previously selected line

                if (closestLineSelector != null)
                    _savedLineColor = SetLineColor(closestLineSelector, Colors.Orange); // Show selected line as red

                _lastSelectedLineSelector = closestLineSelector;
            }

            
            // Clear _selectedLineSelectorData so it can be used in next UpdateClosestLine
            _selectedLineSelectorData.Clear();

            _usedMousePosition = _lastMousePosition;
        }

        private Color SetLineColor(DXLineSelectorData lineSelectorData, Color lineColor)
        {
            Color previousColor;

            var baseLineVisual3D = lineSelectorData.Tag as BaseLineVisual3D;
            if (baseLineVisual3D != null)
            {
                previousColor = baseLineVisual3D.LineColor;
                baseLineVisual3D.LineColor = lineColor;
            }
            else
            {
                var screenSpaceLineNode = lineSelectorData.Tag as ScreenSpaceLineNode;

                if (screenSpaceLineNode != null)
                {
                    previousColor = screenSpaceLineNode.LineMaterial.LineColor.ToWpfColor();
                    
                    var lineMaterial = screenSpaceLineNode.LineMaterial as LineMaterial; // Convert to LineMaterial because ILineMaterial does not have LineColor setter (only getter)
                    if (lineMaterial != null)
                    {
                        lineMaterial.LineColor = lineColor.ToColor4();
                        screenSpaceLineNode.NotifySceneNodeChange(SceneNode.SceneNodeDirtyFlags.MaterialChanged);
                    }
                }
                else
                {
                    previousColor = Colors.Black;
                }
            }

            return previousColor;
        }

        private Vector3[] CreateRandomPositions(int pointsCount)
        {
            var positions = new Vector3[pointsCount];

            var onePosition = new Vector3((float)_rnd.NextDouble() * LinePositionsRange - LinePositionsRange * 0.5f,
                                          (float)_rnd.NextDouble() * LinePositionsRange - LinePositionsRange * 0.5f,
                                          (float)_rnd.NextDouble() * LinePositionsRange - LinePositionsRange * 0.5f);

            // direction in range from -1 ... +1
            var lineDirection = new Vector3((float)_rnd.NextDouble() * 2.0f - 1.0f,
                                            (float)_rnd.NextDouble() * 1.0f - 0.5f,
                                            (float)_rnd.NextDouble() * 2.0f - 1.0f);

            var lineRightDirection = new Vector3(lineDirection.Z, lineDirection.Y, lineDirection.X); // switch X and Z to get vector to the right of lineDirection
            var lineUpDirection = new Vector3(0, 1, 0);

            var positionAdvancement = LinePositionsRange / pointsCount;
            var displacementRange = (float)Math.Max(0.1, LinePositionsRange / pointsCount);

            for (int i = 0; i < pointsCount; i++)
            {
                var vector = lineDirection * positionAdvancement;
                vector += lineUpDirection * displacementRange * ((float)_rnd.NextDouble() * 2.0f - 1.0f);
                vector += lineRightDirection * displacementRange * ((float)_rnd.NextDouble() * 2.0f - 1.0f);

                onePosition += vector;

                positions[i] = onePosition;
            }

            return positions;
        }

        private Color GetRandomColor()
        {
            byte amount = (byte) (_rnd.Next(200));

            return Color.FromArgb(255, amount, amount, amount);
        }

        private void UpdateMaxDistanceText()
        {
            string newMaxDistanceText;
            if (_maxSelectionDistance < 0)
                newMaxDistanceText = "unlimited";
            else
                newMaxDistanceText = string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0:0}", _maxSelectionDistance);

            if (MaxDistanceValue.Text != newMaxDistanceText)
                MaxDistanceValue.Text = newMaxDistanceText;
        }

        private void MaxDistanceSlider_OnValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            _maxSelectionDistance = (float)MaxDistanceSlider.Value;

            if (_maxSelectionDistance > 20)
                _maxSelectionDistance = -1;

            UpdateMaxDistanceText();
        }

        private void OnCheckBoundingBoxCheckBoxCheckedChanged(object sender, RoutedEventArgs e)
        {
            if (!this.IsLoaded || _allLineSelectorData == null)
                return;

            var isChecked = CheckBoundingBoxCheckBox.IsChecked ?? false;

            foreach (var dxLineSelectorData in _allLineSelectorData)
                dxLineSelectorData.CheckBoundingBox = isChecked;

            _isCameraChanged = true; // this will force calling CalculateScreenSpacePositions again
        }
        
        private void LinesCountComboBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!this.IsLoaded)
                return;

            CreateSampleLines();
        }

        private void LinesSegmentsComboBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!this.IsLoaded)
                return;

            CreateSampleLines();
        }
        
        private void CameraRotationButton_OnClick(object sender, RoutedEventArgs e)
        {
            if (Camera1.IsRotating)
            {
                Camera1.StopRotation();
                CameraRotationButton.Content = "Start camera rotation";
            }
            else
            {
                Camera1.StartRotation(20, 0);
                CameraRotationButton.Content = "Stop camera rotation";
            }
        }
    }
}