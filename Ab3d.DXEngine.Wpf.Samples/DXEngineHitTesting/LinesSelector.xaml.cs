using System;
using System.Collections.Generic;
using System.Diagnostics;
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
using Ab3d.Cameras;
using Ab3d.Common.Cameras;
using Ab3d.Common.Models;
using Ab3d.Utilities;
using Ab3d.Visuals;

// NOTE:
// This sample is the same as in the Ab3d.PowerToys samples project (except using DXViewportView).
// The LineSelectorData is defined in the Ab3d.PowerToys library and is not part of the Ab3d.DXEngine library.

namespace Ab3d.DXEngine.Wpf.Samples.DXEngineHitTesting
{
    /// <summary>
    /// Interaction logic for LinesSelector.xaml
    /// </summary>
    public partial class LinesSelector : Page
    {
        private Random _rnd = new Random();

        private double _linePositionsRange = 100;
        private List<LineSelectorData> _lineSelectorData;

        private LineSelectorData _lastSelectedLineSelector;
        private Color _savedLineColor;

        private bool _isCameraChanged;

        private Ab3d.Visuals.SphereVisual3D _closestPositionSphereVisual3D;
        private Point _lastMousePosition;

        private double _maxSelectionDistance;


        public LinesSelector()
        {
            InitializeComponent();

            // LineSelectorData is a utility class that can be used to get the closest distance 
            // of a specified screen position to the 3D line.
            _lineSelectorData = new List<LineSelectorData>();

            for (int i = 0; i < 15; i++)
            {
                // Create random 3D lines
                var randomColor = GetRandomColor();
                var lineVisual3D = GenerateRandomLine(randomColor, 10);

                // Create LineSelectorData from each line.
                // When adjustLineDistanceWithLineThickness is true, then distance is measured from line edge.
                // If adjustLineDistanceWithLineThickness is false, then distance is measured from center of the line.
                var lineSelectorData = new LineSelectorData(lineVisual3D, Camera1, adjustLineDistanceWithLineThickness: true);

                // LineSelectorData transform the line positions when there is any transformation set to the lineVisual3D's Transform property
                // (in this case the lineSelectorData.PositionsTransform3D is set to the used transformation).
                //
                // But when the lineVisual3D is added to a parent Visual3D that has its own transformation,
                // then LineSelectorData is not "aware" of that transformation.
                // To demonstrate that, the lineVisual3D in this sample is added to a RootVisual3D that has TranslateTransform3D with OffsetX = 20.
                //
                // Because we know the organization of objects in this sample, we could simple write the following to support that:
                // lineSelectorData.PositionsTransform3D = RootVisual3D.Transform;
                //
                // But there is a more general way that can be used.
                // With help of TransformationsHelper.GetVisual3DTotalTransform it is possible 
                // to get the combined (total) transformation from Viewport3D to the lineVisual3D.
                // This also includes the transformation on lineVisual3D (second parameter in GetVisual3DTotalTransform is true).
                //
                // This methods can be also used to check if specified Visual3D is connected to Viewport3D (it sets an out parameter isVisualConnected).
                // There is also another override of this method that takes two Visual3D objects and gets the transformation from the first to the second.

                bool isVisualConnected; // This will be set to true if lineVisual3D is connected to Viewport3D (always true in our case).
                lineSelectorData.PositionsTransform3D = Ab3d.Utilities.TransformationsHelper.GetVisual3DTotalTransform(lineVisual3D, true, out isVisualConnected);


                _lineSelectorData.Add(lineSelectorData);
            }

            

            _isCameraChanged = true; // When true, the CalculateScreenSpacePositions method is called before calculating line distances

            Camera1.CameraChanged += delegate(object sender, CameraChangedRoutedEventArgs e)
            {
                _isCameraChanged = true;
                UpdateClosestLine();
            };

            this.MouseMove += delegate(object sender, MouseEventArgs e)
            {
                _lastMousePosition = e.GetPosition(MainBorder);
                UpdateClosestLine();
            };

            Camera1.StartRotation(20, 0);
        }

        private void UpdateClosestLine()
        {
            if (_lineSelectorData == null || _lineSelectorData.Count == 0)
                return;

            if (_isCameraChanged)
            {
                // Each time camera is changed, we need to call CalculateScreenSpacePositions method.
                // This will update the 2D screen positions of the 3D lines.

                // IMPORTANT:
                // Before calling CalculateScreenSpacePositions it is highly recommended to call Refresh method on the camera.
                Camera1.Refresh();

                if (MultiThreadedCheckBox.IsChecked ?? false)
                {
                    // This code demonstrates how to use call CalculateScreenSpacePositions from multiple threads.
                    // This significantly improves performance when many 3D lines are used (thousands)
                    // but is not needed when using only a few lines (as in this demo).
                    //
                    // When calling CalculateScreenSpacePositions we need to prepare all the data
                    // from WPF properties before calling the method because those properties 
                    // are not accessible from the other thread.
                    // We need worldToViewportMatrix and
                    // the _lineSelectorData[i].Camera and _lineSelectorData[i].UsedLineThickness need to be set
                    // (in this sample they are set in the LineSelectorData constructor).

                    var worldToViewportMatrix = new Matrix3D();
                    bool isWorldToViewportMatrixValid = Camera1.GetWorldToViewportMatrix(ref worldToViewportMatrix, forceMatrixRefresh: false);

                    if (isWorldToViewportMatrixValid)
                    {
                        Parallel.For(0, _lineSelectorData.Count, 
                                     i => _lineSelectorData[i].CalculateScreenSpacePositions(ref worldToViewportMatrix, transform: null));
                    }
                }
                else
                {
                    for (var i = 0; i < _lineSelectorData.Count; i++)
                        _lineSelectorData[i].CalculateScreenSpacePositions(Camera1);
                }

                _isCameraChanged = false;
            }


            // Now we can call the GetClosestDistance method.
            // This method calculates the closest distance from the _lastMousePosition to the line that was used to create the LineSelectorData.
            // GetClosestDistance also sets the LastDistance, LastLinePositionIndex properties on the LineSelectorData.

            if (MultiThreadedCheckBox.IsChecked ?? false)
            {
                Parallel.For(0, _lineSelectorData.Count, 
                             i => _lineSelectorData[i].GetClosestDistance(_lastMousePosition));
            }
            else
            {
                for (var i = 0; i < _lineSelectorData.Count; i++)
                    _lineSelectorData[i].GetClosestDistance(_lastMousePosition);
            }


            // Get the closest line
            IEnumerable<LineSelectorData> usedLineSelectors;

            // If we limit the distance of the specified position to the line, then we can filter all the line with Where
            if (_maxSelectionDistance >= 0)
                usedLineSelectors = _lineSelectorData.Where(l => l.LastDistance <= _maxSelectionDistance).ToList();
            else
                usedLineSelectors = _lineSelectorData;


            List<LineSelectorData> orderedLineSelectors;
            if (OrderByDistanceCheckBox.IsChecked ?? false)
            {
                // Order by camera distance
                orderedLineSelectors = usedLineSelectors.OrderBy(l => l.LastDistanceFromCamera).ToList();
            }
            else
            {
                // Order by distance to the specified position
                orderedLineSelectors = usedLineSelectors.OrderBy(l => l.LastDistance).ToList();
            }

            // Get the closest LineSelectorData
            LineSelectorData closestLineSelector;
            if (orderedLineSelectors.Count > 0)
                closestLineSelector = orderedLineSelectors[0];
            else
                closestLineSelector = null;


            // It is possible to get the positions of the line segment that is closest to the mouse position
            //var closestPolyLine = (PolyLineVisual3D)closestLineSelector.LineVisual;
            //Point3D firstSegmentPosition = closestPolyLine.Positions[closestLineSelector.LastLinePositionIndex];
            //Point3D secondSegmentPosition = closestPolyLine.Positions[closestLineSelector.LastLinePositionIndex + 1];

            // To get the actual position on the line that is closest to the mouse position, use the LastClosestPositionOnLine
            //closestLineSelector.LastClosestPositionOnLine;


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

            if (closestLineSelector == null)
            {
                ClosestDistanceValue.Text = "";
                LineSegmentIndexValue.Text = "";
                _closestPositionSphereVisual3D.IsVisible = false;
            }
            else
            {
                ClosestDistanceValue.Text = string.Format("{0:0.0}", closestLineSelector.LastDistance);
                LineSegmentIndexValue.Text = closestLineSelector.LastLinePositionIndex.ToString();

                _closestPositionSphereVisual3D.CenterPosition = closestLineSelector.LastClosestPositionOnLine;
                _closestPositionSphereVisual3D.IsVisible = true;
            }


            // Show the closest line as red
            if (!ReferenceEquals(_lastSelectedLineSelector, closestLineSelector))
            {
                if (_lastSelectedLineSelector != null)
                    _lastSelectedLineSelector.LineVisual3D.LineColor = _savedLineColor;

                if (closestLineSelector != null)
                {
                    _savedLineColor = closestLineSelector.LineVisual3D.LineColor;
                    closestLineSelector.LineVisual3D.LineColor = Colors.Red;
                }

                _lastSelectedLineSelector = closestLineSelector;
            }
        }


        private BaseLineVisual3D GenerateRandomLine(Color lineColor, int lineLength)
        {
            var positions = CreateRandomPositions(lineLength);


            BaseLineVisual3D createdLine;

            if (_rnd.Next(4) == 1) // 25% chance to return a simple Line instead of PolyLine
            {
                createdLine = new Ab3d.Visuals.LineVisual3D()
                {
                    StartPosition = positions[0],
                    EndPosition   = positions[positions.Count - 1],
                    LineColor     = lineColor,
                    LineThickness = _rnd.NextDouble() * 5 + 1,
                };
            }
            else
            {
                createdLine = new Ab3d.Visuals.PolyLineVisual3D()
                {
                    LineColor     = lineColor,
                    LineThickness = _rnd.NextDouble() * 5 + 1,
                    Positions     = positions
                };
            }


            MainViewport.Children.Add(createdLine);

            return createdLine;
        }

        private Point3DCollection CreateRandomPositions(int pointsCount)
        {
            var positions = new Point3DCollection(pointsCount);

            var onePosition = new Point3D(_rnd.NextDouble() * _linePositionsRange - _linePositionsRange / 2,
                                          _rnd.NextDouble() * _linePositionsRange - _linePositionsRange / 2,
                                          _rnd.NextDouble() * _linePositionsRange - _linePositionsRange / 2);

            // direction in range from -1 ... +1
            var lineDirection = new Vector3D(_rnd.NextDouble() * 2.0 - 1.0,
                                             _rnd.NextDouble() * 1.0 - 0.5,
                                             _rnd.NextDouble() * 2.0 - 1.0);

            var lineRightDirection = new Vector3D(lineDirection.Z, lineDirection.Y, lineDirection.X); // switch X and Z to get vector to the right of lineDirection
            var lineUpDirection = new Vector3D(0, 1, 0);

            var positionAdvancement = _linePositionsRange * 1.5 / pointsCount;
            var displacementRange = _linePositionsRange * 0.15;

            for (int i = 0; i < pointsCount; i++)
            {
                var vector = lineDirection * positionAdvancement;
                vector += lineUpDirection * displacementRange * (_rnd.NextDouble() * 2.0 - 1.0);
                vector += lineRightDirection * displacementRange * (_rnd.NextDouble() * 2.0 - 1.0);

                onePosition += vector;

                positions.Add(onePosition);
            }

            return positions;
        }

        public Color GetRandomColor()
        {
            byte amount = (byte) (_rnd.Next(200));

            return Color.FromArgb(255, amount, amount, amount);
            //return Color.FromArgb(255, (byte)_rnd.Next(255), (byte)_rnd.Next(255), (byte)_rnd.Next(255));
        }

        private void UpdateMaxDistanceText()
        {
            if (_maxSelectionDistance < 0)
                MaxDistanceValue.Text = "unlimited";
            else
                MaxDistanceValue.Text = string.Format("{0:0}", _maxSelectionDistance);
        }

        private void MaxDistanceSlider_OnValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            _maxSelectionDistance = MaxDistanceSlider.Value;

            if (_maxSelectionDistance > 20)
                _maxSelectionDistance = -1;

            UpdateMaxDistanceText();
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
