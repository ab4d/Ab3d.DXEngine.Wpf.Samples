using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using SharpDX.DXGI;

namespace Ab3d.DirectX.Client.Settings
{
    // - Examples:
    // Name: NormalQualityHardwareRendering; DriverType: DirectXHardware; ShaderQuality: Normal; MultisampleCount: 4; TextureFiltering: Anisotropic_x4
    // Name: NormalQualitySoftwareRendering; DriverType: DirectXSoftware; ShaderQuality: Normal; MultisampleCount: 2; TextureFiltering: Anisotropic_x2
    // Name: Wpf3D; DriverType: Wpf3D; ShaderQuality: Low; MultisampleCount: 0; TextureFiltering: Bilinear
    // 
    // - with specified adapter:
    // Name: NormalQualityHardwareRendering; DriverType: DirectXHardware; ShaderQuality: Normal; MultisampleCount: 4; TextureFiltering: Anisotropic_x4; Adapter: NVIDIA GeForce GTX 970
    // 
    // - It is also allowed to specify only name of GraphicsProfile:
    // NormalQualitySoftwareRendering
    // Wpf3D

    /// <summary>
    /// GraphicsProfileSerializer class can serialize GraphicsProfile to string or deserialize it from string.
    /// </summary>
    public static class GraphicsProfileSerializer
    {
        private static Regex _propertyNameValuePairsRegex;

        /// <summary>
        /// Serialize GraphicsProfile to string
        /// </summary>
        /// <param name="graphicsProfile">GraphicsProfile</param>
        /// <returns>serialized GraphicsProfile</returns>
        public static string Serialize(GraphicsProfile graphicsProfile)
        {
            return string.Format("Name: {0}; DriverType: {1}; ShaderQuality: {2}; MultisampleCount: {3}; SupersamplingCount: {4}; TextureFiltering: {5}{6}",
                graphicsProfile.Name, graphicsProfile.DriverType, graphicsProfile.ShaderQuality, graphicsProfile.PreferedMultisampleCount, graphicsProfile.SupersamplingCount, graphicsProfile.TextureFiltering,
                (graphicsProfile.DefaultAdapter != null && !graphicsProfile.DefaultAdapter.IsDisposed) ? "; Adapter: " + graphicsProfile.DefaultAdapterDescription : "");
        }

        /// <summary>
        /// Deserialize GraphicsProfile from string
        /// </summary>
        /// <param name="serializedGraphicsProfile">serializedGraphicsProfile as string</param>
        /// <param name="allAdapters">IList of all adapters on the system</param>
        /// <param name="throwExceptions">if true an exception is thrown in case of errors during serialization</param>
        /// <returns>GraphicsProfile</returns>
        public static GraphicsProfile Deserialize(string serializedGraphicsProfile, IList<Adapter1> allAdapters, bool throwExceptions = true)
        {
            if (string.IsNullOrEmpty(serializedGraphicsProfile))
                return null;

            if (_propertyNameValuePairsRegex == null)
                _propertyNameValuePairsRegex = new Regex(@"(\s*(?<Name>\w+)\s*:\s*(?<Value>[^;]*);?)\s*", RegexOptions.Singleline);

            var match = _propertyNameValuePairsRegex.Match(serializedGraphicsProfile);
            if (!match.Success)
            {
                // Maybe serializedGraphicsProfile is only the name of of the standard GraphicProfiles
                var standardGraphicsProfile = GraphicsProfile.GetStandardGraphicsProfile(serializedGraphicsProfile);
                if (standardGraphicsProfile != null)
                    return standardGraphicsProfile;

                return null; // Cannot parse the serializedGraphicsProfile
            }

            string propertyName = match.Groups["Name"].Value;
            string propertyValue = match.Groups["Value"].Value;

            if (!string.Equals(propertyName, "Name", StringComparison.OrdinalIgnoreCase))
                return null; // serializedGraphicsProfile must start with name of the GraphicsProfile


            string graphicProfileName = propertyValue;

            var graphicsProfile = GraphicsProfile.GetStandardGraphicsProfile(graphicProfileName);
            if (graphicsProfile == null)
            {
                // If we did not get the GraphicsProfile from the name we created a new GraphicsProfile with default properties
                graphicsProfile = new GraphicsProfile(graphicProfileName, GraphicsProfile.DriverTypes.DirectXHardware, ShaderQuality.Normal, 4, TextureFilteringTypes.Anisotropic_x4);
            }

            // Setup default values
            // We cannot change the properties of GraphicProfile, so we will need to prepare all the property values and after that create a new GraphicProfile with out property values
            var      driverType               = graphicsProfile.DriverType;
            var      shaderQuality            = graphicsProfile.ShaderQuality;
            int      preferedMultisampleCount = graphicsProfile.PreferedMultisampleCount;
            int      supersamplingCount       = graphicsProfile.SupersamplingCount;
            var      textureFiltering         = graphicsProfile.TextureFiltering;
            Adapter1 defaultAdapter           = graphicsProfile.DefaultAdapter;

            match = match.NextMatch();
            while (match.Success)
            {
                try
                {
                    propertyName = match.Groups["Name"].Value.ToLower();
                    propertyValue = match.Groups["Value"].Value;

                    switch (propertyName)
                    {
                        case "name":
                            break;

                        case "drivertype":
                            driverType = (GraphicsProfile.DriverTypes) Enum.Parse(typeof (GraphicsProfile.DriverTypes), propertyValue);
                            break;

                        case "shaderquality":
                            shaderQuality = (ShaderQuality) Enum.Parse(typeof (ShaderQuality), propertyValue);
                            break;

                        case "multisamplecount":
                            preferedMultisampleCount = Int32.Parse(propertyValue);
                            break;
                        
                        case "supersamplingcount":
                            supersamplingCount = Int32.Parse(propertyValue);
                            break;

                        case "texturefiltering":
                            textureFiltering = (TextureFilteringTypes) Enum.Parse(typeof (TextureFilteringTypes), propertyValue);
                            break;

                        case "adapter":
                            if (allAdapters != null)
                                defaultAdapter = allAdapters.FirstOrDefault(a => a.Description1.Description.StartsWith(propertyValue)); // We use StartsWith because the Description1.Description can have trailing '\0' chars that are trimmed in propertyValue
                            break;
                    }
                }
                catch
                {
                    if (throwExceptions)
                        throw;
                }

                match = match.NextMatch();
            }

            // Now we have the final values of all GraphicsProfile properties and we can create a new instance of GraphicsProfile
            graphicsProfile = new GraphicsProfile(graphicProfileName, driverType, shaderQuality, preferedMultisampleCount, supersamplingCount, textureFiltering, defaultAdapter);

            return graphicsProfile;
        }
    }
}