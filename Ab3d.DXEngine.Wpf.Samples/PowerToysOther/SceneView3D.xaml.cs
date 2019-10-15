using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Ab3d.Cameras;
using Ab3d.Controls;
using Ab3d.DirectX;
using Ab3d.DirectX.Controls;
using Ab3d.DXEngine.Wpf.Samples.Common;
using Ab3d.Visuals;

namespace Ab3d.DXEngine.Wpf.Samples.PowerToysOther
{
    /// <summary>
    /// Interaction logic for SceneView3D.xaml
    /// </summary>
    [ContentProperty("Model3D")] // All content of this UserControl is set to the Model3D property
    public partial class SceneView3D : UserControl, INotifyPropertyChanged, IDisposable
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private bool _isContextMenuInitialized;

        private SceneViewType _selectedSceneViewType;

        public SceneViewType SelectedSceneViewType
        {
            get { return _selectedSceneViewType; }
            set
            {
                _selectedSceneViewType = value;

                if (_selectedSceneViewType != null && _selectedSceneViewType != SceneViewType.StandardCustomSceneView)
                {
                    Camera1.BeginInit(); // We use BeginInit and EndInit to update the camera only once

                    Camera1.Heading = _selectedSceneViewType.Heading;
                    Camera1.Attitude = _selectedSceneViewType.Attitude;

                    Camera1.EndInit();
                }

                OnPropertyChanged("SelectedSceneViewType");
            }
        }

        public DXViewportView MainDXViewportView;
        public Viewport3D MainViewport;
        public WireframeVisual3D WireframeVisual;

        public Viewport3D Viewport3D
        {
            get { return MainViewport; }
        }

        public TargetPositionCamera Camera
        {
            get { return Camera1; }
        }

        public MouseCameraController MouseCameraController
        {
            get { return MouseCameraController1; }
        }

        public WireframeVisual3D WireframeVisual3D
        {
            get { return WireframeVisual; }
        }

        public Model3D Model3D
        {
            get { return WireframeVisual.OriginalModel; }
            set { WireframeVisual.OriginalModel = value; }
        }

        public SceneView3D(DXDevice dxDevice)
        {
            InitializeComponent();

            MainViewport = new Viewport3D();
            MainViewport.IsHitTestVisible = false;

            MainDXViewportView = new DXViewportView(dxDevice, MainViewport);
            MainDXViewportView.BackgroundColor = Colors.Transparent;

            // Workaround for using one DXDevice with multiple DXScenes
            // We need to reset LastUsedFrameNumber stored in each effect
            // This ensures that the effect will be correctly initialized for each DXScene.
            // This will not be needed in the next version of DXEngine.
            MainDXViewportView.SceneRendered += delegate (object sender, EventArgs args)
            {
                if (MainDXViewportView.DXScene == null) // WPF 3D rendering is used
                    return;

                var allEffects = MainDXViewportView.DXScene.DXDevice.EffectsManager.Effects;
                var effectsCount = allEffects.Count;

                for (var i = 0; i < effectsCount; i++)
                    allEffects[i].ResetLastUsedFrameNumber();
            };


            WireframeVisual = new WireframeVisual3D()
            {
                WireframeType = WireframeVisual3D.WireframeTypes.OriginalSolidModel,
                LineColor = Colors.Black,
                LineThickness = 2,
                SolidModelColor = Colors.White
            };

            MainViewport.Children.Add(WireframeVisual);


            ViewportBorder.Child = MainDXViewportView;

            SetupViews();

            this.Loaded += delegate (object sender, RoutedEventArgs args)
            {
                UpdateViewType();
            };
        }

        private void SetupViews()
        {
            ViewTypeComboBox.ItemsSource = SceneViewType.StandardViews;

            SelectedSceneViewType = SceneViewType.StandardCustomSceneView;

            // When view type is set to perspective we do not update the camera so we do it here manually
            Camera1.Heading = SceneViewType.StandardCustomSceneView.Heading;
            Camera1.Attitude = SceneViewType.StandardCustomSceneView.Attitude;
        }

        // Checks correct RadioButtons in ContextMenu based on the current state of Camera and WireframeVisual
        private void InitializeContextMenu()
        {
            if (Camera1.CameraType == BaseCamera.CameraTypes.PerspectiveCamera)
                PerspectiveCameraCheckBox.IsChecked = true;
            else
                OrthographicCameraCheckBox.IsChecked = true;

            if (WireframeVisual.WireframeType == WireframeVisual3D.WireframeTypes.OriginalSolidModel)
            {
                SolidModelCheckBox.IsChecked = true;
            }
            else if (WireframeVisual.WireframeType == WireframeVisual3D.WireframeTypes.Wireframe)
            {
                if (WireframeVisual.UseModelColor)
                    WireframeOriginalColorsCheckBox.IsChecked = true;
                else
                    WireframeCheckBox.IsChecked = true;
            }
            else if (WireframeVisual.WireframeType == WireframeVisual3D.WireframeTypes.WireframeWithSingleColorSolidModel)
            {
                WireframeHiddenLinesCheckBox.IsChecked = true;
            }
            else if (WireframeVisual.WireframeType == WireframeVisual3D.WireframeTypes.WireframeWithOriginalSolidModel)
            {
                WireframeOriginalColorsCheckBox.IsChecked = true;
            }

            _isContextMenuInitialized = true;
        }

        private void UpdateViewType()
        {
            // Create local values (accessing DependencyProperties can be slow)
            double cameraHeading = Camera1.Heading;
            double cameraAttitude = Camera1.Attitude;

            // Check if the current camera match any view type 
            var matchedViewType = SceneViewType.StandardViews.FirstOrDefault(v => Math.Abs(v.Heading - cameraHeading) < 0.01 &&
                                                                                  Math.Abs(v.Attitude - cameraAttitude) < 0.01);

            if (matchedViewType == null)
                matchedViewType = SceneViewType.StandardCustomSceneView;

            if (SelectedSceneViewType != matchedViewType)
                SelectedSceneViewType = matchedViewType;
        }

        private void CameraChanged(object sender, RoutedEventArgs e)
        {
            if (!this.IsLoaded)
                return;

            UpdateViewType();
        }

        private void SettingsButton_OnMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (!_isContextMenuInitialized) // We need to initialize context menu here (if this is done in OnLoaded the settings are not preserved)
                InitializeContextMenu();

            SettingsMenu.Placement = PlacementMode.Bottom;
            SettingsMenu.PlacementTarget = SettingsButton;
            SettingsMenu.IsOpen = true;

            e.Handled = true;
        }

        private void CameraTypeCheckedChanged(object sender, RoutedEventArgs e)
        {
            if (!this.IsLoaded)
                return;

            var newCameraTypes = (PerspectiveCameraCheckBox.IsChecked ?? false) ? BaseCamera.CameraTypes.PerspectiveCamera : BaseCamera.CameraTypes.OrthographicCamera;

            Camera1.CameraType = newCameraTypes;

            SettingsMenu.IsOpen = false;
        }

        private void RenderingTypeCheckedChanged(object sender, RoutedEventArgs e)
        {
            if (!this.IsLoaded)
                return;

            WireframeVisual3D.WireframeTypes selectedWireframeType;


            if (WireframeCheckBox.IsChecked ?? false)
            {
                selectedWireframeType = WireframeVisual3D.WireframeTypes.Wireframe;
                WireframeVisual.UseModelColor = false;
            }
            else if (WireframeHiddenLinesCheckBox.IsChecked ?? false)
            {
                selectedWireframeType = WireframeVisual3D.WireframeTypes.WireframeWithSingleColorSolidModel;
                WireframeVisual.UseModelColor = false;
            }
            else if (WireframeSolidModelCheckBox.IsChecked ?? false)
            {
                selectedWireframeType = WireframeVisual3D.WireframeTypes.WireframeWithOriginalSolidModel;
                WireframeVisual.UseModelColor = false;
            }
            else if (WireframeOriginalColorsCheckBox.IsChecked ?? false)
            {
                selectedWireframeType = WireframeVisual3D.WireframeTypes.Wireframe;
                WireframeVisual.UseModelColor = true;
            }
            else // default and if SolidModelCheckBox.IsChecked ?? false
            {
                selectedWireframeType = WireframeVisual3D.WireframeTypes.OriginalSolidModel;
                WireframeVisual.UseModelColor = true;
            }



            WireframeVisual.WireframeType = selectedWireframeType;

            SettingsMenu.IsOpen = false;
        }


        protected virtual void OnPropertyChanged(string propertyName)
        {
            if (PropertyChanged != null)
                PropertyChanged.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public void Dispose()
        {
            if (MainDXViewportView != null)
            {
                MainDXViewportView.Dispose();
                MainDXViewportView = null;
            }

            MainViewport = null;
            WireframeVisual = null;
        }
    }
}
