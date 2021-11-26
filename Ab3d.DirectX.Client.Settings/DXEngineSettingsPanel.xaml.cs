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
        private readonly int[] _possibleDpiValues = new[] {96, 120, 144, 168, 192, 288};

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

            AntiAliasingInfoControl.InfoText =
@"Anti-aliasing is a technique used in computer graphics that tries to minimizing aliasing. Ab3d.DXEngine support the following two techniques:

1) Multi-sampling (MSAA) can produce smoother edges with storing multiple color values for each pixel. To improve performance, pixel shader is executed once for each pixel and then its result (color) is shared across multiple pixels. This produces smoother edges but do not provide additional sub-pixel details. For example, 4xMSAA runs pixel shader only once per each pixel but require 4 times the amount of memory.

2) Super-sampling (SSAA) is a technique that renders the image at a higher resolution and then down-samples the rendered image to the final resolution. The Ab3d.DXEngine can use smart down-sampling filter that improves smoothness of the final image. Super-sampling produces smoother edges and also provides additional sub-pixel details. For example, 4xSSAA renders the scene to a texture with 4 times more pixels (width and height are multiplied by 2). This requires running the pixel shader 4 times for each final pixel and requires 4 times the amount of memory.";


            ShaderQualityInfoControl.InfoText =
@"Ab3d.DXEngine currently support only Low and Normal quality shaders. 

When using Low quality shaders the per-vertex lighting is used. This means that lighting calculations are done for each vertex (in vertex shader) and then interpolated to the pixels between vertices.
When using Normal and High quality shader, then per-pixel lighting is used. As the name suggests, in per-pixel lighting the lighting calculations are done for each rendered pixel  (in pixel shader). This produces more accurate results but requires more work on the GPU.

Usually modern graphics cards are so fast that using low quality shaders do not result in a significantly better performance.";


            TextureFilteringInfoControl.InfoText = "Texture filtering defines how textures are read. Anisotropic filtering provides better quality when texture is viewed at low angles.";


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
        
        private AdapterCapabilitiesBase.RenderQualityTypes GetExistingRenderQuality(GraphicsProfile selectedGraphicsProfile)
        {
            var allRenderQualities = (AdapterCapabilitiesBase.RenderQualityTypes[]) QualityComboBox.ItemsSource;

            var existingRenderQuality = AdapterCapabilitiesBase.RenderQualityTypes.Custom;

            foreach (var renderQuality in allRenderQualities)
            {
                if (renderQuality == AdapterCapabilitiesBase.RenderQualityTypes.Custom) // Skip Custom quality
                    continue;

                var graphicsProfile = _selectedAdapterCapabilities.GetGraphicsProfileForQuality(renderQuality);

                if (graphicsProfile.PreferedMultisampleCount == selectedGraphicsProfile.PreferedMultisampleCount &&
                    graphicsProfile.SupersamplingCount       == selectedGraphicsProfile.SupersamplingCount &&
                    graphicsProfile.ShaderQuality            == selectedGraphicsProfile.ShaderQuality &&
                    graphicsProfile.TextureFiltering         == selectedGraphicsProfile.TextureFiltering)
                {
                    existingRenderQuality = renderQuality;
                    break;
                }
            }

            return existingRenderQuality;
        }

        //private bool IsCustomRenderQuality()
        //{
        //    var existingGraphicsProfile = GetExistingGraphicsProfile(_selectedGraphicsProfile);

        //    var isCustomRenderQuality = existingGraphicsProfile == null;
        //    return isCustomRenderQuality;
        //}
        
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
                _selectedRenderQuality = GetExistingRenderQuality(_selectedGraphicsProfile);
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
                QualityTextBlock.Visibility = Visibility.Collapsed;
                QualityComboBox.Visibility  = Visibility.Collapsed;
                QualityComboBox.IsEnabled = false;
                
                DpiTitleTextBlock.Visibility = Visibility.Collapsed;
                DpiComboBox.Visibility = Visibility.Collapsed;

                CustomSettingsGrid.Visibility = Visibility.Collapsed;

                MaxThreadsTextBlock.Visibility = Visibility.Collapsed;
                MaxThreadsGrid.Visibility = Visibility.Collapsed;
            }
            else
            {
                QualityTextBlock.Visibility = Visibility.Visible;
                QualityComboBox.Visibility = Visibility.Visible;
                QualityComboBox.IsEnabled = true;

                DpiTitleTextBlock.Visibility = Visibility.Visible;
                DpiComboBox.Visibility = Visibility.Visible;

                CustomSettingsGrid.Visibility = Visibility.Visible;
                
                MaxThreadsTextBlock.Visibility = Visibility.Visible;
                MaxThreadsGrid.Visibility = Visibility.Visible;

                _isInternalChange = true;


                var msaaValues = new List<int>();
                int count = 1;
                while (_selectedAdapterCapabilities.DeviceCapabilities.MaxSupportedMultisamplingCount >= count)
                {
                    msaaValues.Add(count);
                    count *= 2;
                }

                FillAntialiasingComboBox(MultisamplingComboBox, "MSAA", msaaValues.ToArray(),     _selectedGraphicsProfile.PreferedMultisampleCount, firstValueText: "No MSAA");
                FillAntialiasingComboBox(SupersamplingComboBox, "SSAA", new int[]{ 1, 4, 16, 64}, _selectedGraphicsProfile.SupersamplingCount,       firstValueText: "No SSAA");


                FillTextureFilteringComboBox(TextureFilteringComboBox, _selectedGraphicsProfile.TextureFiltering, maxAnisotropicLevel: 16); // All feature levels from 9.2 on support 16x AnisotropicLevel
                ShaderQualityComboBox.SelectedItem = _selectedGraphicsProfile.ShaderQuality;

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
            if (newSelectedAdapter.Adapter != null && newSelectedAdapter.AdapterDescription1.DedicatedVideoMemory >= 4000000000L) // if we have a hardware device with more then 4 GB (approximately) dedicated memory, then we can use High rendering quality (this will use 4xSSAA)
                _selectedRenderQuality = AdapterCapabilitiesBase.RenderQualityTypes.High;
            else
                _selectedRenderQuality = AdapterCapabilitiesBase.RenderQualityTypes.Normal;

            _selectedAdapterCapabilities = newSelectedAdapter;

            if (_selectedRenderQuality == AdapterCapabilitiesBase.RenderQualityTypes.Custom)
                _selectedRenderQuality = AdapterCapabilitiesBase.RenderQualityTypes.Normal;

            _selectedGraphicsProfile = newSelectedAdapter.GetGraphicsProfileForQuality(_selectedRenderQuality);

            UpdateCustomSettingsComboBoxes();

            QualityComboBox.SelectedItem = _selectedRenderQuality;
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
            var selectedRenderQuality = GetExistingRenderQuality(_selectedGraphicsProfile);

            if (selectedRenderQuality == AdapterCapabilitiesBase.RenderQualityTypes.Custom)
            {
                _isInternalChange = true;
                QualityComboBox.SelectedIndex = QualityComboBox.Items.Count - 1; // Set as Custom
                _isInternalChange = false;

                string customName = "Custom";
                
                if (_selectedGraphicsProfile.PreferedMultisampleCount > 1)
                    customName += string.Format("_{0}xMSAA", _selectedGraphicsProfile.PreferedMultisampleCount);
  
                if (_selectedGraphicsProfile.SupersamplingCount > 1)
                    customName += string.Format("_{0}xSSAA", _selectedGraphicsProfile.SupersamplingCount);

                _selectedGraphicsProfile.Name = customName + "_GraphicsProfile";
            }
            else
            {
                QualityComboBox.SelectedItem = selectedRenderQuality;
            }
        }

        private void MultisamplingComboBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!this.IsLoaded || _isInternalChange)
                return;

            int selectedMultisamplingCount = GetSelectedComboBoxItemTagValue<int>(MultisamplingComboBox);

            _selectedGraphicsProfile.PreferedMultisampleCount = selectedMultisamplingCount;

            CheckIfCustomRenderQuality();
        }
        
        private void SupersamplingComboBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!this.IsLoaded || _isInternalChange)
                return;

            int selectedSupersamplingFactor = GetSelectedComboBoxItemTagValue<int>(SupersamplingComboBox);
            if (selectedSupersamplingFactor == 0)
                selectedSupersamplingFactor = 1;

            _selectedGraphicsProfile.SupersamplingCount = selectedSupersamplingFactor;

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
                Content = string.Format(System.Globalization.CultureInfo.InvariantCulture, 
                                        "System default - {0:0} DPI (scale: {1:0.0#})", 
                                        96.0 * systemDpiScale, systemDpiScale),

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
                    Content = string.Format(System.Globalization.CultureInfo.InvariantCulture, 
                                            "{0:0} DPI (scale: {1:0.0#})", 
                                            possibleDpiValue, scale),

                    Tag = scale // Tag = DpiScale 
                };

                // ReSharper disable once CompareOfFloatsByEqualityOperator
                if (SelectedDpiScale == scale)
                    comboBoxItem.IsSelected = true;

                DpiComboBox.Items.Add(comboBoxItem);              
            }

            _isInternalChange = false;
        }


        public static void FillAntialiasingComboBox(ComboBox comboBox, string techniqueName, int[] values, int selectedValue, string firstValueText = null)
        {
            int selectedIndex = 0;

            comboBox.BeginInit();
            comboBox.Items.Clear();

            for (int i = 0; i < values.Length; i++)
            {
                int    oneValue = values[i];
                string contentText;

                if (i == 0 && firstValueText != null)
                    contentText = firstValueText;
                else
                    contentText = string.Format("{0}x {1}", oneValue, techniqueName);

                comboBox.Items.Add(new ComboBoxItem { Content = contentText, Tag = oneValue, HorizontalContentAlignment = HorizontalAlignment.Left, VerticalContentAlignment = VerticalAlignment.Center });

                if (selectedValue == oneValue)
                    selectedIndex = i;
            }

            //comboBox.Items.Add(new ComboBoxItem { Content = "No " + techniqueName, Tag = 0, HorizontalContentAlignment = HorizontalAlignment.Left, VerticalContentAlignment = VerticalAlignment.Center });

            //int index = 1;
            //int count = 2;
            //while (maxCount >= count)
            //{
            //    comboBox.Items.Add(new ComboBoxItem { Content = string.Format("{0}x {1}", count, techniqueName), Tag = count, HorizontalContentAlignment = HorizontalAlignment.Left, VerticalContentAlignment = VerticalAlignment.Center });

            //    if (selectedCount >= count)
            //        selectedIndex ++;

            //    index++;

            //    if (isPowerOfTwo)
            //        count = index * index;
            //    else
            //        count = index * 2;
            //}

            comboBox.EndInit();
            comboBox.SelectedIndex = selectedIndex;
        }

        public static void FillTextureFilteringComboBox(ComboBox comboBox, TextureFilteringTypes textureFiltering, int maxAnisotropicLevel = 16)
        {
            comboBox.BeginInit();
            comboBox.Items.Clear();

            comboBox.Items.Add(new ComboBoxItem { Content = "Point", Tag = TextureFilteringTypes.Point, ToolTip = "Uses the color of the nearest neighboring pixel (produces square pixels when zoomed in).", HorizontalContentAlignment = HorizontalAlignment.Left, VerticalContentAlignment = VerticalAlignment.Center });
            comboBox.Items.Add(new ComboBoxItem { Content = "Bilinear", Tag = TextureFilteringTypes.Bilinear, ToolTip = "Uses the color that is linearly interpolated from the nearest colors from the texture.", HorizontalContentAlignment = HorizontalAlignment.Left, VerticalContentAlignment = VerticalAlignment.Center });
            comboBox.Items.Add(new ComboBoxItem { Content = "Trilinear", Tag = TextureFilteringTypes.Trilinear, ToolTip = "Uses the color that is linearly interpolated from the nearest colors from the two nearest mip map textures.", HorizontalContentAlignment = HorizontalAlignment.Left, VerticalContentAlignment = VerticalAlignment.Center });

            if (maxAnisotropicLevel >= 2)
            {
                comboBox.Items.Add(new ComboBoxItem { Content = "Anisotropic 2x", Tag = TextureFilteringTypes.Anisotropic_x2, ToolTip = "Anisotropic filtering with level 2 (compared to linear interpolation Anisotropic filtering improves details when camera angle is low).", HorizontalContentAlignment = HorizontalAlignment.Left, VerticalContentAlignment = VerticalAlignment.Center });

                if (maxAnisotropicLevel >= 4)
                {
                    comboBox.Items.Add(new ComboBoxItem { Content = "Anisotropic 4x", Tag = TextureFilteringTypes.Anisotropic_x4, ToolTip = "Anisotropic filtering with level 4 (compared to linear interpolation Anisotropic filtering improves details when camera angle is low).", HorizontalContentAlignment = HorizontalAlignment.Left, VerticalContentAlignment = VerticalAlignment.Center });

                    if (maxAnisotropicLevel >= 8)
                    {
                        comboBox.Items.Add(new ComboBoxItem { Content = "Anisotropic 8x", Tag = TextureFilteringTypes.Anisotropic_x8, ToolTip = "Anisotropic filtering with level 8 (compared to linear interpolation Anisotropic filtering improves details when camera angle is low).", HorizontalContentAlignment = HorizontalAlignment.Left, VerticalContentAlignment = VerticalAlignment.Center });

                        if (maxAnisotropicLevel >= 8)
                            comboBox.Items.Add(new ComboBoxItem { Content = "Anisotropic 16x", Tag = TextureFilteringTypes.Anisotropic_x16, ToolTip = "Anisotropic filtering with level 16 (compared to linear interpolation Anisotropic filtering improves details when camera angle is low).", HorizontalContentAlignment = HorizontalAlignment.Left, VerticalContentAlignment = VerticalAlignment.Center });
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
