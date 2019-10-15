using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
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
using System.Windows.Threading;
using Ab3d.Cameras;
using Ab3d.Common.Cameras;
using Ab3d.DirectX;
using Ab3d.DirectX.Controls;
using Ab3d.DXEngine.Wpf.Samples.BackgroundRenderer;
using Ab3d.Visuals;
using SharpDX;
using SharpDX.Direct3D;

namespace Ab3d.DXEngine.Wpf.Samples.DXEngineOther
{
    // This sample shows how to create and render the 3D scene on the background thread.
    // This allows rendering very complex scenes without slowing down the processing on the main UI thread.
    // It also allows rendering many frames per second (many thousands frames per second) because it is possible to run an infinite rendering loop on the background thread.

    // Advantages of background rendering:
    // + allow rendering even the most complex 3D scenes without slowing down the main UI thread
    // + run 3D animations on background thread without slowing them in case of complex code execution on UI thread
    // + can run more then 60 frames per second

    // Disadvantages of background rendering:
    // - only for experienced developers - you need to how to write mult-threaded programs
    // - greatly increase the complexity of the application because of complicated communication between main UI and background thread: main UI thread cannot directly access and modify the 3D objects but need to dispatch all actions or use some other mechanism to safly send data across thread
    // - current implementation of BackgroundDXEngineRenderer is not optimized for low battery usage - it takes 100% of one CPU core.


    // This sample demonstrates one way of designing a background rendering engine.
    // It uses a BackgroundDXEngineRenderer base class and a derived SampleBackgroundDXEngineRenderer that adds the sample 3D scene - both classes are available with full source code in the BackgroundRenderer folder.
    // BackgroundDXEngineRenderer used Dispatcher to dispatch calls from main UI thread to the background thread.

    // When running the sample, the status bar below the sample shows how responsive the main UI and the background threads are.
    // This is shown with FPS (frames per second). The main UI thread also shows how many mouse move events happened in the last second - low number of mouse move events when uses is moving the mouse shows that the UI thread is not very responsive.
    // It is possible to simulate load on both threads with checking the "UI Thread Sleep" or "Rendering Sleep" CheckBoxes - this adds simple Thread.Sleep call.


    /// <summary>
    /// Interaction logic for BackgroundRenderingSample.xaml
    /// </summary>
    public partial class BackgroundRenderingSample : Page
    {
        // SAMPLE CONSTANTS:

        // For testing purposes it is possible to run the BackgroundDXEngineRenderer on the Main UI thread
        // To do that, set the following const to false.
        private const bool IsRenderingInBackgroundThread = true;




        private D3DHost _d3dHost;

        private SampleBackgroundDXEngineRenderer _backgroundDXEngineRenderer;

        private double _dpiScaleX, _dpiScaleY;

        private TargetPositionCamera _targetPositionCamera;

        private int _uiThreadRenderingSleep;

        private bool _isBackgroundThreadCameraRotating;
        private bool _isShowingComplexScene;

        // FPS stats fields:
        private int _uiThreadFramesCount;
        private int _uiThreadMouseMovesCount;
        private int _uiThreadLastFpsMeterSecond = -1;
        private bool _uiThreadIsFirstSecond = true;
        private Thread _backgroundThread;

        public BackgroundRenderingSample()
        {
            InitializeComponent();

            if (!IsRenderingInBackgroundThread) // In case we are running BackgroundDXEngineRenderer on the Main UI thread, mark that on the GUI
                BGRenderingTitleTextBlock.TextDecorations.Add(TextDecorations.Strikethrough);


            // We need to wait until this sample is loaded, otherwise we do not get the correct dpiScale values.
            this.Loaded += delegate(object sender, RoutedEventArgs args)
            {
                // Get dpi scale setting and store that for later
                BackgroundDXEngineRenderer.GetActualDpiScaleValues(this, out _dpiScaleX, out _dpiScaleY);

                // Create D3DHost control that will host the 3D rendered scene
                CreateD3DHost();

                // Create MouseCameraController that will allow user to rotate the camera
                CreateMouseCameraController();
            };


            // Subscribe to rendering to collect Main UI thread FPS statistics
            CompositionTarget.Rendering += UpdateMainUIThreadFPS;

            // We also count mouse move events to see when the UI thread is not responsing well
            this.PreviewMouseMove += delegate(object sender, MouseEventArgs args)
            {
                _uiThreadMouseMovesCount++;
            };
            

            // Handle cleanup
            this.Unloaded += delegate(object sender, RoutedEventArgs args)
            {
                if (_targetPositionCamera.IsRotating)
                    _targetPositionCamera.StopRotation();

                CompositionTarget.Rendering -= UpdateMainUIThreadFPS;

                DXViewportBorder.Child = null;

                if (_d3dHost != null)
                {
                    _d3dHost.Dispose();
                    _d3dHost = null;
                }

                if (_backgroundDXEngineRenderer != null)
                    _backgroundDXEngineRenderer.Dispose();
            };
        }

        private void CreateD3DHost()
        {
            // D3DHost is a control that creates a new window that is hosted inside this WPF application and returns the window's handle (hWnd).
            // The hWnd can be used to initialize DXEngine so it will render the 3D scene to the specified area.
            // NOTE: The D3DHost is used with DXViewport3D when the PresentationType == DirectXOverlay

            _d3dHost = new Ab3d.DirectX.Controls.D3DHost();
            _d3dHost.HandleCreated += _d3dHost_HandleCreated;
            _d3dHost.Painting      += _d3dHost_Painting;
            _d3dHost.SizeChanging  += _d3dHost_SizeChanging;

            // Add D3DHost to this WPF application
            DXViewportBorder.Child = _d3dHost;
        }

        // Window handle created
        private void _d3dHost_HandleCreated(object sender, HandleCreatedEventArgs e)
        {
            if (IsRenderingInBackgroundThread)
            {
                // When we have a window handle (will show the 3D rendered scene), we can create a new background thread that will create the SampleBackgroundDXEngineRenderer
                CreateBackgroundThread(e.Handle);
            }
            else
            {
                // This is a test code:
                // Create the CreateBackgroundDXEngineRenderer on the main UI thread:

                // Delay the call to CreateBackgroundDXEngineRenderer to prevent the "Dispatcher processing has been suspended, but messages are still being processed." exception
                this.Dispatcher.BeginInvoke(new Action(() => CreateBackgroundDXEngineRenderer(e.Handle)));
            }
        }

        private void _d3dHost_Painting(object sender, EventArgs e)
        {
            // We need to re-render the scene on each Painting event
            if (_backgroundDXEngineRenderer != null)
                _backgroundDXEngineRenderer.RenderScene();
        }

        private void _d3dHost_SizeChanging(object sender, EventArgs e)
        {
            if (_backgroundDXEngineRenderer == null)
                return;

            _backgroundDXEngineRenderer.Resize(_d3dHost.ClientWindowSize.Width, _d3dHost.ClientWindowSize.Height);
        }


        // Create background thread that will create the SampleBackgroundDXEngineRenderer
        private void CreateBackgroundThread(IntPtr hWnd)
        {
            _backgroundThread = new Thread(CreateBackgroundDXEngineRenderer);
            _backgroundThread.SetApartmentState(ApartmentState.STA); // WPF only runs on STA
            _backgroundThread.IsBackground = true;
            _backgroundThread.Start(hWnd); // Send hWnd as parameter to CreateBackgroundDXEngineRenderer
        }

        // IMPORTANT:
        // This method is called on a background thread !!!
        private void CreateBackgroundDXEngineRenderer(object hWndObject) // hWndObject is of type object because it is used a Thread start method and that takes only object
        {
            //System.Threading.Thread.Sleep(500);

            var hWnd = (IntPtr)hWndObject;

            _backgroundDXEngineRenderer = new SampleBackgroundDXEngineRenderer(hWnd, logFpsStatsAction: UpdateBackgroundFpsStatsText);

            _backgroundDXEngineRenderer.InitializeDXViewportView(_d3dHost.ClientWindowSize.Width, _d3dHost.ClientWindowSize.Height,
                                                                 _dpiScaleX, _dpiScaleY,
                                                                 preferedMultisamplingCount: 4);


            // InitializeDXViewportView also creates the sample 3D scene and camera on the background thread.
            // Call UpdateMainUICamera to update the _targetPositionCamera from the camera data on the background thread.
            this.Dispatcher.BeginInvoke(new Action(UpdateMainUICamera));


            // NOTE: This method will not return until _backgroundDXEngineRenderer.Dispose method is called
            _backgroundDXEngineRenderer.StartMessageLoop();
        }



        private void CreateMouseCameraController()
        {
            // To support mouse camera rotation and movement
            // we create a dummy Viewport3D, a TargetPositionCamera and a MouseCameraController.
            // We subscribe to the TargetPositionCamera changes and propagate all the changes to the camera in the background rendering thread.
            var dummyViewport3D = new Viewport3D();

            _targetPositionCamera = new TargetPositionCamera()
            {
                TargetViewport3D = dummyViewport3D,
                ShowCameraLight = ShowCameraLightType.Never
            };

            // Because the camera will not be added to the UI three, we need to manually Refresh it
            _targetPositionCamera.Refresh();

            // On each camera change we update the camera in the background rendering thread
            _targetPositionCamera.CameraChanged += delegate (object sender, CameraChangedRoutedEventArgs args)
            {
                UpdateBackgroundRendererCamera(_targetPositionCamera);
            };


            var mouseCameraController = new Ab3d.Controls.MouseCameraController()
            {
                TargetCamera = _targetPositionCamera,
                EventsSourceElement = DXViewportBorder,
                RotateCameraConditions = Ab3d.Controls.MouseCameraController.MouseAndKeyboardConditions.LeftMouseButtonPressed,
                MoveCameraConditions = Ab3d.Controls.MouseCameraController.MouseAndKeyboardConditions.LeftMouseButtonPressed | Ab3d.Controls.MouseCameraController.MouseAndKeyboardConditions.ControlKey,
            };

            mouseCameraController.CameraRotateStarted += delegate (object sender, EventArgs args)
            {
                // When the rotation is stared we need to synchronize the camera's data
                UpdateMainUICamera();

                // If background camera is retating, we pause the rotation during the user's mouse rotation
                if (_isBackgroundThreadCameraRotating)
                    _backgroundDXEngineRenderer.StopCameraRotation();
            };

            mouseCameraController.CameraRotateEnded += delegate (object sender, EventArgs args)
            {
                // resume the background camera rotation
                if (_isBackgroundThreadCameraRotating)
                    _backgroundDXEngineRenderer.StartCameraRotation(headingChangeInSecond: 40);
            };
        }

        // Gets camera data from background renderer and updates the _targetPositionCamera on the main UI thread
        private void UpdateMainUICamera()
        {
            Point3D targetPosition = new Point3D();
            double heading = 0;
            double attitude = 0;
            double distance = 0;

            _backgroundDXEngineRenderer.BackgroundRendererDispatcher.Invoke(new Action(() =>
            {
                _backgroundDXEngineRenderer.GetCameraData(out heading, out attitude, out distance, out targetPosition);
            }));

            _targetPositionCamera.TargetPosition = targetPosition;
            _targetPositionCamera.Heading = heading;
            _targetPositionCamera.Attitude = attitude;
            _targetPositionCamera.Distance = distance;
        }
        
        private void UpdateBackgroundRendererCamera(TargetPositionCamera targetPositionCamera)
        {
            if (_backgroundDXEngineRenderer != null)
                _backgroundDXEngineRenderer.ChangeCamera(targetPositionCamera.Heading, targetPositionCamera.Attitude, targetPositionCamera.Distance, targetPositionCamera.TargetPosition);
        }

        private void UpdateMainUIThreadFPS(object sender, EventArgs args)
        {
            _uiThreadFramesCount++;

            int currentSecond = DateTime.Now.Second;

            if (_uiThreadLastFpsMeterSecond == -1)
            {
                _uiThreadLastFpsMeterSecond = currentSecond;
            }
            else if (currentSecond != _uiThreadLastFpsMeterSecond)
            {
                // We start measuring in the middle of the first second so the result for the first second is not correct - do not show it
                if (_uiThreadIsFirstSecond)
                {
                    _uiThreadIsFirstSecond = false;
                }
                else
                {
                    string newTitle = string.Format("Main UI thread: {0} FPS; Mouse move events count: {1}", _uiThreadFramesCount, _uiThreadMouseMovesCount);
                    MainUIFpsStatsTextBlock.Text = newTitle;
                }

                _uiThreadFramesCount = 0;
                _uiThreadMouseMovesCount = 0;
                _uiThreadLastFpsMeterSecond = currentSecond;
            }


            if (_uiThreadRenderingSleep > 0)
                System.Threading.Thread.Sleep(_uiThreadRenderingSleep);
        }

        // Update the BackgroundFpsStatsTextBlock.Text
        // NOTE: This method can be also called from background thread
        public void UpdateBackgroundFpsStatsText(string statsText)
        {
            statsText = "Background thread: " + statsText;

            if (Dispatcher.CurrentDispatcher == this.Dispatcher) // If we are on the Main UI thread
            {
                this.BackgroundFpsStatsTextBlock.Text = statsText;
            }
            else
            {
                this.Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(() => this.BackgroundFpsStatsTextBlock.Text = statsText));
            }
        }



        #region UI Event handlers

        private void CreateComplexSceneButton_OnClick(object sender, RoutedEventArgs e)
        {
            if (_isShowingComplexScene)
                CreateComplexSceneButton.Content = "Create complex 3D scene";
            else
                CreateComplexSceneButton.Content = "Create simple 3D scene";

            _isShowingComplexScene = !_isShowingComplexScene;

            if (_backgroundDXEngineRenderer != null)
                _backgroundDXEngineRenderer.BackgroundRendererDispatcher.BeginInvoke(new Action(() => _backgroundDXEngineRenderer.CreateScene(_isShowingComplexScene)));
        }

        private void BGThreadCameraRotationButton_OnClick(object sender, RoutedEventArgs e)
        {
            if (_backgroundDXEngineRenderer == null)
                return;

            if (_isBackgroundThreadCameraRotating)
            {
                _backgroundDXEngineRenderer.StopCameraRotation();
                BGThreadCameraRotationButton.Content = "Start camera rotate";

                UIThreadCameraRotationButton.IsEnabled = true;
            }
            else
            {
                _backgroundDXEngineRenderer.StartCameraRotation(headingChangeInSecond: 40);
                BGThreadCameraRotationButton.Content = "Stop camera rotate";

                UIThreadCameraRotationButton.IsEnabled = false;
            }

            _isBackgroundThreadCameraRotating = !_isBackgroundThreadCameraRotating;
        }

        private void UIThreadCameraRotationButton_OnClick(object sender, RoutedEventArgs e)
        {
            if (_targetPositionCamera.IsRotating)
            {
                _targetPositionCamera.StopRotation();
                UIThreadCameraRotationButton.Content = "Start camera rotate";

                BGThreadCameraRotationButton.IsEnabled = true;
            }
            else
            {
                // When the rotation is stared we need to synchronize the camera's data
                UpdateMainUICamera();

                _targetPositionCamera.StartRotation(40, 0);
                UIThreadCameraRotationButton.Content = "Stop camera rotate";

                BGThreadCameraRotationButton.IsEnabled = false;
            }
        }

        private void RenderingTimeLimitComboBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!this.IsLoaded)
                return;

            int renderingTimeLimit;
            switch (RenderingTimeLimitComboBox.SelectedIndex)
            {
                case 1:
                    renderingTimeLimit = 33; // 33 ms (30 FPS)
                    break;

                case 2:
                    renderingTimeLimit = 16; // 16 ms (60 FPS)
                    break;

                case 3:
                    renderingTimeLimit = 11; // 11 ms (90 FPS)
                    break;

                case 0:
                default:
                    renderingTimeLimit = 0; // unlimited
                    break;
            }

            _backgroundDXEngineRenderer.RenderingTimeLimitMs = renderingTimeLimit;
        }

        private void RenderOnlyOnSceneChangeCheckBoxChanged(object sender, RoutedEventArgs e)
        {
            if (_backgroundDXEngineRenderer != null)
                _backgroundDXEngineRenderer.RenderOnlyOnSceneChange = (RenderOnlyOnSceneChangeCheckBox.IsChecked ?? false);
        }

        private void RenderingSleepCheckBoxCheckedChanged(object sender, RoutedEventArgs e)
        {
            if (_backgroundDXEngineRenderer == null)
                return;

            if (RenderingSleepCheckBox.IsChecked ?? false)
                _backgroundDXEngineRenderer.RenderingSleep = 100;
            else
                _backgroundDXEngineRenderer.RenderingSleep = 0;
        }

        private void UIThreadSleepCheckBoxCheckedChanged(object sender, RoutedEventArgs e)
        {
            if (UIThreadSleepCheckBox.IsChecked ?? false)
                _uiThreadRenderingSleep = 100;
            else
                _uiThreadRenderingSleep = 0;
        }

        #endregion
    }
}
