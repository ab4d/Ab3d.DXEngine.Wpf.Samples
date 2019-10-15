using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace Ab3d.DirectX.Client.Settings
{
    /// <summary>
    /// Interaction logic for DXEngineSettingsWindow.xaml
    /// </summary>
    public partial class DXEngineSettingsWindow : Window
    {
        private GraphicsProfile _selectedGraphicsProfile;

        public GraphicsProfile SelectedGraphicsProfile
        {
            get { return _selectedGraphicsProfile; }
            set
            {
                _selectedGraphicsProfile = value;

                if (_selectedGraphicsProfile != null)
                    DxEngineSettingsPanel1.SelectedGraphicsProfile = new GraphicsProfile(_selectedGraphicsProfile); // Create a copy of the profile
            }
        }

        public SystemCapabilities SystemCapabilities
        {
            get { return DxEngineSettingsPanel1.SystemCapabilities; }
            set { DxEngineSettingsPanel1.SystemCapabilities = value; }
        }

        public double SelectedDpiScale
        {
            get { return DxEngineSettingsPanel1.SelectedDpiScale; }
            set { DxEngineSettingsPanel1.SelectedDpiScale = value; }
        }

        public int SelectedMaxBackgroundThreadsCount
        {
            get { return DxEngineSettingsPanel1.SelectedMaxBackgroundThreadsCount; }
            set { DxEngineSettingsPanel1.SelectedMaxBackgroundThreadsCount = value; }
        }

        public DXEngineSettingsWindow()
        {
            InitializeComponent();
        }

        private void CancelButton_OnClick(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void OkButton_OnClick(object sender, RoutedEventArgs e)
        {
            _selectedGraphicsProfile = DxEngineSettingsPanel1.SelectedGraphicsProfile;
            this.Close();
        }
    }
}
