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
using Ab3d.DirectX.Controls;

namespace Ab3d.DirectX.Client.Diagnostics
{
    /// <summary>
    /// Interaction logic for DeviceInfoUserControl.xaml
    /// </summary>
    public partial class DeviceInfoUserControl : UserControl
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
                    _dxView.Disposing -= DXSceneViewBaseOnDisposing;


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
                    _dxView.Disposing += DXSceneViewBaseOnDisposing;
            }
        }

        public DeviceInfoUserControl()
        {
            InitializeComponent();

            this.Unloaded += (sender, args) => UnsubscribeDXSceneInitialized();
        }

        private void Update()
        {
            if (_dxView == null || (_dxView.UsedGraphicsProfile == null && _dxView.DXScene == null)) // It is possible that UsedGraphicsProfile is null, but DXScene is not null when user manually created DXScene
            {
                AdapterInfoTextBlock.Text = "DXView is not initialized";

                if (_dxView != null && _dxView.UsedGraphicsProfile == null)
                    SubscribeDXSceneInitialized();

                return;
            }


            string adapterInfo;

            if (_dxView.UsedGraphicsProfile == null && _dxView.DXScene != null)
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

            AdapterInfoTextBlock.Text = adapterInfo;
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
                _isDXSceneInitializedSubscribed =  true;
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

        private void DXSceneViewBaseOnDisposing(object sender, EventArgs eventArgs)
        {
            AdapterInfoTextBlock.Text = "DXSceneView is disposed";
        }
    }
}
