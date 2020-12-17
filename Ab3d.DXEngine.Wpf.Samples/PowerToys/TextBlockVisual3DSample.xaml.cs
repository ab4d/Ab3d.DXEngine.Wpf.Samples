using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Ab3d.DXEngine.Wpf.Samples.Controls;
using Ab3d.Visuals;

namespace Ab3d.DXEngine.Wpf.Samples.PowerToys
{
    /// <summary>
    /// Interaction logic for TextBlockVisual3DSample.xaml
    /// </summary>
    public partial class TextBlockVisual3DSample : Page
    {
        public TextBlockVisual3DSample()
        {
            InitializeComponent();

            // When setting multiple properties of TextBlockVisual3D
            // it is highly recommended to use BeginInit and EndInit.
            // This way the settings are applied only once and this can improve performance.
            TextBlockVisual1.BeginInit();


            TextTextBox.Text =
@"TextBlockVisual3D
provides the easiest way to show
3D text and has a lot of options to
control text and border style.";


            var positionTextBlock = AddComboBox(OptionsGrid, TextBlockVisual3D.PositionProperty, 0, new string[] { "0 0 0", "-50 0 0", "0 50 0", "50 0 0" },
                "Position property defines the position of the Text. See also PositionType to see what point on the 3D model the Position property represents.\nSelected position is marked with a red cross.",
                2, null, OnPositionChanged);

            positionTextBlock.Foreground = Brushes.Red;

            AddComboBox(OptionsGrid, TextBlockVisual3D.PositionTypeProperty, 0, new string[] { "Center", "Left", "Right", "Top", "Bottom", "TopLeft", "BottomLeft", "TopRight", "BottomRight" },
                "PositionType specifies what point on the 3D model the Position property represents.");

            AddComboBox(OptionsGrid, TextBlockVisual3D.TextDirectionProperty, 0, new string[] { "1 0 0", "0 0 1", "0 1 0", "1 0 1" },
                "TextDirection property is a Vector3D that defines the direction in 3D space in which the text is drawn. Note that TextDirection and UpDirection must not be the same.");

            AddComboBox(OptionsGrid, TextBlockVisual3D.UpDirectionProperty, 0, new string[] { "0 1 0", "0 0 -1", "1 0 0" },
                "UpDirection property is a Vector3D that defines the text up direction in 3D space. Note that TextDirection and UpDirection must not be the same.");


            AddComboBox(OptionsGrid, TextBlockVisual3D.SizeProperty, 0, new string[] { "Size.Empty", "0 20", "0 40", "100 0", "200 0", "200 40", "200 100" },
                "Size property defines the size of 3D model that shows the text.\nNote that it is possible to define only Width or only Height or even set Size to Empty.\nIn case when one Size property is not defined, then it is calculated from the other Size property and the measured text size or BorderSize property. When Size is Empty, then both Width and Height are get from measured text size or BorderSize property.\nThis way it is easy to specify only the desired text height in 3D space and the TextBlockVisual3D will automatically calculate the width of the model.",
                8,
                resolveItemText: delegate (string itemText)
                {
                    if (itemText == "Size.Empty")
                        return (object)Size.Empty;

                    return null;
                });


            AddComboBox(OptionsGrid, TextBlockVisual3D.FontSizeProperty, 0, new string[] { "10", "12", "16", "20", "30" },
                "FontSize defines the size of the text in TextBlock element.", 8);

            AddComboBox(OptionsGrid, TextBlockVisual3D.ForegroundProperty, 1, new string[] { "Black", "Green", "Yellow", "White", "LightBlue" },
                "Foreground defines the foreground brush of the text in TextBlock element.");

            //AddComboBox(OptionsGrid, TextBlockVisual3D.FontWeightProperty, 1, new string[] { "Normal", "Bold", "Black" },
            //    "FontWeight defines the weight of the text in TextBlock element.");

            //AddComboBox(OptionsGrid, TextBlockVisual3D.FontFamilyProperty, 1, new string[] { "Arial", "Consolas", "Times New Roman" },
            //    "FontFamily defines the font family of the text in TextBlock element.");


            AddComboBox(OptionsGrid, TextBlockVisual3D.BackgroundProperty, 3, new string[] { "null", "Transparent", "White", "LightGray", "LightBlue", "Yellow" },
                "Background defines the Brush that fill the background of the Border element.\nNote that when Background is null or set to Transparent brush, you need to define the TextBlockVisual3D object after all other solid 3D models. If this is not done that 3D models that are defined after the TextBlockVisual3D object will not be visible through the transparent text background (see Transparency problem in Utilities section for more info).");

            AddComboBox(OptionsGrid, TextBlockVisual3D.BorderBrushProperty, 6, new string[] { "null", "Transparent", "White", "LightGray", "Black", "LightBlue", "Yellow" },
                "BorderBrush defines the Brush that will be used to show the border around the text.", 8);

            AddComboBox(OptionsGrid, TextBlockVisual3D.BorderThicknessProperty, 1, new string[] { "0", "1", "2", "3", "4", "5" },
                "BorderThickness defines the thickness of the border around the text.");

            AddComboBox(OptionsGrid, TextBlockVisual3D.TextPaddingProperty, 4, new string[] { "0", "1", "2", "5", "3 0", "5 3", "5 4 3 2" },
                "Thickness that specifies the padding of the TextBlock element inside the Border element.");


            //AddComboBox(OptionsGrid, TextBlockVisual3D.BorderSizeProperty, 0, new string[] { "Size.Empty", "0 20", "0 40", "100 0", "200 0", "200 40", "200 100" },
            //    "BorderSize can be set to define the 2D size of the Border element. By default BorderSize is set to Size.Empty. This automatically scales the Border element to show the whole TextBlock element. But when you want to trim the text, you need to set the BorderSize to a valid size value.",
            //    8,
            //    resolveItemText: delegate (string itemText)
            //    {
            //        if (itemText == "Size.Empty")
            //            return (object)Size.Empty;

            //        return null;
            //    });

            AddComboBox(OptionsGrid, TextBlockVisual3D.IsTwoSidedTextProperty, 0, new string[] { "true", "false" },
                "IsTwoSidedText property specifies if the plane 3D models shows text on both front and back sides.", 8);

            AddComboBox(OptionsGrid, TextBlockVisual3D.IsBackSidedTextFlippedProperty, 0, new string[] { "true", "false" },
                "IsBackSidedTextFlipped property specifies if the text on the back side is horizontally flipped so that it appears correct when viewing from the back side.");


            AddComboBox(OptionsGrid, TextBlockVisual3D.RenderBitmapSizeProperty, 3, new string[] { "Size.Empty", "128 64", "256 128", "512 256", "1024 512" },
                "When RenderBitmapSize is set to a valid size value (not Empty or with 0 Width or Height), then TextBlock and Border elements are rendered to bitmap and that bitmap is then shown on a 3D model. When set to Empty then TextBlockVisual3D is using a DiffuseMaterial with a VisualBrush that show TextBlock and Border.\n\nSetting RenderBitmapSize can increase initialization time but can significantly improve rendering performance.\n\nDefault value is Size.Empty; but when rendered with Ab3d.DXEngine the default value is set to TextBlockVisual3D.DefaultDXEngineRenderBitmapSize (512 x 256)",
                8,
                resolveItemText: delegate (string itemText)
                {
                    if (itemText == "Size.Empty")
                        return (object)Size.Empty;

                    return null;
                });


            TextBlockVisual1.EndInit();


            var textBlock2 = AddTextBlock(OptionsGrid, "Additional TextBlockVisual3D properties:");
            textBlock2.Margin = new Thickness(0, 10, 0, 0);

            var textBlock3 = AddTextBlock(OptionsGrid, "FontWeight, FontFamily, BorderSize, TextWrapping, TextTrimming, TextHorizontalAlignment, TextVerticalAlignment, TextAlignment.");
            textBlock3.FontWeight = FontWeights.Normal;
            textBlock3.TextWrapping = TextWrapping.Wrap;
            textBlock3.MaxWidth = 300;


            this.Loaded += delegate (object sender, RoutedEventArgs args)
            {
                TextTextBox.Focus();
                TextTextBox.CaretIndex = TextTextBox.Text.Length;
            };
        }

        private void OnPositionChanged(ComboBox comboBox, object newSelectedValue)
        {
            if (newSelectedValue is Point3D)
            {
                var newPosition = (Point3D)newSelectedValue;
                PositionWireCross.Position = newPosition;
            }
        }

        private TextBlock AddComboBox(Grid parentGrid, DependencyProperty dependencyProperty, int initialItemIndex, string[] comboBoxItems, string tooltipText,
                                 double topMargin = 2,
                                 Func<string, object> resolveItemText = null,
                                 Action<ComboBox, object> selectionChanged = null)
        {
            parentGrid.RowDefinitions.Add(new RowDefinition() { Height = new GridLength(1, GridUnitType.Auto) });
            int rowIndex = parentGrid.RowDefinitions.Count - 1;


            var textBlock = new TextBlock()
            {
                Text = dependencyProperty.Name + ':',
                VerticalAlignment = VerticalAlignment.Center,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, topMargin, 0, 2)
            };

            Grid.SetColumn(textBlock, 0);
            Grid.SetRow(textBlock, rowIndex);
            parentGrid.Children.Add(textBlock);


            var comboBox = new ComboBox()
            {
                Margin = new Thickness(5, topMargin, 10, 2)
            };

            SetupComboBox(TextBlockVisual1, dependencyProperty, comboBox, initialItemIndex, comboBoxItems, resolveItemText, selectionChanged);

            Grid.SetColumn(comboBox, 1);
            Grid.SetRow(comboBox, rowIndex);
            parentGrid.Children.Add(comboBox);


            if (!string.IsNullOrEmpty(tooltipText))
            {
                var infoControl = new InfoControl()
                {
                    InfoText = tooltipText,
                    InfoWidth = 400,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, topMargin, 0, 2)
                };

                Grid.SetColumn(infoControl, 2);
                Grid.SetRow(infoControl, rowIndex);
                parentGrid.Children.Add(infoControl);
            }

            return textBlock;
        }

        private TextBlock AddTextBlock(Grid parentGrid, string text)
        {
            parentGrid.RowDefinitions.Add(new RowDefinition() { Height = new GridLength(1, GridUnitType.Auto) });
            int rowIndex = parentGrid.RowDefinitions.Count - 1;


            var textBlock = new TextBlock()
            {
                Text = text,
                VerticalAlignment = VerticalAlignment.Center,
                FontWeight = FontWeights.Bold,
            };

            Grid.SetColumn(textBlock, 0);
            Grid.SetColumnSpan(textBlock, 2);
            Grid.SetRow(textBlock, rowIndex);
            parentGrid.Children.Add(textBlock);

            return textBlock;
        }

        public static void SetupComboBox(DependencyObject dependencyObject, DependencyProperty dependencyProperty, ComboBox comboBox, int initialItemIndex, string[] comboBoxItems,
                                         Func<string, object> resolveItemText = null,
                                         Action<ComboBox, object> selectionChanged = null)
        {
            var propertyType = dependencyProperty.PropertyType;
            var typeConverter = System.ComponentModel.TypeDescriptor.GetConverter(propertyType);

            if (typeConverter == null)
                throw new Exception("Cannot get TypeConverter for " + propertyType.Name);

            comboBox.SelectionChanged += delegate (object sender, SelectionChangedEventArgs args)
            {
                string selectedItemText = (string)comboBox.SelectedItem;

                object selectedValue;

                if (resolveItemText != null)
                    selectedValue = resolveItemText(selectedItemText);
                else
                    selectedValue = null;

                if (selectedValue == null)
                {
                    if (string.IsNullOrEmpty(selectedItemText) || selectedItemText.Equals("null", StringComparison.OrdinalIgnoreCase))
                    {
                        if (propertyType.IsValueType && Nullable.GetUnderlyingType(propertyType) == null)
                            selectedValue = Activator.CreateInstance(propertyType);
                    }
                    else
                    {
                        selectedValue = typeConverter.ConvertFromString(selectedItemText);
                    }
                }

                dependencyObject.SetValue(dependencyProperty, selectedValue);

                if (selectionChanged != null)
                    selectionChanged(comboBox, selectedValue);
            };

            comboBox.ItemsSource = comboBoxItems;
            comboBox.SelectedIndex = initialItemIndex;
        }

        private void AlignWithCameraButton_OnClick(object sender, RoutedEventArgs e)
        {
            TextBlockVisual1.AlignWithCamera(Camera1);
        }
    }
}
