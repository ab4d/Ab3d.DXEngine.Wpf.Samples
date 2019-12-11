using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
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
using Ab3d.Utilities;

namespace Ab3d.DXEngine.Wpf.Samples.PowerToys
{
    /// <summary>
    /// Interaction logic for StandardTransform3DEditor.xaml
    /// </summary>
    public partial class StandardTransform3DEditor : UserControl
    {
        private bool _isInitialized;
        private bool _isChangedInternally;
        private StandardTransform3D _standardTransform3D;

        public StandardTransform3D StandardTransform3D
        {
            get { return _standardTransform3D; }
            set
            {
                if (_standardTransform3D != null)
                    _standardTransform3D.Changed -= StandardTransform3DOnChanged;

                _standardTransform3D = value; 
                SetInitialControlValues();

                if (_standardTransform3D != null)
                    _standardTransform3D.Changed += StandardTransform3DOnChanged;
            }
        }

        public string PositionTitleText
        {
            get { return PositionTitleTextBlock.Text; }
            set { PositionTitleTextBlock.Text = value; }
        }

        public string RotationTitleText
        {
            get { return RotationTitleTextBlock.Text; }
            set { RotationTitleTextBlock.Text = value; }
        }

        public string ScaleTitleText
        {
            get { return ScaleTitleTextBlock.Text; }
            set { ScaleTitleTextBlock.Text = value; }
        }

        /// <summary>
        /// Gets or sets a CultureInfo that is used to show the initial transformation values. InvariantCulture by default.
        /// </summary>
        public CultureInfo FormatCulture { get; set; }

        /// <summary>
        /// Gets or sets a string that is used as format string to format translation values that are displayed in TextBoxes. Default value is "G".
        /// For example, to show only 2 decimals use "F2".
        /// See for possible values: https://docs.microsoft.com/en-us/dotnet/api/system.double.tostring?view=netframework-4.8#System_Double_ToString_System_String_
        /// </summary>
        public string TranslationStringFormat { get; set; }
        
        /// <summary>
        /// Gets or sets a string that is used as format string to format rotation values that are displayed in TextBoxes. Default value is "G".
        /// For example, to show only 2 decimals use "F2".
        /// See for possible values: https://docs.microsoft.com/en-us/dotnet/api/system.double.tostring?view=netframework-4.8#System_Double_ToString_System_String_
        /// </summary>
        public string RotationStringFormat { get; set; }
        
        /// <summary>
        /// Gets or sets a string that is used as format string to format scale values that are displayed in TextBoxes. Default value is "G".
        /// For example, to show only 2 decimals use "F2".
        /// See for possible values: https://docs.microsoft.com/en-us/dotnet/api/system.double.tostring?view=netframework-4.8#System_Double_ToString_System_String_
        /// </summary>
        public string ScaleStringFormat { get; set; }


        /// <summary>
        /// Changed event is triggered when the transformation value is changed by the user input in this UserControl.
        /// </summary>
        public event EventHandler Changed;


        public StandardTransform3DEditor()
        {
            InitializeComponent();

            FormatCulture = CultureInfo.InvariantCulture;

            TranslationStringFormat = "G";
            RotationStringFormat = "G";
            ScaleStringFormat = "G";

            this.Loaded += delegate
            {
                SetInitialControlValues();
                _isInitialized = true;
            };
        }

        /// <summary>
        /// Update method updates the values shown with this control.
        /// </summary>
        public void Update()
        {
            SetInitialControlValues();
        }

        private void SetInitialControlValues()
        {
            _isChangedInternally = true; // Prevent handling Changed event

            if (StandardTransform3D == null)
            {
                TranslateXTextBox.Text = TranslateYTextBox.Text = TranslateZTextBox.Text = "0";
                RotateXTextBox.Text = RotateYTextBox.Text = RotateZTextBox.Text = "0";
                ScaleXTextBox.Text = ScaleYTextBox.Text = ScaleZTextBox.Text = "1";

                _isChangedInternally = false;
                return;
            }

            

            TranslateXTextBox.Text = StandardTransform3D.TranslateX.ToString(TranslationStringFormat, FormatCulture);
            TranslateYTextBox.Text = StandardTransform3D.TranslateY.ToString(TranslationStringFormat, FormatCulture);
            TranslateZTextBox.Text = StandardTransform3D.TranslateZ.ToString(TranslationStringFormat, FormatCulture);

            RotateXTextBox.Text = StandardTransform3D.RotateX.ToString(RotationStringFormat, FormatCulture);
            RotateYTextBox.Text = StandardTransform3D.RotateY.ToString(RotationStringFormat, FormatCulture);
            RotateZTextBox.Text = StandardTransform3D.RotateZ.ToString(RotationStringFormat, FormatCulture);

            ScaleXTextBox.Text = StandardTransform3D.ScaleX.ToString(ScaleStringFormat, FormatCulture);
            ScaleYTextBox.Text = StandardTransform3D.ScaleY.ToString(ScaleStringFormat, FormatCulture);
            ScaleZTextBox.Text = StandardTransform3D.ScaleZ.ToString(ScaleStringFormat, FormatCulture);

            _isChangedInternally = false;
        }

        private void GetValuesFromControls()
        {
            _isChangedInternally = true; // Prevent handling Changed event

            StandardTransform3D.BeginInit();

            StandardTransform3D.TranslateX = ParseValue(TranslateXTextBox, StandardTransform3D.TranslateX);
            StandardTransform3D.TranslateY = ParseValue(TranslateYTextBox, StandardTransform3D.TranslateY);
            StandardTransform3D.TranslateZ = ParseValue(TranslateZTextBox, StandardTransform3D.TranslateZ);

            StandardTransform3D.RotateX = ParseValue(RotateXTextBox, StandardTransform3D.RotateX);
            StandardTransform3D.RotateY = ParseValue(RotateYTextBox, StandardTransform3D.RotateY);
            StandardTransform3D.RotateZ = ParseValue(RotateZTextBox, StandardTransform3D.RotateZ);

            StandardTransform3D.ScaleX = ParseValue(ScaleXTextBox, StandardTransform3D.ScaleX);
            StandardTransform3D.ScaleY = ParseValue(ScaleYTextBox, StandardTransform3D.ScaleY);
            StandardTransform3D.ScaleZ = ParseValue(ScaleZTextBox, StandardTransform3D.ScaleZ);

            StandardTransform3D.EndInit();

            _isChangedInternally = false;
        }

        private double ParseValue(TextBox textBox, double fallbackValue)
        {
            if (string.IsNullOrEmpty(textBox.Text))
                return fallbackValue;

            double newValue;
            if (double.TryParse(textBox.Text, NumberStyles.Float, this.FormatCulture, out newValue))
            {
                textBox.ClearValue(TextBox.ForegroundProperty);
                textBox.ClearValue(TextBox.BorderBrushProperty);
                return newValue;
            }

            textBox.Foreground = Brushes.Red;
            textBox.BorderBrush = Brushes.Red;

            return fallbackValue;
        }

        private void StandardTransform3DOnChanged(object sender, EventArgs e)
        {
            if (!_isChangedInternally)
                SetInitialControlValues();
        }

        private void OnTextBoxTextChanged(object sender, TextChangedEventArgs e)
        {
            if (!_isInitialized || _isChangedInternally || StandardTransform3D == null)
                return;

            GetValuesFromControls();

            OnChanged();
        }

        protected void OnChanged()
        {
            if (Changed != null)
                Changed(this, null);
        }
    }
}
