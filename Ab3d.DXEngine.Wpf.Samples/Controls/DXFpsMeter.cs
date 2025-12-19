using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Ab3d.DirectX.Controls;

namespace Ab3d.DXEngine.Wpf.Samples.Controls
{
    public class DXFpsMeter : TextBlock
    {
        private bool _isRunning;
        private bool _isFirstSecond;
        private int _lastFrameSecond;
        private int _wpfFramesCount;
        private TimeSpan _lastRenderingTime;
        private bool _isSceneRenderingSubscribed;

        private double _totalRenderTime;
        private int _renderedFramesCount;

        private string _wpfDisplayFormatString;

        /// <summary>
        /// Gets or sets the format string that is used to format the DXFpsMeter results when WPF 3D rendering is used. Default value is "WPF fps: {0:0}"
        /// </summary>
        public string WpfDisplayFormatString
        {
            get { return _wpfDisplayFormatString; }
            set
            {
                _wpfDisplayFormatString = value;
                DisplayFrameStatistics(0, 0);
            }
        }


        private string _dxEngineDisplayFormatString;

        /// <summary>
        /// Gets or sets the format string that is used to format the DXFpsMeter results when DirectX rendering is used. Default value is "WPF fps: {0:0} fps\r\nDX rendering time: {1:0.0}ms\r\nDX theoretical fps: {2:0}"
        /// </summary>
        public string DXEngineDisplayFormatString
        {
            get { return _dxEngineDisplayFormatString; }
            set
            {
                _dxEngineDisplayFormatString = value;
                DisplayFrameStatistics(0, 0);
            }
        }

        #region DXViewProperty
        /// <summary>
        /// RadiusProperty
        /// </summary>
        public static readonly DependencyProperty DXViewProperty =
            DependencyProperty.Register("DXView", typeof(DXView), typeof(DXFpsMeter),
                new PropertyMetadata(null, OnDXViewPropertyChanged));

        /// <summary>
        /// Gets or sets the radius of the circle 
        /// </summary>
        public DXView DXView
        {
            get { return (DXView)GetValue(DXViewProperty); }
            set { SetValue(DXViewProperty, value); }
        }

        protected static void OnDXViewPropertyChanged(DependencyObject obj, DependencyPropertyChangedEventArgs args)
        {
            var dxFpsMeter = (DXFpsMeter)obj;

            dxFpsMeter.UnsubscribeDXSceneViewRendering();
            dxFpsMeter.SubscribeDXSceneViewRendering();
        }
        #endregion

        public DXFpsMeter()
        {
            _wpfDisplayFormatString = "WPF fps: {0:0}";
            _dxEngineDisplayFormatString = "WPF fps: {0:0} fps\r\nDX rendering time: {1:0.0}ms\r\nDX theoretical fps: {2:0}";

            if (!System.ComponentModel.DesignerProperties.GetIsInDesignMode(this))
                this.IsVisibleChanged += OnIsVisibleChanged;

            //this.Loaded += (sender, args) => DisplayFrameStatistics(0, 0);
            this.Loaded += delegate (object sender, RoutedEventArgs args)
            {
                SubscribeDXSceneViewRendering(); // If not already subscribed
                DisplayFrameStatistics(0, 0);
            };
        }

        public void Reset()
        {
            _totalRenderTime = 0;
            _renderedFramesCount = 0;

            _lastRenderingTime = TimeSpan.Zero;
        }

        private void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs dependencyPropertyChangedEventArgs)
        {
            if (this.IsVisible)
            {
                if (!_isRunning)
                {
                    _isRunning = true;
                    _wpfFramesCount = -1; // Mark as the first run

                    CompositionTarget.Rendering += CompositionTarget_Rendering;
                }
            }
            else
            {
                if (_isRunning)
                {
                    CompositionTarget.Rendering -= CompositionTarget_Rendering;
                    _isRunning = false;
                }
            }
        }

        private void UnsubscribeDXSceneViewRendering()
        {
            var dxView = DXView; // Local accessor (accessing DepandencyProperties is slow so this should be minimized)

            if (dxView != null && _isSceneRenderingSubscribed)
            {
                dxView.SceneRendered -= DXViewOnRendered;
                _isSceneRenderingSubscribed = false;
            }
        }

        private void SubscribeDXSceneViewRendering()
        {
            var dxView = DXView; // Local accessor (accessing DepandencyProperties is slow so this should be minimized)

            if (dxView != null && !_isSceneRenderingSubscribed)
            {
                Ab3d.DirectX.DXDiagnostics.IsCollectingStatistics = true; // Ensure that we are collecting statistics (Rendering time, etc.)

                dxView.SceneRendered += DXViewOnRendered;
                _isSceneRenderingSubscribed = true;
            }
        }

        private void DXViewOnRendered(object sender, EventArgs eventArgs)
        {
            var dxView = DXView; // Local accessor (accessing DepandencyProperties is slow so this should be minimized)

            if (dxView == null || dxView.DXScene == null || dxView.DXScene.Statistics == null)
                return;

            _totalRenderTime += dxView.DXScene.Statistics.TotalRenderTimeMs;
            _renderedFramesCount++;
        }

        private void CompositionTarget_Rendering(object sender, EventArgs e)
        {
            // Check if the RenderingTime was actually changed (meaning we have new frame).
            // If RenderingTime was not changed then Rendering was called multiple times in one frame - we do not want to measure
            // Based on http://stackoverflow.com/questions/5812384/why-is-frame-rate-in-wpf-irregular-and-not-limited-to-monitor-refresh

            var renderingEventArgs = e as System.Windows.Media.RenderingEventArgs;
            if (renderingEventArgs != null)
            {
                if (renderingEventArgs.RenderingTime == _lastRenderingTime)
                    return; // Still on the same frame

                _lastRenderingTime = renderingEventArgs.RenderingTime;
            }

            int currentSecond = DateTime.Now.Second;

            if (!this.IsEnabled)
                return;

            if (_wpfFramesCount == -1) // if first run
            {
                // Start with _isFirstSecond = true, because we did not start counting frames at the begging of the frame
                _lastFrameSecond = currentSecond;
                _wpfFramesCount = 1;
                _isFirstSecond = true;
            }
            else
            {
                if (currentSecond == _lastFrameSecond)
                {
                    _wpfFramesCount++;
                }
                else
                {
                    if (_isFirstSecond)
                    {
                        // Do not take the first second into account, because we started checking in the middle of the second
                        _isFirstSecond = false;
                    }
                    else
                    {
                        double averageRenderTime;

                        if (_renderedFramesCount > 0)
                        {
                            averageRenderTime = _totalRenderTime / _renderedFramesCount;

                            _totalRenderTime = 0;
                            _renderedFramesCount = 0;
                        }
                        else
                        {
                            averageRenderTime = 0;
                        }

                        DisplayFrameStatistics(_wpfFramesCount, averageRenderTime);
                    }

                    _wpfFramesCount = 1;
                    _lastFrameSecond = currentSecond;
                }
            }
        }

        private void DisplayFrameStatistics(int wpfFrameCount, double dxAverageRenderTime)
        {
            string infoText;

            if (System.ComponentModel.DesignerProperties.GetIsInDesignMode(this))
            {
                this.Text = "DXFpsMeter";
                return;
            }

            var dxView = DXView; // Local accessor (accessing DepandencyProperties is slow so this should be minimized)

            if (dxView != null && dxView.DXScene != null)
            {
                // DXEngine rendering
                double dxFps;
                if (dxAverageRenderTime > 0)
                    dxFps = 1000 / dxAverageRenderTime;
                else
                    dxFps = 0;

                infoText = string.Format(_dxEngineDisplayFormatString, wpfFrameCount, dxAverageRenderTime, dxFps);

                //if (_dxSceneView.PresentationType == DXView.PresentationTypes.DirectXImage)
                //    infoText += "\r\n(not accurate with PresentationType\r\nset to DirectXImage.\r\nUse DirectXOverlay instead)";
            }
            else
            {
                // WPF rendering
                infoText = string.Format(_wpfDisplayFormatString, wpfFrameCount);
            }

            this.Text = infoText;
        }
    }
}