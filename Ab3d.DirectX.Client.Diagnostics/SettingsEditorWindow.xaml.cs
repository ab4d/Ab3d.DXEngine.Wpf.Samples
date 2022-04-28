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
using Ab3d.DirectX.Controls;
using SharpDX.Direct3D11;

namespace Ab3d.DirectX.Client.Diagnostics
{
    /// <summary>
    /// Interaction logic for SettingsEditorWindow.xaml
    /// </summary>
    public partial class SettingsEditorWindow : Window
    {
        public readonly DXScene CurrentDXScene;
        public readonly DXView CurrentDXView;

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
        
        private static readonly string _dxViewPropertiesList =
@"IsAutomaticallyUpdatingDXScene
IsWaitingInBackgroundUntilRendered
UseTwoSidedMaterialsForSolidObjects";
        

        public SettingsEditorWindow(DXView dxView)
        {
            if (dxView == null)
                return;

            CurrentDXScene = dxView.DXScene;
            CurrentDXView = dxView;

            InitializeComponent();

            CreateDXSceneSettings();
            CreateDXViewSettings();
        }

        private void CreateDXSceneSettings()
        {
            DXSceneSettingsGrid.Children.Clear();
            DXSceneSettingsGrid.RowDefinitions.Clear();

            AddPropertySettings(_dxScenePropertiesList, CurrentDXScene, DXSceneSettingsGrid);
        }
        
        private void CreateDXViewSettings()
        {
            DXViewSettingsGrid.Children.Clear();
            DXViewSettingsGrid.RowDefinitions.Clear();

            if (CurrentDXView != null)
                AddPropertySettings(_dxViewPropertiesList, CurrentDXView, DXViewSettingsGrid);
        }

        private void AddPropertySettings(string propertyNames, object targetObject, Grid targetGrid)
        {
            var propertiesArray = propertyNames.Split(new string[] {"\r\n"}, StringSplitOptions.None);

            bool addExtraTopMargin = false;
            foreach (var onePropertyName in propertiesArray)
            {
                if (onePropertyName.Trim().Length == 0)
                {
                    addExtraTopMargin = true; // extra margin will be added to the next added control
                    continue;
                }

                var propertyInfo = targetObject.GetType().GetProperty(onePropertyName);
                FieldInfo fieldInfo;

                if (propertyInfo == null)
                {
                    fieldInfo = targetObject.GetType().GetField(onePropertyName); // This is required for DXViewportView.UseTwoSidedMaterialsForSolidObjects

                    if (fieldInfo == null)
                    {
                        MessageBox.Show($"SettingsEditorWindow:\r\nCannot find {onePropertyName} in {targetObject.GetType().Name}");
                        continue;
                    }
                }
                else
                {
                    fieldInfo = null;
                }

                targetGrid.RowDefinitions.Add(new RowDefinition() { Height = GridLength.Auto });
                int newRowIndex = targetGrid.RowDefinitions.Count - 1;

                var textBlock = new TextBlock()
                {
                    Text              = onePropertyName,
                    Margin            = new Thickness(0, addExtraTopMargin ? 10 : 3, 5, 0),
                    VerticalAlignment = VerticalAlignment.Center
                };

                FrameworkElement editorControl;

                if (propertyInfo != null)
                    editorControl = CreateEditorControl(propertyInfo, targetObject);
                else if (fieldInfo != null)
                    editorControl = CreateEditorControl(fieldInfo, targetObject);
                else
                    editorControl = null;

                if (editorControl == null)
                    continue; // Not supported type

                editorControl.HorizontalAlignment = HorizontalAlignment.Left;
                editorControl.Margin = new Thickness(0, addExtraTopMargin ? 10 : 3, 5, 0);

                
                Grid.SetColumn(textBlock, 0);
                Grid.SetRow(textBlock, newRowIndex);

                Grid.SetColumn(editorControl, 1);
                Grid.SetRow(editorControl, newRowIndex);

                targetGrid.Children.Add(textBlock);
                targetGrid.Children.Add(editorControl);

                addExtraTopMargin = false;
            }
        }
        
        private FrameworkElement CreateEditorControl(PropertyInfo propertyInfo, object targetObject)
        {
            FrameworkElement editorControl;

            var propertyValue = propertyInfo.GetValue(targetObject, null);

            if (propertyInfo.PropertyType == typeof(Boolean))
            {
                editorControl = CreateBooleanEditorControl(propertyInfo, targetObject, propertyValue);
            }
            else if (propertyInfo.PropertyType == typeof(Int32))
            {
                editorControl = CreateInt32EditorControl(propertyInfo, targetObject, propertyValue);
            }
            else
            {
                // Not supported type
                editorControl = null;
            }

            return editorControl;
        }

        private FrameworkElement CreateBooleanEditorControl(PropertyInfo propertyInfo, object targetObject, object propertyValue)
        {
            var checkBox = new CheckBox();
            checkBox.IsChecked = (bool) propertyValue;

            checkBox.Checked += delegate(object sender, RoutedEventArgs args)
            {
                propertyInfo.SetValue(targetObject, true, null);
                OnValueChanged();
            };


            checkBox.Unchecked += delegate (object sender, RoutedEventArgs args)
            {
                propertyInfo.SetValue(targetObject, false, null);
                OnValueChanged();
            };

            return checkBox;
        }

        private FrameworkElement CreateInt32EditorControl(PropertyInfo propertyInfo, object targetObject, object propertyValue)
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
                    propertyInfo.SetValue(targetObject, newValue, null);

                    OnValueChanged();
                }
                else
                {
                    textBox.Foreground = Brushes.Red;
                }
            };

            return textBox;
        }
        
        private FrameworkElement CreateEditorControl(FieldInfo fieldInfo, object targetObject)
        {
            FrameworkElement editorControl;

            if (fieldInfo.IsStatic)
                targetObject = null;

            var propertyValue = fieldInfo.GetValue(targetObject);

            if (fieldInfo.FieldType == typeof(Boolean))
            {
                editorControl = CreateBooleanEditorControl(fieldInfo, targetObject, propertyValue);
            }
            else if (fieldInfo.FieldType == typeof(Int32))
            {
                editorControl = CreateInt32EditorControl(fieldInfo, targetObject, propertyValue);
            }
            else
            {
                // Not supported type
                editorControl = null;
            }

            return editorControl;
        }

        private FrameworkElement CreateBooleanEditorControl(FieldInfo fieldInfo, object targetObject, object propertyValue)
        {
            var checkBox = new CheckBox();
            checkBox.IsChecked = (bool) propertyValue;

            checkBox.Checked += delegate(object sender, RoutedEventArgs args)
            {
                fieldInfo.SetValue(targetObject, true);
                OnValueChanged();
            };


            checkBox.Unchecked += delegate (object sender, RoutedEventArgs args)
            {
                fieldInfo.SetValue(targetObject, false);
                OnValueChanged();
            };

            return checkBox;
        }

        private FrameworkElement CreateInt32EditorControl(FieldInfo fieldInfo, object targetObject, object propertyValue)
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
                    fieldInfo.SetValue(targetObject, newValue);

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
