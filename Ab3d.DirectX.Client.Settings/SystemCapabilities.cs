using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.InteropServices;
using SharpDX.Direct3D;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using Device = SharpDX.Direct3D11.Device;

namespace Ab3d.DirectX.Client.Settings
{
    /// <summary>
    /// SystemCapabilities class can be used to query the available adapters on this computer.
    /// </summary>
    public class SystemCapabilities : IDisposable
    {
        /// <summary>
        /// Gets all DirectX Adapters on this computer.
        /// </summary>
        public Adapter1[] AllAdapters { get; private set; }

        /// <summary>
        /// Gets a collection of all AdapterCapabilities classes on this computer.
        /// </summary>
        public ReadOnlyCollection<AdapterCapabilitiesBase> AllAdapterCapabilities { get; private set; }

        /// <summary>
        /// Gets the SoftwareAdapterCapabilities.
        /// </summary>
        public SoftwareAdapterCapabilities SoftwareAdapterCapabilities { get; private set; }

        /// <summary>
        /// Gets the WpfAdapterCapabilities.
        /// </summary>
        public WpfAdapterCapabilities WpfAdapterCapabilities { get; private set; }


        private bool? _isDebugLayerAvailable;
        /// <summary>
        /// Gets a Boolean that specifies if DirectX debug layer is installed on the system (Debug layer is part of DirectX SDK or Windows SDK)
        /// </summary>
        public bool IsDebugLayerAvailable
        {
            get
            {
                if (!_isDebugLayerAvailable.HasValue)
                    _isDebugLayerAvailable = CheckDebugSdkAvailable();

                return _isDebugLayerAvailable.Value;
            }
        }

        private static bool? _isSupportedOS;
        /// <summary>
        /// Gets a Boolean that specifies if the current operating system supports DirectX 11.
        /// DirectX 11 is supported on Windows 7 or newer operating systems and on Vista/Server 2008 with SP2 or Platform Update.
        /// </summary>
        public static bool IsSupportedOS
        {
            get
            {
                if (!_isSupportedOS.HasValue)
                    _isSupportedOS = CheckSupportedOS();

                return _isSupportedOS.Value;
            }
        }


        private static bool _isPlatformUpdateNeeded;

        /// <summary>
        /// IsPlatformUpdateNeeded can be true only on Windows Vista or Windows Server 2008 before SP2 when DirectX 11 cannot be used because a Windows Platform Update (KB971644) or Service Pack 2 is needed.
        /// </summary>
        public static bool IsPlatformUpdateNeeded
        {
            get
            {
                if (!_isSupportedOS.HasValue)
                    _isSupportedOS = CheckSupportedOS(); // _isPlatformUpdateNeeded is set in CheckSupportedOS

                return _isPlatformUpdateNeeded;
            }
        }

        /// <summary>
        /// Constructor
        /// </summary>
        public SystemCapabilities()
        {
            var allAdapterCapabilities = new List<AdapterCapabilitiesBase>();

            if (IsSupportedOS) // If this operating system support DirectX 11?
            {
                try
                {
                    AllAdapters = DXDevice.GetAllSystemAdapters();
                }
                catch
                {
                    AllAdapters = null;
                }
                

                if (AllAdapters != null && AllAdapters.Length > 0)
                {
                    foreach (var oneAdapter in AllAdapters)
                    {
                        if (IsSoftwareRenderingAdapter(oneAdapter))
                            continue; // Skip "Microsoft Basic Render Driver" that is identified by specified VendorId and DeviceId - it is the same as Software rendered (WARP) so we do not want to show it 2 times

                        // Error handling is done inside HardwareAdapterCapabilities constructor
                        allAdapterCapabilities.Add(new HardwareAdapterCapabilities(oneAdapter));
                    }
                }

                SoftwareAdapterCapabilities = new SoftwareAdapterCapabilities();
                allAdapterCapabilities.Add(SoftwareAdapterCapabilities);
            }

            // WPF 3D is always supported
            WpfAdapterCapabilities = new WpfAdapterCapabilities();
            allAdapterCapabilities.Add(WpfAdapterCapabilities);

            AllAdapterCapabilities = new ReadOnlyCollection<AdapterCapabilitiesBase>(allAdapterCapabilities);
        }

        /// <summary>
        /// Returns true if the specified adapter is software rendering adapter
        /// </summary>
        /// <param name="adapter">adapter</param>
        /// <returns>true if the specified adapter is software rendering adapter</returns>
        public static bool IsSoftwareRenderingAdapter(Adapter1 adapter)
        {
            var adapterDescription1 = adapter.Description1;

            return ((((int)adapterDescription1.Flags & 2) != 0) || // SharpDX 2.5 does not have DXGI_ADAPTER_FLAG_SOFTWARE in AdapterFlags enum
                    (adapterDescription1.VendorId == 0x1414 && adapterDescription1.DeviceId == 0x8c)); // "Microsoft Basic Render Driver" is identified by by specified VendorId and DeviceId - it is the same as Software rendered (WARP) so we do not want to show it 2 times
        }

        /// <summary>
        /// Returns array of GraphicsProfile that are recommended for this system.
        /// If possible a hardware GraphicsProfile will be the first in the array. Than a software will follow as a fallback and after that a WPF 3D GraphicsProfile will be in the array.
        /// </summary>
        /// <returns>array of GraphicsProfile that are recommended for this system</returns>
        public GraphicsProfile[] GetRecommendedGraphicsProfiles()
        {
            AdapterCapabilitiesBase bestAdapterCapabilities;
            AdapterCapabilitiesBase.RenderQualityTypes renderQuality;

            SelectRecommendedAdapter(out bestAdapterCapabilities, out renderQuality);

            GraphicsProfile bestGraphicsProfile = bestAdapterCapabilities.GetGraphicsProfileForQuality(renderQuality);

            return CreateArrayOfRecommendedGraphicsProfiles(bestGraphicsProfile);
        }

        /// <summary>
        /// Returns an array of GraphicsProfile where the first GraphicsProfile is recommendedGraphicsProfile.
        /// The following graphics profiles in the list are fallback GraphicsProfiles that can be used if recommendedGraphicsProfile cannot be used.
        /// </summary>
        /// <param name="recommendedGraphicsProfile">GraphicsProfile that will be used as first GraphicsProfile in the returned list</param>
        /// <returns>an array of GraphicsProfile</returns>
        public GraphicsProfile[] CreateArrayOfRecommendedGraphicsProfiles(GraphicsProfile recommendedGraphicsProfile)
        {
            List<GraphicsProfile> graphicsProfiles = new List<GraphicsProfile>();

            if (recommendedGraphicsProfile.DriverType == GraphicsProfile.DriverTypes.DirectXHardware)
            {
                // First we need to add the recommended graphic profile
                graphicsProfiles.Add(recommendedGraphicsProfile);

                // Now if there are more than one hardware graphics profile on the system we will add other graphics profiles
                // So in case the recommended one fails to initialize, we can than use the next hardware graphics card (better than use software or WPF)
                if (recommendedGraphicsProfile.DefaultAdapter != null &&
                    AllAdapters != null &&
                    AllAdapters.Length > 1)
                {
                    foreach (var oneAdapter in AllAdapters)
                    {
                        if (oneAdapter == recommendedGraphicsProfile.DefaultAdapter || IsSoftwareRenderingAdapter(oneAdapter))
                            continue;

                        graphicsProfiles.Add(new GraphicsProfile(GraphicsProfile.NormalQualityHardwareRendering, oneAdapter));
                    }
                }

                // As first fallback we also add software rendering graphic profile
                // Software rendering is highly optimized for multi-threading so more cores means we can afford better performance
                if (Environment.ProcessorCount > 2)
                    graphicsProfiles.Add(GraphicsProfile.NormalQualitySoftwareRendering);
                else
                    graphicsProfiles.Add(GraphicsProfile.LowQualitySoftwareRendering);
            }
            else if (recommendedGraphicsProfile.DriverType == GraphicsProfile.DriverTypes.DirectXSoftware)
            {
                graphicsProfiles.Add(recommendedGraphicsProfile);
            }

            // As a last resort we always add WPF graphics profile
            graphicsProfiles.Add(GraphicsProfile.Wpf3D);

            return graphicsProfiles.ToArray();
        }

        internal void SelectRecommendedAdapter(out AdapterCapabilitiesBase bestAdapterCapabilities, out AdapterCapabilitiesBase.RenderQualityTypes renderQuality)
        {
            if (AllAdapterCapabilities == null || AllAdapterCapabilities.Count == 0 || !IsSupportedOS)
            {
                bestAdapterCapabilities = WpfAdapterCapabilities;
                renderQuality = AdapterCapabilitiesBase.RenderQualityTypes.Normal;

                return;
            }

            // First try to get dedicated graphics card from nvidia or ATI that has some dedicated video RAM and support at least 10.0 feature level

            // VendorID from http://gamedev.stackexchange.com/questions/31625/get-video-chipset-manufacturer-in-direct3d
            // Full list of vendors: http://www.pcidatabase.com/vendors.php?sort=id
            bestAdapterCapabilities = AllAdapterCapabilities.OfType<HardwareAdapterCapabilities>()
                                                            .Where(a => a.IsSupported &&
                                                                        (a.AdapterDescription1.VendorId == 0x10DE || // NVidia
                                                                         a.AdapterDescription1.VendorId == 0x1002 || a.AdapterDescription1.VendorId == 0x1022)) // AMD
                                                            .OrderByDescending(a => (long)a.AdapterDescription1.DedicatedVideoMemory)  // Sort by DedicatedVideoMemory - we assume that more RAM means better card - so pick the one with most RAM (DedicatedVideoMemory is of type PointerSize - to be able to compare by it we need to convert it to long)

                                                            .FirstOrDefault();

            if (bestAdapterCapabilities == null)
            {
                // If no NVidia or ATI graphics card was found than just take the first supported graphics card
                bestAdapterCapabilities = AllAdapterCapabilities.OfType<HardwareAdapterCapabilities>().FirstOrDefault(a => a.IsSupported);
            }

            if (bestAdapterCapabilities != null)
            {
                // Found the best dedicated graphics card
                // If the card is 11.0 feature level and has at least 1 GB RAM use High quality, else use Normal quality
                if ((int)bestAdapterCapabilities.DeviceCapabilities.FeatureLevel >= (int)FeatureLevel.Level_11_0 &&
                    (bestAdapterCapabilities.AdapterDescription1.DedicatedVideoMemory >= 1000000000L || 
                    (bestAdapterCapabilities.AdapterDescription1.DedicatedVideoMemory < 0 && IntPtr.Size == 4))) // When running in 32 bit, the DedicatedVideoMemory has only 4 bytes to store memory size - in case of 8 GB, this means this is converted into negaitve number
                {
                    renderQuality = AdapterCapabilitiesBase.RenderQualityTypes.High;
                }
                else
                {
                    renderQuality = AdapterCapabilitiesBase.RenderQualityTypes.Normal;
                }
            }
            else
            {
                // No hardware graphics card - use software rendering
                bestAdapterCapabilities = SoftwareAdapterCapabilities;

                if (bestAdapterCapabilities != null)
                {
                    // no 10.0 feature level adapter found - we use software rendering
                    // Software rendering is highly optimized for multi-threading so more cores means we can afford better performance
                    if (Environment.ProcessorCount > 2)
                        renderQuality = AdapterCapabilitiesBase.RenderQualityTypes.Normal;
                    else
                        renderQuality = AdapterCapabilitiesBase.RenderQualityTypes.Low;
                }
                else
                {
                    // WPF 3D
                    bestAdapterCapabilities = WpfAdapterCapabilities;
                    renderQuality = AdapterCapabilitiesBase.RenderQualityTypes.Normal;
                }
            }
        }

        /// <summary>
        /// CreateAdapterCapabilitiesFromGraphicsProfile creates appropriate AdapterCapabilities from the specified GraphicsProfile.
        /// </summary>
        /// <param name="graphicsProfile">GraphicsProfile</param>
        /// <returns>AdapterCapabilities</returns>
        public AdapterCapabilitiesBase CreateAdapterCapabilitiesFromGraphicsProfile(GraphicsProfile graphicsProfile)
        {
            AdapterCapabilitiesBase adapterCapabilities;

            if (graphicsProfile.DriverType == GraphicsProfile.DriverTypes.DirectXHardware)
            {
                Adapter1 hardwarAdapter = graphicsProfile.DefaultAdapter;

                if (hardwarAdapter == null)
                {
                    if (AllAdapters.Length > 0)
                        hardwarAdapter = AllAdapters[0];
                    else
                        throw new DXEngineException("Cannot create default adapter from GraphicsProfile " + graphicsProfile.Name ?? "");
                }
                else
                {
                    var hardwarAdapterDescription1 = hardwarAdapter.Description1;

                    // Check if the adapter is in the list of available adapters
                    if (AllAdapters.FirstOrDefault(a => a.Description1.Description == hardwarAdapterDescription1.Description) == null)
                    {
                        if (AllAdapters.Length > 0)
                            hardwarAdapter = AllAdapters[0]; // Just use the first adapter
                        else
                            throw new DXEngineException("Cannot create default adapter instead of adapter specified in GraphicsProfile " + graphicsProfile.Name ?? "");                                
                    }
                }

                adapterCapabilities = new HardwareAdapterCapabilities(hardwarAdapter);
            }
            else if (graphicsProfile.DriverType == GraphicsProfile.DriverTypes.DirectXSoftware)
            {
                adapterCapabilities = SoftwareAdapterCapabilities;
            }
            else
            {
                adapterCapabilities = WpfAdapterCapabilities;
            }

            return adapterCapabilities;
        }

        private static bool CheckDebugSdkAvailable()
        {
            bool deviceCreated = false;

            try
            {
                // Create a null device with debug layer to check if it is available
                var tempDevice = DXDevice.CreateDevice(DriverType.Null, adapter: null, deviceCreationFlags: DeviceCreationFlags.Debug, featureLevels: null);

                if (tempDevice != null)
                {
                    deviceCreated = true;
                    tempDevice.Dispose();
                }
            }
            catch
            {
                // pass
            }

            return deviceCreated;
        }

        private static bool CheckSupportedOS()
        {
            if (Environment.OSVersion.Platform != PlatformID.Win32NT)
                return false;

            // See list of OS versions: http://en.wikipedia.org/wiki/User:IMSoP/Winver
            // Windows Vista 	        6.0.6000
            // Windows Server 2008      6.0.6000
            // Windows 7 	            6.1.7600
            // Windows Server 2008 R2   6.1.7600
            // Windows 8 	            6.2.9200

            var osVersion = Environment.OSVersion.Version;

            // Also based on Direct3D 11 Install Helper - http://code.msdn.microsoft.com/Direct3D-11-Install-Helper-3044575e/sourcecode?fileId=57661&pathId=793431158
            if (osVersion.Major < 6)
                return false;

            if (osVersion.Major == 6 && osVersion.Minor == 0 && // Windows Vista/Server 2008
                osVersion.Build < 6002)                         // before Service Pack 2 (SP2 should already include Direct3D 11)
            {
                IntPtr libPtr = IntPtr.Zero;

                // If user have installed Platform update than this OS supports DirectX
                try
                {
                    libPtr = NativeMethods.LoadLibrary("D3D11.DLL"); // according to Direct3D 11 Install Helper we can check if library can be loaded
                }
                catch
                {
                    libPtr = IntPtr.Zero;
                }

                if (libPtr == IntPtr.Zero)
                {
                    _isPlatformUpdateNeeded = true;
                    return false;
                }

                try
                {
                    NativeMethods.FreeLibrary(libPtr);
                }
                catch
                {
                    // pass
                }
            }

            // If we come to here than the OS is supported
            return true;
        }

        /// <summary>
        /// Dispose
        /// </summary>
        public void Dispose()
        {
            if (AllAdapters != null)
            {
                foreach (Adapter1 oneAdapter in AllAdapters)
                    oneAdapter.Dispose();

                AllAdapters = null;
            }

            if (AllAdapterCapabilities != null)
            {
                foreach (var adapterCapabilitiesBase in AllAdapterCapabilities)
                    adapterCapabilitiesBase.Dispose();

                AllAdapterCapabilities = null;
            }

            SoftwareAdapterCapabilities = null;
            WpfAdapterCapabilities = null;
        }

        internal static class NativeMethods
        {
            [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
            public static extern IntPtr LoadLibrary(string dllToLoad);

            [DllImport("kernel32.dll")]
            public static extern bool FreeLibrary(IntPtr hModule);
        }
    }
}