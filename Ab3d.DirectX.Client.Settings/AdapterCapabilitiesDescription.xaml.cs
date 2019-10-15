using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace Ab3d.DirectX.Client.Settings
{
    /// <summary>
    /// Interaction logic for AdapterCapabilitiesDescription.xaml
    /// </summary>
    public partial class AdapterCapabilitiesDescription : UserControl
    {
        public AdapterCapabilitiesDescription()
        {
            InitializeComponent();

            this.DataContextChanged += OnDataContextChanged;
        }

        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            var adapterCapabilities = this.DataContext as AdapterCapabilitiesBase;

            if (adapterCapabilities == null)
                return;

            AdapterNameTextBlock.Text = adapterCapabilities.DisplayName;

            if (!string.IsNullOrEmpty(adapterCapabilities.DeviceInfoText))
            {
                DetailsTextBlock.Visibility = Visibility.Visible;
                DetailsTextBlock.Text = adapterCapabilities.DeviceInfoText;
            }
            else
            {
                DetailsTextBlock.Visibility = Visibility.Collapsed;
            }

            if (adapterCapabilities.IsSupported)
            {
                WarningImage.Visibility = Visibility.Collapsed;
            }
            else
            {
                WarningImage.Visibility = Visibility.Visible;

                if (!string.IsNullOrEmpty(adapterCapabilities.UnsupportedReason))
                    RootGrid.ToolTip = adapterCapabilities.UnsupportedReason;
            }
        }
    }
}
