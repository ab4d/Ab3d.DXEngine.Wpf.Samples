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
using Ab3d.Common;
using Ab3d.Common.EventManager3D;
using Ab3d.Utilities;
using Ab3d.Visuals;

namespace Ab3d.DXEngine.Wpf.Samples.PowerToysOther
{
    /// <summary>
    /// Interaction logic for ModelRotatorSample.xaml
    /// </summary>
    public partial class ModelRotatorSample : Page
    {

        // !!!!!!!!!!!!!!!!!!!!!!!!!!!!!
        // !!!        IMPORTANT      !!!
        // !!!!!!!!!!!!!!!!!!!!!!!!!!!!!
        //
        // When MouseRotatorVisual3D is used inside DXEngine, the mouse events on UIElement3D objects that are used inside MouseRotatorVisual3D will not work.
        // Therefore MouseRotatorVisual3D also support using Ab3d.Utilities.EventManager3D that can process mouse events when inside Ab3d.DXEngine.
        //
        // To make MouseRotatorVisual3D work inside DXEngine, the following code changes need to be done:
        // 1) EventManager3D needs to be created and its CustomEventsSourceElement must be set the DXViewportView
        //    or a parent Border or some other parent element that has Background property set (see line 69)
        //
        // 2) When MouseRotatorVisual3D is created, we need to call the SubscribeWithEventManager3D method 
        //    on the created MouseRotatorVisual3D and pass the EventManager3D as parameter (see line 84)
        // 
        // 3) To allow user to click on arrows that are inside the selected model, we need to exclude the selected
        //    model from being processed by EventManager3D. This can be done with calling RegisterExcludedVisual3D on EventManager3D (see line 226)
        //
        // 4) Because we called RegisterExcludedVisual3D, we need to call RemoveExcludedVisual3D after the mouse moving is completed (see line 207)
        //

        private static Random _rnd = new Random();

        private readonly Ab3d.Utilities.EventManager3D _eventManager;

        private readonly DiffuseMaterial _normalMaterial;
        private readonly DiffuseMaterial _selectedMaterial;

        private Ab3d.UIElements.BoxUIElement3D _selectedBoxModel;

        private AxisAngleRotation3D _axisAngleRotation3D;


        public ModelRotatorSample()
        {
            InitializeComponent();

            _normalMaterial = new DiffuseMaterial(Brushes.Silver);
            _selectedMaterial = new DiffuseMaterial(new SolidColorBrush(Color.FromArgb(150, 192, 192, 192))); // semi-transparent Silver

            _eventManager = new Ab3d.Utilities.EventManager3D(MainViewport);
            _eventManager.CustomEventsSourceElement = ViewportBorder;

            // IMPORTANT !!!
            // When ModelMoverVisual3D is used with EventManager3D
            // we need to call SubscribeWithEventManager3D to use EventManager3D for mouse events processing
            SelectedModelRotator.SubscribeWithEventManager3D(_eventManager);

            // Setup events on ModelRotatorVisual3D
            SelectedModelRotator.ModelRotateStarted += delegate (object sender, ModelRotatedEventArgs args)
            {
                if (_selectedBoxModel == null)
                    return;

                // When a new rotation is started, we create a new AxisAngleRotation3D with the used Axis or rotation
                // During the rotation we will adjust the angle (inside ModelRotated event handler)
                _axisAngleRotation3D = new AxisAngleRotation3D(args.RotationAxis, 0);

                // Insert the new rotate transform before the last translate transform that positions the box
                var rotateTransform3D = new RotateTransform3D(_axisAngleRotation3D);

                AddRotateTransform(_selectedBoxModel, rotateTransform3D);
            };

            SelectedModelRotator.ModelRotated += delegate (object sender, ModelRotatedEventArgs args)
            {
                if (_selectedBoxModel == null)
                    return;

                _axisAngleRotation3D.Angle = args.RotationAngle;
            };

            SelectedModelRotator.ModelRotateEnded += delegate (object sender, ModelRotatedEventArgs args)
            {
                // Nothing to do here in this sample
                // The event handler is here only for description purposes
            };


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
                // Create simple box that user will be able to rotate
                // In order to support rotation, we need to create the box at (0,0,0)
                // and then after performing rotation, translate the object to its final location.
                // If we would create the object at its final rotation (basically applying translation before rotation), 
                // then the box would not be rotated around its center but around the coordinate axes center.
                var boxModel = new Ab3d.UIElements.BoxUIElement3D()
                {
                    CenterPosition = new Point3D(0, 0, 0),
                    Size = new Size3D(50, 20, 50),
                    Material = _normalMaterial
                };

                // Create Transform3DGroup that will hold the 
                var transform3DGroup = new Transform3DGroup();
                transform3DGroup.Children.Add(new TranslateTransform3D(_rnd.NextDouble() * 400 - 200, _rnd.NextDouble() * 40 - 20, _rnd.NextDouble() * 400 - 200));

                boxModel.Transform = transform3DGroup;

                SceneObjectsContainer.Children.Add(boxModel);


                // Use EventManager from Ab3d.PowerToys to add support for click event on the box model
                var visualEventSource3D = new Ab3d.Utilities.VisualEventSource3D(boxModel);
                visualEventSource3D.MouseClick += delegate (object sender, MouseButton3DEventArgs e)
                {
                    var selectedBoxModel = e.HitObject as Ab3d.UIElements.BoxUIElement3D;
                    SelectObject(selectedBoxModel);
                };

                _eventManager.RegisterEventSource3D(visualEventSource3D);


                // Automatically select first box
                if (_selectedBoxModel == null)
                {
                    boxModel.Refresh(); // Force creating the model
                    SelectObject(boxModel);
                }
            }
        }

        private void SelectObject(Ab3d.UIElements.BoxUIElement3D selectedBox)
        {
            // Deselect currently selected model
            if (_selectedBoxModel != null)
            {
                // Set material back to normal
                _selectedBoxModel.Material = _normalMaterial;
                _selectedBoxModel.BackMaterial = null;

                // Allow hit testing again - so user can select that object again
                _selectedBoxModel.IsHitTestVisible = true;

                _selectedBoxModel = null;
            }

            _selectedBoxModel = selectedBox;
            if (_selectedBoxModel == null)
                return;


            // Prevent hit-testing in selected model
            // This will allow clicking on the parts of move arrows that are inside the selected model
            // Note that IsHitTestVisible is available only on models derived from UIElement3D (if you need that on GeometryModel3D or ModelVisual3D, then use ModelUIElement3D as parent of your model)
            _selectedBoxModel.IsHitTestVisible = false;


            // Change material to semi-transparent Silver
            _selectedBoxModel.Material = _selectedMaterial;
            _selectedBoxModel.BackMaterial = _selectedMaterial; // We also set BackMaterial so the inner side of boxes will be visible

            // To render the transpant object correctly, we need to sort the objects so that the transparent objects are rendered after other objects
            // We can use the TransparencySorter from Ab3d.PowerToys
            // Note that it is also possible to use TransparencySorter with many advanced features - see the Model3DTransparencySortingSample for more info
            TransparencySorter.SimpleSort(SceneObjectsContainer.Children);

            // In our simple case (we have only one transparent object), we could also manually "sort" the objects with moving the transparent object to the back of the Children collection:
            //SceneObjectsContainer.Children.Remove(_selectedBoxVisual3D);
            //SceneObjectsContainer.Children.Add(_selectedBoxVisual3D);

            var boxPosition = GetBoxPosition(_selectedBoxModel);
            SelectedModelRotator.Position = boxPosition;
        }

        // Get position from the last TranslateTransform3D in Transform3DGroup
        private Point3D GetBoxPosition(Visual3D visual3D)
        {
            var transform3DGroup = _selectedBoxModel.Transform as Transform3DGroup;
            if (transform3DGroup != null)
            {
                var translateTransform3D = transform3DGroup.Children[transform3DGroup.Children.Count - 1] as TranslateTransform3D;

                if (translateTransform3D != null)
                    return new Point3D(translateTransform3D.OffsetX, translateTransform3D.OffsetY, translateTransform3D.OffsetZ);
            }

            return new Point3D();
        }

        // Add rotateTransform3D before the last TranslateTransform3D
        private void AddRotateTransform(Visual3D visual3D, RotateTransform3D rotateTransform3D)
        {
            var transform3DGroup = _selectedBoxModel.Transform as Transform3DGroup;
            if (transform3DGroup != null)
            {
                if (transform3DGroup.Children.Count > 0)
                    transform3DGroup.Children.Insert(transform3DGroup.Children.Count - 1, rotateTransform3D);
                else
                    transform3DGroup.Children.Add(rotateTransform3D);
            }
        }

        private void OnRotateModelRotatorCheckBoxCheckedChanged(object sender, RoutedEventArgs e)
        {
            if (!this.IsLoaded)
                return;

            if (RotateModelRotatorCheckBox.IsChecked ?? false)
                SelectedModelRotator.SetRotation(new RotateTransform3D(new AxisAngleRotation3D(new Vector3D(0, 1, 0), 45)));
            else
                SelectedModelRotator.SetRotation(null);
        }
    }
}
