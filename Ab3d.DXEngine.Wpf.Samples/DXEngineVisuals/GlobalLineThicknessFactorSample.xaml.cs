using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using Ab3d.DirectX.Effects;

namespace Ab3d.DXEngine.Wpf.Samples.DXEngineVisuals
{
    /// <summary>
    /// Interaction logic for GlobalLineThicknessFactorSample.xaml
    /// </summary>
    public partial class GlobalLineThicknessFactorSample : Page
    {
        private bool _useAutomaticGlobalLineThicknessFactor;

        private ThickLineEffect _thickLineEffect;

        private DateTime _startAnimationTime;

        public GlobalLineThicknessFactorSample()
        {
            InitializeComponent();

            LoadTestModel();

            _useAutomaticGlobalLineThicknessFactor = AutoRadioButton.IsChecked ?? false;

            MainDXViewportView.DXSceneDeviceCreated += delegate(object sender, EventArgs args)
            {
                // When DXEngine creates the DXDevice we can get the ThickLineEffect that is used to render 3D lines.
                // The ThickLineEffect.GlobalLineThicknessFactor property will be used to adjust line thickness globally.
                _thickLineEffect = MainDXViewportView.DXScene.DXDevice.EffectsManager.GetEffect<ThickLineEffect>();
            };

            this.Unloaded += delegate(object sender, RoutedEventArgs args)
            {
                CompositionTarget.Rendering -= CompositionTargetOnRendering;

                if (_thickLineEffect != null)
                {
                    _thickLineEffect.Dispose();
                    _thickLineEffect = null;
                }

                MainDXViewportView.Dispose();
            };


            _startAnimationTime = DateTime.Now;
            CompositionTarget.Rendering += CompositionTargetOnRendering;
        }

        private void CompositionTargetOnRendering(object sender, EventArgs e)
        {
            var elapsedSeconds = (_startAnimationTime - DateTime.Now).TotalSeconds;

            if (AnimateCameraCheckBox.IsChecked ?? false)
            {
                // Animate camera Distance from 100 to 1100
                Camera1.Distance = 600 + Math.Sin(elapsedSeconds) * 500;
            }

            if (_useAutomaticGlobalLineThicknessFactor && _thickLineEffect != null)
            {
                // Dynamically adjust GlobalLineThicknessFactor based on camera's distance:
                float globalLineThicknessFactor;
                if (Camera1.Distance < 100)
                    globalLineThicknessFactor = 1; // when very close to the model, do not change the line thickness
                else if (Camera1.Distance > 1000)
                    globalLineThicknessFactor = 0.1f; // when very far away, set line thickness to 1/10 of the originally set line thickness
                else
                    globalLineThicknessFactor = 1.0f / (float)((Camera1.Distance) / 100); // adjust the factor from 1 to 0.1 based on distance from 100 to 1100

                _thickLineEffect.GlobalLineThicknessFactor = globalLineThicknessFactor;
            }

            UpdateDisplayInfoTextBlocks();
        }

        private void LoadTestModel()
        {
            var readerObj = new Ab3d.ReaderObj();
            var model3D = readerObj.ReadModel3D(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"Resources\Models\dragon_vrip_res3.obj"));

            Ab3d.Utilities.ModelUtils.CenterAndScaleModel3D(model3D, new Point3D(0, 0, 0), new Size3D(100, 100, 100), preserveAspectRatio: true);

            ContentWireframeVisual.OriginalModel = model3D;
        }

        private void OnLineThicknessFactorChanged(object sender, RoutedEventArgs e)
        {
            if (!this.IsLoaded)
                return;

            if (ReferenceEquals(sender, AutoRadioButton))
            {
                _useAutomaticGlobalLineThicknessFactor = true;
                return;
            }

            var radioButton = sender as RadioButton;
            if (radioButton == null)
                return;

            var contentText = (string)radioButton.Content;

            int spacePos = contentText.IndexOf(' ');

            if (spacePos != -1)
                contentText = contentText.Substring(0, spacePos); // use only numeric part

            float lineThicknessValue = float.Parse(contentText, NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture);

            if (_thickLineEffect != null)
                _thickLineEffect.GlobalLineThicknessFactor = lineThicknessValue;

            _useAutomaticGlobalLineThicknessFactor = false;

            UpdateDisplayInfoTextBlocks();

            MainDXViewportView.Refresh();
        }

        private void UpdateDisplayInfoTextBlocks()
        {
            string cameraDistanceText = string.Format("Camera.Distance: {0:0}", Camera1.Distance);
            CameraDistanceTextBlock.Text = cameraDistanceText;

            if (_thickLineEffect != null)
            {
                string globalLineThicknessFactorText = string.Format(System.Globalization.CultureInfo.InvariantCulture, "GlobalLineThicknessFactor: {0:0.00}", _thickLineEffect.GlobalLineThicknessFactor);
                GlobalLineThicknessFactorTextBlock.Text = globalLineThicknessFactorText;
            }
        }
    }
}
