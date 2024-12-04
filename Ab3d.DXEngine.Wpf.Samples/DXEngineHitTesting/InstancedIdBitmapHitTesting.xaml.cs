using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
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
using Ab3d.Common.EventManager3D;
using Ab3d.Visuals;
using SharpDX;
using Ab3d.DirectX;
using Ab3d.DirectX.Utilities;
using Ab3d.Meshes;
using Ab3d.Utilities;
using InstanceData = Ab3d.DirectX.InstanceData;
using Point = System.Windows.Point;

namespace Ab3d.DXEngine.Wpf.Samples.DXEngineHitTesting
{
    // This sample shows how to render instanced objects in such a way that the color of the object represent the instance id (instance index).
    // This can be used for hit testing (checking which instance is behind the mouse position).
    //
    // A more traditional hit testing with using DXEventManager3D, DXScene.GetClosestHitObject or DXScene.GetAllHitObjects is demonstrated in InstancedObjectsHitTesting sample.
    // Advantaged of this hit testing are:
    // - the most easy to use
    // - can use DXEventManager3D that support 15 different mouse events (including click, enter, drag, etc.)
    // - can get all hit positions / instances behind the position (not just the closest hit)
    // - do not require to change the 3d scene (make other objects hidden, change instance rendering properties)
    // - does not require to copy the memory from GPU to CPU
    //
    // Disadvantages:
    // - When using many instances, the traditional hit testing can be slower because many instances need to be checked
    // - It is hard to implement multiple selection (rectangle selection or selection with polygon)
    //
    //
    // Advantages of using InstanceId bitmap:
    // - when using many instances and fast GPU, then rendering InstanceId bitmap and copying it to main memory is faster then using CPU to check all instances
    // - when the scene is static and camera is not changed very often, then we only need to render one InstanceId bitmap and then when the mouse position is changed
    //   can use the same bitmap to get the closest instance id (this is much faster then then checking hit triangles that is done in the traditional hit testing)
    // - can be used to implement multiple selection (rectangle selection or selection with polygon)
    //
    // Disadvantages:
    // - It takes a lot of time to copy the rendered bitmap from GPU to CPU (reducing the size of bitmap can help)
    // - When the scene is animated, this require to render InstanceId bitmap on each frame and this may be slower then using traditional hit testing
    // - It is not possible to get all hit instances (all hit instances behind the position and not just the closest position)
    // - The result just provides hit instance id and does not provide the actual hit position, distance to camera and index of the triangle.


    /// <summary>
    /// Interaction logic for InstancedIdBitmapHitTesting.xaml
    /// </summary>
    public partial class InstancedIdBitmapHitTesting : Page
    {
        private InstanceData[] _instancedData;
        private MeshGeometry3D _instanceMeshGeometry3D;

        private Stopwatch _stopwatch;

        private int _selectedInstanceIndex = -1;

        private Color4 _addedInstanceIdColor;
        private byte[] _addedInstanceIdColorBytes;
        
        private bool _isCameraChanging;
        private bool _isInstanceIdBitmapDirty;

        private WriteableBitmap _writeableBitmap;

        private byte[] _instanceIdBitmapBytes;
        private int _instanceIdBitmapWidth;
        private int _instanceIdBitmapHeight;
        private int _instanceIdBitmapSize;
        private int _instanceIdBitmapRowSize;

        private InstancedMeshGeometryVisual3D _instancedMeshGeometryVisual3D;

        private double _renderedBitmapScale = 1;

        public InstancedIdBitmapHitTesting()
        {
            InitializeComponent();

            // Setting GraphicsProfiles tells DXEngine which graphics device it should use (and what devices are fallback devices if the first device cannot be initialized)
            // ApplicationContext.Current.GraphicsProfiles is changed by changing the "Graphics settings" with clicking on its button in upper left part of application.
            MainDXViewportView.GraphicsProfiles = DirectX.Client.Settings.DXEngineSettings.Current.GraphicsProfiles;

            if (DesignerProperties.GetIsInDesignMode(this))
                return;

            BitmapScaleComboBox.ToolTip = 
@"It takes a lot of time to copy the rendered bitmap from the GPU memory to the main CPU memory.
To speed this up, it is possible to reduce the size of the rendered bitmap and 
this significantly reduces the time to get the bitmap.";


            this.Loaded += delegate(object sender, RoutedEventArgs args)
            {
                // Create instance MeshGeometry3D
                _instanceMeshGeometry3D = new Ab3d.Meshes.SphereMesh3D(centerPosition: new Point3D(0, 0, 0), radius: 5, segments: 20, generateTextureCoordinates: false).Geometry;

                SelectedInstanceWireBox.Size = new Size3D(12, 12, 12); // Set size slightly bigger then mesh's size

                // Prepare data for 8000 instances (each InstanceData represents one instance's Color and its World matrix that specifies its position, scale and rotation)
                _instancedData = InstancedObjectsHitTesting.CreateInstancesData(new Point3D(0, 200, 0), new Size3D(400, 400, 400), 20, 20, 20);

                CreateScene();

                SubscribeCameraAndMouseEvents();
            };

            // IMPORTANT:
            // It is very important to call Dispose method on DXSceneView after the control is not used any more (see help file for more info)
            this.Unloaded += (sender, args) => MainDXViewportView.Dispose();
        }

        private void SubscribeCameraAndMouseEvents()
        {
            // Subscribe to Camera changes and size changed to mark that we need to render the InstanceId bitmap again
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
            ClearSelectedInstance();
        }

        private void ProcessCameraChanged()
        {
            _isInstanceIdBitmapDirty = true;

            if (_isCameraChanging || _instanceIdBitmapBytes == null) // Do not update InstanceId bitmap and hit vertex while camera is changing; also wait until first bitmap is rendered and only then update
                return;


            if (IsUpdatingInstanceIdBitmapCheckBox.IsChecked ?? false)
                UpdateInstanceIdBitmap();

            if (IsHitTestingCheckBox.IsChecked ?? false)
            {
                var mousePosition = Mouse.GetPosition(MainDXViewportView);
                HitTestWithInstanceIdBitmap(mousePosition);
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (_isCameraChanging) // Do not update VertexId bitmap and hit vertex while camera is changing
                return;

            if (_isInstanceIdBitmapDirty && (IsUpdatingInstanceIdBitmapCheckBox.IsChecked ?? false))
                UpdateInstanceIdBitmap();

            if (IsHitTestingCheckBox.IsChecked ?? false)
            {
                var mousePosition = e.GetPosition(MainDXViewportView);
                HitTestWithInstanceIdBitmap(mousePosition);
            }

            base.OnMouseMove(e);
        }

        private void HitTestWithInstanceIdBitmap(Point mousePosition)
        {
            if (IsUpdatingInstanceIdBitmapCheckBox.IsChecked ?? false)
                UpdateInstanceIdBitmap();

            if (_instanceIdBitmapBytes == null || _instanceIdBitmapWidth <= 0 || _instanceIdBitmapHeight <= 0)
                return;

            if (mousePosition.X < 0 || mousePosition.Y < 0 || mousePosition.X > MainDXViewportView.ActualWidth || mousePosition.Y > MainDXViewportView.ActualHeight)
                return;

            // We need to scale from WPF's coordinates to the rendered coordinates
            // The xScale and yScale are usually the same as dpi scale,
            // but when rendering to a smaller instance id bitmap, this scale can be different.
            double xScele = (double)_instanceIdBitmapWidth / MainDXViewportView.ActualWidth;
            double yScele = (double)_instanceIdBitmapHeight / MainDXViewportView.ActualHeight;

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
                InstanceIdTextBlock.Text = null;
                return; // Cannot get data
            }

            // get instance if from byte array at that offset
            int instanceId = GetInstanceIdFromColor(pixelByteOffset, _addedInstanceIdColorBytes);


            // DEBUG data (uncomment to show raw byte array values in VS output)
            //System.Diagnostics.Debug.WriteLine($"{xPos} {yPos} => ({_instanceIdBitmapBytes[pixelByteOffset + 3]}, {_instanceIdBitmapBytes[pixelByteOffset + 2]}, {_instanceIdBitmapBytes[pixelByteOffset + 1]}, {_instanceIdBitmapBytes[pixelByteOffset]}) => {instanceId}");


            if (instanceId == -1)
            {
                // No instance hit
                ClearSelectedInstance();
                InstanceIdTextBlock.Text = null;
            }
            else
            {
                UpdateSelectedInstance(instanceId);
            }

            // After instance data is changed we need to call Update method
            //_instancedMeshGeometryVisual3D.Update();
        }

        private void UpdateInstanceIdBitmap()
        {
            if (!_isInstanceIdBitmapDirty) 
                return;

            // When rendering instance id bitmap we will add an Opaque black color (0, 0, 0, 255: with alpha set to 1) to the rendered bitmap. 
            // This makes it possible for us to see the colors on the rendered image because alpha value is set to 1.
            // If we render more the 16.7 million instances, then we should set added color to (0, 0, 0, 0) so that 
            // the alpha value can be also used for instance id value.
            
            _addedInstanceIdColor = Color4.Black;
            _addedInstanceIdColorBytes = _addedInstanceIdColor.ToArray().Select(c => (byte)(c * 255)).ToArray(); // Convert to byte array

            MainDXViewportView.Update();

            // Prepare the scene:

            // First call UseInstanceIdColor that will use instance id rendering.
            // We pass added color as parameter (see comment above for more info).
            //
            // We could also use the different _addedInstanceIdColor when rendering multiple instanced objects,
            // for example by using different alpha value for each instanced object.
            _instancedMeshGeometryVisual3D.UseInstanceIdColor(_addedInstanceIdColor);

            // Set BackgroundColor to black with zero alpha (0, 0, 0, 0)
            var savedBgColor = MainDXViewportView.BackgroundColor;
            MainDXViewportView.BackgroundColor = System.Windows.Media.Color.FromArgb(0, 0, 0, 0);

            // Here we could also set other 3D objects to hidden (to prevent them from rendering)
            SelectedInstanceWireBox.IsVisible = false;


            int width  = (int)(MainDXViewportView.DXFinalPixelSize.Width * _renderedBitmapScale);   // To get the bitmap size use DXFinalPixelSize as it is not increased when using super-sampling
            int height = (int)(MainDXViewportView.DXFinalPixelSize.Height * _renderedBitmapScale);

            _stopwatch.Restart();

            // Render to bitmap with the specified size but without multi-sampling and super-sampling
            MainDXViewportView.RenderToBitmap(OnRenderedBitmapReady, width, height, preferedMultisampling: 1, supersamplingCount: 1, dpiX: 96, dpiY: 96, convertToNonPreMultipledAlpha: false);

            _stopwatch.Stop();


            // Reset the setting for normal rendering
            _instancedMeshGeometryVisual3D.UseInstanceObjectColor();
            MainDXViewportView.BackgroundColor = savedBgColor;

            if (_selectedInstanceIndex != -1)
                SelectedInstanceWireBox.IsVisible = true;

            // Mark that the current instance id bitmap is correct (this will be set to false when camera or size is changed)
            _isInstanceIdBitmapDirty = false;

            RenderTimeTextBlock.Text = string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0:0.0} ms", _stopwatch.Elapsed.TotalMilliseconds);
        }

        public int GetPixelByteOffset(int xPos, int yPos)
        {
            if (_instanceIdBitmapBytes == null)
                return -1;

            int offset = yPos * _instanceIdBitmapRowSize + xPos * 4;

            if (offset > _instanceIdBitmapSize)
                return -1;

            return offset;
        }

        public int GetInstanceIdFromColor(int pixelByteOffset, byte[] addedColor)
        {
            byte alpha = _instanceIdBitmapBytes[pixelByteOffset + 3];

            // Because we are using opaque black as added color and transparent black as Background color
            // we know that we hit background when alpha value is 0
            if (alpha == 0)
                return -1; 

            int instanceId = ((alpha - addedColor[3]) << 24) +                                         // alpha
                             ((_instanceIdBitmapBytes[pixelByteOffset + 2] - addedColor[2]) << 16) +   // red
                             ((_instanceIdBitmapBytes[pixelByteOffset + 1] - addedColor[1]) << 8) +    // green
                              (_instanceIdBitmapBytes[pixelByteOffset]     - addedColor[0]);           // blue

            return instanceId;
        }
        
        private void OnRenderedBitmapReady(int width, int height, DataBox data)
        {
            if (_instanceIdBitmapBytes != null && _instanceIdBitmapBytes.Length < data.SlicePitch)
                _instanceIdBitmapBytes = null;

            if (_instanceIdBitmapBytes == null)
                _instanceIdBitmapBytes = new byte[data.SlicePitch];

            Marshal.Copy(data.DataPointer, _instanceIdBitmapBytes, 0, data.SlicePitch);

            _instanceIdBitmapWidth   = width;
            _instanceIdBitmapHeight  = height;
            _instanceIdBitmapSize    = data.SlicePitch;
            _instanceIdBitmapRowSize = data.RowPitch;


            var showInstance = ShowInstanceIdBitmapCheckBox.IsChecked ?? false;
            var saveToDesktop = SaveToDesktopBitmapCheckBox.IsChecked ?? false;

            if (showInstance || saveToDesktop)
            {
                UpdateWriteableBitmap(width, height, data);

                if (saveToDesktop)
                {
                    string fileName = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), 
                                                             "InstanceIdBitmap.png");
                    
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

        private void CreateScene()
        {
            ObjectsPlaceholder.Children.Clear();

            if (_instancedMeshGeometryVisual3D != null)
                _instancedMeshGeometryVisual3D.Dispose();


            _stopwatch = new Stopwatch();

            // Create InstancedGeometryVisual3D
            _instancedMeshGeometryVisual3D = new InstancedMeshGeometryVisual3D(_instanceMeshGeometry3D);

            _instancedMeshGeometryVisual3D.InstancesData = _instancedData;

            ObjectsPlaceholder.Children.Add(_instancedMeshGeometryVisual3D);

            // If we would only change the InstancedData we would need to call Update method (but here this is not needed because we have set the data for the first time)
            //_instancedGeometryVisual3D.Update();
        }

        private void UpdateSelectedInstance(int hitInstanceIndex)
        {
            if (_selectedInstanceIndex == hitInstanceIndex) // Selecting the same instance
                return;

            // Reset the previously selected instance
            ClearSelectedInstance();

            if (hitInstanceIndex < 0 || hitInstanceIndex >= _instancedData.Length)
                return; // We did not find the instance index


            // Size is already set, just set the center position from the offset defined in the World matrix for the selected instance
            SelectedInstanceWireBox.CenterPosition = new Point3D(_instancedData[hitInstanceIndex].World.M41,
                                                                 _instancedData[hitInstanceIndex].World.M42,
                                                                 _instancedData[hitInstanceIndex].World.M43);

            SelectedInstanceWireBox.IsVisible = true;

            InstanceIdTextBlock.Text = hitInstanceIndex.ToString();

            _selectedInstanceIndex = hitInstanceIndex;
        }

        private void ClearSelectedInstance()
        {
            if (_selectedInstanceIndex != -1)
            {
                SelectedInstanceWireBox.IsVisible = false;
                _selectedInstanceIndex = -1;
            }
        }

        private void BitmapScaleComboBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!this.IsLoaded || BitmapScaleComboBox.SelectedIndex < 0)
                return;

            _renderedBitmapScale = 1.0 / (double)(BitmapScaleComboBox.SelectedIndex + 1); // 1.0, 0.5, 0.33, 0.25

            _isInstanceIdBitmapDirty = true;
            UpdateInstanceIdBitmap();
        }
    }
}
