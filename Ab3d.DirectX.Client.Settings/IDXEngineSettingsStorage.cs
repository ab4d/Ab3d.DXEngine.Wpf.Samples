namespace Ab3d.DirectX.Client.Settings
{
    /// <summary>
    /// IDXEngineSettingsStorage interface defines properties that are used to read and save DXEngine graphics settings.
    /// UserGraphicProfileText is used to get and set user selected graphics settings.
    /// OverrideGraphicProfileText is used to override the saved user settings with some other settings (for example from application startup parameters or config file).
    /// </summary>
    public interface IDXEngineSettingsStorage
    {
        /// <summary>
        /// Gets a graphics settings that (when set) will override the saved user settings with some other settings.
        /// This can be read from application startup parameters or config file.
        /// </summary>
        string OverrideGraphicProfileText { get; }

        /// <summary>
        /// Gets or sets a user selected graphics settings.
        /// The text should be in format that can be used by the <see cref="GraphicsProfileSerializer"/>.
        /// </summary>
        string UserGraphicProfileText { get; set; }

        /// <summary>
        /// Gets a Boolean that specifies if DXEngine will be using DirectX Overlay presentation type.
        /// </summary>
        bool UseDirectXOverlay { get; }
    }
}