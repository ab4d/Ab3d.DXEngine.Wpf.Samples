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
using Ab3d.Utilities;

namespace Ab3d.DXEngine.Wpf.Samples.PowerToys
{
    /// <summary>
    /// Interaction logic for Lines3DSample.xaml
    /// </summary>
    public partial class Lines3DSample : Page
    {
        public Lines3DSample()
        {
            InitializeComponent();

            ArrowLengthInfoImage.ToolTip =
@"Specifies the maximum arrow length set as fraction of the line length.
For example: 0.2 means that the maximum arrow length will be 1 / 5 (=0.2) of the line length.
If the line is short so that the arrow length exceeds the amount defined by MaxLineArrowLength,
the arrow is shortened (the arrow angle is increased).";


            SetUpMultiPolyLineVisual3D();

            MainDXViewportView.GraphicsProfiles = DirectX.Client.Settings.DXEngineSettings.Current.GraphicsProfiles;

            // IMPORTANT:
            // It is very important to call Dispose method on DXSceneView after the control is not used any more (see help file for more info)
            this.Unloaded += (sender, args) => MainDXViewportView.Dispose();
        }

        private void SetUpMultiPolyLineVisual3D()
        {
            // MultiPolyLineVisual3D.PositionsList need a list of Point3DCollection to draw multiple polylines with one Visual3D
            var allPolylines = new List<Point3DCollection>();

            // We will create multiple circles with 6 segments
            int segmentsCount = 6;

            var circlePositions = new List<Point>();
            double angleStep = 2 * Math.PI / segmentsCount;
            double angle = 0;
            for (int i = 0; i < segmentsCount; i++)
            {
                circlePositions.Add(new Point(Math.Sin(angle), Math.Cos(angle)));
                angle += angleStep;
            }

            // Start center position
            var centerPosition = new Point3D(-120, 0, -120);

            for (int i = 1; i < 5; i++)
            {
                var point3DCollection = new Point3DCollection(segmentsCount);
                for (int j = 0; j < segmentsCount; j++)
                {
                    var onePosition = centerPosition + new Vector3D(circlePositions[j].X * i * 5 + 10, circlePositions[j].Y * i * 5 + 10, -i * 10);
                    point3DCollection.Add(onePosition);
                }

                allPolylines.Add(point3DCollection);
            }

            MultiPolyLineVisual.PositionsList = allPolylines;

            // We can also set IsClosed to close (connect first and last point) all the polylines
            MultiPolyLineVisual.IsClosed = true;
        }

        private void AngleComboBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!this.IsLoaded)
                return;

            var comboBoxItem = AngleComboBox.SelectedItem as ComboBoxItem;

            LinesUpdater.Instance.LineArrowAngle = double.Parse((string)comboBoxItem.Content, System.Globalization.CultureInfo.InvariantCulture);

            LinesUpdater.Instance.Refresh();
        }

        private void LengthComboBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!this.IsLoaded)
                return;

            var comboBoxItem = LengthComboBox.SelectedItem as ComboBoxItem;

            LinesUpdater.Instance.MaxLineArrowLength = double.Parse((string)comboBoxItem.Content, System.Globalization.CultureInfo.InvariantCulture);

            LinesUpdater.Instance.Refresh();
        }
    }
}
