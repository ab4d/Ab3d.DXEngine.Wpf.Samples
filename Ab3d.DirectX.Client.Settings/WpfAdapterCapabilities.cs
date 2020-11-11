using System;
using System.Collections.Generic;
using System.Linq;

namespace Ab3d.DirectX.Client.Settings
{
    /// <summary>
    /// WpfAdapterCapabilities class describes the capabilities of the WPF 3D rendering.
    /// </summary>
    public class WpfAdapterCapabilities : AdapterCapabilitiesBase
    {
        /// <summary>
        /// Initializes a new instance of the WpfAdapterCapabilities class.
        /// </summary>
        public WpfAdapterCapabilities()
            : base(adapter: null)
        {
            DisplayName = "WPF 3D";
            IsSupported = true;
        }

        /// <summary>
        /// GetGraphicsProfileForQuality in WpfAdapterCapabilities always returns the Wpf3D GraphicsProfile.
        /// </summary>
        /// <param name="quality">RenderQualityTypes</param>
        /// <returns>Wpf3D GraphicsProfile</returns>
        public override GraphicsProfile GetGraphicsProfileForQuality(RenderQualityTypes quality)
        {
            return GraphicsProfile.Wpf3D;
        }

        /// <summary>
        /// GetCustomGraphicsProfile in WpfAdapterCapabilities always returns the Wpf3D GraphicsProfile.
        /// </summary>
        /// <param name="multisampleCount">multisampleCount</param>
        /// <param name="superSamplingCount"></param>
        /// <param name="shaderQuality">shaderQuality</param>
        /// <param name="textureFiltering">textureFiltering</param>
        /// <returns>Wpf3D GraphicsProfile</returns>
        public override GraphicsProfile GetCustomGraphicsProfile(int multisampleCount, int superSamplingCount, ShaderQuality shaderQuality, TextureFilteringTypes textureFiltering)
        {
            return GraphicsProfile.Wpf3D;
        }
    }
}