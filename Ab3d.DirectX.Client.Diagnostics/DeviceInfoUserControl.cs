using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Ab3d.DirectX.Common;
using Ab3d.DirectX.Controls;

namespace Ab3d.DirectX.Client.Diagnostics
{
    /// <summary>
    /// Interaction logic for DeviceInfoUserControl.xaml
    /// </summary>
    public class DeviceInfoUserControl : TextBlock
    {
        private bool _isDXSceneInitializedSubscribed;

        private DXView _dxView;

        public DXView DXView
        {
            get { return _dxView; }
            set
            {
                if (ReferenceEquals(_dxView, value))
                    return;

                if (_dxView != null)
                {
                    _dxView.Disposing -= DXViewOnDisposing;
                    _dxView.DXRenderSizeChanged -= DxViewOnDXRenderSizeChanged;
                }

                _dxView = value;

                try
                {
                    Update();
                }
                catch
                {
                    // This can happen in case of DXEngineSnoop that is started with wrong SharpDX reference  
                }

                if (_dxView != null)
                {
                    _dxView.Disposing += DXViewOnDisposing;
                    _dxView.DXRenderSizeChanged += DxViewOnDXRenderSizeChanged;
                }
            }
        }

        public bool ShowViewSize { get; set; }
        public bool ShowAntialiasinigSettings { get; set; }
        public bool ShowViewMemoryUsage { get; set; }

        public DeviceInfoUserControl()
        {
            ShowViewSize              = true;
            ShowAntialiasinigSettings = true;
            ShowViewMemoryUsage       = true;

            this.Unloaded += (sender, args) => UnsubscribeDXSceneInitialized();
        }

        private void Update()
        {
            if (_dxView == null || (_dxView.UsedGraphicsProfile == null && _dxView.DXScene == null)) // It is possible that UsedGraphicsProfile is null, but DXScene is not null when user manually created DXScene
            {
                this.Text = "DXView is not initialized";

                if (_dxView != null && _dxView.UsedGraphicsProfile == null)
                    SubscribeDXSceneInitialized();

                return;
            }


            string adapterInfo;

            var dxScene = _dxView.DXScene;

            if (_dxView.UsedGraphicsProfile == null && dxScene != null)
            {
                try
                {
                    // In case of invalid SharpDX reference (for example when used in DXEngineSnoop) or some other problem, the call GetHardwareAdapterInfo method may fail.
                    adapterInfo = GetHardwareAdapterInfo();
                }
                catch (Exception ex)
                {
                    adapterInfo = "Error getting adapter info:\r\n" + ex.Message;
                }
            }
            else if (_dxView.UsedGraphicsProfile != null)
            {
                switch (_dxView.UsedGraphicsProfile.DriverType)
                {
                    case GraphicsProfile.DriverTypes.DirectXHardware:
                        try
                        {
                            // In case of invalid SharpDX reference (for example when used in DXEngineSnoop) or some other problem, the call GetHardwareAdapterInfo method may fail.
                            adapterInfo = GetHardwareAdapterInfo();
                        }
                        catch (Exception ex)
                        {
                            adapterInfo = _dxView.UsedGraphicsProfile.Name + "\r\nError getting adapter info:\r\n" + ex.Message;
                        }

                        break;
                    case GraphicsProfile.DriverTypes.DirectXSoftware:
                        adapterInfo = "Software rendering (WARP)";
                        break;

                    case GraphicsProfile.DriverTypes.Wpf3D:
                        adapterInfo = "WPF 3D";
                        break;

                    default:
                        adapterInfo = _dxView.UsedGraphicsProfile.Name;
                        break;
                }
            }
            else
            {
                adapterInfo = "";
            }

            string viewInfo;
            if (dxScene != null)
            {
                int width = dxScene.BackBufferDescription.Width;
                int height = dxScene.BackBufferDescription.Height;

                if (ShowViewSize)
                    viewInfo = string.Format("{0} x {1}", width, height);
                else
                    viewInfo = "";

                var multisampleCount    = dxScene.MultisampleCount;
                var supersamplingCount  = dxScene.SupersamplingCount;  // number of pixels used for one final pixel

                if (ShowAntialiasinigSettings)
                {
                    if (multisampleCount > 1)
                        viewInfo += string.Format(" x {0}xMSAA", multisampleCount);
                    
                    if (supersamplingCount > 1)
                        viewInfo += string.Format(" x {0}xSSAA", supersamplingCount);
                }

                if (ShowViewMemoryUsage)
                {
                    // Get memory size of the finally shown texture
                    long finalBackBufferSize = (long)(4.0 * (double)dxScene.Width * (double)dxScene.Height / supersamplingCount); // 4 bytes for RGBA format

                    // we start with the same size for depth buffer - this can change in case of MSAA or SSAA
                    long depthBufferSize = finalBackBufferSize;                                                        

                    long msaaBackBufferSize, ssaaBackBufferSize;

                    if (supersamplingCount > 1)
                    {
                        // when using SSAA we render to this buffer (or to MSAA is used - but this is then down-sampled to ssaa buffer)
                        ssaaBackBufferSize = finalBackBufferSize * supersamplingCount;
                        depthBufferSize    = ssaaBackBufferSize;
                    }
                    else
                    {
                        ssaaBackBufferSize = 0;
                    }

                    if (multisampleCount > 1)
                    {
                        // when using MSAA we render to this buffer that is then resolved into SSAA (if used) or to finalBackBufferSize
                        msaaBackBufferSize = finalBackBufferSize * multisampleCount * supersamplingCount;
                        depthBufferSize    = msaaBackBufferSize;
                    }
                    else
                    {
                        msaaBackBufferSize = 0;
                    }

                    long totalMemoryUsage = ssaaBackBufferSize + msaaBackBufferSize + finalBackBufferSize + depthBufferSize;

                    if (totalMemoryUsage > 1024 * 1024)
                        viewInfo += string.Format(System.Globalization.CultureInfo.InvariantCulture, " = {0:#,##0.0} Mb", (double)totalMemoryUsage / (1024.0 * 1024.0));
                    else
                        viewInfo += string.Format(System.Globalization.CultureInfo.InvariantCulture, " = {0:#,##0.0} Kb", (double)totalMemoryUsage / 1024.0);
                }
            }
            else
            {
                viewInfo = "";
            }

            this.Text = adapterInfo;

            if (viewInfo.Length > 0)
                this.Text += Environment.NewLine + viewInfo;
        }

        [MethodImpl(MethodImplOptions.NoInlining)] // No inlining so case of invalid SharpDX reference, the caller method will still be called
        private string GetHardwareAdapterInfo()
        {
            string adapterInfo;

            if (_dxView.DXScene != null && _dxView.DXScene.DXDevice != null && _dxView.DXScene.DXDevice.Adapter != null)
            {
                adapterInfo = _dxView.DXScene.DXDevice.Adapter.Description1.Description;

                if (adapterInfo != null)
                {
                    adapterInfo = adapterInfo.Trim();

                    int pos = adapterInfo.IndexOf('\0'); // UH: In SharpDX 3.1, some adapters report description with many zero characters
                    if (pos >= 0)
                        adapterInfo = adapterInfo.Substring(0, pos);

                    // Add feature level:
                    string featureLevelText = _dxView.DXScene.DXDevice.Device.FeatureLevel.ToString();
                    featureLevelText = featureLevelText.Replace("Level_", "").Replace('_', '.');

                    adapterInfo += " - DX" + featureLevelText;

                    if (_dxView.DXScene.DXDevice.IsDebugDevice)
                        adapterInfo += " (debug layer)";
                }
                else
                {
                    adapterInfo = "";
                }
            }
            else
            {
                adapterInfo = "";
            }

            return adapterInfo;
        }

        private void DXSceneViewBaseOnDXSceneInitialized(object sender, EventArgs eventArgs)
        {
            UnsubscribeDXSceneInitialized();

            Update();
        }

        private void SubscribeDXSceneInitialized()
        {
            if (_isDXSceneInitializedSubscribed)
                return;

            if (_dxView != null)
            {
                _dxView.DXSceneInitialized += DXSceneViewBaseOnDXSceneInitialized;
                _isDXSceneInitializedSubscribed = true;
            }
        }

        private void UnsubscribeDXSceneInitialized()
        {
            if (!_isDXSceneInitializedSubscribed)
                return;

            if (_dxView != null)
                _dxView.DXSceneInitialized -= DXSceneViewBaseOnDXSceneInitialized;

            _isDXSceneInitializedSubscribed = false;
        }

        private void DXViewOnDisposing(object sender, EventArgs eventArgs)
        {
            try
            {
                this.Text = "DXSceneView is disposed";
            }
            catch (InvalidOperationException)
            {
                // In case DXView was disposed in background thread
            }
        }

        private void DxViewOnDXRenderSizeChanged(object sender, DXViewSizeChangedEventArgs e)
        {
            Update();
        }
    }
}
