using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using Ab3d.Visuals;
using SharpDX;
using Ab3d.DirectX;
using Ab3d.Assimp;
using Ab3d.Common;
using Ab3d.Common.Models;
using Ab3d.DirectX.Effects;
using Ab3d.DXEngine.Wpf.Samples.Common;
using Point = System.Windows.Point;
using Ab3d.Controls;

namespace Ab3d.DXEngine.Wpf.Samples.DXEngineHitTesting
{
    /// <summary>
    /// Interaction logic for VertexIdBitmapHitTesting.xaml
    /// </summary>
    public partial class VertexIdBitmapHitTesting : Page
    {
        private Stopwatch _stopwatch;

        private int _selectedVertexIndex = -1;

        private Color4 _addedVertexIdColor;
        private byte[] _addedVertexIdColorBytes;

        private bool _isMeasuringDistance;
        private bool _isCameraChanging;
        private bool _isVertexIdBitmapDirty;

        private WriteableBitmap _writeableBitmap;

        private byte[] _vertexIdBitmapBytes;
        private int _vertexIdBitmapWidth;
        private int _vertexIdBitmapHeight;
        private int _vertexIdBitmapSize;
        private int _vertexIdBitmapRowSize;

        private DisposeList _disposables;
        private string _fileName;

        private float _pixelSize;
        private double _pixelSizeSliderFactor;

        private AssimpWpfImporter _assimpWpfImporter;

        private PixelsVisual3D _pixelsVisual3D;

        private PixelEffect _pixelEffect;
        private Vector3[] _pointCloudPositions;

        private WireCrossVisual3D _selectedVertexWireCross;
        private LineVisual3D _distanceLineVisual3D;
        private TextBlockVisual3D _distanceTextBlockVisual3D;

        public VertexIdBitmapHitTesting()
        {
            InitializeComponent();

            MouseCameraControllerInfo1.AddCustomInfoLine(0, MouseCameraController.MouseAndKeyboardConditions.LeftMouseButtonPressed, "MEASURE DISTANCE");

            _disposables = new DisposeList();

            _stopwatch = new Stopwatch();

            _pixelSize = 2;
            _pixelSizeSliderFactor = 0.05; // set slider to go from 0 to 5

            UpdatePixelSize();

            // Setting GraphicsProfiles tells DXEngine which graphics device it should use (and what devices are fallback devices if the first device cannot be initialized)
            // ApplicationContext.Current.GraphicsProfiles is changed by changing the "Graphics settings" with clicking on its button in upper left part of application.
            MainDXViewportView.GraphicsProfiles = DirectX.Client.Settings.DXEngineSettings.Current.GraphicsProfiles;

            if (DesignerProperties.GetIsInDesignMode(this))
                return;


            MainDXViewportView.DXSceneInitialized += delegate (object sender, EventArgs args)
            {
                _selectedVertexWireCross = new WireCrossVisual3D()
                {
                    LineColor = Colors.Red,
                    LineThickness = 2,
                    IsVisible = false
                };

                // Put _selectedVertexWireCross in the OverlayRenderingQueue
                // This will render it over all other 3D objects.
                // See DXEngineAdvanced/BackgroundAndOverlayRendering sample for more info.
                _selectedVertexWireCross.SetDXAttribute(DXAttributeType.CustomRenderingQueue, MainDXViewportView.DXScene.OverlayRenderingQueue);

                MainViewport.Children.Add(_selectedVertexWireCross);


                // Setup overlay rendering so the distance line and text are rendered on top of 3D objects
                // See DXEngineAdvanced/BackgroundAndOverlayRendering sample for more info.
                MainDXViewportView.DXScene.OverlayRenderingQueue.ClearDepthStencilBufferBeforeRendering = true;

                // The initially loaded point-cloud is a cropped version of a bigger point-cloud created by maxch (see Resources/PointClouds/readme.txt for more info)
                string fileName = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"Resources\PointClouds\14 Ladybrook Road 10 - cropped.ply");
                LoadPointCloud(fileName);
                
                SubscribeCameraAndMouseEvents();
            };

            var dragAndDropHelper = new DragAndDropHelper(this, ".*");
            dragAndDropHelper.FileDropped += (sender, args) => LoadPointCloud(args.FileName);

            // IMPORTANT:
            // It is very important to call Dispose method on DXSceneView after the control is not used any more (see help file for more info)
            this.Unloaded += (sender, args) =>
            {
                if (_disposables != null)
                {
                    _disposables.Dispose();
                    _disposables = null;
                }

                if (_pixelEffect != null)
                {
                    _pixelEffect.Dispose();
                    _pixelEffect = null;
                }

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

            PixelsPlaceholder.Children.Clear();


            try
            {
                Color4[] pointCloudColors;
                bool swapYZCoordinates = ZUpAxisCheckBox.IsChecked ?? false;

                if (_assimpWpfImporter == null)
                {
                    // See Ab3d.PowerToys.Samples/AssimpSamples/AssimpWpfImporterSample.xaml.cs in Ab3d.PowerToys samples for more information.
                    AssimpLoader.LoadAssimpNativeLibrary();
                    _assimpWpfImporter = new AssimpWpfImporter();
                }

                _pointCloudPositions = DXEnginePerformance.PointCloudImporterSample.LoadPositions(fileName, swapYZCoordinates, _assimpWpfImporter, out pointCloudColors);

                if (_pointCloudPositions == null)
                    return;

                var positionsBounds = BoundingBox.FromPoints(_pointCloudPositions);
                Camera1.TargetPosition = positionsBounds.Center.ToWpfPoint3D();
                Camera1.Offset = new Vector3D(0, 0, 0);
                Camera1.Distance = positionsBounds.ToRect3D().GetDiagonalLength() * 2;

                _selectedVertexWireCross.LinesLength = Camera1.Distance / 20;


                _pixelsVisual3D = new PixelsVisual3D(_pointCloudPositions)
                {
                    PixelSize = _pixelSize
                };

                _disposables.Add(_pixelsVisual3D);


                if (pointCloudColors != null)
                {
                    _pixelsVisual3D.PixelColor = Colors.White; // When using PixelColors, PixelColor is used as a mask (multiplied with each color)
                    _pixelsVisual3D.PixelColors = pointCloudColors;
                }
                else
                {
                    _pixelsVisual3D.PixelColor = Colors.Black;
                }

                PixelsPlaceholder.Children.Add(_pixelsVisual3D);

                _fileName = fileName;


                if (_distanceLineVisual3D != null)
                {
                    // Remove previous line
                    MainViewport.Children.Remove(_distanceLineVisual3D);
                    _distanceLineVisual3D = null;
                }

                EndMeasuringDistance();
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

        private void SubscribeCameraAndMouseEvents()
        {
            // Subscribe to Camera changes and size changed to mark that we need to render the VertexId bitmap again
            Camera1.CameraChanged                  += (sender, args) => ProcessCameraChanged();
            MainDXViewportView.DXRenderSizeChanged += (sender, args) => ProcessCameraChanged();

            // Disable hit-testing (rendering to bitmap) while user is rotating or moving the camera
            MouseCameraController1.CameraRotateStarted += (sender, args) => StartCameraChanging();
            MouseCameraController1.CameraRotateEnded   += (sender, args) => StopCameraChanging();

            MouseCameraController1.CameraMoveStarted += (sender, args) => StartCameraChanging();
            MouseCameraController1.CameraMoveEnded   += (sender, args) => StopCameraChanging();

            MouseCameraController1.CameraQuickZoomStarted += (sender, args) => StartCameraChanging();
            MouseCameraController1.CameraQuickZoomEnded   += (sender, args) => StopCameraChanging();
        }

        private void StartCameraChanging()
        {
            _isCameraChanging = true;
        }

        private void StopCameraChanging()
        {
            _isCameraChanging = false;
            ClearSelectedVertex();
        }

        private void ProcessCameraChanged()
        {
            _isVertexIdBitmapDirty = true;

            if (_isCameraChanging) // Do not update VertexId bitmap and hit vertex while camera is changing
                return;


            if (IsUpdatingVertexIdBitmapCheckBox.IsChecked ?? false)
                UpdateVertexIdBitmap();

            if (IsHitTestingCheckBox.IsChecked ?? false)
            {
                var mousePosition = Mouse.GetPosition(MainDXViewportView);
                HitTestWithVertexIdBitmap(mousePosition);
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (_isCameraChanging) // Do not update VertexId bitmap and hit vertex while camera is changing
                return;

            if (_isVertexIdBitmapDirty && (IsUpdatingVertexIdBitmapCheckBox.IsChecked ?? false))
                UpdateVertexIdBitmap();

            if (IsHitTestingCheckBox.IsChecked ?? false)
            {
                var mousePosition = e.GetPosition(MainDXViewportView);
                HitTestWithVertexIdBitmap(mousePosition);
            }

            base.OnMouseMove(e);
        }

        protected override void OnMouseDown(MouseButtonEventArgs e)
        {
            if (_selectedVertexIndex != -1)
            {
                StartMeasuringDistance();
                e.Handled = true;
            }

            base.OnMouseDown(e);
        }

        /// <inheritdoc />
        protected override void OnMouseUp(MouseButtonEventArgs e)
        {
            if (_isMeasuringDistance)
            {
                EndMeasuringDistance();
                e.Handled = true;
            }

            base.OnMouseUp(e);
        }
        
        private void StartMeasuringDistance()
        {
            if (_selectedVertexIndex == -1)
                return;

            var vertexPoint3D = _pointCloudPositions[_selectedVertexIndex].ToWpfPoint3D();

            // In this sample we are rendering _distanceLineVisual3D and _distanceTextBlockVisual3D
            // over all other 3D objects. Therefore, we do not need to adjust the vertexPoint3D.
            // 
            // But if we would render the line and text normally, we would need
            // to move the position closer to camera to prevent obstructing by nearby pixels:
            //var adjustedVertexPoint3D = MovePointToCamera(vertexPoint3D);

            var adjustedVertexPoint3D = vertexPoint3D;

            if (_distanceLineVisual3D == null)
            {
                _distanceLineVisual3D = new LineVisual3D()
                {
                    EndLineCap = LineCap.ArrowAnchor,
                    LineColor = Colors.Green,
                    LineThickness = 2,
                };

                // Put _distanceLineVisual3D in the OverlayRenderingQueue
                // This will render it over all other 3D objects.
                // See DXEngineAdvanced/BackgroundAndOverlayRendering sample for more info.
                _distanceLineVisual3D.SetDXAttribute(DXAttributeType.CustomRenderingQueue, MainDXViewportView.DXScene.OverlayRenderingQueue);

                MainViewport.Children.Add(_distanceLineVisual3D);
            }

            _distanceLineVisual3D.StartPosition = adjustedVertexPoint3D;
            _distanceLineVisual3D.EndPosition = adjustedVertexPoint3D;


            StartPositionTextBlock.Text = vertexPoint3D.ToVector3().ToString("F2");
            EndPositionTextBlock.Text = EndPositionTextBlock.Text;
                
            DistanceTextBlock.Text = "0.00";

            InfoPanel.Visibility = Visibility.Collapsed;
            DistancePanel.Visibility = Visibility.Visible;

            _isMeasuringDistance = true;
        }

        private void EndMeasuringDistance()
        {
            _isMeasuringDistance = false;
        }

        private void HitTestWithVertexIdBitmap(Point mousePosition)
        {
            if (_vertexIdBitmapBytes == null || _vertexIdBitmapWidth <= 0 || _vertexIdBitmapHeight <= 0)
                return;

            if (mousePosition.X < 0 || mousePosition.Y < 0 || mousePosition.X > MainDXViewportView.ActualWidth || mousePosition.Y > MainDXViewportView.ActualHeight)
                return;

            // We need to scale from WPF's coordinates to the rendered coordinates
            // The xScale and yScale are usually the same as dpi scale,
            // but when rendering to a smaller instance id bitmap, this scale can be different.
            double xScele = (double)_vertexIdBitmapWidth / MainDXViewportView.ActualWidth;
            double yScele = (double)_vertexIdBitmapHeight / MainDXViewportView.ActualHeight;

            // Just in case ActualWidth or ActualHeight is not set, check for NaN
            if (double.IsNaN(xScele))
                xScele = MainDXViewportView.DpiScaleX;

            if (double.IsNaN(yScele))
                yScele = MainDXViewportView.DpiScaleY;


            // Get position in the rendered instance id bitmap
            int xPos = (int)Math.Round(mousePosition.X * xScele);
            int yPos = (int)Math.Round(mousePosition.Y * yScele);

            // convert that position to byte array offset
            var pixelByteOffset = GetPixelByteOffset(xPos, yPos);

            if (pixelByteOffset == -1)
            {
                VertexIndexTextBlock.Text = null;
                VertexPositionTextBlock.Text = null;
                return; // Cannot get data
            }

            // get vertex index from byte array at that offset
            int vertexId = GetVertexIndexFromColor(pixelByteOffset, _addedVertexIdColorBytes);


            // DEBUG data (uncomment to show raw byte array values in VS output)
            //System.Diagnostics.Debug.WriteLine($"{xPos} {yPos} => ({_instanceIdBitmapBytes[pixelByteOffset + 3]}, {_instanceIdBitmapBytes[pixelByteOffset + 2]}, {_instanceIdBitmapBytes[pixelByteOffset + 1]}, {_instanceIdBitmapBytes[pixelByteOffset]}) => {instanceId}");


            if (vertexId == -1)
            {
                // No instance hit
                ClearSelectedVertex();
                VertexIndexTextBlock.Text = null;
                VertexPositionTextBlock.Text = null;
            }
            else
            {
                UpdateSelectedVertex(vertexId);
            }
        }

        private void UpdateVertexIdBitmap()
        {
            if (_pixelEffect == null)
            {
                // Get an instance of PixelEffect (it is used to provide the correct shaders to render specified positions as pixels)
                _pixelEffect = MainDXViewportView.DXScene.DXDevice.EffectsManager.GetEffect<PixelEffect>(createNewEffectInstanceIfNotFound: true);

                // Do not forget to dispose the effect when it is not used anymore - we will do that in the Unloaded event handler

                if (_pixelEffect == null) 
                    return;
            }
            

            // When rendering vertex id bitmap we will add an Opaque black color (0, 0, 0, 255: with alpha set to 1) to the rendered bitmap. 
            // This makes it possible for us to see the colors on the rendered image because alpha value is set to 1.
            // If we render more the 16.7 million instances, then we should set added color to (0, 0, 0, 0) so that 
            // the alpha value can be also used for instance id value (this is also the default color).
            
            _addedVertexIdColor = Color4.Black;
            _addedVertexIdColorBytes = _addedVertexIdColor.ToArray().Select(c => (byte)(c * 255)).ToArray(); // Convert to byte array


            // To render vertex id bitmap, we need to render each pixel so that its color is set by the index of the color.
            // Then we could get the index of the pixel from the color on the bitmap.
            _pixelEffect.PixelColor = _addedVertexIdColor;
            _pixelEffect.UseVertexIdColor = true;

            // Set BackgroundColor to black with zero alpha (0, 0, 0, 0)
            var savedBgColor = MainDXViewportView.BackgroundColor;
            MainDXViewportView.BackgroundColor = System.Windows.Media.Color.FromArgb(0, 0, 0, 0);
            
            // When rendering VertexId bitmap, we must not render other 3D objects
            // (in our case the _selectedVertexWireCross, _distanceLineVisual3D and _distanceTextBlockVisual3D).
            // This is done by filtering the rendering queues that are rendered when calling RenderToBitmap.
            MainDXViewportView.DXScene.DefaultRenderObjectsRenderingStep.FilterRenderingQueuesFunction = FilterRenderingQueuesFunction;

            // Instead of using FilterRenderingQueuesFunction (this is much faster)
            // we could also set all the objects to be hidden:
            //
            // Here we could also set other 3D objects to hidden (to prevent them from rendering)
            //_selectedVertexWireCross.IsVisible = false;
            //if (_distanceLineVisual3D != null)
            //    _distanceLineVisual3D.IsVisible = false;
            //if (_distanceTextBlockVisual3D != null)
            //    _distanceTextBlockVisual3D.IsVisible = false;


            _stopwatch.Restart();

            // Render to bitmap without multi-sampling and super-sampling
            MainDXViewportView.RenderToBitmap(OnRenderedBitmapReady,
                                              width: MainDXViewportView.DXFinalPixelSize.Width,
                                              height: MainDXViewportView.DXFinalPixelSize.Height,
                                              preferedMultisampling: 1, supersamplingCount: 1,
                                              dpiX: 96, dpiY: 96,
                                              convertToNonPreMultipledAlpha: false);

            _stopwatch.Stop();


            // Reset the setting for normal rendering
            _pixelEffect.UseVertexIdColor = false;
            _pixelEffect.PixelColor = new Color4(0, 0, 0, 0);

            MainDXViewportView.BackgroundColor = savedBgColor;

            
            MainDXViewportView.DXScene.DefaultRenderObjectsRenderingStep.FilterRenderingQueuesFunction = null;

            // If we would not use FilterRenderingQueuesFunction, then uncomment the following:
            //if (_selectedVertexIndex != -1)
            //    _selectedVertexWireCross.IsVisible = true;

            //if (_distanceLineVisual3D != null)
            //    _distanceLineVisual3D.IsVisible = true;

            //if (_distanceTextBlockVisual3D != null)
            //    _distanceTextBlockVisual3D.IsVisible = true;

            // Mark that the current instance id bitmap is correct (this will be set to false when camera or size is changed)
            _isVertexIdBitmapDirty = false;

            RenderTimeTextBlock.Text = string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0:0.0} ms", _stopwatch.Elapsed.TotalMilliseconds);

            InfoPanel.Visibility = Visibility.Visible;
            DistancePanel.Visibility = Visibility.Collapsed;
        }

        private bool FilterRenderingQueuesFunction(RenderingQueue renderingQueue)
        {
            return renderingQueue != MainDXViewportView.DXScene.OverlayRenderingQueue;
        }

        public int GetPixelByteOffset(int xPos, int yPos)
        {
            if (_vertexIdBitmapBytes == null)
                return -1;

            int offset = yPos * _vertexIdBitmapRowSize + xPos * 4;

            if (offset > _vertexIdBitmapSize)
                return -1;

            return offset;
        }

        public int GetVertexIndexFromColor(int pixelByteOffset, byte[] addedColor)
        {
            byte alpha = _vertexIdBitmapBytes[pixelByteOffset + 3];

            // Because we are using opaque black as added color and transparent black as Background color
            // we know that we hit background when alpha value is 0
            if (alpha == 0)
                return -1; 

            int instanceId = ((alpha - addedColor[3]) << 24) +                                       // alpha
                             ((_vertexIdBitmapBytes[pixelByteOffset + 2] - addedColor[2]) << 16) +   // red
                             ((_vertexIdBitmapBytes[pixelByteOffset + 1] - addedColor[1]) << 8) +    // green
                              (_vertexIdBitmapBytes[pixelByteOffset]     - addedColor[0]);           // blue

            return instanceId;
        }
        
        private void OnRenderedBitmapReady(int width, int height, DataBox data)
        {
            if (_vertexIdBitmapBytes != null && _vertexIdBitmapBytes.Length < data.SlicePitch)
                _vertexIdBitmapBytes = null;

            if (_vertexIdBitmapBytes == null)
                _vertexIdBitmapBytes = new byte[data.SlicePitch];

            Marshal.Copy(data.DataPointer, _vertexIdBitmapBytes, 0, data.SlicePitch);

            _vertexIdBitmapWidth   = width;
            _vertexIdBitmapHeight  = height;
            _vertexIdBitmapSize    = data.SlicePitch;
            _vertexIdBitmapRowSize = data.RowPitch;


            var showInstance = ShowVertexIdBitmapCheckBox.IsChecked ?? false;
            var saveToDesktop = SaveToDesktopBitmapCheckBox.IsChecked ?? false;

            if (showInstance || saveToDesktop)
            {
                UpdateWriteableBitmap(width, height, data);

                if (saveToDesktop)
                {
                    string fileName = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), 
                                                             "VertexIdBitmap.png");
                    
                    RectangularSelectionSample.SaveBitmap(_writeableBitmap, fileName);
                }
            }
        }

        private void UpdateWriteableBitmap(int width, int height, DataBox data)
        {
            // If size has changed, then we do not need existing WriteableBitmap anymore
            if (_writeableBitmap != null &&
                (_writeableBitmap.PixelWidth != width || _writeableBitmap.PixelHeight != height))
            {
                _writeableBitmap = null;
            }

            if (_writeableBitmap == null)
            {
                _writeableBitmap = new WriteableBitmap(width, height, 96, 96, PixelFormats.Pbgra32, null);
                PreviewImage.Source = _writeableBitmap;
            }

            _writeableBitmap.Lock();

            var viewportRect = new Int32Rect(0, 0, width, height);

            // Copy bitmap from e.Data.DataPointer to writeableBitmap
            _writeableBitmap.WritePixels(viewportRect, data.DataPointer, data.SlicePitch, data.RowPitch);

            _writeableBitmap.AddDirtyRect(viewportRect);
            _writeableBitmap.Unlock();
        }

        private void UpdateSelectedVertex(int hitVertexIndex)
        {
            if (_selectedVertexIndex == hitVertexIndex) // Selecting the same vertex
                return;

            // Reset the previously selected instance
            ClearSelectedVertex();

            if (hitVertexIndex < 0 || hitVertexIndex >= _pointCloudPositions.Length)
                return; // We did not find the instance index


            var vertexPosition = _pointCloudPositions[hitVertexIndex];
            var vertexPoint3D = vertexPosition.ToWpfPoint3D();

            // Size is already set, just set the center position from the offset defined in the World matrix for the selected instance
            _selectedVertexWireCross.Position = vertexPoint3D;
            _selectedVertexWireCross.IsVisible = true;

            VertexIndexTextBlock.Text = hitVertexIndex.ToString();
            VertexPositionTextBlock.Text = vertexPosition.ToString("F2");

            _selectedVertexIndex = hitVertexIndex;

            if (_isMeasuringDistance)
            {
                // In this sample we are rendering _distanceLineVisual3D and _distanceTextBlockVisual3D
                // over all other 3D objects. Therefore, we do not need to adjust the vertexPoint3D.
                // 
                // But if we would render the line and text normally, we would need
                // to move the position closer to camera to prevent obstructing by nearby pixels:
                //var adjustedVertexPoint3D = MovePointToCamera(vertexPoint3D);
                
                var adjustedVertexPoint3D = vertexPoint3D;

                if (_distanceLineVisual3D != null)
                {
                    _distanceLineVisual3D.EndPosition = adjustedVertexPoint3D;

                    EndPositionTextBlock.Text = vertexPoint3D.ToVector3().ToString("F2");

                    var distance = (_distanceLineVisual3D.EndPosition - _distanceLineVisual3D.StartPosition).Length;
                    DistanceTextBlock.Text = distance.ToString("F2");


                    var textDirection = _distanceLineVisual3D.EndPosition - _distanceLineVisual3D.StartPosition;
                    var centerPosition = _distanceLineVisual3D.StartPosition + 0.5 * textDirection;
                    textDirection.Normalize();


                    var startPosScreen = Camera1.Point3DTo2D(_distanceLineVisual3D.StartPosition);
                    var endPosScreen = Camera1.Point3DTo2D(_distanceLineVisual3D.EndPosition);

                    if (startPosScreen.X > endPosScreen.X)
                        textDirection *= -1; // Prevent rendering text that is horizontally flipped
                    
                    var toCameraVector = Camera1.GetCameraPosition() - _distanceLineVisual3D.StartPosition;
                    toCameraVector.Normalize();

                    // Up direction is perpendicular to the line direction and to-camera vector
                    var upDirection = Vector3D.CrossProduct(toCameraVector, textDirection);

                    if (_distanceTextBlockVisual3D == null)
                    {
                        _distanceTextBlockVisual3D = new TextBlockVisual3D()
                        {
                            Foreground = Brushes.Green,
                            FontSize = Camera1.Distance / 40,
                            PositionType = PositionTypes.Center | PositionTypes.Bottom
                        };

                        // Put _distanceTextBlockVisual3D in the OverlayRenderingQueue
                        // This will render it over all other 3D objects.
                        // See DXEngineAdvanced/BackgroundAndOverlayRendering sample for more info.
                        _distanceTextBlockVisual3D.SetDXAttribute(DXAttributeType.CustomRenderingQueue, MainDXViewportView.DXScene.OverlayRenderingQueue);

                        MainViewport.Children.Add(_distanceTextBlockVisual3D);
                    }

                    _distanceTextBlockVisual3D.TextDirection = textDirection;
                    _distanceTextBlockVisual3D.UpDirection = upDirection;
                    _distanceTextBlockVisual3D.Position = centerPosition;

                    _distanceTextBlockVisual3D.Text = DistanceTextBlock.Text;
                }
            }
        }

        private Point3D MovePointToCamera(Point3D position)
        {
            var cameraPosition = Camera1.GetCameraPosition();

            var toCameraVector = cameraPosition - position;
            toCameraVector.Normalize();

            toCameraVector *= Camera1.Distance / 50; // move towards the camera of 1/50 of the camera's distance

            return position + toCameraVector;
        }

        private void ClearSelectedVertex()
        {
            if (_selectedVertexIndex != -1)
            {
                _selectedVertexWireCross.IsVisible = false;
                _selectedVertexIndex = -1;
            }
        }
        
        private void UpdatePixelSize()
        {
            // PixelSizeSlider has a max value 100; use _pixelSizeSliderFactor to convert that to correct PixelSize
            _pixelSize = (float)(_pixelSizeSliderFactor * PixelSizeSlider.Value);

            PixelSizeTextBlock.Text = string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0:0.#}", _pixelSize);

            if (_pixelsVisual3D != null)
                _pixelsVisual3D.PixelSize = _pixelSize;

            _isVertexIdBitmapDirty = true;
        }

        private void PixelSizeSlider_OnValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!this.IsLoaded)
                return;

            UpdatePixelSize();

            MainDXViewportView.Refresh();
        }

        private void OnZUpAxisCheckBoxCheckedChanged(object sender, RoutedEventArgs e)
        {
            if (_fileName == null)
                return;

            // Reload the data again
            LoadPointCloud(_fileName);
        }
    }
}
