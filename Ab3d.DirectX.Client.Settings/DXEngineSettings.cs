using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using Ab3d.DirectX;
using Ab3d.DirectX.Client.Settings;
using Ab3d.DirectX.Controls;

namespace Ab3d.DirectX.Client.Settings
{
    /// <summary>
    /// DXEngineSettings is a helper class that can create GraphicsProfiles for Ab3d.DXEngine from saved user settings.
    /// The class can also save the changed user settings. 
    /// It is also possible to provide OverrideGraphicProfileText value (from application startup parameters or config file) that can override the saved user settings.
    /// </summary>
    /// <remarks>
    /// <para>
    /// DXEngineSettings is a helper class that can create GraphicsProfiles for Ab3d.DXEngine from saved user settings.
    /// The class can also save the changed user settings. 
    /// It is also possible to provide OverrideGraphicProfileText value (from application startup parameters or config file) that can override the saved user settings.
    /// </para>
    /// <para>
    /// Before first use the DXEngineSettings must be initialized with calling static <see cref="Initialize"/> method.
    /// After that user can use the static <see cref="Current"/> property.
    /// </para>
    /// </remarks>
    public class DXEngineSettings : IDisposable
    {
        private static DXEngineSettings _current;

        /// <summary>
        /// Singleton instance of the DXEngineSettings class
        /// </summary>
        public static DXEngineSettings Current
        {
            get
            {
                if (_current == null)
                    throw new Exception("DXEngineSettings must be initialized with calling static Initialize method before its first use");

                return _current;
            }
        }

        /// <summary>
        /// Initialize static method creates the static Current property and initializes is from the data read from settingsStorage.
        /// settingsStorage can be provided to read and save user graphics settings.
        /// </summary>
        /// <param name="settingsStorage">IDXEngineSettingsStorage that is used to read and save user graphics settings</param>
        /// <returns>DXEngineSettings</returns>
        public static DXEngineSettings Initialize(IDXEngineSettingsStorage settingsStorage)
        {
            if (_current == null)
                _current = new DXEngineSettings(settingsStorage);

            return _current;
        }



        private readonly IDXEngineSettingsStorage _settingsStorage;

        private SystemCapabilities _systemCapabilities;

        /// <summary>
        /// Gets the current SystemCapabilities
        /// </summary>
        public SystemCapabilities SystemCapabilities
        {
            get
            {
                if (_systemCapabilities == null)
                    _systemCapabilities = new SystemCapabilities();

                return _systemCapabilities;
            }
        }

        /// <summary>
        /// Gets or sets the GraphicsProfiles that will be used to initialize DXEngine
        /// </summary>
        public GraphicsProfile[] GraphicsProfiles { get; set; }

        /// <summary>
        /// Gets a Boolean that specifies the value of UseDirectXOverlay from application config file
        /// </summary>
        public bool UseDirectXOverlay
        {
            get
            {
                if (_settingsStorage == null)
                    return false; // Default

                return _settingsStorage.UseDirectXOverlay;
            }
        }

        private DXEngineSettings(IDXEngineSettingsStorage settingsStorage)
        {
            _settingsStorage = settingsStorage;
            
            // Start by using default GraphicsProfile array (used by default in DXView)
            GraphicsProfiles = new GraphicsProfile[] { GraphicsProfile.NormalQualityHardwareRendering,
                                                       GraphicsProfile.NormalQualitySoftwareRendering,
                                                       GraphicsProfile.Wpf3D };
        }

        /// <summary>
        /// Checks if the operating system supports DirectX 11. If not a message box with information for the user is shown.
        /// </summary>
        /// <returns>true if supported OS, otherwise false</returns>
        public bool CheckIfSupportedOperatingSystem()
        {
            if (!SystemCapabilities.IsSupportedOS)
            {
                if (SystemCapabilities.IsPlatformUpdateNeeded)
                    MessageBox.Show("DirectX 11 is not supported on Windows Vista or Windows Server 2008 without Windows Platform Update (KB971644) or Service Pack 2.\r\nPlease install the required updates.\r\n\r\nWPF 3D will be used instead of DirectX 11.", "DirectX 11 not supported", MessageBoxButton.OK, MessageBoxImage.Error);
                else
                    MessageBox.Show("DirectX 11 is not supported on this operating system.\r\n\r\nDirectX 11 is supported on Windows 7 or newer operating systems\r\nand on Vista/Server 2008 with SP2 or Platform Update.\r\n\r\nWPF 3D will be used instead of DirectX 11.", "DirectX 11 not supported", MessageBoxButton.OK, MessageBoxImage.Error);

                return false;
            }

            return true;
        }

        public void InitializeGraphicProfiles()
        {
            GraphicsProfile serializedGraphicsProfile = null;

            // First check if DXEngineGraphicProfile is set in application config file (this overrides saved setting)
            string dxEngineGraphicProfileText;

            if (_settingsStorage != null)
                dxEngineGraphicProfileText = _settingsStorage.OverrideGraphicProfileText;
            else
                dxEngineGraphicProfileText = null;

            if (!string.IsNullOrEmpty(dxEngineGraphicProfileText))
            {
                try
                {
                    serializedGraphicsProfile = GraphicsProfileSerializer.Deserialize(dxEngineGraphicProfileText, SystemCapabilities.AllAdapters, throwExceptions: false);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Cannot serialize the GraphicsProfile setting in config file (appSettings is DXEngineGraphicProfile).\r\n\r\nError:\r\n" + ex.Message);
                }
            }



            // If GraphicsProfile is not yet set, read the GraphicsProfile setting saved to application's settings
            if (serializedGraphicsProfile == null && _settingsStorage != null)
            {
                dxEngineGraphicProfileText = _settingsStorage.UserGraphicProfileText;

                if (!string.IsNullOrEmpty(dxEngineGraphicProfileText))
                {
                    try
                    {
                        serializedGraphicsProfile = GraphicsProfileSerializer.Deserialize(dxEngineGraphicProfileText, SystemCapabilities.AllAdapters, throwExceptions: false);
                    }
                    catch
                    {
                        // UH - this should not happen (maybe the  GraphicsProfile was serialized with previous version)
                    }
                }
            }

            if (serializedGraphicsProfile != null)
            {
                // CreateArrayOfRecommendedGraphicsProfiles creates an array of GraphicsProfile where the first GraphicsProfile is serializedGraphicsProfile.
                // The following graphics profiles in the list are fallback GraphicsProfiles that can be used if serializedGraphicsProfile cannot be used (for example software rendering and wpf 3D)
                this.GraphicsProfiles = SystemCapabilities.CreateArrayOfRecommendedGraphicsProfiles(serializedGraphicsProfile);
            }
            else
            {
                // GraphicsProfile still not set - probably the first run or user never changed graphics profile manually
                // Use the recommended settings for this computer

                this.GraphicsProfiles = DXEngineSettings.Current.SystemCapabilities.GetRecommendedGraphicsProfiles();
            }
        }

        public void SaveGraphicsProfile(GraphicsProfile graphicsProfile)
        {
            if (_settingsStorage == null)
                return;

            // Serialize the graphicsProfile data to string and store that into applications settings
            _settingsStorage.UserGraphicProfileText = GraphicsProfileSerializer.Serialize(graphicsProfile);
        }

        /// <summary>
        /// Disposes SystemCapabilities class and its unmanaged resources
        /// </summary>
        public void Dispose()
        {
            if (_systemCapabilities != null)
            {
                _systemCapabilities.Dispose();
                _systemCapabilities = null;
            }
        }
    }
}