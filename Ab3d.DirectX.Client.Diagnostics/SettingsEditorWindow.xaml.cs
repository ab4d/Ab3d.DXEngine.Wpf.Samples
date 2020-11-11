using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using SharpDX.Direct3D11;

namespace Ab3d.DirectX.Client.Diagnostics
{
    /// <summary>
    /// Interaction logic for SettingsEditorWindow.xaml
    /// </summary>
    public partial class SettingsEditorWindow : Window
    {
        public readonly DXScene CurrentDXScene;

        public event EventHandler ValueChanged;

        private static readonly string _dxScenePropertiesList =
@"MaxBackgroundThreadsCount
IsCachingCommandLists

IsAutomaticallyUpdatingBeforeEachRender

UseGeometryShaderFor3DLines
RenderAntialiased3DLines
RenderConnectedLinesAsDisconnected
  
IsMaterialSortingEnabled
IsTransparencySortingEnabled
  
OptimizeNearAndFarCameraPlanes";
        

        public SettingsEditorWindow(DXScene dxScene)
        {
            if (dxScene == null)
                return;

            CurrentDXScene = dxScene;

            InitializeComponent();

            CreateDXSceneSettings();
        }
        
        private void CreateDXSceneSettings()
        {
            var propertiesArray = _dxScenePropertiesList.Split(new string[] {"\r\n"}, StringSplitOptions.None);

            SettingsGrid.Children.Clear();
            SettingsGrid.RowDefinitions.Clear();

            bool addExtraTopMargin = false;
            foreach (var onePropertyName in propertiesArray)
            {
                if (onePropertyName.Trim().Length == 0)
                {
                    addExtraTopMargin = true; // extra margin will be added to the next added control
                    continue;
                }

                var propertyInfo = typeof(DXScene).GetProperty(onePropertyName);
                if (propertyInfo == null)
                {
                    MessageBox.Show($"SettingsEditorWindow:\r\nCannot find {onePropertyName} in DXScene");
                    continue;
                }

                SettingsGrid.RowDefinitions.Add(new RowDefinition() { Height = GridLength.Auto });
                int newRowIndex = SettingsGrid.RowDefinitions.Count - 1;

                var textBlock = new TextBlock()
                {
                    Text              = onePropertyName,
                    Margin            = new Thickness(0, addExtraTopMargin ? 10 : 3, 5, 0),
                    VerticalAlignment = VerticalAlignment.Center
                };

                var editorControl = CreateEditorControl(propertyInfo);
                if (editorControl == null)
                    continue; // Not supported type

                editorControl.HorizontalAlignment = HorizontalAlignment.Left;
                editorControl.Margin = new Thickness(0, addExtraTopMargin ? 10 : 3, 5, 0);

                
                Grid.SetColumn(textBlock, 0);
                Grid.SetRow(textBlock, newRowIndex);

                Grid.SetColumn(editorControl, 1);
                Grid.SetRow(editorControl, newRowIndex);

                SettingsGrid.Children.Add(textBlock);
                SettingsGrid.Children.Add(editorControl);

                addExtraTopMargin = false;
            }
        }
        
        private FrameworkElement CreateEditorControl(PropertyInfo propertyInfo)
        {
            FrameworkElement editorControl;

            var propertyValue = propertyInfo.GetValue(CurrentDXScene, null);

            if (propertyInfo.PropertyType == typeof(Boolean))
            {
                editorControl = CreateBooleanEditorControl(propertyInfo, propertyValue);
            }
            else if (propertyInfo.PropertyType == typeof(Int32))
            {
                editorControl = CreateInt32EditorControl(propertyInfo, propertyValue);
            }
            else
            {
                // Not supported type
                editorControl = null;
            }

            return editorControl;
        }

        private FrameworkElement CreateBooleanEditorControl(PropertyInfo propertyInfo, object propertyValue)
        {
            var checkBox = new CheckBox();
            checkBox.IsChecked = (bool) propertyValue;

            checkBox.Checked += delegate(object sender, RoutedEventArgs args)
            {
                propertyInfo.SetValue(CurrentDXScene, true, null);
                OnValueChanged();
            };


            checkBox.Unchecked += delegate (object sender, RoutedEventArgs args)
            {
                propertyInfo.SetValue(CurrentDXScene, false, null);
                OnValueChanged();
            };

            return checkBox;
        }

        private void BooleanCheckBoxOnCheckedChanged(object sender, RoutedEventArgs e)
        {
            throw new NotImplementedException();
        }

        private FrameworkElement CreateInt32EditorControl(PropertyInfo propertyInfo, object propertyValue)
        {
            var textBox = new TextBox()
            {
                Text = propertyValue.ToString(),
                Width = 40,
            };

            textBox.TextChanged += delegate(object sender, TextChangedEventArgs args)
            {
                string valueText = textBox.Text;

                if (valueText.Trim().Length == 0)
                    return;


                int newValue;

                if (Int32.TryParse(valueText, out newValue))
                {
                    textBox.ClearValue(ForegroundProperty);
                    propertyInfo.SetValue(CurrentDXScene, newValue, null);

                    OnValueChanged();
                }
                else
                {
                    textBox.Foreground = Brushes.Red;
                }
            };

            return textBox;
        }

        protected void OnValueChanged()
        {
            if (ValueChanged != null)
                ValueChanged(this, null);
        }

        private void CloseButton_OnClick(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void RefreshButton_OnClick(object sender, RoutedEventArgs e)
        {
            CreateDXSceneSettings();
        }
    }
}
