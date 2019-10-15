using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace Ab3d.DirectX.Client.Settings
{
    /// <summary>
    /// Interaction logic for DXEngineSettingsPanel.xaml
    /// </summary>
    public partial class DXEngineSettingsPanel : UserControl
    {
        private readonly int[] _possibleDpiValues = new[] {96, 120, 144, 192};

        private bool _isInternalChange;

        private AdapterCapabilitiesBase _selectedAdapterCapabilities;
        private AdapterCapabilitiesBase.RenderQualityTypes _selectedRenderQuality;

        private BackgroundWorker _backgroundWorker;

        public SystemCapabilities SystemCapabilities { get; set; }

        public string SelectedAdapterName { get; set; }

        private GraphicsProfile _selectedGraphicsProfile;

        public GraphicsProfile SelectedGraphicsProfile
        {
            get { return _selectedGraphicsProfile; }
            set { _selectedGraphicsProfile = value; }
        }

        public double SelectedDpiScale { get; set; }

        public int SelectedMaxBackgroundThreadsCount { get; set; }

        public bool IsAsyncAdaptersCheck { get; set; }

        public bool ShowDpiSetting
        {
            get { return DpiTitleTextBlock.Visibility == Visibility.Visible; }
            set
            {
                if (value)
                {
                    DpiTitleTextBlock.Visibility = Visibility.Visible;
                    DpiComboBox.Visibility = Visibility.Visible;
                }
                else
                {
                    DpiTitleTextBlock.Visibility = Visibility.Collapsed;
                    DpiComboBox.Visibility = Visibility.Collapsed;                   
                }
            }
        }

        /// <summary>
        /// Constructor
        /// </summary>
        public DXEngineSettingsPanel()
        {
            InitializeComponent();

            // IsAsyncAdaptersCheck can be set to true to show the panel immediately and when the device data are read fill controls that depend on device data
            IsAsyncAdaptersCheck = false;

            SelectedDpiScale = double.NaN; // System default

            ShaderQualityComboBox.ItemsSource = new ShaderQuality[] { ShaderQuality.Low, ShaderQuality.Normal, ShaderQuality.High };
            QualityComboBox.ItemsSource = new AdapterCapabilitiesBase.RenderQualityTypes[] { AdapterCapabilitiesBase.RenderQualityTypes.Low,
                                                                                             AdapterCapabilitiesBase.RenderQualityTypes.Normal,
                                                                                             AdapterCapabilitiesBase.RenderQualityTypes.High,
                                                                                             AdapterCapabilitiesBase.RenderQualityTypes.Ultra,
                                                                                             AdapterCapabilitiesBase.RenderQualityTypes.Custom };

            // Other ComboBoxes are initialized after the device capabilities are known in the following methods:
            // FillAntialiasingComboBox, FillTextureFilteringComboBox, FillDpiCombobox

            if (System.ComponentModel.DesignerProperties.GetIsInDesignMode(this))
                return;


            this.Loaded += OnLoaded;
        }

        private bool IsCustomRenderQuality()
        {
            var renderQuality = AdapterCapabilitiesBase.GetGraphicsProfileQuality(_selectedGraphicsProfile);
            var graphicsProfile = _selectedAdapterCapabilities.GetGraphicsProfileForQuality(renderQuality);

            var isCustomRenderQuality = !(graphicsProfile.PreferedMultisampleCount == _selectedGraphicsProfile.PreferedMultisampleCount &&
                                          graphicsProfile.ShaderQuality == _selectedGraphicsProfile.ShaderQuality &&
                                          graphicsProfile.TextureFiltering == _selectedGraphicsProfile.TextureFiltering);

            return isCustomRenderQuality;
        }
        
        private void OnLoaded(object sender, RoutedEventArgs routedEventArgs)
        {
            FillDpiCombobox();

            int maxBackgroundThreadCount = Environment.ProcessorCount - 1;
            MaxThreadsSlider.Maximum = maxBackgroundThreadCount;
            MaxThreadsSlider.Value   = SelectedMaxBackgroundThreadsCount <= maxBackgroundThreadCount ? SelectedMaxBackgroundThreadsCount : maxBackgroundThreadCount;



            if (SystemCapabilities != null)
            {
                ShowAdapters();
                return;
            }

            if (!IsAsyncAdaptersCheck)
            {
                CollectAvailableAdapters();
                ShowAdapters();
                
                return;
            }


            _backgroundWorker = new BackgroundWorker();
            _backgroundWorker.DoWork += delegate(object o, DoWorkEventArgs args)
            {
                CollectAvailableAdapters();
            };

            _backgroundWorker.RunWorkerCompleted += delegate(object o, RunWorkerCompletedEventArgs args)
            {
                if (this.IsLoaded) // If panel was already closed, we do not need to show adapters
                    ShowAdapters();
            };

            _backgroundWorker.RunWorkerAsync();
        }

        private void CollectAvailableAdapters()
        {
            SystemCapabilities = new SystemCapabilities();

            if (_selectedGraphicsProfile == null)
            {
                // This sets _selectedAdapterCapabilities and _selectedRenderQuality
                SystemCapabilities.SelectRecommendedAdapter(out _selectedAdapterCapabilities, out _selectedRenderQuality);

                _selectedGraphicsProfile = _selectedAdapterCapabilities.GetGraphicsProfileForQuality(_selectedRenderQuality);
            }
            else
            {
                // This sets _selectedAdapterCapabilities and _selectedRenderQuality
                _selectedAdapterCapabilities = SystemCapabilities.CreateAdapterCapabilitiesFromGraphicsProfile(_selectedGraphicsProfile);

                var isCustomRenderQuality = IsCustomRenderQuality();

                if (isCustomRenderQuality)
                    _selectedRenderQuality = AdapterCapabilitiesBase.RenderQualityTypes.Custom;
                else
                    _selectedRenderQuality = AdapterCapabilitiesBase.GetGraphicsProfileQuality(_selectedGraphicsProfile);
            }      
        }

        private void ShowAdapters()
        {
            CreateAdapterRadioBoxes();

            QualityComboBox.SelectedItem = _selectedRenderQuality;

            UpdateCustomSettingsComboBoxes();
        }

        // Set selected items based on current _selectedGraphicsProfile
        private void UpdateCustomSettingsComboBoxes()
        {
            if (_selectedGraphicsProfile.DriverType == GraphicsProfile.DriverTypes.Wpf3D)
            {
                CustomSettingsGrid.Visibility = Visibility.Collapsed;
                QualityComboBox.IsEnabled = false;

                DpiTitleTextBlock.Visibility = Visibility.Collapsed;
                DpiComboBox.Visibility = Visibility.Collapsed;

                QualityTextBlock.Visibility = Visibility.Collapsed;
                QualityComboBox.Visibility = Visibility.Collapsed;
            }
            else
            {
                QualityTextBlock.Visibility = Visibility.Visible;
                QualityComboBox.Visibility = Visibility.Visible;

                DpiTitleTextBlock.Visibility = Visibility.Visible;
                DpiComboBox.Visibility = Visibility.Visible;

                CustomSettingsGrid.Visibility = Visibility.Visible;
                QualityComboBox.IsEnabled = true;

                _isInternalChange = true;

                FillAntialiasingComboBox(AntialiasingComboBox, _selectedGraphicsProfile.PreferedMultisampleCount, _selectedAdapterCapabilities.DeviceCapabilities.MaxSupportedMultisamplingCount);
                FillTextureFilteringComboBox(TextureFilteringComboBox, _selectedGraphicsProfile.TextureFiltering, maxAnisotropicLevel: 16); // All feature levels from 9.2 on support 16x AnisotropicLevel
                ShaderQualityComboBox.SelectedItem = _selectedGraphicsProfile.ShaderQuality;

                // Comboboxes inside Custom panel can be changed only when on custom quality settings
                //CustomSettingsGrid.IsEnabled = (_selectedRenderQuality == AdapterCapabilitiesBase.RenderQualityTypes.Custom);

                _isInternalChange = false;
            }
        }
        
        private void CreateAdapterRadioBoxes()
        {
            AdapterStackPanel.BeginInit();

            foreach (var oneAdapterCapabilities in SystemCapabilities.AllAdapterCapabilities)
            {
                var adapterCapabilitiesDescription = new AdapterCapabilitiesDescription();
                adapterCapabilitiesDescription.DataContext = oneAdapterCapabilities;

                var radioButton = new RadioButton();
                radioButton.Content = adapterCapabilitiesDescription;
                radioButton.Margin = new Thickness(0, 0, 0, 5);

                radioButton.IsEnabled = oneAdapterCapabilities.IsSupported;
                radioButton.Tag = oneAdapterCapabilities;

                radioButton.IsChecked = _selectedAdapterCapabilities.DisplayName == oneAdapterCapabilities.DisplayName;

                radioButton.Checked += (sender, e) => ChangeSelectedAdapter((AdapterCapabilitiesBase) ((RadioButton) sender).Tag);

                AdapterStackPanel.Children.Add(radioButton);
            }

            AdapterStackPanel.Visibility = Visibility.Visible;
            AdapterStackPanel.EndInit();
        }

        private void ChangeSelectedAdapter(AdapterCapabilitiesBase newSelectedAdapter)
        {
            _selectedAdapterCapabilities = newSelectedAdapter;

            if (_selectedRenderQuality == AdapterCapabilitiesBase.RenderQualityTypes.Custom)
                _selectedRenderQuality = AdapterCapabilitiesBase.RenderQualityTypes.Normal;

            _selectedGraphicsProfile = newSelectedAdapter.GetGraphicsProfileForQuality(_selectedRenderQuality);

            UpdateCustomSettingsComboBoxes();
        }

        private void ShowError(string message)
        {
            ErrorTextBlock.Text = message;
            ErrorTextBlock.Visibility = Visibility.Visible;
        }

        private void HideError()
        {
            ErrorTextBlock.Visibility = Visibility.Collapsed;
            ErrorTextBlock.Text = null;
        }

        private void QualityComboBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!this.IsLoaded || _isInternalChange)
                return;

            _selectedRenderQuality = (AdapterCapabilitiesBase.RenderQualityTypes)QualityComboBox.SelectedItem;

            if (_selectedRenderQuality == AdapterCapabilitiesBase.RenderQualityTypes.Custom)
            {
                // When custom quality is selected we create a copy of the current graphics profile so we can safely change it
                _selectedGraphicsProfile = new GraphicsProfile(_selectedGraphicsProfile, _selectedGraphicsProfile.DefaultAdapter);
            }
            else
            {
                _selectedGraphicsProfile = _selectedAdapterCapabilities.GetGraphicsProfileForQuality(_selectedRenderQuality);
            }

            UpdateCustomSettingsComboBoxes();
        }

        private void MaxThreadsSlider_OnValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!this.IsLoaded)
                return;

            SelectedMaxBackgroundThreadsCount = (int) MaxThreadsSlider.Value;
        }

        private void CheckIfCustomRenderQuality()
        {
            var isCustomRenderQuality = IsCustomRenderQuality();
            if (isCustomRenderQuality)
            {
                _isInternalChange = true;
                QualityComboBox.SelectedIndex = QualityComboBox.Items.Count - 1; // Set as Custom
                _isInternalChange = false;
            }
        }

        private void AntialiasingComboBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!this.IsLoaded || _isInternalChange)
                return;

            int selectedMultisamplingCount = GetSelectedComboBoxItemTagValue<int>(AntialiasingComboBox);

            _selectedGraphicsProfile.PreferedMultisampleCount = selectedMultisamplingCount;

            CheckIfCustomRenderQuality();
        }

        private void ShaderQualityComboBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!this.IsLoaded || _isInternalChange)
                return;

            ShaderQuality selectedShaderQuality = (ShaderQuality)ShaderQualityComboBox.SelectedItem;

            _selectedGraphicsProfile.ShaderQuality = selectedShaderQuality;

            CheckIfCustomRenderQuality();
        }

        private void TextureFilteringComboBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!this.IsLoaded || _isInternalChange)
                return;

            TextureFilteringTypes selectedTextureFiltering = GetSelectedComboBoxItemTagValue<TextureFilteringTypes>(TextureFilteringComboBox);

            _selectedGraphicsProfile.TextureFiltering = selectedTextureFiltering;

            CheckIfCustomRenderQuality();
        }

        //private void ShadowsComboBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        //{
        //    CheckIfCustomRenderQuality();
        //}

        private void DpiComboBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!this.IsLoaded || _isInternalChange)
                return;

            SelectedDpiScale = ((double) ((ComboBoxItem) DpiComboBox.SelectedItem).Tag);
        }

        private T GetSelectedComboBoxItemTagValue<T>(ComboBox comboBox)
        {
            if (comboBox.SelectedItem is T)
                return (T) comboBox.SelectedItem;

            var comboBoxItem = comboBox.SelectedItem as ComboBoxItem;

            if (comboBoxItem == null)
                return default(T);

            return (T)comboBoxItem.Tag;
        }

        private void FillDpiCombobox()
        {
            double systemDpiScale;
            
            _isInternalChange = true;

            // We can get the system DPI scale from 
            // PresentationSource.FromVisual(this).CompositionTarget.TransformToDevice.M11 and M22
            var presentationSource = PresentationSource.FromVisual(this);

            if (presentationSource != null && presentationSource.CompositionTarget != null)
            {
                // Theoretically we can have different x any y DPI scale so we make an average
                systemDpiScale = (presentationSource.CompositionTarget.TransformToDevice.M11 + presentationSource.CompositionTarget.TransformToDevice.M22)/2;
            }
            else
            {
                systemDpiScale = 1.0; // This should not happen
            }

            // First entry is "System default"
            var systemDefaultComboBoxItem = new ComboBoxItem()
            {
                Content = string.Format("System default - {0:0} DPI (scale: {1:0.0})", 96.0 * systemDpiScale, systemDpiScale),
                Tag = double.NaN // Tag = DpiScale (NaN = system default)
            };

            if (double.IsNaN(SelectedDpiScale))
                systemDefaultComboBoxItem.IsSelected = true;

            DpiComboBox.Items.Add(systemDefaultComboBoxItem);

            foreach (var possibleDpiValue in _possibleDpiValues)
            {
                double scale = possibleDpiValue / 96.0;

                var comboBoxItem = new ComboBoxItem()
                {
                    Content = string.Format("{0:0} DPI (scale: {1:0.0})", possibleDpiValue, scale),
                    Tag = scale // Tag = DpiScale 
                };

                // ReSharper disable once CompareOfFloatsByEqualityOperator
                if (SelectedDpiScale == scale)
                    comboBoxItem.IsSelected = true;

                DpiComboBox.Items.Add(comboBoxItem);              
            }

            _isInternalChange = false;
        }


        public static void FillAntialiasingComboBox(ComboBox comboBox, int selectedMultisampleCount, int maxMultisampleCount = 8)
        {
            int selectedIndex = 0;

            comboBox.BeginInit();
            comboBox.Items.Clear();

            comboBox.Items.Add(new ComboBoxItem { Content = "Off", Tag = 0 });

            if (maxMultisampleCount >= 2)
            {
                comboBox.Items.Add(new ComboBoxItem { Content = "2x MSAA", Tag = 2 });

                if (selectedMultisampleCount >= 2)
                    selectedIndex = 1;

                if (maxMultisampleCount >= 4)
                {
                    comboBox.Items.Add(new ComboBoxItem { Content = "4x MSAA", Tag = 4 });

                    if (selectedMultisampleCount >= 4)
                        selectedIndex = 2;

                    if (maxMultisampleCount >= 8)
                    {
                        comboBox.Items.Add(new ComboBoxItem { Content = "8x MSAA", Tag = 8 });

                        if (selectedMultisampleCount >= 8)
                            selectedIndex = 3;

                        if (maxMultisampleCount >= 16)
                        {
                            comboBox.Items.Add(new ComboBoxItem { Content = "16x MSAA", Tag = 16 });

                            if (selectedMultisampleCount >= 16)
                                selectedIndex = 4;
                        }
                    }
                }
            }

            comboBox.EndInit();
            comboBox.SelectedIndex = selectedIndex;
        }


        public static void FillTextureFilteringComboBox(ComboBox comboBox, TextureFilteringTypes textureFiltering, int maxAnisotropicLevel = 16)
        {
            comboBox.BeginInit();
            comboBox.Items.Clear();

            comboBox.Items.Add(new ComboBoxItem { Content = "Point", Tag = TextureFilteringTypes.Point, ToolTip = "Uses the color of the nearest neighboring pixel (produces square pixels when zoomed in)." });
            comboBox.Items.Add(new ComboBoxItem { Content = "Bilinear", Tag = TextureFilteringTypes.Bilinear, ToolTip = "Uses the color that is linearly interpolated from the nearest colors from the texture." });
            comboBox.Items.Add(new ComboBoxItem { Content = "Trilinear", Tag = TextureFilteringTypes.Trilinear, ToolTip = "Uses the color that is linearly interpolated from the nearest colors from the two nearest mip map textures." });

            if (maxAnisotropicLevel >= 2)
            {
                comboBox.Items.Add(new ComboBoxItem { Content = "Anisotropic 2x", Tag = TextureFilteringTypes.Anisotropic_x2, ToolTip = "Anisotropic filtering with level 2 (compared to linear interpolation Anisotropic filtering improves details when camera angle is low)." });

                if (maxAnisotropicLevel >= 4)
                {
                    comboBox.Items.Add(new ComboBoxItem { Content = "Anisotropic 4x", Tag = TextureFilteringTypes.Anisotropic_x4, ToolTip = "Anisotropic filtering with level 4 (compared to linear interpolation Anisotropic filtering improves details when camera angle is low)." });

                    if (maxAnisotropicLevel >= 8)
                    {
                        comboBox.Items.Add(new ComboBoxItem { Content = "Anisotropic 8x", Tag = TextureFilteringTypes.Anisotropic_x8, ToolTip = "Anisotropic filtering with level 8 (compared to linear interpolation Anisotropic filtering improves details when camera angle is low)." });

                        if (maxAnisotropicLevel >= 8)
                            comboBox.Items.Add(new ComboBoxItem { Content = "Anisotropic 16x", Tag = TextureFilteringTypes.Anisotropic_x16, ToolTip = "Anisotropic filtering with level 16 (compared to linear interpolation Anisotropic filtering improves details when camera angle is low)." });
                    }
                }
            }

            comboBox.EndInit();


            for (int i = 0; i < comboBox.Items.Count; i++)
            {
                if ((TextureFilteringTypes)((ComboBoxItem)comboBox.Items[i]).Tag == textureFiltering)
                {
                    comboBox.SelectedIndex = i;
                    break;
                }
            }
        }
    }
}
