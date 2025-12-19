using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Ab3d.DirectX;
using Ab3d.DirectX.Materials;
using Ab3d.DXEngine.Wpf.Samples.Common;
using Assimp;
using UserControl = System.Windows.Controls.UserControl;

#if SHARPDX
using SharpDX.Direct3D11;
#endif

namespace Ab3d.DXEngine.Wpf.Samples.PhysicallyBasedRendering
{
    /// <summary>
    /// Interaction logic for TextureMapSelectionControl.xaml
    /// </summary>
    public partial class TextureMapSelectionControl : UserControl
    {
        private PhysicallyBasedMaterial _physicallyBasedMaterial;

        private TextureMapInfo _textureMapInfo;
        private bool _showTextureTextBox;
        private bool _showFilter;
        private bool _showMask;
        private Color _currentMaskColor;
        private float _currentFilterValue;

        public IMultiMapMaterial Material { get; private set; }

        public TextureMapTypes TextureMapType { get; private set; }

        // If you want that the ShaderResourceView is loaded in this control, then you need to set the DXDevice
        public DXDevice DXDevice { get; set; }

        public Scene AssimpScene { get; set; }

        public Dictionary<string, ShaderResourceView> TexturesCache { get; set; }


        public bool ShowTextureTextBox
        {
            get { return _showTextureTextBox; }
            set
            {
                _showTextureTextBox = value;
                TextureGrid.Visibility = value ? Visibility.Visible : Visibility.Hidden;

                if (!value)
                    TextureCheckBox.IsChecked = false;
            }
        }

        public bool ShowFilter
        {
            get { return _showFilter; }
            set
            {
                _showFilter = value;

                if (value)
                {
                    FilterSlider.Visibility         = Visibility.Visible;
                    FilterValueTextBlock.Visibility = Visibility.Visible;

                    UpdateShownFilterValue();
                }
                else
                {
                    FilterSlider.Visibility = Visibility.Collapsed;
                    FilterValueTextBlock.Visibility = Visibility.Collapsed;
                }

                ShowMask = false;
            }
        }

        public bool ShowMask
        {
            get { return _showMask; }
            set
            {
                _showMask = value;

                if (value)
                {
                    MaskHeadingTextBlock.Visibility = Visibility.Visible;
                    MaskTextBox.Visibility          = Visibility.Visible;
                    MaskColorRectangle.Visibility   = Visibility.Visible;
                }
                else
                {
                    MaskHeadingTextBlock.Visibility = Visibility.Collapsed;
                    MaskTextBox.Visibility          = Visibility.Collapsed;
                    MaskColorRectangle.Visibility   = Visibility.Collapsed;
                }
            }
        }

        public float CurrentFilterValue
        {
            get { return _currentFilterValue; }
            set
            {
                _currentFilterValue = value;

                FilterSlider.Value = value;
                UpdateShownFilterValue();
            }
        }

        public Color CurrentMaskColor
        {
            get { return _currentMaskColor; }
            set
            {
                _currentMaskColor = value;

                MaskColorRectangle.Fill = new SolidColorBrush(CurrentMaskColor);
                MaskTextBox.Text = string.Format("{0:X2}{1:X2}{2:X2}", CurrentMaskColor.R, CurrentMaskColor.G, CurrentMaskColor.B);
            }
        }


        public string BaseFolder { get; set; }

        public event EventHandler MapSettingsChanged;


        public TextureMapSelectionControl(IMultiMapMaterial material, TextureMapTypes textureMapType, string baseFolder = null)
        {
            Material = material;
            _physicallyBasedMaterial = material as PhysicallyBasedMaterial;

            TextureMapType = textureMapType;
            
            if (baseFolder != null && !baseFolder.EndsWith("\\") && !baseFolder.EndsWith("/"))
                baseFolder += '\\';

            BaseFolder = baseFolder;

            _textureMapInfo = Material.TextureMaps.FirstOrDefault(m => m.MapType == TextureMapType);


            InitializeComponent();

            ShowTextureTextBox = true;



            if (_physicallyBasedMaterial != null)
            {
                ShowFilter = textureMapType == TextureMapTypes.Metalness ||
                             textureMapType == TextureMapTypes.Glossiness ||
                             textureMapType == TextureMapTypes.Roughness ||
                             textureMapType == TextureMapTypes.MetalnessRoughness ||
                             textureMapType == TextureMapTypes.AmbientOcclusion;

                ShowMask = textureMapType == TextureMapTypes.Albedo ||
                           textureMapType == TextureMapTypes.BaseColor ||
                           textureMapType == TextureMapTypes.DiffuseColor;

                if (textureMapType == TextureMapTypes.Metalness || textureMapType == TextureMapTypes.MetalnessRoughness)
                    CurrentFilterValue = _physicallyBasedMaterial.Metalness;

                if (textureMapType == TextureMapTypes.Roughness || textureMapType == TextureMapTypes.MetalnessRoughness)
                    CurrentFilterValue = _physicallyBasedMaterial.Roughness;

                if (ShowMask)
                    CurrentMaskColor = _physicallyBasedMaterial.BaseColor.ToWpfColor();
            }
            else
            {
                ShowFilter = false;
                ShowMask = false;
            }

            // To add support for drop into TextBox, we need to use PreviewDrop and PreviewDragOver events in DragAndDropHelper
            //var dragAndDropHelper = new DragAndDropHelper(FileNameTextBox, ".*");
            //dragAndDropHelper.FileDroped += delegate (object sender, FileDroppedEventArgs e)
            //{
            //    FileNameTextBox.Text = e.FileName;
            //    MasterCheckBox.IsChecked = true;

            //    LoadCurrentTexture();
            //};


            MapTypeTextBlock.Text = textureMapType.ToString();

            if (_textureMapInfo == null)
            {
                FileNameTextBox.Text = "";
                TextureCheckBox.IsChecked = false;
            }
            else
            {
                if (BaseFolder != null && _textureMapInfo.TextureResourceName.StartsWith(BaseFolder))
                    FileNameTextBox.Text = _textureMapInfo.TextureResourceName.Substring(BaseFolder.Length);
                else
                    FileNameTextBox.Text = _textureMapInfo.TextureResourceName;

                TextureCheckBox.IsChecked = true;
            }

            UpdateMaskHeadingTextBlock();
        }


        private void LoadCurrentTexture()
        {
            if (this.DXDevice == null)
                return;

            bool hasChanges = false;


            FileNameTextBox.ClearValue(ForegroundProperty);
            FileNameTextBox.ToolTip = null;


            if (TextureCheckBox.IsChecked ?? false)
            {
                var fileName = FileNameTextBox.Text;

                if (!string.IsNullOrEmpty(fileName))
                {
                    if (!fileName.StartsWith("*") && BaseFolder != null && !System.IO.Path.IsPathRooted(fileName))
                        fileName = System.IO.Path.Combine(BaseFolder, fileName);

                    var physicallyBasedMaterial = Material as PhysicallyBasedMaterial;

                    bool isUpdated = AssimpPbrHelper.UpdatePbrMap(physicallyBasedMaterial, this.DXDevice, this.AssimpScene, fileName, BaseFolder, TextureMapType, true, this.TexturesCache);

                    if (isUpdated)
                    {
                        if (CurrentMaskColor == Colors.Black)
                            CurrentMaskColor = Colors.White;

                        if (CurrentFilterValue <= 0.01)
                            CurrentFilterValue = 1.0f;

                        hasChanges = true;
                    }
                    else
                    {
                        FileNameTextBox.Foreground = Brushes.Red;
                        FileNameTextBox.ToolTip = fileName + " does not exist!";
                        return;
                    }
                }
            }
            else
            {
                var textureMaps = Material.TextureMaps;
                for (var i = 0; i < textureMaps.Count; i++)
                {
                    if (textureMaps[i].MapType == TextureMapType)
                    {
                        textureMaps.RemoveAt(i);
                        hasChanges = true;
                        break;
                    }
                }
            }


            if (hasChanges)
                OnMapSettingsChanged();
        }

        private void OpenFileButton_OnClick(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new Microsoft.Win32.OpenFileDialog();
            
            openFileDialog.Filter = "All texture files (*.*)|*.*";
            openFileDialog.Title  = "Select texture file";

            if (!string.IsNullOrEmpty(BaseFolder) && System.IO.Directory.Exists(BaseFolder))
                openFileDialog.InitialDirectory = BaseFolder;


            if ((openFileDialog.ShowDialog() ?? false) && !string.IsNullOrEmpty(openFileDialog.FileName))
            {
                TextureCheckBox.IsChecked = true;
                FileNameTextBox.Text = openFileDialog.FileName;

                LoadCurrentTexture();
            }
        }

        protected void OnMapSettingsChanged()
        {
            if (MapSettingsChanged != null)
                MapSettingsChanged(this, null);
        }

        private void OnTextureCheckBoxCheckedChanged(object sender, RoutedEventArgs e)
        {
            if (!this.IsLoaded)
                return;

            LoadCurrentTexture();

            UpdateMaskHeadingTextBlock();
        }

        private void UpdateMaskHeadingTextBlock()
        {
            if (TextureCheckBox.IsChecked ?? false)
                MaskHeadingTextBlock.Text = "Mask: #";
            else
                MaskHeadingTextBlock.Text = "Color: #";
        }

        private void UpdateShownFilterValue()
        {
            FilterValueTextBlock.Text = String.Format("{0:0}%", FilterSlider.Value * 100);

            OnMapSettingsChanged();
        }

        private void FilterSlider_OnValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!this.IsLoaded)
                return;

            CurrentFilterValue = (float)FilterSlider.Value;
            UpdateShownFilterValue();

            if (_physicallyBasedMaterial != null)
            {
                if (TextureMapType == TextureMapTypes.Metalness || TextureMapType == TextureMapTypes.MetalnessRoughness)
                    _physicallyBasedMaterial.Metalness = CurrentFilterValue;

                if (TextureMapType == TextureMapTypes.Roughness || TextureMapType == TextureMapTypes.MetalnessRoughness)
                    _physicallyBasedMaterial.Roughness = CurrentFilterValue;

                if (TextureMapType == TextureMapTypes.AmbientOcclusion)
                    _physicallyBasedMaterial.AmbientOcclusionFactor = CurrentFilterValue;

                OnMapSettingsChanged();
            }
        }

        private void MaskTextBox_OnTextChanged(object sender, TextChangedEventArgs e)
        {
            bool isMaskValid = false;

            try
            {
                // Usinig color converter is not a good solution because in case of an invalid text it throws an exception
                // and this moves focus from sample application into Visual Studio
                //if (_colorConverter == null)
                //    _colorConverter = System.ComponentModel.TypeDescriptor.GetConverter(typeof(Color));
                //
                //CurrentMaskColor = (Color)_colorConverter.ConvertFromString(MaskTextBox.Text);

                var maskText = MaskTextBox.Text.Trim();
                if (maskText.Length == 6)
                {
                    int red   = HexToByte(maskText, 0);
                    int green = HexToByte(maskText, 2);
                    int blue  = HexToByte(maskText, 4);

                    if (red != -1 && green != -1 && blue != -1)
                    {
                        CurrentMaskColor = Color.FromRgb((byte) red, (byte) green, (byte) blue);
                        MaskColorRectangle.Fill = new SolidColorBrush(CurrentMaskColor);

                        isMaskValid = true;
                    }
                }
            }
            catch
            {
                isMaskValid = false;
            }

            if (isMaskValid)
            {
                MaskTextBox.ClearValue(ForegroundProperty);

                if (_physicallyBasedMaterial != null)
                {
                    if (TextureMapType == TextureMapTypes.Albedo || TextureMapType == TextureMapTypes.BaseColor || TextureMapType == TextureMapTypes.DiffuseColor)
                        _physicallyBasedMaterial.BaseColor = CurrentMaskColor.ToColor4();

                    OnMapSettingsChanged();
                }

                OnMapSettingsChanged();
            }
            else
            {
                MaskTextBox.Foreground = Brushes.Red;
            }
        }

        // Returns -1 if not valid
        private int HexCharToInt(string text, int index)
        {
            if (index >= text.Length)
                return -1;


            char ch = text[index];

            if (ch >= '0' && ch <= '9')
                return ch - '0';

            if (ch >= 'A' && ch <= 'F')
                return ch - 'A' + 10;

            if (ch >= 'a' && ch <= 'f')
                return ch - 'a' + 10;

            return -1;
        }

        // Returns -1 if not valid
        private int HexToByte(string text, int index)
        {
            int v1 = HexCharToInt(text, index);
            int v2 = HexCharToInt(text, index + 1);

            if (v1 == -1 || v2 == -1)
                return -1;

            return v1 * 16 + v2;
        }
    }
}