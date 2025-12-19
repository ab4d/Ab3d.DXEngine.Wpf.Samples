using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using Ab3d.Animation;
using Ab3d.Common.Cameras;
using Ab3d.Common.Models;
using Ab3d.DirectX;
using Ab3d.Visuals;
using Point = System.Windows.Point;

#if SHARPDX
using SharpDX;
using Matrix = SharpDX.Matrix;
#endif

namespace Ab3d.DXEngine.Wpf.Samples.DXEnginePerformance
{
    /// <summary>
    /// Interaction logic for InstanceWorldMatrixSamples.xaml
    /// </summary>
    public partial class InstanceWorldMatrixSamples : Page
    {
        private struct SampleData
        {
            public Matrix Matrix;
            public string Description;
            public string MatrixText;

            public SampleData(Matrix matrix, string description, string matrixText = null)
            {
                Matrix = matrix;
                Description = description;

                if (string.IsNullOrEmpty(matrixText))
                    MatrixText = Ab3d.Utilities.Dumper.GetMatrix3DText(matrix.ToWpfMatrix3D());
                else
                    MatrixText = matrixText;
            }
        }



        private const bool CenterBoxMeshToCoordinateCenter = true; // See comments in CalculateMatrixFromPositionsAndSize and GenerateSampleObjects for more info.


        private int _currentSampleIndex;

        private List<SampleData> _samplesData;

        private const float SamplesDistance = 10;

        public InstanceWorldMatrixSamples()
        {
            InitializeComponent();

            AddSampleTransformation(
                Matrix.Identity, 
                @"The original MeshGeometry3D is not changed when the World transformation is set to the Identity matrix (all matrix fields are zero, except diagonal fields that are 1)");

            AddSampleTransformation(
                Matrix.Translation(-1.5f, 0.4f, 1f),
@"To change the position of one MeshGeometry3D instance, we set the M41 (for x offset), M42 (for y offset) and M43 (for z offset) fields in the World matrix.

The following offset is used here:
x offset: -1.5
y offset: 0.4
z offset: 1");

            AddSampleTransformation(
                Matrix.Scaling(0.5f, 1.0f, 3.0f),
@"To scale one MeshGeometry3D instance, we set the 3 diagonal matrix values: M11 (for x scale), M22 (for y scale) and M33 (for z scale).

The following scale factors are used here:
x scale: 0.5
y scale: 1.0
z scale: 3.0");

            AddSampleTransformation(
                Matrix.RotationAxis(new Vector3(0, 1, 0), MathUtil.DegreesToRadians(33)),
@"There are many ways to rotate an instance. One way is to use WPF's RotateTransform3D and AxisAngleRotation3D to specify axis of rotation and rotation angle. Then we need to convert the rotation matrix from the WPF matrix to the SharpDX matrix.

The following rotation is used for this instance:
Matrix.RotationAxis(new Vector3(0, 1, 0), MathUtil.DegreesToRadians(33))

This is the same as the following WPF rotation would be used:
RotateTransform3D(new AxisAngleRotation3D(new Vector3D(0, 1, 0), 33))");

            // We can also use WPF's rotation objects:
            //var rotateTransform3D = new RotateTransform3D(new AxisAngleRotation3D(new Vector3D(0, 1, 0), 33));
            //AddSampleTransformation(
            //    rotateTransform3D.Value.ToMatrix(),
            //    @"There are many ways to rotate an instance. One way is to use WPF's RotateTransform3D and AxisAngleRotation3D to specify axis of rotation and rotation angle. Then we need to convert the rotation matrix from the WPF matrix to the SharpDX matrix.\n\nThe following rotation is used for this instance:\nRotateTransform3D(new AxisAngleRotation3D(new Vector3D(0, 1, 0), 33))");


            AddSampleTransformation(
                Matrix.Scaling(0.5f, 1.0f, 3.0f) * 
                Matrix.Translation(-1.5f, 0.4f, 1f),
@"We can combine transformations with multiplying them.

This sample combines scale and translation with:
finalTransformation = scale(0.5, 1.0, 3.0) * translation(-1.5, 0.4, 1)");

            AddSampleTransformation(
                Matrix.Scaling(0.5f, 1.0f, 3.0f) *
                Matrix.RotationAxis(new Vector3(0, 1, 0), MathUtil.DegreesToRadians(33)) *
                Matrix.Translation(-1.5f, 0.40f, 1f),
@"We can also add rotation (as the second step in transformations):
finalTransformation = scale * rotation * translation");

            AddSampleTransformation(
                Matrix.Translation(-1.5f, 0.40f, 1f) *
                Matrix.RotationAxis(new Vector3(0, 1, 0), MathUtil.DegreesToRadians(33)) *
                Matrix.Scaling(0.5f, 1.0f, 3.0f) ,
@"IMPORTANT
\!Order in which the transformations are applied (are multiplied) is very important!\!
The proper order is:
1) scale
2) rotate
3) traslate

This example shows the result where the transformantions are not application in the correct order: 
finalTransformation = translation * rotation * scale");


            AddSampleTransformation(
                CalculateMatrixFromPositionsAndSize(startPosition: new Vector3(-1, 0, 0), endPosition: new Vector3(0, 0, -1), size: new Size(0.5, 0.5)),
@"Using \!CalculateMatrixFromPositionsAndSize\! method to create a Matrix that will transform the box mesh so that it starts at the specified start position, end at the specified end position and have the specified width and height.

\bstartPosition:\b new Vector3(-1, 0, 0)
\bendPosition:\b new Vector3(0, 0, -1)
\bsize:\b new Size(0.5, 0.5)");

            AddSampleTransformation(
                CalculateMatrixFromPositionsAndSize(startPosition: new Vector3(-1, 0, -1), endPosition: new Vector3(1, 0, 0), size: new Size(1.0, 0.1)),
@"Another sample of using CalculateMatrixFromPositionsAndSize. Note that even if the MeshGeometry3D box has size of 1 x 1 x 1, we can easily convert it into a plate with scaling its height - to 0.1 in this case.

\!startPosition:\! new Vector3(-1, 0, -1)
\!endPosition:\! new Vector3(1, 0, 0)
\!size:\! new Size(1.0, 0.1)");


            GenerateSampleObjects();


            this.Focusable = true; // by default Page is not focusable and therefore does not receive keyDown event
            this.PreviewKeyDown += new KeyEventHandler(InstanceWorldMatrixSamples_PreviewKeyDown); // Use PreviewKeyDown to get arrow keys also (KeyDown event does not get them)
            //this.Focus();


            this.Loaded += delegate(object sender, RoutedEventArgs args)
            {
                Camera1.Refresh();

                SetupIntroText();
            };
        }

        private void SetupIntroText()
        {
            DescriptionTextBlockEx.ContentText = "INTRODUCTION:\n\nAll the green boxes shown in this sample are created with using one InstancedMeshGeometryVisual3D.\nThis means that many object instances are render with using one MeshGeometry3D (provides base mesh data with positions and triangle indices). To define the location, scale, orientation and color of each instance, an array of InstanceData objects is set to the InstancedMeshGeometryVisual3D. Each InstanceData object defines a World field that is a 4x4 matrix.\n\nThis sample shows how to set the correct value to the World field to achieve desired effect.\n\nPress START to start the sample.";
            UpdateDescriptionPosition();

            _currentSampleIndex = -1;
        }

        private void AddSampleTransformation(Matrix worldMatrix, string matrixDescription, string matrixText = null)
        {
            if (_samplesData == null)
                _samplesData = new List<SampleData>();

            _samplesData.Add(new SampleData(worldMatrix, matrixDescription, matrixText));
        }

        private void GenerateSampleObjects()
        {
            // We will show multiple instances of box MeshGeometry3D that is created here.
            // 
            // IMPORTANT:
            //
            // It is very important to specify the correct position and size of the mesh.
            // The size should be one unit so that when you scale this mesh in instance data, 
            // the scale factor will tell you the final size (for example xScale = 3 would render box with xSize set to 3).
            //
            // The position is even more important because when you will scale and rotate the mesh, 
            // the mesh will be scaled and rotated around (0, 0, 0).
            // So if center of mesh is at (0, 0, 0), then the mesh will be also scale in all directions.
            // But if left edge is at (0, 0, 0), then the object will scale to the right (all coordinates are multiplied by the scale).
            //
            // What is more, the position that is at (0, 0, 0) in the original mesh will be positioned at the position 
            // defined in the World transform by the M41, M42, M43 fields.
            // So if center of the mesh is at (0, 0, 0) - as in our case - then M41, M42, M43 will define the position of the center.
            // But if left-bottom-front edge would be at (0, 0, 0) - if we would use "centerPosition: new Point3D(0.5, 0.5, 0.5)" -
            // then  M41, M42, M43 will define the position of the left-bottom-front edge when the box is shown.
            //
            // See also comments in CalculateMatrixFromPositionsAndSize method.

            Point3D boxCenterPosition = CenterBoxMeshToCoordinateCenter ? new Point3D(0, 0, 0) : new Point3D(0.5, 0, 0);

            var boxMeshGeometry3D = new Ab3d.Meshes.BoxMesh3D(centerPosition: boxCenterPosition, 
                                                              size: new Size3D(1, 1, 1), 
                                                              xSegments: 1, ySegments: 1, zSegments: 1).Geometry;

            // Uncomment the following line to see how an ArrowMesh3D would look like (note that arrow part is also scaled so ArrowMesh3D is not a very good candidate for mesh used for instancing when scale is not uniform)
            //boxMeshGeometry3D = new Ab3d.Meshes.ArrowMesh3D(startPosition: new Point3D(0, 0, 0), endPosition: new Point3D(1, 0, 0), radius: 0.1f, arrowRadius: 0.2f, arrowAngle: 45, segments: 10, generateTextureCoordinates: false).Geometry;

            // Create an instance of InstancedMeshGeometryVisual3D
            var instancedMeshGeometryVisual3D = new InstancedMeshGeometryVisual3D(boxMeshGeometry3D);


            // Create an array of InstanceData with the matrices define in the _matrices list and Green color.

            var instanceData = new InstanceData[_samplesData.Count];
            for (int i = 0; i < _samplesData.Count; i++)
            {
                instanceData[i] = new InstanceData(_samplesData[i].Matrix, Colors.Green.ToColor4());
                instanceData[i].World.M43 -= SamplesDistance * i;

                var wireGridVisual3D = new WireGridVisual3D()
                {
                    CenterPosition   = new Point3D(0, 0, -SamplesDistance * i),
                    Size             = new Size(4, 4),
                    WidthCellsCount  = 4,
                    HeightCellsCount = 4,
                    LineColor        = Colors.Gray,
                    LineThickness    = 1
                };

                MainViewport.Children.Add(wireGridVisual3D);


                var yAxisLineVisual3D = new LineVisual3D()
                {
                    StartPosition = wireGridVisual3D.CenterPosition,
                    EndPosition   = wireGridVisual3D.CenterPosition + new Vector3D(0, 1.2, 0),
                    LineColor     = wireGridVisual3D.LineColor,
                    LineThickness = wireGridVisual3D.LineThickness,
                    EndLineCap    = LineCap.ArrowAnchor
                };

                MainViewport.Children.Add(yAxisLineVisual3D);
            }

            instancedMeshGeometryVisual3D.InstancesData = instanceData;

            MainViewport.Children.Add(instancedMeshGeometryVisual3D);
        }

        private Matrix CalculateMatrixFromPositionsAndSize(Vector3 startPosition, Vector3 endPosition, Size size)
        {
            // We need to offset the Mesh by 0.5 on xAxis so that the (0,0,0) will be on the left edge of the mesh (without this (0,0,0) is in the center of the mesh).
            // This way the mesh will be positioned so that it will start at startPosition.
            // If this step is skipped, then startPosition will position center of the mesh.
            //
            // This step can be skipped if mesh is created in such a way that it is already positioned so that its left edge starts at (0, 0, 0).
            //
            // See also comments in GenerateSampleObjects method.

            var direction = endPosition - startPosition;
            var length = direction.Length(); // length will be used for x scale

            var scale = new Vector3(length, (float)size.Height, (float)size.Width);

            //direction.Normalize(); // Because we already have length, we can quickly normalize with simply dividing by length
            direction /= length;

            var matrix = Common.MatrixUtils.GetMatrixFromNormalizedDirection(direction, startPosition, scale);

            if (CenterBoxMeshToCoordinateCenter)
            {
                // The following line nicely demonstrates the power of matrices.
                // Because the left matrix in the following multiplication is translation for 0.5 on x axis, 
                // this means that first we will transform the mesh by this translation (moving its left edge to 0,0,0)
                // and then we will do the GetMatrixFromDirection transformation.
                //
                // If the order would be reversed, then we would first transform by GetMatrixFromDirection and then translate by 0.5.
                // 
                // The beauty of matrices is that all such rules are embedded into 4 x 4 group of numbers.
                matrix = Matrix.Translation(0.5f, 0, 0) * matrix;
            }

            return matrix;
        }

        private void UpdateButtonState()
        {
            PreviousSampleButton.IsEnabled = _currentSampleIndex > 0;
            NextSampleButton.IsEnabled = _currentSampleIndex < (_samplesData.Count - 1);
        }

        private void UpdateDescription()
        {
            if (_currentSampleIndex < 0)
            {
                OverlayCanvas.Visibility = Visibility.Collapsed;
                return;
            }

            var sampleData = _samplesData[_currentSampleIndex];

            DescriptionTextBlockEx.ContentText = sampleData.Description + "\n\nMatrix:\n" + sampleData.MatrixText.TrimEnd();

            UpdateDescriptionPosition();
        }

        private void UpdateDescriptionPosition()
        {   
            DescriptionTextBlockEx.Measure(new Size(DesciptionInnerBorder.Width - 20, 0));
            DescriptionTextBlockEx.Arrange(new Rect(new Point(0, 0), DescriptionTextBlockEx.DesiredSize));

            double x = MainGrid.ActualWidth - DesciptionInnerBorder.Width - 20;
            if (x < 0)
                x = 0;

            double y = (MainGrid.ActualHeight - DescriptionTextBlockEx.ActualHeight) * 0.3; // Position to upper 1/3

            Canvas.SetLeft(DesciptionOuterBorder, x);
            Canvas.SetTop(DesciptionOuterBorder, y);

            UpdateDescriptionConnectionLine();

            OverlayCanvas.Visibility = Visibility.Visible;
        }

        private void UpdateDescriptionConnectionLine()
        {
            var sampleData = _samplesData[_currentSampleIndex];

            Point3D targetObjectPosition = new Point3D(sampleData.Matrix.M41, sampleData.Matrix.M42, sampleData.Matrix.M43);
            targetObjectPosition.Z -= SamplesDistance * _currentSampleIndex;


            double descriptionBorderXPos = Canvas.GetLeft(DesciptionOuterBorder);
            double descriptionBorderYPos = Canvas.GetTop(DesciptionOuterBorder);

            Point objectPositionOnScreen = Camera1.Point3DTo2D(targetObjectPosition);
            DescriptionConnectionLine.X1 = objectPositionOnScreen.X;
            DescriptionConnectionLine.Y1 = objectPositionOnScreen.Y;
            DescriptionConnectionLine.X2 = descriptionBorderXPos;
            DescriptionConnectionLine.Y2 = descriptionBorderYPos + 20;

            Canvas.SetLeft(DescriptionConnectionRectangle, objectPositionOnScreen.X - (DescriptionConnectionRectangle.Width * 0.5));
            Canvas.SetTop(DescriptionConnectionRectangle, objectPositionOnScreen.Y - (DescriptionConnectionRectangle.Height * 0.5));
        }


        private void MoveNext()
        {
            if (_currentSampleIndex >= (_samplesData.Count - 1))
                return;

            _currentSampleIndex++;

            //Camera1.TargetPosition += new Vector3D(0, 0,SamplesDistance);
            Camera1.MoveTargetPositionTo(finalTargetPosition: Camera1.TargetPosition - new Vector3D(0, 0, SamplesDistance),
                animationDurationInMilliseconds: 300,
                easingFunction: EasingFunctions.CubicEaseInOutFunction);

            NextSampleButton.IsEnabled = false;
            OverlayCanvas.Visibility = Visibility.Collapsed;
        }

        private void MovePrevious()
        {
            if (_currentSampleIndex <= 0)
                return;

            _currentSampleIndex--;

            //Camera1.TargetPosition -= new Vector3D(0, 0, SamplesDistance);
            Camera1.MoveTargetPositionTo(finalTargetPosition: Camera1.TargetPosition + new Vector3D(0, 0, SamplesDistance),
                animationDurationInMilliseconds: 300,
                easingFunction: EasingFunctions.CubicEaseInOutFunction);

            PreviousSampleButton.IsEnabled = false;
            OverlayCanvas.Visibility = Visibility.Collapsed;
        }


        void InstanceWorldMatrixSamples_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.Up:
                    if (NextSampleButton.IsEnabled)
                    {
                        MoveNext();
                        e.Handled = true;
                    }

                    break;

                case Key.Down:
                    if (PreviousSampleButton.IsEnabled)
                    {
                        MovePrevious();
                        e.Handled = true;
                    }

                    break;
            }
        }

        private void NextSampleButton_OnClick(object sender, RoutedEventArgs e)
        {
            MoveNext();
        }

        private void PreviousSampleButton_OnClick(object sender, RoutedEventArgs e)
        {
            MovePrevious();
        }


        private void ResetCameraSampleButton_OnClick(object sender, RoutedEventArgs e)
        {
            Camera1.Heading = 20;
            Camera1.Attitude = -30;
            Camera1.Distance = 10;
        }

        private void StartButton_OnClick(object sender, RoutedEventArgs e)
        {
            StartButton.IsEnabled = false;

            Camera1.RotateTo(20, -30, 500, Ab3d.Animation.EasingFunctions.CubicEaseInOutFunction);
            Camera1.AnimationController.AnimationCompleted += OnStartupAnimationCompleted;
        }

        private void OnStartupAnimationCompleted(object sender, EventArgs eventArgs)
        {
            Camera1.AnimationController.AnimationCompleted -= OnStartupAnimationCompleted;

            StartButton.Visibility = Visibility.Collapsed;

            NextSampleButton.Visibility = Visibility.Visible;
            PreviousSampleButton.Visibility = Visibility.Visible;
            ResetCameraSampleButton.Visibility = Visibility.Visible;

            _currentSampleIndex = 0;

            DescriptionConnectionLine.Visibility = Visibility.Visible;
            DescriptionConnectionRectangle.Visibility = Visibility.Visible;


            UpdateButtonState();
            UpdateDescription();

            Camera1.CameraChanged += delegate (object o, CameraChangedRoutedEventArgs eventArgs2)
            {
                UpdateDescriptionConnectionLine();
            };

            Camera1.AnimationController.AnimationCompleted += delegate (object o, EventArgs eventArgs3)
            {
                Camera1.RotationCenterPosition = new Point3D(Camera1.TargetPosition.X - 1, Camera1.TargetPosition.Y, Camera1.TargetPosition.Z);

                UpdateButtonState();
                UpdateDescription();
            };
        }
    }
}