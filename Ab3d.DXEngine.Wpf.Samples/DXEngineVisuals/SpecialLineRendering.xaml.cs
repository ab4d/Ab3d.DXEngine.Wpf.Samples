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
using Ab3d.DirectX;
using Ab3d.DirectX.Materials;
using Ab3d.DirectX.Models;
using Ab3d.Visuals;

namespace Ab3d.DXEngine.Wpf.Samples.DXEngineVisuals
{
    /// <summary>
    /// Interaction logic for SpecialLineRendering.xaml
    /// </summary>
    public partial class SpecialLineRendering : Page
    {
        private GeometryModel3D _wireframeGeometryModel3D;
        private LineMaterial _dxLineMaterial;
        private HiddenLineMaterial _hiddenLineMaterial;

        private Dictionary<object, SceneNode> _sceneNodesDictionary;

        public SpecialLineRendering()
        {
            InitializeComponent();


            // First create HiddenLineMaterial
            _hiddenLineMaterial = new HiddenLineMaterial()
            {
                LineColor = Colors.Yellow.ToColor4(),
                LineThickness = 1f, // Use very small line thickness (smaller than 1)
                LinePattern = 0x1111,
            };

            CreateTest3DObjects();

            MainDXViewportView.DXSceneInitialized += delegate (object sender, EventArgs args)
            {
                CreateSceneNodesDictionary();

                // After the DXScene was initialized and the DXEngine's SceneNodes objects are created, 
                // we can set advanced DXEngine properties
                ShowVisibleAndHiddenLines();
            };


            // IMPORTANT:
            // It is very important to call Dispose method on DXSceneView after the control is not used any more (see help file for more info)
            this.Unloaded += delegate
            {
                if (_hiddenLineMaterial != null)
                {
                    _hiddenLineMaterial.Dispose();
                    _hiddenLineMaterial = null;
                }

                if (_dxLineMaterial != null)
                {
                    _dxLineMaterial.Dispose();
                    _dxLineMaterial = null;
                }

                MainDXViewportView.Dispose();
            };
        }


        private void LineTypesRadioButtonChanged(object sender, RoutedEventArgs e)
        {
            if (!this.IsLoaded)
                return;

            // First recreate all 3D objects to reset their attributes and settings
            CreateTest3DObjects();
            MainDXViewportView.Update();

            if (StandardLinesRadioButton.IsChecked ?? false)
            {
                // Nothing to do - just show standard lines
            }
            else if (OnlyHiddenLinesRadioButton.IsChecked ?? false)
            {
                ShowOnlyHiddenLines();
            }
            else if (StandardAndHiddenLinesRadioButton.IsChecked ?? false)
            {
                ShowVisibleAndHiddenLines();
            }
            else if (AlwaysVisibleLines1RadioButton.IsChecked ?? false)
            {
                ShowAlwaysVisibleLinesWithDXAttribute();
            }
            else if (AlwaysVisibleLines2RadioButton.IsChecked ?? false)
            {
                ShowAlwaysVisibleLinesAdvanced();
            }
        }

        private void ShowVisibleAndHiddenLines()
        {
            // To show both visible and hidden lines we need to render each line twice:
            // once with standard settings to shew visible part of the one,
            // once with using HiddenLineMaterial to show the hidden part of the line.

            // Now we will clone the existing 3D lines
            var existingLineVisuals = TestObjectsModelVisual3D.Children.OfType<BaseLineVisual3D>().ToList();

            var newLineVisuals = new List<BaseLineVisual3D>();
            foreach (var lineVisual3D in existingLineVisuals)
            {
                var clonedLineVisual = CloneLineVisuals(lineVisual3D);

                TestObjectsModelVisual3D.Children.Add(clonedLineVisual);
                newLineVisuals.Add(clonedLineVisual);
            }

            // After adding new WPF objects to the scene, we need to manually call Update to create DXEngine's SceneNode objects that will be needed later
            MainDXViewportView.Update();

            
            // We need to update the _sceneNodesDictionary because we have changed the scene
            CreateSceneNodesDictionary();



            // Now change the materials of the clones lines to hiddenLineMaterial
            foreach (var newLineVisual3D in newLineVisuals)
                ChangeLineMaterial(newLineVisual3D, _hiddenLineMaterial);


            if (_wireframeGeometryModel3D != null)
            {
                // Clone the GeometryModel3D that shows teapot wireframe and use hiddenLineMaterial to render it
                var newWpfWireframeMaterial = new DiffuseMaterial(Brushes.Red);
                newWpfWireframeMaterial.SetUsedDXMaterial(_hiddenLineMaterial);

                var geometryModel3D = new GeometryModel3D(_wireframeGeometryModel3D.Geometry, newWpfWireframeMaterial);
                geometryModel3D.Transform = _wireframeGeometryModel3D.Transform;
                var modelVisual3D = new ModelVisual3D()
                {
                    Content = geometryModel3D
                };

                TestObjectsModelVisual3D.Children.Add(modelVisual3D);
            }
        }

        private void ShowOnlyHiddenLines()
        {
            foreach (var lineVisual3D in TestObjectsModelVisual3D.Children.OfType<BaseLineVisual3D>())
                ChangeLineMaterial(lineVisual3D, _hiddenLineMaterial);


            if (_wireframeGeometryModel3D != null)
            {
                var newWpfWireframeMaterial = new DiffuseMaterial(Brushes.Red);
                newWpfWireframeMaterial.SetUsedDXMaterial(_hiddenLineMaterial);

                _wireframeGeometryModel3D.Material = newWpfWireframeMaterial;
            }
        }

        private void ShowAlwaysVisibleLinesWithDXAttribute()
        {
            foreach (var lineVisual3D in TestObjectsModelVisual3D.Children.OfType<BaseLineVisual3D>())
            {
                // The easiest way to disable reading ZBuffer is to set ReadZBuffer DXAttribute with using SetDXAttribute extension method.
                // This method adds the specified ReadZBuffer and its value (false) to the WPF objects.
                // This way the DXEngine can read the value when creating the SceneNode object from WPF object.
                // The DXAttributes also support change notifications, so when the value is changed, the SceneNode object is notified about that.
                lineVisual3D.SetDXAttribute(DXAttributeType.ReadZBuffer, false);
            }

            // If we would be using WireframeVisual3D, we could just use SetDXAttribute the set ReadZBuffer to false:
            //_wireframeVisual3D.SetDXAttribute(DXAttributeType.ReadZBuffer, false);

            ShowAlwaysVisibleLinesForWireframeTeapot();
        }

        // Create a _sceneNodesDictionary that will allow us to get very quick way
        // of getting SceneNode from a Wpf object.
        // On a complex scene with lots of SceneNodes this is much faster then using
        // the MainDXViewportView.GetSceneNodeForWpfObject because this method
        // goes through each SceneNode in the scene and for each SceneNode calls IsMyWpfObject method.
        private void CreateSceneNodesDictionary()
        {
            _sceneNodesDictionary = new Dictionary<object, SceneNode>();

            MainDXViewportView.DXScene.RootNode.ForEachChildNode<SceneNode>(delegate(SceneNode sceneNode)
            {
                if (sceneNode is WpfModelVisual3DNode)
                    _sceneNodesDictionary.Add(((WpfModelVisual3DNode)sceneNode).ModelVisual3D, sceneNode);

                else if (sceneNode is WpfGeometryModel3DNode)
                    _sceneNodesDictionary.Add(((WpfGeometryModel3DNode)sceneNode).GeometryModel3D, sceneNode);

                else if (sceneNode is WpfModel3DGroupNode)
                    _sceneNodesDictionary.Add(((WpfModel3DGroupNode)sceneNode).Model3DGroup, sceneNode);

                else if (sceneNode is WpfWireframeVisual3DNode)
                    _sceneNodesDictionary.Add(((WpfWireframeVisual3DNode)sceneNode).WireframeVisual3D, sceneNode);

                // There are also other SceneNode type but they are not handled here - this will be greatly simplified in the future.
            });

            // Another way to fill the _sceneNodesDictionary is to use the OnSceneNodeCreatedAction DXAttribute
            //foreach (var lineVisual3D in TestObjectsModelVisual3D.Children.OfType<BaseLineVisual3D>())
            //{
            //    // We add special DXEngine attribute to the WPF's BaseLineVisual3D object, 
            //    // that will call the provided Action when the DXEngine will create a 
            //    // SceneNode object from the WPF object.
            //    // This is then added to the _sceneNodesDictionary
            //    lineVisual3D.SetDXAttribute(
            //        DXAttributeType.OnSceneNodeCreatedAction, 
            //        new Action<SceneNode>(delegate(SceneNode sceneNode)
            //        {
            //            _sceneNodesDictionary.Add(lineVisual3D, sceneNode);
            //        }));
            //}
        }

        private SceneNode GetSceneNodeForWpfObject(object wpfObject)
        {
            SceneNode sceneNode = null;

            // Use _sceneNodesDictionary if possible
            if (_sceneNodesDictionary != null)
                _sceneNodesDictionary.TryGetValue(wpfObject, out sceneNode);


            // If SceneNode is not found, we an use the GetSceneNodeForWpfObject method.
            // Note that this method is very slow because it goes through each SceneNode in the scene
            // and for each SceneNode calls IsMyWpfObject method.
            if (sceneNode == null)
                sceneNode = MainDXViewportView.GetSceneNodeForWpfObject(wpfObject);

            return sceneNode;
        }

        private void ShowAlwaysVisibleLinesAdvanced()
        {
            foreach (var lineVisual3D in TestObjectsModelVisual3D.Children.OfType<BaseLineVisual3D>())
                SetReadZBufferAdvanced(lineVisual3D, readZBuffer: false);

            // The following commented line can be used when we would be using WireframeVisual3D:
            //SetReadZBufferAdvanced(_wireframeVisual3D, readZBuffer: false);

            ShowAlwaysVisibleLinesForWireframeTeapot();
        }

        private void ShowAlwaysVisibleLinesForWireframeTeapot()
        {
            // We just set the ReadZBuffer to false on the _dxLineMaterial
            _dxLineMaterial.ReadZBuffer = false;

            // We also need to notify the change on the object that is using this material
            var sceneNode = GetSceneNodeForWpfObject(_wireframeGeometryModel3D);
            if (sceneNode != null)
                sceneNode.NotifySceneNodeChange(SceneNode.SceneNodeDirtyFlags.MaterialChanged);
        }


        #region Create scene objects

        private void CreateTest3DObjects()
        {
            TestObjectsModelVisual3D.Children.Clear();

            var objectMaterial = new DiffuseMaterial(Brushes.Silver);


            CreateCylinderWithCircles(new Point3D(0, 0, -5), 10, 30, objectMaterial);

            CreateBoxWithEdgeLines(new Point3D(0, 10, 40), new Size3D(20, 20, 20), objectMaterial);

            CreateTeapotWireframeModel(new Point3D(0, 10, -50), new Size3D(50, 50, 50), objectMaterial);
        }

        private void CreateCylinderWithCircles(Point3D bottomCenterPosition, double radius, double height, DiffuseMaterial material)
        {
            var cylinderVisual3D = new CylinderVisual3D()
            {
                BottomCenterPosition = bottomCenterPosition,
                Radius = radius,
                Height = height,
                Material = material
            };

            TestObjectsModelVisual3D.Children.Add(cylinderVisual3D);


            int circlesCount = 4;
            for (int i = 0; i < circlesCount; i++)
            {
                var lineArcVisual3D = new LineArcVisual3D()
                {
                    CircleCenterPosition = bottomCenterPosition + Constants.UpVector * height * (double)i / (double)(circlesCount - 1),
                    Radius = radius * 1.02,
                    CircleNormal = Constants.UpVector,
                    ZeroAngleDirection = Constants.XAxis,
                    StartAngle = 0,
                    EndAngle = 360,
                    LineColor = Colors.Yellow,
                    LineThickness = 3
                };

                TestObjectsModelVisual3D.Children.Add(lineArcVisual3D);
            }
        }

        private void CreateBoxWithEdgeLines(Point3D centerPosition, Size3D size, DiffuseMaterial material)
        {
            var boxVisual3D = new BoxVisual3D()
            {
                CenterPosition = centerPosition,
                Size = size,
                Material = material
            };

            TestObjectsModelVisual3D.Children.Add(boxVisual3D);

            var wireBoxVisual3D = new WireBoxVisual3D()
            {
                CenterPosition = centerPosition,
                Size = size,
                LineColor = Colors.Yellow,
                LineThickness = 3
            };

            // Set LineDepthBias to prevent rendering wireframe at the same depth as the 3D objects.
            // This creates much nicer 3D lines. See the LineDepthBiasSample for more information.
            wireBoxVisual3D.SetDXAttribute(DXAttributeType.LineDepthBias, 0.1);

            TestObjectsModelVisual3D.Children.Add(wireBoxVisual3D);
        }

        private void CreateTeapotWireframeModel(Point3D centerPosition, Size3D size, DiffuseMaterial material)
        {
            // The most common way to show wireframe models in DXEngine is to use WireframeVisual3D from Ab3d.PowerToys - see commented code below:
            //var wireframeVisual3D = new WireframeVisual3D()
            //{
            //    WireframeType = WireframeVisual3D.WireframeTypes.WireframeWithOriginalSolidModel,
            //    UseModelColor = false,
            //    LineThickness = 1,
            //    LineColor = Colors.Yellow,
            //    Transform = new TranslateTransform3D(0, 0, -50)
            //};
            //
            //wireframeVisual3D.OriginalModel = teapotModel;
            //// Set LineDepthBias to prevent rendering wireframe at the same depth as the 3D objects.
            //// This creates much nicer 3D lines. See the LineDepthBiasSample for more information.
            //wireframeVisual3D.SetDXAttribute(DXAttributeType.LineDepthBias, 0.1);

            // But in this sample we show special line rendering.
            // Therefore we will create standard WPF GeometryModel3D and then apply LineMaterial to it so the model will be rendered with wireframe lines

            // First read teapot model from Teapot.obj file
            var readerObj = new Ab3d.ReaderObj();
            var teapotModel = readerObj.ReadModel3D(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"Resources\Models\Teapot.obj")) as GeometryModel3D;

            if (teapotModel == null)
                return;

            // Get the teapot MeshGeometry3D
            var meshGeometry3D = (MeshGeometry3D)teapotModel.Geometry;


            // Get transformation to scale and position the model to the centerPosition and size
            var bounds = meshGeometry3D.Bounds;

            double scaleX = size.X / bounds.SizeX;
            double scaleY = size.Y / bounds.SizeY;
            double scaleZ = size.Z / bounds.SizeZ;

            double minScale = Math.Min(scaleX, Math.Min(scaleY, scaleZ));
            scaleX = scaleY = scaleZ = minScale;

            var scaleTransform3D = new ScaleTransform3D(scaleX, scaleY, scaleZ);

            bounds = scaleTransform3D.TransformBounds(bounds);


            double cx = bounds.X + bounds.SizeX * 0.5;
            double cy = bounds.Y + bounds.SizeY * 0.5;
            double cz = bounds.Z + bounds.SizeZ * 0.5;

            var translateTransform3D = new TranslateTransform3D(centerPosition.X - cx, centerPosition.Y - cy, centerPosition.Z - cz);

            var transform3DGroup = new Transform3DGroup();
            transform3DGroup.Children.Add(scaleTransform3D);
            transform3DGroup.Children.Add(translateTransform3D);



            // First create the standard solid model with the specified material
            var geometryModel3D = new GeometryModel3D(meshGeometry3D, material);
            geometryModel3D.Transform = transform3DGroup;

            var modelVisual3D = new ModelVisual3D()
            {
                Content = geometryModel3D
            };

            TestObjectsModelVisual3D.Children.Add(modelVisual3D);


            // To render wireframe object, we first create a DXEngine material that is used to rendered lines or wireframe
            if (_dxLineMaterial == null)
            {
                _dxLineMaterial = new LineMaterial()
                {
                    LineThickness = 1,
                    LineColor = Colors.Yellow.ToColor4(),
                    DepthBias = 0.1f
                    // Set DepthBias to prevent rendering wireframe at the same depth as the 3D objects. This creates much nicer 3D lines because lines are rendered on top of 3D object and not in the same position as 3D object.
                };
            }
            else
            {
                _dxLineMaterial.ReadZBuffer = true;
            }

            // Now create standard WPF material and assign DXEngine's LineMaterial to it.
            // This will use the dxLineMaterial when the wpfLineMaterial will be rendered in DXEngine
            var wpfWireframeMaterial = new DiffuseMaterial(Brushes.Red);
            wpfWireframeMaterial.SetUsedDXMaterial(_dxLineMaterial);


            // Finally, create another GeometryModel3D, but this time we will use DXEngine's LineMaterial to render it
            _wireframeGeometryModel3D = new GeometryModel3D(meshGeometry3D, wpfWireframeMaterial);
            _wireframeGeometryModel3D.Transform = transform3DGroup;

            modelVisual3D = new ModelVisual3D()
            {
                Content = _wireframeGeometryModel3D
            };

            TestObjectsModelVisual3D.Children.Add(modelVisual3D);
        }

        #endregion

        #region Helper methods

        // This method supports only cloning LineArcVisual3D
        private BaseLineVisual3D CloneLineVisuals(BaseLineVisual3D lineVisual)
        {
            BaseLineVisual3D clonedLineVisual = null;

            var lineArcVisual3D = lineVisual as LineArcVisual3D;
            if (lineArcVisual3D != null)
            {
                clonedLineVisual = new LineArcVisual3D()
                {
                    CircleCenterPosition = lineArcVisual3D.CircleCenterPosition,
                    Radius = lineArcVisual3D.Radius,
                    CircleNormal = lineArcVisual3D.CircleNormal,
                    ZeroAngleDirection = lineArcVisual3D.ZeroAngleDirection,
                    StartAngle = lineArcVisual3D.StartAngle,
                    EndAngle = lineArcVisual3D.EndAngle,
                    LineColor = lineArcVisual3D.LineColor,
                    LineThickness = lineArcVisual3D.LineThickness
                };
            }
            else
            {
                var wireBoxVisual3D = lineVisual as WireBoxVisual3D;
                if (wireBoxVisual3D != null)
                {
                    clonedLineVisual = new WireBoxVisual3D()
                    {
                        CenterPosition = wireBoxVisual3D.CenterPosition,
                        Size = wireBoxVisual3D.Size,
                        LineColor = wireBoxVisual3D.LineColor,
                        LineThickness = wireBoxVisual3D.LineThickness
                    };
                }
            }

            return clonedLineVisual;
        }



        // SetReadZBufferAdvanced does not set the DXAttribute on WPF object,
        // but instead sets the ReadZBuffer on DXEngine's objects directly.
        // This can be done with setting the ReadZBuffer on created WpfWireframeVisual3DNode object or
        // setting ReadZBuffer on used LineMaterial.
        //
        // It is recommended to use SetDXAttribute method to change ReadZBuffer and other properties.
        // This approach with SetReadZBufferAdvanced is shown only to show a sneak view into what is going on behind the scenes.
        // It also demonstrates an advanced DXEngine usage that may be useful in same case.
        private void SetReadZBufferAdvanced(Visual3D visual3D, bool readZBuffer)
        {
            // First get the DXEngine's SceneNode that was created from WPF's Visual3D
            var sceneNode = GetSceneNodeForWpfObject(visual3D);

            // When the created SceneNode is WpfWireframeVisual3DNode we can simply change its ReadZBuffer property.
            var wpfWireframeVisual3DNode = sceneNode as WpfWireframeVisual3DNode;
            if (wpfWireframeVisual3DNode != null)
            {
                wpfWireframeVisual3DNode.ReadZBuffer = readZBuffer;

                // IMPORTANT:
                // When we manually change the properties of materials or SceneNodes,
                // we also need to notify the changed SceneNode about the change.
                // Without this the DXEngine will not re-render the scene because it will think that there is no change.
                wpfWireframeVisual3DNode.NotifySceneNodeChange(SceneNode.SceneNodeDirtyFlags.MaterialChanged);
            }
            else
            {
                // Otherwise we need to get the ScreenSpaceLineNode.
                var screenSpaceLineNode = sceneNode as ScreenSpaceLineNode;

                if (screenSpaceLineNode == null)
                {
                    // ScreenSpaceLineNode is usually created as a child node of a WpfModelVisual3DNode:
                    // WpfModelVisual3DNode is created for LineArcVisual3D (because it is derived from BaseLineVisual3D and that is derived from ModelVisual3D)
                    if (sceneNode != null && sceneNode.ChildNodesCount == 1)
                        screenSpaceLineNode = sceneNode.ChildNodes[0] as ScreenSpaceLineNode;
                }

                if (screenSpaceLineNode != null)
                {
                    // Once we have the ScreenSpaceLineNode, we can get the used LineMaterial that has ReadZBuffer
                    var lineMaterial = screenSpaceLineNode.LineMaterial as LineMaterial;
                    if (lineMaterial != null)
                    {
                        lineMaterial.ReadZBuffer = readZBuffer; // Disable reading Z buffer

                        // NOTE:
                        // Line material also support disabling writing to z-buffer. 
                        // This is commonly not very useful. But if you want to use that, you can do that with setting WriteZBuffer to false:
                        //lineMaterial.WriteZBuffer = false;

                        // IMPORTANT:
                        // When we manually change the properties of materials or SceneNodes,
                        // we also need to notify the changed SceneNode about the change.
                        // Without this the DXEngine will not re-render the scene because it will think that there is no change.
                        screenSpaceLineNode.NotifySceneNodeChange(SceneNode.SceneNodeDirtyFlags.MaterialChanged);
                    }
                }
            }
        }

        private void ChangeLineMaterial(BaseLineVisual3D visual3D, Ab3d.DirectX.Materials.ILineMaterial newDXMaterial)
        {
            // First get the DXEngine's SceneNode that was created from WPF's Visual3D
            var sceneNode = GetSceneNodeForWpfObject(visual3D);

            // Otherwise we need to get the ScreenSpaceLineNode.
            var screenSpaceLineNode = sceneNode as ScreenSpaceLineNode;

            if (screenSpaceLineNode == null)
            {
                // ScreenSpaceLineNode is usually created as a child node of a WpfModelVisual3DNode:
                // WpfModelVisual3DNode is created for LineArcVisual3D (because it is derived from BaseLineVisual3D and that is derived from ModelVisual3D)
                if (sceneNode != null && sceneNode.ChildNodesCount == 1)
                    screenSpaceLineNode = sceneNode.ChildNodes[0] as ScreenSpaceLineNode;
            }

            if (screenSpaceLineNode != null)
            {
                // Once we have the ScreenSpaceLineNode, we can get the used LineMaterial that has ReadZBuffer
                screenSpaceLineNode.LineMaterial = newDXMaterial;

                // IMPORTANT:
                // When we manually change the properties of materials or SceneNodes,
                // we also need to notify the changed SceneNode about the change.
                // Without this the DXEngine will not re-render the scene because it will think that there is no change.
                screenSpaceLineNode.NotifySceneNodeChange(SceneNode.SceneNodeDirtyFlags.MaterialChanged);
            }
        }

        #endregion
    }
}
