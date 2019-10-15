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
using Ab3d.Common.Cameras;
using Ab3d.Common.EventManager3D;
using Ab3d.Utilities;
using Ab3d.Visuals;

namespace Ab3d.DXEngine.Wpf.Samples.PowerToysOther
{
    /// <summary>
    /// Interaction logic for ModelMoverOverlaySample.xaml
    /// </summary>
    public partial class ModelMoverOverlaySample : Page
    {
        private static Random _rnd = new Random();

        private readonly Ab3d.Utilities.EventManager3D _eventManager;

        private readonly DiffuseMaterial _normalMaterial;

        private Ab3d.Visuals.BoxVisual3D _selectedBoxModel;

        private Point3D _startMovePosition;

        public ModelMoverOverlaySample()
        {
            InitializeComponent();

            // We need to synchronize the Camera and Lights in OverlayViewport with the camera in the MainViewport
            Camera1.CameraChanged += delegate(object s, CameraChangedRoutedEventArgs args)
            {
                OverlayViewport.Camera = MainViewport.Camera;
                OverlayViewportLight.Direction = ((DirectionalLight)Camera1.CameraLight).Direction;
            };


            // NOTE:
            // To define custom axes directions for ModelMoverVisual3D, define it in code and specify the axes direction it the constructor - for example:
            // ModelMover = new ModelMoverVisual3D(new Vector3D(-1, 0, 0), new Vector3D(0, -1, 0), new Vector3D(0, 0, -1));
            //
            // You can even define angles that are not aligned with coordinate axes:
            // ModelMover = new ModelMoverVisual3D(new Vector3D(-1, -1, 0), new Vector3D(-1, 1, 0), new Vector3D(0, 0, -1));


            // Setup event handlers on ModelMoverVisual3D
            ModelMover.ModelMoveStarted += delegate(object o, EventArgs eventArgs)
            {
                if (_selectedBoxModel == null)
                    return;

                _startMovePosition = _selectedBoxModel.CenterPosition;
            };

            ModelMover.ModelMoved += delegate(object o, Ab3d.Common.ModelMovedEventArgs e)
            {
                if (_selectedBoxModel == null)
                    return;

                var newCenterPosition = _startMovePosition + e.MoveVector3D;

                if (Math.Abs(newCenterPosition.X) > 2000 ||
                    Math.Abs(newCenterPosition.Y) > 2000 ||
                    Math.Abs(newCenterPosition.Z) > 2000)
                {
                    InfoTextBlock.Text = "Move out of range";
                    return;
                }

                _selectedBoxModel.CenterPosition = newCenterPosition;
                ModelMover.Position = _selectedBoxModel.CenterPosition;

                InfoTextBlock.Text = string.Format("MoveVector3D: {0:0}", e.MoveVector3D);
            };

            ModelMover.ModelMoveEnded += delegate(object sender, EventArgs args)
            {
                InfoTextBlock.Text = "";
            };


            _normalMaterial = new DiffuseMaterial(Brushes.Silver);
            _eventManager = new Ab3d.Utilities.EventManager3D(MainViewport);

            CreateRandomScene();

            // IMPORTANT:
            // It is very important to call Dispose method on DXSceneView after the control is not used any more (see help file for more info)
            this.Unloaded += (sender, args) => MainDXViewportView.Dispose();
        }

        private void CreateRandomScene()
        {
            SceneObjectsContainer.Children.Clear();

            for (int i = 0; i < 10; i++)
            {
                var boxModel = new Ab3d.Visuals.BoxVisual3D()
                {
                    CenterPosition = new Point3D(_rnd.NextDouble() * 400 - 200, _rnd.NextDouble() * 40 - 20, _rnd.NextDouble() * 400 - 200),
                    Size = new Size3D(50, 20, 50),
                    Material = _normalMaterial
                };

                SceneObjectsContainer.Children.Add(boxModel);


                // Use EventManager from Ab3d.PowerToys to add support for click event on the box model
                var visualEventSource3D = new Ab3d.Utilities.VisualEventSource3D(boxModel);
                visualEventSource3D.MouseClick += delegate(object sender, MouseButton3DEventArgs e)
                {
                    var selectedBoxModel = e.HitObject as Ab3d.Visuals.BoxVisual3D;
                    SelectObject(selectedBoxModel);
                };

                _eventManager.RegisterEventSource3D(visualEventSource3D);


                // Automatically select first box
                if (_selectedBoxModel == null)
                    SelectObject(boxModel);
            }
        }

        private void SelectObject(Ab3d.Visuals.BoxVisual3D selectedBox)
        {
            _selectedBoxModel = selectedBox;
            if (_selectedBoxModel == null)
                return;

            ModelMover.Position = _selectedBoxModel.CenterPosition;


            // Tell ModelDecoratorVisual3D which Model3D to show
            SelectedModelDecorator.TargetModel3D = _selectedBoxModel.Content;
            
            // NOTE:
            // When the 3D models are organized into hierarchy of models with using different ModelVisual3D or Model3DGroup objects, 
            // you also need to so specify the SelectedModelDecorator.RootModelVisual3D in order to get the correct position of the TargetModel3D
        }
    }
}
