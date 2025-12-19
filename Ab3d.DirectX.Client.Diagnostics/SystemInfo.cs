using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.Versioning;
using System.Text;
using Microsoft.Win32;

#if SHARPDX
using SharpDX.Direct3D;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using Device = SharpDX.DXGI.Device;
#else
using Ab3d.DirectX.Common;
using Ab3d.DXGI;
using Ab3d.Direct3D;
using Ab3d.Direct3D11;
using Device = Ab3d.DXGI.Device;
#endif

namespace Ab3d.DirectX.Client.Diagnostics
{
    // NOTE: This required a reference to System.Management
    public static class SystemInfo
    {
        public static string GetFullSystemInfo()
        {
            var sb = new StringBuilder();

            var entryAssembly = System.Reflection.Assembly.GetEntryAssembly();

            string entryAssemblyName, entryAssemblyTargetFramework;
            Version entryAssemblyVersion;

            if (entryAssembly != null)
            {
                entryAssemblyName = entryAssembly.GetName().Name;
                entryAssemblyVersion = entryAssembly.GetName().Version;
                entryAssemblyTargetFramework = "";

                // NOTE: .Net 4.0 does not have entryAssembly.GetCustomAttribute<TargetFrameworkAttribute>() method
                var customAttributes = entryAssembly.GetCustomAttributes(true);
                if (customAttributes != null)
                {
                    for (int i = 0; i < customAttributes.Length; i++)
                    {
                        var targetFrameworkAttribute = customAttributes[i] as TargetFrameworkAttribute;
                        if (targetFrameworkAttribute != null)
                        {
                            entryAssemblyTargetFramework = targetFrameworkAttribute.FrameworkName;
                            break;
                        }
                    }
                }
            }
            else
            {
                entryAssemblyName = "(no EntryAssembly)";
                entryAssemblyVersion = new Version();
                entryAssemblyTargetFramework = "";
            }

            // Do not use strng type reference for DXDevice, so that this class can be used without DXEngine assembly (for example in SystemInfo console application)
            string dxEngineVersion;

            try
            {
                var dxDeviceType = Type.GetType("Ab3d.DirectX.DXDevice, Ab3d.DXEngine", throwOnError: false);
                if (dxDeviceType != null)
                    dxEngineVersion = dxDeviceType.Assembly.GetName().Version.ToString();
                else
                    dxEngineVersion = "";
            }
            catch
            {
                dxEngineVersion = "";
            }

            sb.AppendFormat("<SystemInfo ApplicationName=\"{0}\" ApplicationVersion=\"{1}\" TargetFramework=\"{2}\" DXEngineVersion=\"{3}\" Date=\"{4:yyyy-MM-dd}\" />\r\n",
                entryAssemblyName,
                entryAssemblyVersion,
                entryAssemblyTargetFramework,
                dxEngineVersion,
                DateTime.Now);


            string systemName = GetOSName();

            if (systemName.Length > 0)
                systemName = string.Format("OSName=\"{0}\" ", systemName);

            bool isDirectXDebugLayerAvailable = CheckDebugSdkAvailable();

            string cpuInfo;
            try
            {
                cpuInfo = System.Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER");

                if (!string.IsNullOrEmpty(cpuInfo))
                    cpuInfo = string.Format("CPU=\"{0}\" " , cpuInfo);
            }
            catch
            {
                cpuInfo = "";
            }

            sb.AppendFormat("    <GeneralInfo {0}OSVersion=\"{1}\" Is64BitOS=\"{2}\" Is64BitProcess=\"{3}\" ProcessorCount=\"{4}\" {5}IsDirectXDebugLayerAvailable=\"{6}\" />\r\n",
                systemName, Environment.OSVersion.Version, Environment.Is64BitOperatingSystem, Environment.Is64BitProcess, Environment.ProcessorCount, cpuInfo, isDirectXDebugLayerAvailable);


#if NETCOREAPP || NET5_0
            try
            {
                sb.AppendFormat("    <RuntimeInformation FrameworkDescription=\"{0}\" OSDescription=\"{1}\" OSArchitecture=\"{2}\" ProcessArchitecture=\"{3}\" />\r\n",
                    System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
                    System.Runtime.InteropServices.RuntimeInformation.OSDescription,
                    System.Runtime.InteropServices.RuntimeInformation.OSArchitecture,
                    System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture);
            }
            catch (Exception ex)
            {
                AppendError(sb, "Error getting RuntimeInformation info", ex, indent: 4);
            }
#else
            try
            {
                sb.AppendFormat("    <NetRuntime Versions=\"{0}\" />\r\n", GetNetVersionsFromRegistry());
            }
            catch (Exception ex)
            {
                AppendError(sb, "Error getting NetRuntime info", ex, indent: 4);
            }
#endif



            sb.AppendLine("    <Adapters>");

            sb.Append(GetAllAdaptersInfo(indent: 4));

            sb.AppendLine("    </Adapters>");


#if !NETCOREAPP && !NET5_0
            try
            {
                string adapterDetailsText = GetVideoControllerDetailsText(indent: 4);
                sb.AppendLine(adapterDetailsText);
            }
            catch (Exception ex)
            {
                AppendError(sb, "Error getting Video Controller details", ex, indent: 4);
            }
#endif


            // Add all Ab3d and SharpDX assemblies
            sb.AppendLine("    <Assemblies>");

            try
            {
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    bool writeAssemblyInfo = true;

                    try
                    {
                        writeAssemblyInfo = assembly.FullName.StartsWith("Ab3d.") || assembly.FullName.StartsWith("SharpDX.");
                    }
                    catch
                    {
                        // pass
                    }

                    if (!writeAssemblyInfo)
                        continue;

                    sb.AppendFormat("        <Assembly FullName=\"{0}\" />\r\n", assembly.FullName);
                }
            }
            catch (Exception ex)
            {
                AppendError(sb, "Error getting assemblies info", ex, indent: 4);
            }

            sb.AppendLine("    </Assemblies>");



            sb.AppendLine("</SystemInfo>");

            return sb.ToString();
        }

        private static bool CheckDebugSdkAvailable()
        {
            bool deviceCreated = false;

            try
            {
                // Create a null device with debug layer to check if it is available
#if SHARPDX
                var tempDevice = new SharpDX.Direct3D11.Device(DriverType.Null, DeviceCreationFlags.Debug, null);
#else
                var tempDevice = Ab3d.Direct3D11.Device.Create(DriverType.Null, DeviceCreationFlags.Debug);
#endif                

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

        /// <summary>
        /// Returns true if the specified adapter is software rendering adapter
        /// </summary>
        /// <param name="adapter">adapter</param>
        /// <returns>true if the specified adapter is software rendering adapter</returns>
        private static bool IsSoftwareRenderingAdapter(Adapter1 adapter)
        {
            var adapterDescription1 = adapter.Description1;

            return ((((int) adapterDescription1.Flags & 2) != 0) || // SharpDX 2.5 does not have DXGI_ADAPTER_FLAG_SOFTWARE in AdapterFlags enum
                    (adapterDescription1.VendorId == 0x1414 && adapterDescription1.DeviceId == 0x8c)); // "Microsoft Basic Render Driver" is identified by by specified VendorId and DeviceId - it is the same as Software rendered (WARP) so we do not want to show it 2 times
        }

#if !NETCOREAPP && !NET5_0
        // From: http://msdn.microsoft.com/en-us/library/hh925568%28v=vs.110%29.aspx
        private static string GetNetVersionsFromRegistry()
        {
            RegistryKey ndpKey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\NET Framework Setup\NDP\", false);

            if (ndpKey == null)
                return "";

            var sb = new StringBuilder();

            try
            {
                string lastVersionText = null;

                foreach (string versionKeyName in ndpKey.GetSubKeyNames())
                {
                    if (versionKeyName.StartsWith("v"))
                    {
                        RegistryKey versionKey = ndpKey.OpenSubKey(versionKeyName);

                        if (versionKey != null)
                        {
                            string versionText = (string) versionKey.GetValue("Version", "");

                            if (!string.IsNullOrEmpty(versionText))
                            {
                                if (lastVersionText != versionText)
                                {
                                    sb.Append(versionText)
                                        .Append(", ");

                                    lastVersionText = versionText;
                                }
                            }

                            foreach (string subKeyName in versionKey.GetSubKeyNames())
                            {
                                RegistryKey subKey = versionKey.OpenSubKey(subKeyName);

                                if (subKey != null)
                                {
                                    versionText = (string) subKey.GetValue("Version", "");
                                    subKey.Close();

                                    if (!string.IsNullOrEmpty(versionText))
                                    {
                                        if (lastVersionText != versionText)
                                        {
                                            sb.Append(versionText)
                                                .Append(", ");

                                            lastVersionText = versionText;
                                        }
                                    }
                                }
                            }

                            versionKey.Close();
                        }
                    }
                }

                if (sb.Length > 1)
                    sb.Remove(sb.Length - 2, 2); // remove last ", "

                return sb.ToString();
            }
            finally
            {
                ndpKey.Close();
            }
        }
#endif

        /// <summary>
        /// Gets a driver version string for the specified device id in format "1.2.3 Build 456".
        /// This method is using <see cref="GetDriverVersion"/> method.
        /// </summary>
        /// <param name="deviceId">device id</param>
        /// <returns>driver version string in format "1.2.3 Build 456"</returns>
        public static string GetDriverVersionString(int deviceId)
        {
            var driverVersion = GetDriverVersion(deviceId);

            if (driverVersion.Major == 0 && driverVersion.Minor == 0)
                return "";

            return string.Format("{0}.{1}.{2} Build {3}", driverVersion.Major, driverVersion.Minor, driverVersion.Build, driverVersion.Revision);
        }

        /// <summary>
        /// Returns a driver version for the specified device id.
        /// If the version cannot be retrieved a version with 0.0 is returned.
        /// Note that driver version is usually written as 1.2.3 Build 456 (the Build version in Version object is set in the Revision field and not in Build field).
        /// </summary>
        /// <param name="deviceId">device id</param>
        /// <returns>driver version</returns>
        public static Version GetDriverVersion(int deviceId)
        {
            // Based on https://stackoverflow.com/questions/56935213/how-to-get-adapter-driver-version-in-directx12/56960922

            RegistryKey localMachineKey;

            try
            {
                if (Environment.Is64BitProcess)
                    localMachineKey = Registry.LocalMachine;
                else
                    localMachineKey = RegistryKey.OpenBaseKey(Microsoft.Win32.RegistryHive.LocalMachine, RegistryView.Registry64);

                var directXSubKey = localMachineKey.OpenSubKey(@"SOFTWARE\Microsoft\DirectX", writable: false);

                if (directXSubKey == null)
                    return new Version(0, 0);

                var subKeyNames = directXSubKey.GetSubKeyNames();

                // UH: There are no subkey names returned

                foreach (var subKeyName in subKeyNames)
                {
                    var subKey = localMachineKey.OpenSubKey(@"SOFTWARE\Microsoft\DirectX\" + subKeyName);

                    if (subKey != null)
                    {
                        var oneDeviceId = (int)subKey.GetValue("DeviceId");

                        if (oneDeviceId == deviceId)
                        {
                            var longDriverVersion = (long)subKey.GetValue("DriverVersion");

                            int major = (int)((longDriverVersion >> 48) & 0xFFFF);
                            int minor = (int)((longDriverVersion >> 32) & 0xFFFF);
                            int revision = (int)((longDriverVersion >> 16) & 0xFFFF);
                            int build = (int)(longDriverVersion & 0xFFFF);

                            var version = new Version(major, minor, revision, build);

                            return version;
                        }
                    }
                }
            }
            catch
            {
                // pass
            }

            return new Version(0, 0);
        }

        public static string GetAllAdaptersInfo(int indent = 0)
        {
            var sb = new StringBuilder();

            Adapter1[] allSystemAdapters = null;


            try
            {
                allSystemAdapters = DXDevice.GetAllSystemAdapters();

                foreach (var oneSystemAdapter in allSystemAdapters)
                {
                    // Skip "Microsoft Basic Render Driver" that is identified by specified VendorId and DeviceId - it is the same as Software rendered (WARP) so we do not want to show it 2 times
                    if (!IsSoftwareRenderingAdapter(oneSystemAdapter))
                    {
                        var deviceCapabilities = DeviceCapabilities.QueryAdapter(oneSystemAdapter);
                        var adapterDetailsText = GetAdapterDetailsText(oneSystemAdapter, deviceCapabilities, indent + 4);

                        sb.Append(adapterDetailsText);
                    }

                    oneSystemAdapter.Dispose();
                }
            }
            catch (Exception ex)
            {
                AppendError(sb, "Error getting adapters info", ex, indent);
            }
            finally
            {
                if (allSystemAdapters != null)
                {
                    foreach (var oneSystemAdapter in allSystemAdapters)
                        oneSystemAdapter.Dispose();
                }
            }

            return sb.ToString();
        }

#if !NETCOREAPP && !NET5_0 // System.Management is not supported in core3
        /// <summary>
        /// Return a string with details of all graphics adapters on the system. The details are get using Windows WMI with the following query: "SELECT * FROM Win32_VideoController".
        /// </summary>
        /// <returns>string with details of all graphics adapters on the system</returns>
        public static string GetVideoControllerDetailsText(int indent = 0)
        {
            try
            {
                var sb = new StringBuilder();

                // Description of properties http://msdn.microsoft.com/en-us/library/aa394512%28v=vs.85%29.aspx
                // Problems with Win32_VideoController in Win8: http://social.msdn.microsoft.com/Forums/wpapps/en-US/0e7d38f5-21be-4afc-ba8d-ddf7b35f6d04/wmi-query-to-get-driverversion-is-broken-after-kb2795944-update
                using (var searcher = new System.Management.ManagementObjectSearcher("root\\CIMV2", "SELECT * FROM Win32_VideoController"))
                {
                    sb.AppendFormat("{0}<VideoControllers>\r\n", new String(' ', indent));

                    foreach (System.Management.ManagementObject queryObj in searcher.Get())
                    {
                        sb.AppendFormat("{0}<VideoController ", new String(' ', indent + 4));

                        foreach (var property in queryObj.Properties)
                            sb.AppendFormat("{0}=\"{1}\" ", property.Name, property.Value);

                        sb.AppendLine("/>");
                    }
                }

                sb.AppendFormat("{0}</VideoControllers>", new String(' ', indent));

                return sb.ToString();
            }
            catch (System.Management.ManagementException e)
            {
                return String.Format("{0}<VideoControllers Error=\"An error occurred while querying for Win32_VideoController WMI data: {1}\" />\r\n", new String(' ', indent), e.Message);
            }
        }
#endif

        // Returns null if not recognized
        public static string GetOSName()
        {
            // UH: Environment.OSVersion does not detect Windows 10 any more - returns 6.2
            // Therefore we need to read the name of the OS from registry (see http://stackoverflow.com/questions/31885302/how-can-i-detect-if-my-app-is-running-on-windows-10)

            string osProductName = null;

            try
            {
                // This was tested on Windows 7, 8, 8.1 and 10:
                using (var registryKey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion", writable: false))
                {
                    osProductName = registryKey.GetValue("ProductName") as string;
                }
            }
            catch
            {
            }

            if (osProductName == null)
                osProductName = "";

            return osProductName;
        }

        private static string GetAdapterDetailsText(Adapter1 adapter, DeviceCapabilities deviceCapabilities, int indent = 0)
        {
            if (adapter == null) throw new ArgumentNullException("adapter");

            var sb = new StringBuilder();

            string description = adapter.Description.Description;
            int pos = description.IndexOf('\0'); // UH: In SharpDX 3.1, some adapters report description with many zero characters
            if (pos >= 0)
                description = description.Substring(0, pos);

            sb.AppendFormat("{0}<Adapter Description=\"{1}\"\r\n", new String(' ', indent), description);

            sb.AppendFormat("{0}DeviceId=\"0x{1:X}\" VendorId=\"0x{2:X}\" SubSysId=\"0x{3:X}\" AdapterLuid=\"0x{4:X}\" Revision=\"{5}\" Flags=\"{6}\"\r\n{0}DedicatedVideoMemory=\"{7}\" DedicatedSystemMemory=\"{8}\" SharedSystemMemory=\"{9}\" >\r\n",
                new String(' ', indent + 9),
                adapter.Description.DeviceId, adapter.Description.VendorId, adapter.Description.SubsystemId, adapter.Description.Luid, adapter.Description.Revision, adapter.Description1.Flags,
                (uint)adapter.Description.DedicatedVideoMemory,
                (uint)adapter.Description.DedicatedSystemMemory,
                (uint)adapter.Description.SharedSystemMemory);

            // According to http://msdn.microsoft.com/en-us/library/windows/desktop/bb174524%28v=vs.85%29.aspx
            // If you try to use CheckInterfaceSupport to check whether a Direct3D 11.x and later version interface is supported, CheckInterfaceSupport returns DXGI_ERROR_UNSUPPORTED. 
            //adapter.IsInterfaceSupported<>()

            if (adapter.Outputs == null || adapter.Outputs.Length == 0)
            {
                sb.AppendFormat("{0}<Outputs />\r\n", new String(' ', indent + 4));
            }
            else
            {
                sb.AppendFormat("{0}<Outputs>\r\n", new String(' ', indent + 4));

                for (int j = 0; j < adapter.Outputs.Length; j++)
                {
                    var oneOutput = adapter.Outputs[j];
                    var desktopBounds = oneOutput.Description.DesktopBounds;

                    int width, height;
                    width = desktopBounds.Right - desktopBounds.Left;
                    height = desktopBounds.Bottom - desktopBounds.Top;

                    sb.AppendFormat("{0}<Output DeviceName=\"{1}\" AttachedToDesktop=\"{2}\" Width=\"{3}\" Height=\"{4}\" Rotation=\"{5}\" />\r\n",
                        new String(' ', indent + 8), oneOutput.Description.DeviceName, oneOutput.Description.IsAttachedToDesktop, width, height, oneOutput.Description.Rotation);
                }

                sb.AppendFormat("{0}</Outputs>\r\n", new String(' ', indent + 4));
            }

            if (deviceCapabilities != null)
            {
                sb.AppendFormat("{0}<DeviceCapabilities IsDirectX11Supported=\"{1}\" FeatureLevel=\"{2}\" MaxSupportedMultisamplingCount=\"{3}\" />\r\n",
                        new String(' ', indent + 4), deviceCapabilities.IsDirectX11Supported, deviceCapabilities.FeatureLevel, deviceCapabilities.MaxSupportedMultisamplingCount);
            }

            sb.AppendFormat("{0}</Adapter>\r\n", new String(' ', indent));

            return sb.ToString();
        }

        private static void AppendError(StringBuilder sb, string contextDescription, Exception ex, int indent)
        {
            string indentString = new String(' ', indent);

            if (!string.IsNullOrEmpty(contextDescription))
                contextDescription = string.Format("Context=\"{0}\" ", contextDescription);
            else
                contextDescription = "";

            sb.AppendFormat("{0}<Exception {1}Type=\"{2}\" Message=\"{3}\" ", indentString, contextDescription, ex.GetType().Name, ex.Message);

            ex = ex.InnerException;
            if (ex == null)
            {
                sb.AppendLine("/>"); // Complete the element
            }
            else
            {
                sb.AppendLine(">");

                while (ex != null)
                {
                    sb.AppendFormat("{0}    <InnerException Type=\"{1}\" Message=\"{2}\" />\r\n", indentString, ex.GetType().Name, ex.Message);
                    ex = ex.InnerException;
                }

                sb.AppendFormat("{0}</Exception>\r\n", indentString);
            }
        }
    }
}