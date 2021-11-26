using System;
using System.Collections.Generic;
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
using System.Windows.Media.Media3D;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Ab3d.Visuals;

namespace Ab3d.DXEngine.Wpf.Samples.PowerToysOther
{
    /// <summary>
    /// Interaction logic for HierarchyWithContentVisual3D.xaml
    /// </summary>
    public partial class HierarchyWithContentVisual3D : Page
    {
        private Dictionary<Visual3D, ModelVisual3D> _parentModelVisuals;

        public HierarchyWithContentVisual3D()
        {
            InitializeComponent();

            AddContentVisual3DObjects();

            // _parentModelVisuals will store parent ModelVisual3D when a ModelVisual3D is removed from its parent
            _parentModelVisuals = new Dictionary<Visual3D, ModelVisual3D>();

            AddModelVisual3DObjects();
        }

        private void AddContentVisual3DObjects()
        {
            var rootContentVisual3D = new ContentVisual3D("Root_ContentVisual3D");
            rootContentVisual3D.Transform = new TranslateTransform3D(-200, 0, 0);

            MainViewport.Children.Add(rootContentVisual3D);


            
            var bottomContentVisual3D = new ContentVisual3D("Bottom_ContentVisual3D");

            var bottomBox3D = new BoxVisual3D() { CenterPosition = new Point3D(0, 0, 0), Size = new Size3D(200, 20, 120), Material = new DiffuseMaterial(Brushes.Green) };
            bottomContentVisual3D.Children.Add(bottomBox3D);

            // You can add child Visual3D objects to ContentVisual3D
            rootContentVisual3D.Children.Add(bottomContentVisual3D);


            var left1ContentVisual3D = new ContentVisual3D("Left_1_ContentVisual3D");

            var left1Box3D = new BoxVisual3D() { CenterPosition = new Point3D(-50, 50, 0), Size = new Size3D(80, 60, 100), Material = new DiffuseMaterial(Brushes.LimeGreen) };
            left1ContentVisual3D.Children.Add(left1Box3D);


            var right1ContentVisual3D = new ContentVisual3D("Right_1_ContentVisual3D");

            var right1Box3D = new BoxVisual3D() { CenterPosition = new Point3D(50, 40, 0), Size = new Size3D(80, 20, 100), Material = new DiffuseMaterial(Brushes.LimeGreen) };
            right1ContentVisual3D.Children.Add(right1Box3D);


            bottomContentVisual3D.Children.Add(left1ContentVisual3D);
            bottomContentVisual3D.Children.Add(right1ContentVisual3D);


            var right21ContentVisual3D = new ContentVisual3D("Right_2_1_ContentVisual3D");

            var right2_1Box3D = new BoxVisual3D() { CenterPosition = new Point3D(30, 70, 0), Size = new Size3D(30, 20, 80), Material = new DiffuseMaterial(Brushes.PaleGreen) };
            right21ContentVisual3D.Children.Add(right2_1Box3D);


            var right22ContentVisual3D = new ContentVisual3D("Right_2_2_ContentVisual3D");

            // With ContentVisual3D we can also show Model3D (GeometryModel3D or Model3DGroup) objects.
            // To do that we set it to ContentVisual3D.Content property or specify it in the constructor.
            var right2_2Model3D = Ab3d.Models.Model3DFactory.CreateBox(new Point3D(70, 70, 0), new Size3D(30, 20, 80), new DiffuseMaterial(Brushes.PaleGreen));
            right22ContentVisual3D.Content = right2_2Model3D;


            right1ContentVisual3D.Children.Add(right21ContentVisual3D);
            right1ContentVisual3D.Children.Add(right22ContentVisual3D);


            Ab3d.Utilities.ModelIterator.IterateModelVisualsObjects(rootContentVisual3D.Children, null,
                delegate(ModelVisual3D visual3D, Transform3D transform3D)
                {
                    CreateIsVisibleCheckBoxes(visual3D, ContentVisualStackPanel);
                });
        }
        
        private void AddModelVisual3DObjects()
        {
            var rootModelVisual3D = new ModelVisual3D();

            var transform3DGroup = new Transform3DGroup();
            transform3DGroup.Children.Add(new RotateTransform3D(new AxisAngleRotation3D(new Vector3D(0, 1, 0), -90)));
            transform3DGroup.Children.Add(new TranslateTransform3D(0, 0, 200));
            rootModelVisual3D.Transform = transform3DGroup;

            rootModelVisual3D.SetName("Root_ModelVisual3D"); // ModelVisual3D is not defined in Ab3d.PowerToys and therefore does not define the Name property and does not take name as a constructor's parameter. Therefore we need to use SetName extension method.

            MainViewport.Children.Add(rootModelVisual3D);


            
            var bottomModelVisual3D = new ModelVisual3D();
            bottomModelVisual3D.SetName("Bottom_ModelVisual3D");

            var bottomBox3D = new BoxVisual3D() { CenterPosition = new Point3D(0, 0, 0), Size = new Size3D(200, 20, 120), Material = new DiffuseMaterial(Brushes.Blue) };
            bottomModelVisual3D.Children.Add(bottomBox3D);

            // You can add child Visual3D objects to ModelVisual3D
            rootModelVisual3D.Children.Add(bottomModelVisual3D);


            var left1ModelVisual3D = new ModelVisual3D();
            left1ModelVisual3D.SetName("Left_1_ModelVisual3D");

            var left1Box3D = new BoxVisual3D() { CenterPosition = new Point3D(-50, 50, 0), Size = new Size3D(80, 60, 100), Material = new DiffuseMaterial(Brushes.DodgerBlue) };
            left1ModelVisual3D.Children.Add(left1Box3D);


            var right1ModelVisual3D = new ModelVisual3D();
            right1ModelVisual3D.SetName("Right_1_ModelVisual3D");

            var right1Box3D = new BoxVisual3D() { CenterPosition = new Point3D(50, 40, 0), Size = new Size3D(80, 20, 100), Material = new DiffuseMaterial(Brushes.DodgerBlue) };
            right1ModelVisual3D.Children.Add(right1Box3D);


            bottomModelVisual3D.Children.Add(left1ModelVisual3D);
            bottomModelVisual3D.Children.Add(right1ModelVisual3D);


            var right21ModelVisual3D = new ModelVisual3D();
            right21ModelVisual3D.SetName("Right_2_1_ModelVisual3D");

            var right2_1Box3D = new BoxVisual3D() { CenterPosition = new Point3D(30, 70, 0), Size = new Size3D(30, 20, 80), Material = new DiffuseMaterial(Brushes.LightSkyBlue) };
            right21ModelVisual3D.Children.Add(right2_1Box3D);


            var right22ModelVisual3D = new ModelVisual3D();
            right22ModelVisual3D.SetName("Right_2_2_ModelVisual3D");

            // With ModelVisual3D we can also show Model3D (GeometryModel3D or Model3DGroup) objects.
            // To do that we set it to ModelVisual3D.Content property or specify it in the constructor.
            var right2_2Model3D = Ab3d.Models.Model3DFactory.CreateBox(new Point3D(70, 70, 0), new Size3D(30, 20, 80), new DiffuseMaterial(Brushes.LightSkyBlue));
            right22ModelVisual3D.Content = right2_2Model3D;


            right1ModelVisual3D.Children.Add(right21ModelVisual3D);
            right1ModelVisual3D.Children.Add(right22ModelVisual3D);


            Ab3d.Utilities.ModelIterator.IterateModelVisualsObjects(rootModelVisual3D.Children, null,
                delegate(ModelVisual3D visual3D, Transform3D transform3D)
                {
                    CreateIsVisibleCheckBoxes(visual3D, ModelVisualStackPanel);
                });
        }


        private void CreateIsVisibleCheckBoxes(ModelVisual3D visual3D, StackPanel parentPanel)
        {
            var name = visual3D.GetName();

            if (string.IsNullOrEmpty(name))
                return; // Only show CheckBoxes for Visual3D objects that have Name set


            name = name.Replace("_ContentVisual3D", "").Replace("_ModelVisual3D", "").Replace("_", "."); // Remove ContentVisual3D and ModelVisual3D text
            var hierarchyLevel = GetHierarchyLevel(visual3D);

            var checkBox = new CheckBox()
            {
                Content   = name,
                IsChecked = true,
                Margin = new Thickness((hierarchyLevel - 2) * 16, 3, 0, 0) // Indent based on how deep in the hierarchy the Visual3D is (level 2 is the base for our models).
            };

            checkBox.Checked   += (sender, args) => SetVisibility(visual3D, isVisible: true);
            checkBox.Unchecked += (sender, args) => SetVisibility(visual3D, isVisible: false);

            parentPanel.Children.Add(checkBox);
        }

        private void SetVisibility(Visual3D visual3D, bool isVisible)
        {
            var contentVisual3D = visual3D as ContentVisual3D;
            if (contentVisual3D != null)
            {
                // Visibility of ContentVisual3D can be easily changed with setting IsVisible property.
                // When IsVisible is set to false, the DirectX resources are preserved, so the objects can be quickly shown again when the property is set back to true.
                // Also processing the change of IsVisible property is very fast in DXEngine and do not require any bigger processing of DXEngine objects.
                contentVisual3D.IsVisible = isVisible;
            }
            else
            {
                // To change visibility of ModelVisual3D objects, we need to add to or remove from parent ModelVisual3D.
                // When a Visual3D object is removed from its parent (and if it is not added back to another parent object), then its DirectX resources are disposed.
                // This means that when the objects will be shown again (added to parent ModelVisual3D), then the DirectX resources will need to be generated again.
                // What is more, changing the number of child objects require a bigger update of DXEngine objects (RenderingQueues are regenerated) and this is also much slower then using ContentVisual3D.

                if (isVisible)
                {
                    // Show visual3D:
                    // First get saved parent ModelVisual3D
                    ModelVisual3D parentVisual3D;
                    if (_parentModelVisuals.TryGetValue(visual3D, out parentVisual3D))
                    {
                        parentVisual3D.Children.Add(visual3D);
                        _parentModelVisuals.Remove(visual3D);
                    }
                }
                else
                {
                    var parentVisual3D = VisualTreeHelper.GetParent(visual3D) as ModelVisual3D;
                    if (parentVisual3D != null)
                    {
                        _parentModelVisuals[visual3D] = parentVisual3D;
                        parentVisual3D.Children.Remove(visual3D);
                    }
                }
            }
        }

        private int GetHierarchyLevel(Visual3D visual3D)
        {
            int      level = 0;
            Visual3D parent = visual3D;

            do
            {
                level++;
                parent = VisualTreeHelper.GetParent(parent) as Visual3D;
            } while (parent != null);

            return level;
        }
    }
}
