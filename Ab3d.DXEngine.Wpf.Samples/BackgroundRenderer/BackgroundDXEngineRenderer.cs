using System;
using System.Diagnostics;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using System.Windows.Threading;
using Ab3d.Cameras;
using Ab3d.Common.Cameras;
using Ab3d.DirectX;
using Ab3d.DirectX.Controls;
using Ab3d.Visuals;
using SharpDX;
using SharpDX.Direct3D;

namespace Ab3d.DXEngine.Wpf.Samples.BackgroundRenderer
{
    public class BackgroundDXEngineRenderer : IDisposable
    {
        private IntPtr _hWnd;

        private DXDevice _dxDevice;

        private object _lockHelper = new object();

        private int _newClientWindowWidth, _newClientWindowHeight;
        private bool _isClientWindowSizeDirty;

        private bool _abortBackgroundRendering;

        private Delegate _renderSceneInternalDelegate;



        protected DXViewportView dxViewportView;

        protected DXScene dxScene;

        protected Viewport3D wpfViewport3D;

        protected bool isSceneDirty; // if true, then there were some changes in the scene / camea and we need to render the scene


        /// <summary>
        /// Gets the Dispatcher that is used to "send work" to this BackgroundDXEngineRenderer
        /// </summary>
        public Dispatcher BackgroundRendererDispatcher { get; private set; }


        private volatile int _renderingTimeLimitMs = 0; // to get 60 FPS (approximately) we can set that to 16; set that to 0 to render as fast as possible

        /// <summary>
        /// Gets or sets an integer that specifies the target rendering time.
        /// If rendering is done faster, then the rendering loop will wait for the remaining time to achive the RenderingTimeLimitMs.
        /// Specifying 0 for RenderingTimeLimitMs will render as fast as possible and without any waiting. 
        /// But this will use 100% of one CPU core even when waiting for changes (so this is not recommended on laptop when it is running on batteries).
        /// Default value is 0 to have unlimited rendering time.
        /// </summary>
        public int RenderingTimeLimitMs
        {
            get { return _renderingTimeLimitMs; }
            set
            {
                _renderingTimeLimitMs = value;
                isSceneDirty = true;
            }
        }


        private volatile bool _renderOnlyOnSceneChange = false; // if false then we render all time - as many frames as possible

        /// <summary>
        /// Gets or sets a Boolean that specifies if scene is rendered on each rendering loop pass (when false - by default) or only when there are some changes (when true).
        /// </summary>
        public bool RenderOnlyOnSceneChange
        {
            get { return _renderOnlyOnSceneChange; }
            set
            {
                _renderOnlyOnSceneChange = value;
                isSceneDirty = true;
            }
        }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="hWnd">handle to the window (area of the screen) that will be used to render the 3D scene to - can be created with D3DHost control.</param>
        public BackgroundDXEngineRenderer(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero)
                throw new ArithmeticException("hWnd cannot be IntPtr.Zero");

            _hWnd = hWnd;

            BackgroundRendererDispatcher = Dispatcher.CurrentDispatcher;
        }

        /// <summary>
        /// CreateDXViewportView 
        /// </summary>
        /// <param name="clientWindowWidth">clientWindowWidth</param>
        /// <param name="clientWindowHeight">clientWindowHeight</param>
        /// <param name="dpiScaleX">DPI scale: 1 means no scale (96 DPI)</param>
        /// <param name="dpiScaleY">DPI scale: 1 means no scale (96 DPI)</param>
        /// <param name="preferedMultisamplingCount">preferedMultisamplingCount</param>
        public void InitializeDXViewportView(int clientWindowWidth,
                                             int clientWindowHeight,
                                             double dpiScaleX,
                                             double dpiScaleY,
                                             int preferedMultisamplingCount)
        {
            // To render the 3D scene to the custom hWnd, we need to create the DXViewportView with a custom DXScene,
            // that was initialized with calling InitializeSwapChain mathod (with passed hWnd).

            // To create a custom DXScene we first need to create a DXDevice objects (wrapper around DirectX Device object)
            var dxDeviceConfiguration = new DXDeviceConfiguration();
            dxDeviceConfiguration.DriverType = DriverType.Hardware; // We could also specify Software rendering here

            try
            {
                _dxDevice = new DXDevice(dxDeviceConfiguration);
                _dxDevice.InitializeDevice();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Cannot create required DirectX device.\r\n" + ex.Message);
                return;
            }

            if (_dxDevice.Device == null)
            {
                MessageBox.Show("Cannot create required DirectX device.");
                return;
            }


            // Now we can create the DXScene
            dxScene = new Ab3d.DirectX.DXScene(_dxDevice);

            // ensure we have a valid size; we will resize later to the correct size
            if (clientWindowWidth <= 0) clientWindowWidth = 1;
            if (clientWindowHeight <= 0) clientWindowHeight = 1;

            dxScene.InitializeSwapChain(_hWnd,
                                        (int) (clientWindowWidth * dpiScaleX),
                                        (int) (clientWindowHeight * dpiScaleY),
                                        preferedMultisamplingCount,
                                        (float) dpiScaleX,
                                        (float) dpiScaleY);


            wpfViewport3D = new Viewport3D();

            dxViewportView = new DXViewportView(dxScene, wpfViewport3D);


            // Because _dxViewportView is not shown in the UI, the DXEngineShoop (DXEngine's diagnostics tool) cannot find it
            // To enable using DXEngineSnoop in such cases, we can set the Application's Property:
            Application.Current.Properties["DXView"] = new WeakReference(dxViewportView);

            OnDXViewportViewInitialized();
        }

        /// <summary>
        /// StartMessageLoop starts message loop that handles Dispatcher invokations, BackgroundUpdate and renders the frames.
        /// NOTE: This method will return after the message loop is stopped - after the AbortBackgroundRendering is called.
        /// </summary>
        public virtual void StartMessageLoop()
        {
            var stopwatch = new Stopwatch();

            while (true) // infinite loop - break is called when _abortBackgroundRendering is set to true
            {
                stopwatch.Reset();
                stopwatch.Start();

                // First process all the Dispatcher calls that could be added from the Main UI thread

                // Processes all pending messages down to the specified priority.
                // This method returns after all messages have been processed.
                // From: http://stackoverflow.com/questions/25442018/doevents-dispatcher-invoke-vs-pushframe
                Dispatcher.CurrentDispatcher.Invoke(new Action(delegate { }), DispatcherPriority.ContextIdle); // ContextIdle is one less then Background

                if (_abortBackgroundRendering) // Abort?
                    break;

                // Now do the internal Update that is called on every frame - for example update the camera if it is rotated
                bool isSceneChanged = BackgroundUpdate();

                if (_isClientWindowSizeDirty)
                {
                    dxViewportView.DXScene.Resize(_newClientWindowWidth, _newClientWindowHeight);
                    _isClientWindowSizeDirty = false;
                    isSceneDirty = true;
                }

                // Check if we need to render the scene
                if (isSceneChanged || isSceneDirty || !_renderOnlyOnSceneChange)
                    RenderSceneInternal();

                if (_abortBackgroundRendering) // Abort?
                    break;


                stopwatch.Stop();
                long elapsedMilliseconds = stopwatch.ElapsedMilliseconds;

                

                if (_renderingTimeLimitMs == 0)
                {
                    Thread.Yield();
                }
                else
                {
                    int sleepTime = _renderingTimeLimitMs - (int)elapsedMilliseconds - 1; // Sleep 1 ms less than calculated - this gives us slightly higher FPS that expected, but this is better then having lower FPS then expected

                    if (sleepTime > 0)
                        Thread.Sleep(sleepTime);
                }

                if (_abortBackgroundRendering) // Abort?
                    break;
            }
        }
        
        public virtual void RenderScene()
        {
            // Create only one delegate for Invoke call
            if (_renderSceneInternalDelegate == null)
            {
                _renderSceneInternalDelegate = new Action(() =>
                {
                    RenderSceneInternal();
                });
            }

            BackgroundRendererDispatcher.Invoke(DispatcherPriority.Normal, _renderSceneInternalDelegate);
        }

        public void Resize(int newWidth, int newHeight)
        {
            // Do not resize the scene immediatly, but save the new size and then resize before rendering the new frame
            lock (_lockHelper)
            {
                _newClientWindowWidth = newWidth;
                _newClientWindowHeight = newHeight;
                _isClientWindowSizeDirty = true;
            }
        }

        private void RenderSceneInternal()
        {
            dxViewportView.Refresh();
            isSceneDirty = false;
        }

        // Returns true if rendering is needed because of the BackgroundUpdate
        protected virtual bool BackgroundUpdate()
        {
            return false;
        }

        protected virtual void OnDXViewportViewInitialized()
        {
        }

        public void Dispose()
        {
            if (BackgroundRendererDispatcher == Dispatcher.CurrentDispatcher)
            {
                Dispose(true);
            }
            else
            {
                BackgroundRendererDispatcher.BeginInvoke(new Action(() =>
                {
                    _abortBackgroundRendering = true;
                    Dispose(true);
                }));
            }
        }

        // This must be called on the same thread as the objects were created on
        protected virtual void Dispose(bool disposing)
        {
            if (dxViewportView != null)
            {
                dxViewportView.Dispose();
                dxViewportView = null;
            }

            if (dxScene != null)
            {
                dxScene.Dispose();
                dxScene = null;
            }

            if (_dxDevice != null)
            {
                _dxDevice.Dispose();
                _dxDevice = null;
            }

            wpfViewport3D = null;
        }



        #region public static helper methods

        public static void GetActualDpiScaleValues(Visual visual, out double dpiScaleX, out double dpiScaleY)
        {
            // We can get the system DPI scale from 
            // PresentationSource.FromVisual(this).CompositionTarget.TransformToDevice.M11 and M22
            var presentationSource = PresentationSource.FromVisual(visual);

            if (presentationSource != null && presentationSource.CompositionTarget != null)
            {
                dpiScaleX = presentationSource.CompositionTarget.TransformToDevice.M11;
                dpiScaleY = presentationSource.CompositionTarget.TransformToDevice.M22;
            }
            else
            {
                dpiScaleX = 1;
                dpiScaleY = 1;
            }
        }

        #endregion
    }
}