using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
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
using Ab3d.DirectX;
using Ab3d.DirectX.PostProcessing;
using Ab3d.Visuals;
using SharpDX;

namespace Ab3d.DXEngine.Wpf.Samples.DXEngineVisuals
{
    /// <summary>
    /// Interaction logic for PostProcessingTest.xaml
    /// </summary>
    public partial class PostProcessingTest : Page
    {
        private List<PostProcessBase> _createdPostProcesses;

        private GaussianBlurPostProcess _gaussianHorizontalBlurPostProcess;
        private GaussianBlurPostProcess _gaussianVerticalBlurPostProcess;
        private SimpleBlurPostProcess _simpleBlurHorizontalBlurPostProcess;
        private SimpleBlurPostProcess _simpleBlurVerticalBlurPostProcess;
        private ExpandPostProcess _expandHorizontalPostProcess;
        private ExpandPostProcess _expandVerticalPostProcess;
        private SoberEdgeDetectionPostProcess _edgeDetectionPostProcess;
        private GammaCorrectionPostProcess _gammaCorrectionPostProcess;

        public PostProcessingTest()
        {
            InitializeComponent();

            _createdPostProcesses = new List<PostProcessBase>();

            MainDXViewportView.DXSceneDeviceCreated += delegate(object sender, EventArgs args)
            {
                // After the DirectX device has been initialized and the DXScene is created, we can add initial post processes:
                UpdatePostProcesses();
            };

            // IMPORTANT:
            // It is very important to call Dispose method on DXSceneView after the control is not used any more (see help file for more info)
            this.Unloaded += (sender, args) =>
            {
                // Because we have created the PostProcess objects here, we also need to dipose them
                DisposeCreatedPostProcesses();

                // Now we can also dispose the MainDXViewportView
                MainDXViewportView.Dispose();
            };
        }

        private void PostProcessesCheckBoxChanged(object sender, RoutedEventArgs e)
        {
            if (!this.IsLoaded)
                return;

            UpdatePostProcesses();
        }

        private void UpdatePostProcesses()
        {
            if (MainDXViewportView.DXScene == null)
                return; // Probably WPF 3D rendering

            // Because we have created the PostProcess objects here, we also need to dipose them
            DisposeCreatedPostProcesses();

            MainDXViewportView.DXScene.PostProcesses.Clear();

            if (ToonCheckBox.IsChecked ?? false)
            {
                var toonShadingPostProcess = new ToonShadingPostProcess();
                MainDXViewportView.DXScene.PostProcesses.Add(toonShadingPostProcess);

                _createdPostProcesses.Add(toonShadingPostProcess);
            }

            if (BlackAndWhiteCheckBox.IsChecked ?? false)
            {
                var blackAndWhitePostProcess = new BlackAndWhitePostProcess();
                MainDXViewportView.DXScene.PostProcesses.Add(blackAndWhitePostProcess);

                _createdPostProcesses.Add(blackAndWhitePostProcess);
            }

            if (SimpleBlurCheckBox.IsChecked ?? false)
            {
                // Blur is done in two passes - one horizontal and one vertical
                _simpleBlurHorizontalBlurPostProcess = new Ab3d.DirectX.PostProcessing.SimpleBlurPostProcess(isVerticalBlur: false, filterWidth: 5);
                _simpleBlurVerticalBlurPostProcess = new Ab3d.DirectX.PostProcessing.SimpleBlurPostProcess(isVerticalBlur: true, filterWidth: 5);

                UpdateSimpleBlurParameters();

                MainDXViewportView.DXScene.PostProcesses.Add(_simpleBlurHorizontalBlurPostProcess);
                MainDXViewportView.DXScene.PostProcesses.Add(_simpleBlurVerticalBlurPostProcess);

                _createdPostProcesses.Add(_simpleBlurHorizontalBlurPostProcess);
                _createdPostProcesses.Add(_simpleBlurVerticalBlurPostProcess);
            }

            if (GaussianBlurCheckBox.IsChecked ?? false)
            {
                // Blur is done in two passes - one horizontal and one vertical
                _gaussianHorizontalBlurPostProcess = new Ab3d.DirectX.PostProcessing.GaussianBlurPostProcess(isVerticalBlur: false, blurStandardDeviation: 2.0f);
                _gaussianVerticalBlurPostProcess   = new Ab3d.DirectX.PostProcessing.GaussianBlurPostProcess(isVerticalBlur: true, blurStandardDeviation: 2.0f);

                UpdateGaussianBlurParameters();

                MainDXViewportView.DXScene.PostProcesses.Add(_gaussianHorizontalBlurPostProcess);
                MainDXViewportView.DXScene.PostProcesses.Add(_gaussianVerticalBlurPostProcess);

                _createdPostProcesses.Add(_gaussianHorizontalBlurPostProcess);
                _createdPostProcesses.Add(_gaussianVerticalBlurPostProcess);
            }

            if (ExpandCheckBox.IsChecked ?? false)
            {
                int expansionWidth = (int)ExpansionWidthSlider.Value;
                var backgroundColor = MainDXViewportView.DXScene.BackgroundColor; // Note that you should not use MainDXViewportView.BackgroundColor because it is not yet alpha premultiplied - for example (1,1,1,0) is used instead of (0,0,0,0).

                _expandHorizontalPostProcess = new Ab3d.DirectX.PostProcessing.ExpandPostProcess(false, expansionWidth, backgroundColor);
                _expandVerticalPostProcess   = new Ab3d.DirectX.PostProcessing.ExpandPostProcess(true,  expansionWidth, backgroundColor);

                if (FixExpansionColorCheckBox.IsChecked ?? false)
                {
                    // With Offsets and Factors we can adjust the colors of the effect.
                    // Offsets are added to each color and then the color is multiplied by Factors.
                    //
                    // The following values render expansion in red color
                    _expandHorizontalPostProcess.Offsets = new Vector4(1, 0, 0, 1); // RGBA values: add 1 to red and alpha
                    _expandHorizontalPostProcess.Factors = new Vector4(1, 0, 0, 1); // RGBA values: set green and blur to 0

                    _expandVerticalPostProcess.Offsets = new Vector4(1, 0, 0, 1);
                    _expandVerticalPostProcess.Factors = new Vector4(1, 0, 0, 1);
                }

                UpdateExpandParameters();

                MainDXViewportView.DXScene.PostProcesses.Add(_expandHorizontalPostProcess);
                MainDXViewportView.DXScene.PostProcesses.Add(_expandVerticalPostProcess);

                _createdPostProcesses.Add(_expandHorizontalPostProcess);
                _createdPostProcesses.Add(_expandVerticalPostProcess);
            }

            if (EdgeDetectionCheckBox.IsChecked ?? false)
            {
                _edgeDetectionPostProcess = new Ab3d.DirectX.PostProcessing.SoberEdgeDetectionPostProcess();

                UpdateEdgeDetectionParameters();

                MainDXViewportView.DXScene.PostProcesses.Add(_edgeDetectionPostProcess);
                _createdPostProcesses.Add(_edgeDetectionPostProcess);
            }
            
            if (GammaCorrectionCheckBox.IsChecked ?? false)
            {
                _gammaCorrectionPostProcess = new Ab3d.DirectX.PostProcessing.GammaCorrectionPostProcess();

                _gammaCorrectionPostProcess.Gamma = GetSelectedComboBoxFloatValue(GammaCorrectionComboBox);

                MainDXViewportView.DXScene.PostProcesses.Add(_gammaCorrectionPostProcess);
                _createdPostProcesses.Add(_gammaCorrectionPostProcess);
            }
        }

        private void DisposeCreatedPostProcesses()
        {
            foreach (var onePostProcess in _createdPostProcesses)
                onePostProcess.Dispose();

            _createdPostProcesses.Clear();
        }

        private void UpdateEdgeDetectionParameters()
        {
            if (_edgeDetectionPostProcess == null)
                return;

            _edgeDetectionPostProcess.EdgeThreshold = GetSelectedComboBoxFloatValue(EdgeThresholdComboBox);
            _edgeDetectionPostProcess.MultiplyWithCurrentColor = MultiplyWithCurrentColorCheckBox.IsChecked ?? false;
        }

        private void UpdateGaussianBlurParameters()
        {
            if (_gaussianHorizontalBlurPostProcess == null)
                return;

            _gaussianHorizontalBlurPostProcess.BlurStandardDeviation = (float)StandardDeviationSlider.Value;
            _gaussianVerticalBlurPostProcess.BlurStandardDeviation = (float)StandardDeviationSlider.Value;
        }

        private float GetSelectedComboBoxFloatValue(ComboBox comboBox)
        {
            float selectedValue;

            var comboBoxItem = comboBox.SelectedItem as ComboBoxItem;
            if (comboBoxItem != null && comboBoxItem.Content != null)
            {
                string selectedText = comboBoxItem.Content.ToString();
                if (!float.TryParse(selectedText, NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out selectedValue))
                    selectedValue = float.NaN;
            }
            else
            {
                selectedValue = float.NaN;
            }

            return selectedValue;
        }

        private void UpdateSimpleBlurParameters()
        {
            if (_simpleBlurHorizontalBlurPostProcess == null)
                return;

            _simpleBlurHorizontalBlurPostProcess.FilterWidth = (int)FilterWidthSlider.Value;
            _simpleBlurVerticalBlurPostProcess.FilterWidth   = (int)FilterWidthSlider.Value;
        }

        private void UpdateExpandParameters()
        {
            if (_expandHorizontalPostProcess == null)
                return;

            int expansionWidth = (int)ExpansionWidthSlider.Value;

            _expandHorizontalPostProcess.ExpansionWidth = expansionWidth;
            _expandVerticalPostProcess.ExpansionWidth   = expansionWidth;
        }

        private void EdgeThresholdComboBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!this.IsLoaded)
                return;

            UpdateEdgeDetectionParameters();

            MainDXViewportView.Refresh(); // Render the scene again
        }

        private void StandardDeviationSlider_OnValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!this.IsLoaded)
                return;

            UpdateGaussianBlurParameters();

            MainDXViewportView.Refresh(); // Render the scene again
        }

        private void FilterWidthSlider_OnValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!this.IsLoaded)
                return;

            UpdateSimpleBlurParameters();

            MainDXViewportView.Refresh(); // Render the scene again
        }

        private void ExpansionWidthSlider_OnValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!this.IsLoaded)
                return;

            UpdateExpandParameters();

            MainDXViewportView.Refresh(); // Render the scene again
        }

        private void GammaCorrectionComboBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!this.IsLoaded && _gammaCorrectionPostProcess == null)
                return;

            _gammaCorrectionPostProcess.Gamma = GetSelectedComboBoxFloatValue(GammaCorrectionComboBox);

            MainDXViewportView.Refresh(); // Render the scene again
        }
    }
}
