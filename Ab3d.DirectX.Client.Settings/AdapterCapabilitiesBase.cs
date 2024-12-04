using System;
using System.Collections.Generic;
using System.Linq;
using SharpDX.DXGI;

namespace Ab3d.DirectX.Client.Settings
{
    /// <summary>
    /// AdapterCapabilitiesBase is a base class for HardwareAdapterCapabilities, SoftwareAdapterCapabilities or WpfAdapterCapabilities.
    /// The class describes the capabilities of the specified adapter.
    /// </summary>
    public abstract class AdapterCapabilitiesBase : IDisposable
    {
        /// <summary>
        /// RenderQualityTypes defines the possible render quality values that are used to define GraphicsProfile settings.
        /// </summary>
        public enum RenderQualityTypes
        {
            /// <summary>
            /// Low quality
            /// </summary>
            Low,

            /// <summary>
            /// Normal quality
            /// </summary>
            Normal,
            
            /// <summary>
            /// Optimized High quality
            /// </summary>
            OptimizedHigh,

            /// <summary>
            /// High quality
            /// </summary>
            High,

            /// <summary>
            /// Ultra quality
            /// </summary>
            Ultra,

            /// <summary>
            /// Custom quality where used manually changed some settings so that the GraphicsProfile does not match any of Low, Normal or High quality settings.
            /// </summary>
            Custom
        }

        /// <summary>
        /// Gets the nicely formated name of this AdapterCapabilities class.
        /// </summary>
        public string DisplayName { get; protected set; }

        /// <summary>
        /// Gets the specific Adapter that is used for this AdapterCapabilities class.
        /// </summary>
        public Adapter1 Adapter { get; private set; }

        /// <summary>
        /// Get cached AdapterDescription1 struct (provided here so that it is not needed to call the native GetDescription call)
        /// </summary>
        public AdapterDescription1 AdapterDescription1 { get; private set; }

        /// <summary>
        /// Gets DeviceCapabilities
        /// </summary>
        public DeviceCapabilities DeviceCapabilities { get; protected set; }

        /// <summary>
        /// DeviceInfoText is used for hardware graphics card and provides a simple string that describes the capabilities of the graphic card.
        /// </summary>
        public string DeviceInfoText { get; protected set; }

        /// <summary>
        /// Gets a Boolean that specifies if this Adapter can be used for DXEngine - if it supports the required FeatureLevel.
        /// </summary>
        public bool IsSupported { get; protected set; }

        /// <summary>
        /// Gets a string is set when this Adapter is not supported by DXEngine and describes the reason why the Adapter is not supported.
        /// </summary>
        public string UnsupportedReason { get; protected set; }

        /// <summary>
        /// Initializes a new instance of the AdapterCapabilitiesBase class.
        /// </summary>
        protected AdapterCapabilitiesBase(Adapter1 adapter)
        {
            Adapter = adapter;

            if (adapter != null)
                AdapterDescription1 = adapter.Description1; // Cache the Description1 struct
        }

        /// <summary>
        /// GetGraphicsProfileForQuality returns the GraphicsProfile for this type of AdapterCapabilitiesBase and with the specified render quality.
        /// </summary>
        /// <param name="quality">RenderQualityTypes</param>
        /// <returns>GraphicsProfile for this type of AdapterCapabilitiesBase and with the specified render quality</returns>
        public abstract GraphicsProfile GetGraphicsProfileForQuality(RenderQualityTypes quality);

        /// <summary>
        /// GetCustomGraphicsProfile returns a custom quality GraphicsProfile with settings provided as method parameters.
        /// </summary>
        /// <param name="multisampleCount">multisampleCount</param>
        /// <param name="superSamplingCount">superSamplingCount</param>
        /// <param name="shaderQuality">shaderQuality</param>
        /// <param name="textureFiltering">textureFiltering</param>
        /// <returns>GraphicsProfile</returns>
        public abstract GraphicsProfile GetCustomGraphicsProfile(int multisampleCount, int superSamplingCount, ShaderQuality shaderQuality, TextureFilteringTypes textureFiltering);

        // Note: this method "guesses" the RenderQualityTypes from the name of GraphicsProfile

        /// <summary>
        /// GetGraphicsProfileQuality returns the RenderQualityTypes for the specified graphicsProfile (GraphicsProfile's Name is used to get the quality level).
        /// </summary>
        /// <param name="graphicsProfile">GraphicsProfile</param>
        /// <returns>RenderQualityTypes</returns>
        public static RenderQualityTypes GetGraphicsProfileQuality(GraphicsProfile graphicsProfile)
        {
            RenderQualityTypes qualityType;

            if (graphicsProfile.Name.StartsWith("Low"))
                qualityType = RenderQualityTypes.Low;
            else if (graphicsProfile.Name.StartsWith("Normal"))
                qualityType = RenderQualityTypes.Normal;
            else if (graphicsProfile.Name.StartsWith("High"))
                qualityType = RenderQualityTypes.High;
            else if (graphicsProfile.Name.StartsWith("Ultra"))
                qualityType = RenderQualityTypes.Ultra;
            else
                qualityType = RenderQualityTypes.Custom;

            return qualityType;
        }

        /// <summary>
        /// Dispose
        /// </summary>
        public void Dispose()
        {
            if (Adapter != null)
            {
                Adapter.Dispose();
                Adapter = null;
            }

            Dispose(true);
        }

        /// <summary>
        /// Dispose
        /// </summary>
        /// <param name="isDisposing">isDisposing</param>
        protected virtual void Dispose(bool isDisposing)
        {
        }
    }         
}