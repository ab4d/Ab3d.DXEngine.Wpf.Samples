using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using Ab3d.Common;
using Ab3d.Common.EventManager3D;
using Ab3d.Utilities;

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
        //    or a parent Border or some other parent element that has Background property set (see line 68)
        //
        // 2) When MouseRotatorVisual3D is created, we need to call the SubscribeWithEventManager3D method 
        //    on the created MouseRotatorVisual3D and pass the EventManager3D as parameter (see line 73)
        //

        private static Random _rnd = new Random();

        private readonly Ab3d.Utilities.EventManager3D _eventManager;

        private readonly DiffuseMaterial _normalMaterial;
        private readonly DiffuseMaterial _selectedMaterial;

        private ModelUIElement3D _selectedModel;

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
                if (_selectedModel == null)
                    return;

                var rotationAxis = args.RotationAxis;

                if ((RotateModelRotatorCheckBox.IsChecked ?? false) && SelectedModelRotator.Transform != null)
                    rotationAxis = SelectedModelRotator.Transform.Transform(rotationAxis);

                // When a new rotation is started, we create a new AxisAngleRotation3D with the used Axis or rotation
                // During the rotation we will adjust the angle (inside ModelRotated event handler)
                _axisAngleRotation3D = new AxisAngleRotation3D(rotationAxis, 0);

                // Insert the new rotate transform before the last translate transform that positions the box
                var rotateTransform3D = new RotateTransform3D(_axisAngleRotation3D);

                AddTransformBeforeTranslate(_selectedModel, rotateTransform3D);
            };

            SelectedModelRotator.ModelRotated += delegate (object sender, ModelRotatedEventArgs args)
            {
                if (_selectedModel == null)
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


            // First load a teapot model
            string fileName = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources/Models/Teapot.obj");

            var readerObj = new Ab3d.ReaderObj();
            var loadedModel3D = readerObj.ReadModel3D(fileName) as GeometryModel3D; // We assume that a single GeometryModel3D is returned

            if (loadedModel3D == null)
                return;


            // It is important to center the object around (0, 0, 0) so the rotation will rotate the object around its center.
            Ab3d.Utilities.ModelUtils.CenterAndScaleModel3D(loadedModel3D, new Point3D(0, 0, 0), new Size3D(80, 80, 80));

            var teapotMesh = loadedModel3D.Geometry as MeshGeometry3D;

            teapotMesh = Ab3d.Utilities.MeshUtils.TransformMeshGeometry3D(teapotMesh, loadedModel3D.Transform);


            // Create 10 teapot models
            for (int i = 0; i < 10; i++)
            {
                var teapotModel3D = new GeometryModel3D()
                {
                    Geometry = teapotMesh,
                    Material = _normalMaterial
                };

                // We create ModelUIElement3D instead of ModelVisual3D so we can skip 
                var teapotModelElement3D = new ModelUIElement3D()
                {
                    Model = teapotModel3D
                };


                var newTranslateTransform3D = new TranslateTransform3D();
                teapotModelElement3D.Transform = newTranslateTransform3D;

                for (int j = 0; j < 100; j++)
                {
                    newTranslateTransform3D.OffsetX = _rnd.NextDouble() * 400 - 200;
                    newTranslateTransform3D.OffsetY = _rnd.NextDouble() * 40 - 20;
                    newTranslateTransform3D.OffsetZ = _rnd.NextDouble() * 400 - 200;

                    // Make sure that we do not intersect with any model that is already added to the scene
                    if (!IntersectsAnyOtherVisual3D(teapotModelElement3D, SceneObjectsContainer.Children))
                        break;
                }

                // Create Transform3DGroup that will hold the TranslateTransform3D and RotateTransform3D (added by ModelRotatorVisual3D)
                var transform3DGroup = new Transform3DGroup();
                transform3DGroup.Children.Add(newTranslateTransform3D);

                teapotModelElement3D.Transform = transform3DGroup;

                SceneObjectsContainer.Children.Add(teapotModelElement3D);


                // Use EventManager from Ab3d.PowerToys to add support for click event on the box model
                var visualEventSource3D = new Ab3d.Utilities.VisualEventSource3D(teapotModelElement3D);
                visualEventSource3D.MouseClick += delegate (object sender, MouseButton3DEventArgs e)
                {
                    var selectedBoxModel = e.HitObject as ModelUIElement3D;
                    SelectObject(selectedBoxModel);
                };

                _eventManager.RegisterEventSource3D(visualEventSource3D);


                // Automatically select first box
                if (_selectedModel == null)
                {
                    //boxModel.Refresh(); // Force creating the model
                    SelectObject(teapotModelElement3D);
                }
            }
        }

        public void SelectObject(ModelUIElement3D selectedModel)
        {
            // Deselect currently selected model
            GeometryModel3D geometryModel3D;

            if (_selectedModel != null)
            {
                geometryModel3D = _selectedModel.Model as GeometryModel3D;

                // Set material back to normal
                if (geometryModel3D != null)
                {
                    geometryModel3D.Material = _normalMaterial;
                    geometryModel3D.BackMaterial = null;
                }

                // Allow hit testing again - so user can select that object again
                _selectedModel.IsHitTestVisible = true;

                _selectedModel = null;
            }


            _selectedModel = selectedModel;
            if (_selectedModel == null)
                return;


            // Prevent hit-testing in selected model
            // This will allow clicking on the parts of move arrows that are inside the selected model
            // Note that IsHitTestVisible is available only on models derived from UIElement3D (if you need that on GeometryModel3D or ModelVisual3D, then use ModelUIElement3D as parent of your model)
            _selectedModel.IsHitTestVisible = false;


            // Change material to semi-transparent Silver
            geometryModel3D = _selectedModel.Model as GeometryModel3D;
            if (geometryModel3D != null)
            {
                geometryModel3D.Material = _selectedMaterial;
                geometryModel3D.BackMaterial = _selectedMaterial; // We also set BackMaterial so the inner side of boxes will be visible
            }

            // To render transparent objects correctly, we need to sort the objects so that the transparent objects are rendered after other objects
            // We can use the TransparencySorter from Ab3d.PowerToys
            // Note that it is also possible to use TransparencySorter with many advanced features - see the Model3DTransparencySortingSample for more info
            TransparencySorter.SimpleSort(SceneObjectsContainer.Children);

            // In our simple case (we have only one transparent object), we could also manually "sort" the objects with moving the transparent object to the back of the Children collection:
            //SceneObjectsContainer.Children.Remove(_selectedBoxVisual3D);
            //SceneObjectsContainer.Children.Add(_selectedBoxVisual3D);

            var modelPosition = GetModelPosition(_selectedModel);

            if (RotateModelRotatorCheckBox.IsChecked ?? false)
                SelectedModelRotator.Transform = selectedModel.Transform;
            else
                SelectedModelRotator.Position = modelPosition;
        }

        // Get position from the last TranslateTransform3D in Transform3DGroup
        private Point3D GetModelPosition(Visual3D visual3D)
        {
            var transform3DGroup = visual3D.Transform as Transform3DGroup;
            if (transform3DGroup != null)
            {
                var translateTransform3D = transform3DGroup.Children[transform3DGroup.Children.Count - 1] as TranslateTransform3D;

                if (translateTransform3D != null)
                    return new Point3D(translateTransform3D.OffsetX, translateTransform3D.OffsetY, translateTransform3D.OffsetZ);
            }

            return new Point3D();
        }

        // Insert Transform3D before any TranslateTransform3D
        // Order of transformation is important: scale, rotate, translate !!!
        private void AddTransformBeforeTranslate(Visual3D visual3D, Transform3D transform3D)
        {
            if (visual3D.Transform == null)
            {
                visual3D.Transform = transform3D;
                return;
            }

            var transform3DGroup = visual3D.Transform as Transform3DGroup;
            if (transform3DGroup == null)
            {
                // If there is already some other transformation, then create a new Transform3DGroup
                // and add existing transformation as its first child.
                transform3DGroup = new Transform3DGroup();
                transform3DGroup.Children.Add(visual3D.Transform);

                visual3D.Transform = transform3DGroup;
            }

            if (transform3DGroup.Children.Count > 0)
            {
                int insertIndex = 0;
                for (int i = 0; i < transform3DGroup.Children.Count; i++)
                {
                    if (transform3DGroup.Children[i] is TranslateTransform3D)
                        break;

                    insertIndex++;
                }

                transform3DGroup.Children.Insert(insertIndex, transform3D);
            }
            else
            {
                transform3DGroup.Children.Add(transform3D);
            }
        }

        public static bool IntersectsAnyOtherVisual3D(Visual3D newModel, Visual3DCollection otherModels)
        {
            var newModelBounds = GetVisual3DBounds(newModel);

            if (newModelBounds.IsEmpty)
                return false;

            foreach (var otherModel in otherModels)
            {
                var otherModelBounds = GetVisual3DBounds(otherModel);

                if (otherModelBounds.IsEmpty)
                    continue;

                if (newModelBounds.IntersectsWith(otherModelBounds))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Gets Bounds from ModelVisual3D or ModelUIElement3D (also applies transformation that is assigned to Visual3D)
        /// </summary>
        /// <param name="visual3D">Visual3D</param>
        /// <returns>Bounds as Rect3D</returns>
        public static Rect3D GetVisual3DBounds(Visual3D visual3D)
        {
            Rect3D bounds = Rect3D.Empty;

            var modelVisual3D = visual3D as ModelVisual3D;
            if (modelVisual3D != null && modelVisual3D.Content != null)
            {
                bounds = modelVisual3D.Content.Bounds;
            }
            else
            {
                var modelUIElement3D = visual3D as ModelUIElement3D;
                if (modelUIElement3D != null && modelUIElement3D.Model != null)
                    bounds = modelUIElement3D.Model.Bounds;
            }

            if (bounds.IsEmpty)
                return bounds;

            if (visual3D.Transform != null)
                bounds = visual3D.Transform.TransformBounds(bounds);

            return bounds;
        }

        private void OnRotateModelRotatorCheckBoxCheckedChanged(object sender, RoutedEventArgs e)
        {
            if (!this.IsLoaded || _selectedModel == null)
                return;

            if (RotateModelRotatorCheckBox.IsChecked ?? false)
            {
                SelectedModelRotator.Position = new Point3D();
                SelectedModelRotator.Transform = _selectedModel.Transform;
            }
            else
            {
                SelectedModelRotator.Transform = new TranslateTransform3D(); // UH if Transform is set to null, then children preserve the existing transform. Setting to TranslateTransform3D without any offset solves this.

                var boxPosition = GetModelPosition(_selectedModel);
                SelectedModelRotator.Position = boxPosition;
            }
        }
    }
}
