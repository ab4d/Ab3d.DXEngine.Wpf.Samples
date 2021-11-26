using System;
using System.Collections.Generic;
using System.Linq;
using SharpDX.Direct3D;
using SharpDX.Direct3D11;

namespace Ab3d.DirectX.Client.Settings
{
    /// <summary>
    /// SoftwareAdapterCapabilities class describes the capabilities of the DirectX 11 software rendering.
    /// </summary>
    public class SoftwareAdapterCapabilities : AdapterCapabilitiesBase
    {
        /// <summary>
        /// Initializes a new instance of the SoftwareAdapterCapabilities class.
        /// </summary>
        public SoftwareAdapterCapabilities()
            : base(adapter: null)
        {
            DisplayName = "Software rendering (DirectX WARP)";

            IsSupported = true;

            // Software device capabilities are the same on all the systems
            this.DeviceCapabilities = DeviceCapabilities.SoftwareDeviceCapabilities;

            // This means that we can speed-up things and do not create a software device just to get DeviceCapabilities (commented code below)

            // SharpDX.Direct3D11.Device softwareDevice;
            //
            //try
            //{
            //    softwareDevice = new SharpDX.Direct3D11.Device(DriverType.Warp, DeviceCreationFlags.None, DeviceCapabilities.AllFeatureLevels);
            //}
            //catch (Exception ex)
            //{
            //    softwareDevice = null;
            //    IsSupported = false;
            //    UnsupportedReason = "Error creating software device:\r\n" + ex.Message;
            //}

            //if (softwareDevice != null)
            //{
            //    try
            //    {
            //        this.DeviceCapabilities = DeviceCapabilities.QueryDevice(softwareDevice);
            //    }
            //    catch (Exception ex)
            //    {
            //        IsSupported = false;
            //        UnsupportedReason = "Error checking software device capabilities:\r\n" + ex.Message;
            //    }

            //    softwareDevice.Dispose();
            //}
        }

        /// <summary>
        /// GetGraphicsProfileForQuality returns the GraphicsProfile for software adapter type and with the specified render quality.
        /// </summary>
        /// <param name="quality">RenderQualityTypes</param>
        /// <returns>GraphicsProfile for software adapter type and with the specified render quality</returns>
        public override GraphicsProfile GetGraphicsProfileForQuality(RenderQualityTypes quality)
        {
            switch (quality)
            {
                case RenderQualityTypes.Low:
                    return GraphicsProfile.LowQualitySoftwareRendering;
                    
                case RenderQualityTypes.Normal:
                    return GraphicsProfile.NormalQualitySoftwareRendering;
                    
                case RenderQualityTypes.High:
                case RenderQualityTypes.Ultra:
                    return GraphicsProfile.HighQualitySoftwareRendering;

                case RenderQualityTypes.Custom:
                default:
                    throw new ArgumentOutOfRangeException("quality");
            }
        }

        /// <summary>
        /// GetCustomGraphicsProfile returns a custom quality GraphicsProfile with settings provided as method parameters.
        /// </summary>
        /// <param name="multisampleCount">multisampleCount</param>
        /// <param name="superSamplingCount"></param>
        /// <param name="shaderQuality">shaderQuality</param>
        /// <param name="textureFiltering">textureFiltering</param>
        /// <returns>GraphicsProfile</returns>
        public override GraphicsProfile GetCustomGraphicsProfile(int multisampleCount, int superSamplingCount, ShaderQuality shaderQuality, TextureFilteringTypes textureFiltering)
        {
            return new GraphicsProfile("CustomSoftwareRendering", GraphicsProfile.DriverTypes.DirectXSoftware, shaderQuality, multisampleCount, superSamplingCount, textureFiltering);
        }
    }
}