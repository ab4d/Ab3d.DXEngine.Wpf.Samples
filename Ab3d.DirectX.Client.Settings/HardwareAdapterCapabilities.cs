using System;
using System.Collections.Generic;
using System.Linq;
using SharpDX.Direct3D;
using SharpDX.DXGI;

namespace Ab3d.DirectX.Client.Settings
{
    /// <summary>
    /// HardwareAdapterCapabilities class describes the capabilities of the specified hardware adapter.
    /// Hardware adapter represent a real graphics card that is installed on the system.
    /// </summary>
    public class HardwareAdapterCapabilities : AdapterCapabilitiesBase
    {
        /// <summary>
        /// Initializes a new instance of the HardwareAdapterCapabilities class with the specifed Adapter.
        /// </summary>
        /// <param name="adapter">DirectX Adapter</param>
        public HardwareAdapterCapabilities(Adapter1 adapter)
            : base(adapter: adapter)
        {
            string adapterDescription = AdapterDescription1.Description.Trim();

            int pos = adapterDescription.IndexOf('\0'); // UH: In SharpDX 3.1, some adapters report description with many zero characters
            if (pos >= 0)
                adapterDescription = adapterDescription.Substring(0, pos);

            DisplayName = adapterDescription;

            try
            {
                DeviceCapabilities = DeviceCapabilities.QueryAdapter(adapter);

                long videoMemory = (long)AdapterDescription1.DedicatedVideoMemory;

                string videoMemoryText;
                if (videoMemory > (1024 * 1024 * 1024))
                    videoMemoryText = string.Format("{0:0.#} GB", videoMemory / (1024 * 1024 * 1024.0));
                else
                    videoMemoryText = string.Format("{0:0} MB", videoMemory / (1024 * 1024));

                DeviceInfoText = string.Format("Feature level: {0}; Video memory: {1}",
                    GetFeatureLevelText(DeviceCapabilities.FeatureLevel),
                    videoMemoryText);


                IsSupported = this.DeviceCapabilities.FeatureLevel >= FeatureLevel.Level_10_0;

                if (!IsSupported)
                    UnsupportedReason = "Adapter not supported because it does not supported feature level 10.0 or higher";
            }
            catch (Exception ex)
            {
                IsSupported = false;
                UnsupportedReason = "Error checking device capabilities:\r\n" + ex.Message;
            }
        }

        /// <summary>
        /// GetGraphicsProfileForQuality returns the GraphicsProfile for hardware adapter type and with the specified render quality.
        /// </summary>
        /// <param name="quality">RenderQualityTypes</param>
        /// <returns>GraphicsProfile for hardware adapter type and with the specified render quality</returns>
        public override GraphicsProfile GetGraphicsProfileForQuality(RenderQualityTypes quality)
        {
            GraphicsProfile graphicsProfile;

            switch (quality)
            {
                case RenderQualityTypes.Low:
                    graphicsProfile = GraphicsProfile.LowQualityHardwareRendering;
                    break;
                case RenderQualityTypes.Normal:
                    graphicsProfile = GraphicsProfile.NormalQualityHardwareRendering;
                    break;
                case RenderQualityTypes.High:
                    graphicsProfile = GraphicsProfile.HighQualityHardwareRendering;
                    break;
                case RenderQualityTypes.Ultra:
                    graphicsProfile = GraphicsProfile.UltraQualityHardwareRendering;
                    break;
                case RenderQualityTypes.Custom:
                default:
                    throw new ArgumentOutOfRangeException("quality");
            }

            return new GraphicsProfile(graphicsProfile, this.Adapter); // Create a copy of GraphicsProfile and specify this Adapter to be used
        }

        /// <summary>
        /// GetCustomGraphicsProfile returns a custom quality GraphicsProfile with settings provided as method parameters.
        /// </summary>
        /// <param name="multisampleCount">multisampleCount</param>
        /// <param name="superSamplingCount">superSamplingCount</param>
        /// <param name="shaderQuality">shaderQuality</param>
        /// <param name="textureFiltering">textureFiltering</param>
        /// <returns>GraphicsProfile</returns>
        public override GraphicsProfile GetCustomGraphicsProfile(int multisampleCount, int superSamplingCount, ShaderQuality shaderQuality, TextureFilteringTypes textureFiltering)
        {
            return new GraphicsProfile("CustomHardwareRendering", GraphicsProfile.DriverTypes.DirectXHardware, shaderQuality, multisampleCount, superSamplingCount, textureFiltering, this.Adapter);
        }


        private string GetFeatureLevelText(FeatureLevel featureLevel)
        {
            string featureLevelText;

            switch (featureLevel)
            {
                case FeatureLevel.Level_9_1:
                    featureLevelText = "9.1";
                    break;

                case FeatureLevel.Level_9_2:
                    featureLevelText = "9.2";
                    break;

                case FeatureLevel.Level_9_3:
                    featureLevelText = "9.3";
                    break;

                case FeatureLevel.Level_10_0:
                    featureLevelText = "10.0";
                    break;

                case FeatureLevel.Level_10_1:
                    featureLevelText = "10.1";
                    break;

                case FeatureLevel.Level_11_0:
                    featureLevelText = "11.0";
                    break;

                default:
                    if ((int)featureLevel > (int)FeatureLevel.Level_11_0)
                        featureLevelText = "11.0+";
                    else
                        featureLevelText = "unknown";

                    break;
            }

            return featureLevelText;
        }
    }
}