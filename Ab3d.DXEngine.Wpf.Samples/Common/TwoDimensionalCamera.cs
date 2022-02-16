using System;
using System.Windows;
using System.Windows.Media.Media3D;
using System.Windows.Threading;
using Ab3d.Cameras;
using Ab3d.Common.Cameras;
using Ab3d.Controls;
using Ab3d.DirectX.Common;
using Ab3d.DirectX.Controls;
using Ab3d.Utilities;
using SharpDX;
using Point = System.Windows.Point;

namespace Ab3d.DirectX
{
    // NOTE:
    // Please submit improvements and fixed to TwoDimensionalCamera to https://github.com/ab4d/Ab3d.DXEngine.Wpf.Samples


    /// <summary>
    /// TwoDimensionalCamera is a helper object that creates a TargetPositionCamera and MouseCameraController
    /// and can be used to show 2D objects and 2D lines in a 3D space.
    /// This is achieved with using an Orthographic camera type where the CameraWidth is based on the width of the view (visible area).
    /// The 2D coordinates should be converted into 3D coordinates with reusing the X and Y coordinates and setting Z coordinates to 0 (or slightly bigger value to show the shape or line above other lines).
    /// </summary>
    public class TwoDimensionalCamera
    {
        private double _dpiScale;

        private double _zoomFactor;

        /// <summary>
        /// CoordinateSystemTypes defines possible coordinate system and axis origin types.
        /// </summary>
        public enum CoordinateSystemTypes
        {
            /// <summary>
            /// Coordinate system and axis origin (0, 0) start at the center of the view. Y axis points up.
            /// </summary>
            CenterOfViewOrigin = 0,

            // Currently unsupported:
            ///// <summary>
            ///// Coordinate system and axis origin (0, 0) start at the bottom left corner of the view. Y axis points up.
            ///// </summary>
            //LowerLeftCornerOrigin = 1
        }

        /// <summary>
        /// Gets a TargetPositionCamera that is used to show the scene. The UsedCamera is created in the constructor of the TwoDimensionalCamera.
        /// The CameraType is set to OrthographicCamera. Initial TargetPosition is set to (0, 0, 1).
        /// </summary>
        public TargetPositionCamera UsedCamera { get; private set; }

        /// <summary>
        /// Gets a MouseCameraController that is used to control the camera. The UsedMouseCameraController is created in the constructor of the TwoDimensionalCamera.
        /// By default the camera rotation and quick zoom is disabled. Camera movement is assigned to left mouse button.
        /// Mouse wheel zoom is enabled and MouseWheelDistanceChangeFactor is set to 1.2 to slightly increase the zoom speed.
        /// ZoomMode is set to MousePosition.
        /// </summary>
        public MouseCameraController UsedMouseCameraController { get; private set; }

        /// <summary>
        /// Gets the DXViewportView that was used to create this TwoDimensionalCamera.
        /// </summary>
        public DXViewportView ParentDXViewportView { get; private set; }

        /// <summary>
        /// Gets a Boolean that was used create this TwoDimensionalCamera and specifies if screen space units are used.
        /// When false the device independent units are used - scaled by DPI scale (the same units are used by WPF).
        /// </summary>
        public bool UseScreenPixelUnits { get; private set; }

        /// <summary>
        /// Gets the CoordinateSystemType that was used to create this TwoDimensionalCamera.
        /// </summary>
        public CoordinateSystemTypes CoordinateSystemType { get; private set; }


        /// <summary>
        /// Gets the size of screen pixel. This value can be used as LineThickness value that would create lines with 1 screen pixel thickness.
        /// This value is set in when the TwoDimensionalCamera is loaded and after the dpi scale is read (see <see cref="IsLoaded"/> property and <see cref="Loaded"/> event).
        /// </summary>
        public double ScreenPixelSize { get; private set; }

        /// <summary>
        /// Gets or sets the ShowCameraLight property from the <see cref="UsedCamera"/>.
        /// </summary>
        public ShowCameraLightType ShowCameraLight
        {
            get => UsedCamera.ShowCameraLight;
            set => UsedCamera.ShowCameraLight = value;
        }

        /// <summary>
        /// Gets or sets the MoveCameraConditions from the <see cref="UsedMouseCameraController"/>.
        /// </summary>
        public MouseCameraController.MouseAndKeyboardConditions MoveCameraConditions
        {
            get
            {
                CheckUsedMouseCameraController(nameof(MoveCameraConditions));
                return UsedMouseCameraController.MoveCameraConditions;
            }
            set
            {
                CheckUsedMouseCameraController(nameof(MoveCameraConditions));
                UsedMouseCameraController.MoveCameraConditions = value;

                UsedMouseCameraController.IsTouchMoveEnabled = value != MouseCameraController.MouseAndKeyboardConditions.Disabled;
            }
        }

        /// <summary>
        /// Gets or sets the QuickZoomConditions from the <see cref="UsedMouseCameraController"/>.
        /// </summary>
        public MouseCameraController.MouseAndKeyboardConditions QuickZoomConditions
        {
            get
            {
                CheckUsedMouseCameraController(nameof(QuickZoomConditions));
                return UsedMouseCameraController.QuickZoomConditions;
            }
            set
            {
                CheckUsedMouseCameraController(nameof(QuickZoomConditions));
                UsedMouseCameraController.QuickZoomConditions = value;
            }
        }

        /// <summary>
        /// Gets or sets the IsMouseAndTouchZoomEnabled from the <see cref="UsedMouseCameraController"/>.
        /// </summary>
        public bool IsMouseAndTouchZoomEnabled
        {
            get
            {
                CheckUsedMouseCameraController(nameof(IsMouseAndTouchZoomEnabled)); 
                return UsedMouseCameraController.IsMouseWheelZoomEnabled;
            }
            set
            {
                CheckUsedMouseCameraController(nameof(IsMouseAndTouchZoomEnabled));
                UsedMouseCameraController.IsMouseWheelZoomEnabled = value;

                UsedMouseCameraController.IsTouchZoomEnabled = value;
            }
        }

        /// <summary>
        /// Gets or sets the zoom factor.
        /// </summary>
        public double ZoomFactor
        {
            get { return _zoomFactor; }
            set
            {
                _zoomFactor = value;
                UsedCamera.CameraWidth = ViewSize.Width / _zoomFactor;
            }
        }
        
        /// <summary>
        /// Gets dpi scale factor.
        /// </summary>
        public double DpiScale
        {
            get { return _dpiScale; }
        }

        /// <summary>
        /// Gets or sets the Offset of the camera - the amount for which the camera was moved.
        /// </summary>
        public System.Windows.Media.Media3D.Vector3D Offset
        {
            get { return UsedCamera.Offset; }
            set { UsedCamera.Offset = value; }
        }

        /// <summary>
        /// Gets a Rect that represent the visible area in the units of this camera.
        /// This takes <see cref="ZoomFactor"/> and <see cref="Offset"/> into account.
        /// To see the size of visible area when there is no zoom applied, see the <see cref="ViewSize"/> property.
        /// </summary>
        public Rect VisibleRect
        {
            get
            {
                var offset = this.Offset;

                double width = ViewSize.Width;
                double height = ViewSize.Height;

                double centerX = offset.X;
                double centerY = offset.Y;

                //if (this.CoordinateSystemType == CoordinateSystemTypes.LowerLeftCornerOrigin)
                //{
                //    centerX += width * 0.5;
                //    centerY += height * 0.5;
                //}

                width /= ZoomFactor;
                height /= ZoomFactor;

                return new Rect(centerX - width * 0.5, centerY - height * 0.5, width, height);
            }
        }

        /// <summary>
        /// Gets the Size of the visible area when there is no zoom applied.
        /// To get the currently visible area based on the current zoom factor and offset see the <see cref="VisibleRect" /> property.
        /// </summary>
        public Size ViewSize { get; private set; }

        /// <summary>
        /// IsLoaded is true when the TwoDimensionalCamera and its ParentDXViewportView is loaded and the dpi scale information is valid. 
        /// </summary>
        public bool IsLoaded { get; private set; }

        /// <summary>
        /// CameraChanged event is triggered when the camera is changed (this can happen also when the size of the view is changed).
        /// </summary>
        public event EventHandler CameraChanged;

        /// <summary>
        /// Loaded event is triggered when the parentDXViewportView is loaded and when the dpi scale information is valid. See also <see cref="IsLoaded"/> property.
        /// </summary>
        public event EventHandler Loaded;


        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="parentDXViewportView">DXViewportView</param>
        /// <param name="mouseEventsSourceElement">FrameworkElement that will be used to subscribe to mouse events (when null) then MouseCameraController will not be created.</param>
        /// <param name="useScreenPixelUnits">Boolean that specifies if screen space units are used. When false the device independent units are used - scaled by DPI scale (the same units are used by WPF).</param>
        /// <param name="coordinateSystemType">CoordinateSystemTypes</param>
        public TwoDimensionalCamera(DXViewportView parentDXViewportView, FrameworkElement mouseEventsSourceElement, bool useScreenPixelUnits, CoordinateSystemTypes coordinateSystemType)
        {
            if (parentDXViewportView == null) throw new ArgumentNullException(nameof(parentDXViewportView));

            ParentDXViewportView = parentDXViewportView;
            UseScreenPixelUnits  = useScreenPixelUnits;
            CoordinateSystemType = coordinateSystemType;

            if (!parentDXViewportView.IsSceneInitialized)
                parentDXViewportView.DXSceneInitialized += ParentDXViewOnDXSceneInitialized;

            parentDXViewportView.DXRenderSizeChanged += ParentDXViewOnDXRenderSizeChanged;

            _zoomFactor = 1;
            UpdateDpiScale(); // Call this method even if DXScene is not yet initialized - in this case the _dpiScale and _singlePixelLineThickness will be set to 1 (and will not be 0 anymore)


            var targetPositionCamera = new TargetPositionCamera();

            // Set camera initial values
            targetPositionCamera.BeginInit();
            targetPositionCamera.TargetViewport3D = parentDXViewportView.Viewport3D;
            targetPositionCamera.CameraType       = BaseCamera.CameraTypes.OrthographicCamera;
            targetPositionCamera.TargetPosition   = new Point3D(0, 0, 1); // Set to 0,0,1 so that we will never come after the lines
            targetPositionCamera.Heading          = 0;
            targetPositionCamera.Attitude         = 0;
            targetPositionCamera.Bank             = 0;
            targetPositionCamera.EndInit();

            UsedCamera = targetPositionCamera;

            ProcessSizeChanged();

            targetPositionCamera.CameraChanged += delegate(object sender, CameraChangedRoutedEventArgs args)
            {
                ProcessCameraChanged();
            };


            if (mouseEventsSourceElement != null)
            {
                UsedMouseCameraController = new MouseCameraController()
                {
                    MoveCameraConditions           = MouseCameraController.MouseAndKeyboardConditions.LeftMouseButtonPressed,
                    QuickZoomConditions            = MouseCameraController.MouseAndKeyboardConditions.Disabled,
                    RotateCameraConditions         = MouseCameraController.MouseAndKeyboardConditions.Disabled,
                    IsMouseWheelZoomEnabled        = true,
                    MouseWheelDistanceChangeFactor = 1.2, // Increase mouse wheel zooming speed by changing the factor from 1.05 to 1.2
                    ZoomMode                       = MouseCameraController.CameraZoomMode.MousePosition,
                    IsTouchRotateEnabled           = false,
                    IsConcurrentTouchZoomEnabled   = false,
                    IsTouchZoomEnabled             = true,
                    IsTouchMoveEnabled             = true,
                    TargetCamera                   = targetPositionCamera,
                    EventsSourceElement            = mouseEventsSourceElement
                };
            }

            if (parentDXViewportView.IsLoaded)
                IsLoaded = true;
            else
                parentDXViewportView.Loaded += ParentDXViewportViewOnLoaded;
        }

        private void ParentDXViewportViewOnLoaded(object sender, RoutedEventArgs e)
        {
            IsLoaded = true;

            ParentDXViewportView.Loaded -= ParentDXViewportViewOnLoaded;

            UpdateDpiScale();
            OnLoaded();
        }

        /// <summary>
        /// Update method manually updates the DpiScale and ViewSize.
        /// </summary>
        public void Update()
        {
            UpdateDpiScale();
            ProcessSizeChanged();
        }

        /// <summary>
        /// Reset method resets the camera to show the center of the axis and reset zoom factor to 1.
        /// </summary>
        public void Reset()
        {
            UsedCamera.Offset         = new Vector3D(0, 0, 0);
            UsedCamera.TargetPosition = new Point3D(0, 0, 1);

            ZoomFactor = 1;
        }


        /// <summary>
        /// Returns a Point3D in the view coordinates from the 2D wpfPosition (for example converts a mouse coordinate to coordinates in which the shown lines and shapes are defined).
        /// See <see cref="ToWpfPosition(Point3D)"/> to get the opposite conversion.
        /// </summary>
        /// <param name="wpfPosition">WPF's 2D position</param>
        /// <returns>Point3D in the view coordinates</returns>
        public Point3D ToViewPosition3D(Point wpfPosition)
        {
            var point2d = ToViewPosition(wpfPosition);
            return new Point3D(point2d.X, point2d.Y, 0);
        }

        /// <summary>
        /// Returns a Point in the view coordinates from the 2D wpfPosition (for example converts a mouse coordinate to coordinates in which the shown lines and shapes are defined).
        /// See <see cref="ToWpfPosition(Point)"/> to get the opposite conversion.
        /// </summary>
        /// <param name="wpfPosition">WPF's 2D position</param>
        /// <returns>Point in the view coordinates</returns>
        public Point ToViewPosition(Point wpfPosition)
        {
            double scale = _zoomFactor;
            if (UseScreenPixelUnits)
                scale *= _dpiScale;

            // adjust because we have center of screen in the middle (where the camera looks to)
            var offset = UsedCamera.Offset + UsedCamera.TargetPosition;

            double x = ( (wpfPosition.X - ViewSize.Width  * 0.5) / scale) + offset.X;
            double y = (-(wpfPosition.Y - ViewSize.Height * 0.5) / scale) + offset.Y;

            return new Point(x, y);
        }

        /// <summary>
        /// Returns a Point in the WPF coordinates from the 2D view coordinates (for example converts a line or shape coordinate to WPF coordinates of an overlay Canvas).
        /// See <see cref="ToViewPosition(Point)"/> to get the opposite conversion.
        /// </summary>
        /// <param name="viewPosition">2D view coordinates</param>
        /// <returns>WPF's 2D position</returns>        
        public Point ToWpfPosition(Point viewPosition)
        {
            double scale = _zoomFactor;
            if (UseScreenPixelUnits)
                scale *= _dpiScale;

            // adjust because we have center of screen in the middle (where the camera looks to)
            var offset = UsedCamera.Offset + UsedCamera.TargetPosition;

            // For the formula in ToViewPosition
            // x2                      = ((x1 - ViewSize.Width * 0.5) / scale) + offset.X;
            // x2 - offset.X           = ((x1 - ViewSize.Width * 0.5) / scale);
            // (x2 - offset.X) * scale = x1 - ViewSize.Width * 0.5;
            // x1                      = ((x2 - offset.X) * scale) + ViewSize.Width * 0.5;

            double x = ( (viewPosition.X - offset.X) * scale) + ViewSize.Width * 0.5;
            double y = (-(viewPosition.Y - offset.Y) * scale) + ViewSize.Height * 0.5;

            return new Point(x, y);
        }

        /// <summary>
        /// Returns a Point in the WPF coordinates from the view coordinates specified as as Point3D (for example converts a line or shape coordinate to WPF coordinates of an overlay Canvas).
        /// See <see cref="ToViewPosition3D(Point)"/> to get the opposite conversion.
        /// </summary>
        /// <param name="viewPosition3D">view coordinates as Point3D</param>
        /// <returns>WPF's 2D position</returns>           
        public Point ToWpfPosition(Point3D viewPosition3D)
        {
            var point2d = ToWpfPosition(new Point(viewPosition3D.X, viewPosition3D.Y));
            return new Point(point2d.X, point2d.Y);
        }


        /// <summary>
        /// Gets the size in the current view units and zoom level
        /// (for example if you want to show a rectangle that will be shown as 10 WPF units wide rectangle, then use this method to get the required size in view units and for the current zoom factor).
        /// </summary>
        /// <param name="wpfSize">wpfSize</param>
        /// <returns>size in the current view units and zoom factor</returns>
        public double GetViewSizeFromWpfSize(double wpfSize)
        {
            return wpfSize / ZoomFactor;
        }

        /// <summary>
        /// Gets the size in WPF units form the view units.
        /// This represents the size as visible by the user in WPF units based on the view units and current zoom factor.
        /// </summary>
        /// <param name="viewSize">size in view units</param>
        /// <returns>size in WPF units</returns>        
        public double GetWpfSizeFromViewSize(double viewSize)
        {
            return viewSize * ZoomFactor;
        }
        

        /// <summary>
        /// OnCameraChanged
        /// </summary>
        protected void OnCameraChanged()
        {
            if (CameraChanged != null)
                CameraChanged(this, null);
        }

        /// <summary>
        /// OnLoaded
        /// </summary>
        protected void OnLoaded()
        {
            if (Loaded != null)
                Loaded(this, null);
        }


        private void ParentDXViewOnDXSceneInitialized(object sender, EventArgs e)
        {
            ParentDXViewportView.DXSceneInitialized -= ParentDXViewOnDXSceneInitialized;

            UpdateDpiScale();
            ProcessSizeChanged();
        }

        private void ParentDXViewOnDXRenderSizeChanged(object sender, DXViewSizeChangedEventArgs e)
        {
            ProcessSizeChanged();

            if (!this.IsLoaded)
                ParentDXViewportViewOnLoaded(sender, null);
        }

        private void UpdateDpiScale()
        {
            _dpiScale = (ParentDXViewportView.DpiScaleX + ParentDXViewportView.DpiScaleY) * 0.5;
            ScreenPixelSize = 1 / _dpiScale;
        }

        private void ProcessCameraChanged()
        {
            if (ViewSize.Width > 0)
                _zoomFactor = ViewSize.Width / UsedCamera.CameraWidth;

            OnCameraChanged();
        }

        private void ProcessSizeChanged()
        {
            var dxView = ParentDXViewportView;

            if (!dxView.IsInitialized)
                return;


            double viewWidth, viewHeight;

            if (dxView.DXScene != null)
            {
                if (this.UseScreenPixelUnits)
                {
                    viewWidth  = dxView.DXFinalPixelSize.Width;
                    viewHeight = dxView.DXFinalPixelSize.Height;
                }
                else
                {
                    viewWidth  = dxView.DXRenderSize.Width;
                    viewHeight = dxView.DXRenderSize.Height;
                }
            }
            else
            {
                // We use WPF 3D rendering
                viewWidth  = dxView.ActualWidth;
                viewHeight = dxView.ActualHeight;
            }

            ViewSize = new Size(viewWidth, viewHeight);

            UsedCamera.CameraWidth = viewWidth / _zoomFactor;

            //OnCameraChanged(); // OnCameraChanged is called from ProcessCameraChanged that is called after we change the CameraWidth
        }

        private void CheckUsedMouseCameraController(string propertyName)
        {
            if (UsedMouseCameraController == null)
                throw new Exception(string.Format("Cannot access property {0} because TwoDimensionalCamera was not created with mouseEventsSourceElement set and this did not create an instance of MouseCameraController", propertyName));
        }
    }
}