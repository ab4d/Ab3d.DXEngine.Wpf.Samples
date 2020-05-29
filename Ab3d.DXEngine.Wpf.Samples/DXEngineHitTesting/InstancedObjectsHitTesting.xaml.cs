using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
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
using Ab3d.Common.EventManager3D;
using Ab3d.Visuals;
using SharpDX;
using Ab3d.DirectX;
using Ab3d.DirectX.Utilities;
using Ab3d.Meshes;
using Ab3d.Utilities;
using InstanceData = Ab3d.DirectX.InstanceData;

namespace Ab3d.DXEngine.Wpf.Samples.DXEngineHitTesting
{
    /// <summary>
    /// Interaction logic for InstancedObjectsHitTesting.xaml
    /// </summary>
    public partial class InstancedObjectsHitTesting : Page
    {
        private bool _useDXEngineHitTesting = true;

        private Ab3d.Utilities.EventManager3D _wpfEventManager;
        private DXEventManager3D _dxEventManager3D;

        private InstanceData[] _instancedData;
        private MeshGeometry3D _instanceMeshGeometry3D;

        private Stopwatch _stopwatch;

        private Color4 _savedColor4;
        private int _selectedInstanceIndex = -1;

        private InstancedMeshGeometryVisual3D _instancedMeshGeometryVisual3D;

        public InstancedObjectsHitTesting()
        {
            InitializeComponent();

            // Setting GraphicsProfiles tells DXEngine which graphics device it should use (and what devices are fallback devices if the first device cannot be initialized)
            // ApplicationContext.Current.GraphicsProfiles is changed by changing the "Graphics settings" with clicking on its button in upper left part of application.
            MainDXViewportView.GraphicsProfiles = DirectX.Client.Settings.DXEngineSettings.Current.GraphicsProfiles;

            if (DesignerProperties.GetIsInDesignMode(this))
                return;


            // Create instance MeshGeometry3D
            _instanceMeshGeometry3D = new Ab3d.Meshes.SphereMesh3D(centerPosition: new Point3D(0, 0, 0), radius: 5, segments: 20, generateTextureCoordinates: false).Geometry;

            // Prepare data for 8000 instances (each InstanceData represents one instance's Color and its World matrix that specifies its position, scale and rotation)
            _instancedData = CreateInstancesData(new Point3D(0, 200, 0), new Size3D(400, 400, 400), 20, 20, 20);


            MainDXViewportView.SceneRendered += MainDXViewportViewOnSceneRendered;


            // IMPORTANT:
            // It is very important to call Dispose method on DXSceneView after the control is not used any more (see help file for more info)
            this.Unloaded += (sender, args) => MainDXViewportView.Dispose();
        }

        private void MainDXViewportViewOnSceneRendered(object sender, EventArgs e)
        {
            if (_instancedMeshGeometryVisual3D == null)
            {
                CreateScene(_useDXEngineHitTesting);
                return;
            }

            if (_stopwatch != null)
            {
                _stopwatch.Stop();
                InfoTextBlock.Text = string.Format("Time to first frame: {0:0.0} ms", _stopwatch.Elapsed.TotalMilliseconds);

                _stopwatch = null;
            }
        }

        private void CreateScene(bool useDXEngineHitTesting)
        {
            ObjectsPlaceholder.Children.Clear();

            if (_dxEventManager3D != null)
            {
                _dxEventManager3D.ResetEventSources3D();
                _dxEventManager3D = null;
            }

            if (_wpfEventManager != null)
            {
                _wpfEventManager.ResetEventSources3D();
                _wpfEventManager = null;
            }

            if (_instancedMeshGeometryVisual3D != null)
                _instancedMeshGeometryVisual3D.Dispose();


            _stopwatch = new Stopwatch();
            _stopwatch.Start();

            Mouse.OverrideCursor = Cursors.Wait;


            // Create InstancedGeometryVisual3D
            _instancedMeshGeometryVisual3D = new InstancedMeshGeometryVisual3D(_instanceMeshGeometry3D);

            _instancedMeshGeometryVisual3D.InstancesData = _instancedData;


            // Setup hit testing
            if (useDXEngineHitTesting)
            {
                // Use DXEventManager3D from Ab3d.DXEngine - it has optimized hit testing for instanced objects
                _dxEventManager3D = new Ab3d.DirectX.Utilities.DXEventManager3D(MainDXViewportView);

                var visualEventSource3D = new Ab3d.DirectX.Utilities.VisualEventSource3D(_instancedMeshGeometryVisual3D);
                visualEventSource3D.MouseEnter += delegate (object sender, DirectX.Common.EventManager3D.Mouse3DEventArgs e)
                {
                    var dxRayInstancedHitTestResult = e.RayHitResult as DXRayInstancedHitTestResult;
                    if (dxRayInstancedHitTestResult != null)
                        ProcessMouseEnter(dxRayInstancedHitTestResult.HitInstanceIndex);
                };

                visualEventSource3D.MouseMove += delegate (object sender, DirectX.Common.EventManager3D.Mouse3DEventArgs e)
                {
                    var dxRayInstancedHitTestResult = e.RayHitResult as DXRayInstancedHitTestResult;
                    if (dxRayInstancedHitTestResult != null)
                        ProcessMouseMove(dxRayInstancedHitTestResult.HitInstanceIndex);
                };

                visualEventSource3D.MouseLeave += delegate (object sender, DirectX.Common.EventManager3D.Mouse3DEventArgs e)
                {
                    ProcessMouseLeave();
                };

                _dxEventManager3D.RegisterEventSource3D(visualEventSource3D);
            }
            else
            {
                // 
                // IMPORTANT:
                //
                // To make WPF hit testing work (also used by EventManager3D), you need to set the IsWpfHitTestVisible to true.
                // This increases initialization time because WPF objects needs to be created for each instance, but this makes the WPF hit testing work.
                _instancedMeshGeometryVisual3D.IsWpfHitTestVisible = true;


                _wpfEventManager = new Ab3d.Utilities.EventManager3D(MainViewport);

                // Because Viewport3D is actually not shown, we need to specify different WPF's object for the source of mouse events - this could be MainDXViewportView or even better a parent Border
                _wpfEventManager.CustomEventsSourceElement = MainDXViewportView;

                var visualEventSource3D = new Ab3d.Utilities.VisualEventSource3D(_instancedMeshGeometryVisual3D);
                visualEventSource3D.MouseEnter += delegate(object sender, Mouse3DEventArgs e)
                {
                    if (e.RayHitResult == null || e.RayHitResult.ModelHit == null)
                        return; // This should not happen, but it is safer to have this check anyway

                    // Get instance index of the hit object
                    int hitInstanceIndex = GetHitInstanceIndex(e.RayHitResult);

                    ProcessMouseEnter(hitInstanceIndex);
                };
                    
                visualEventSource3D.MouseMove  += delegate(object sender, Mouse3DEventArgs e)
                {
                    if (e.RayHitResult == null || e.RayHitResult.ModelHit == null)
                        return; // This should not happen, but it is safer to have this check anyway

                    // Get instance index of the hit object
                    int hitInstanceIndex = GetHitInstanceIndex(e.RayHitResult);

                    ProcessMouseMove(hitInstanceIndex);
                };
                    
                visualEventSource3D.MouseLeave += delegate(object sender, Mouse3DEventArgs e)
                {
                    ProcessMouseLeave();
                };

                _wpfEventManager.RegisterEventSource3D(visualEventSource3D);
            }



            ObjectsPlaceholder.Children.Add(_instancedMeshGeometryVisual3D);

            Mouse.OverrideCursor = null;

            // If we would only change the InstancedData we would need to call Update method (but here this is not needed because we have set the data for the first time)
            //_instancedGeometryVisual3D.Update();
        }

        private void ProcessMouseEnter(int instanceIndex)
        {
            MainDXViewportView.Cursor = Cursors.Hand;

            // Change the selected instance color to yellow and increase its size by 2
            // Get the index of selected instance we need the bounds of hit object
            UpdateSelectedInstance(instanceIndex);

            // After instance data is changed we need to call Update method
            _instancedMeshGeometryVisual3D.Update();
        }

        private void ProcessMouseMove(int instanceIndex)
        {
            if (instanceIndex != _selectedInstanceIndex)
            {
                // Change the selected instance color to yellow and increase its size by 2
                // Get the index of selected instance we need the bounds of hit object
                UpdateSelectedInstance(instanceIndex);

                // After instance data is changed we need to call Update method
                _instancedMeshGeometryVisual3D.Update();
            }
        }

        private void ProcessMouseLeave()
        {
            MainDXViewportView.Cursor = null;

            ClearSelectedInstance();

            // After instance data is changed we need to call Update method
            _instancedMeshGeometryVisual3D.Update();
        }


        // returns instance index or -1 if instance not found
        private int GetHitInstanceIndex(RayMeshGeometry3DHitTestResult rayHitResult)
        {
            var modelHit = rayHitResult.ModelHit;

            // When WPF GeometryModel3D objects are crated with InstancedMeshGeometryVisual3D,
            // each GeometryModel3D has a InstancedMeshGeometryVisual3D.InstanceIndexProperty set to an int value
            // that represent an index of the instance.
            int hitInstanceIndex = (int)modelHit.GetValue(InstancedMeshGeometryVisual3D.InstanceIndexProperty);

            if (hitInstanceIndex == -1) // If InstanceIndexProperty was not set, then we get a default value of -1.
            {
                // In this case we need to use the slower path 
                // of finding the instance index from the bounds of the hit Model3D.

                // InstanceData.GetHitInstanceIndex method gets the instance index in the _instancedData array that has the bounds set to hitBounds
                // The last parameter (useOnlyMatrixTranslation) can be set to true when the InstanceData define only translation (OffsetX, OffsetY, OffsetZ) and no scale or rotation.
                // In this case a faster code path can be taken. When useOnlyMatrixTranslation is false (by default) a full matrix transformation is executed on each position.
                Rect3D hitBounds = rayHitResult.ModelHit.Bounds;
                hitInstanceIndex = InstanceData.GetHitInstanceIndex(hitBounds, _instanceMeshGeometry3D.Bounds, _instancedData, useOnlyMatrixTranslation: false);

                // If not found then hitInstanceIndex is -1
            }

            return hitInstanceIndex;
        }

        private void UpdateSelectedInstance(int hitInstanceIndex)
        {
            // Reset the previously selected instance
            ClearSelectedInstance();

            if (hitInstanceIndex == -1)
                return; // We did not find the instance index


            // Save the current instance color and then set a Yellow color to selected instance
            _savedColor4 = _instancedData[hitInstanceIndex].DiffuseColor;
            _instancedData[hitInstanceIndex].DiffuseColor = Colors.Yellow.ToColor4();

            // Set scale to selected instance to 2
            _instancedData[hitInstanceIndex].World.M11 = 2;
            _instancedData[hitInstanceIndex].World.M22 = 2;
            _instancedData[hitInstanceIndex].World.M33 = 2;

            _selectedInstanceIndex = hitInstanceIndex;
        }

        private void ClearSelectedInstance()
        {
            if (_selectedInstanceIndex != -1)
            {
                // Reset the color
                _instancedData[_selectedInstanceIndex].DiffuseColor = _savedColor4;

                // Set scale back to 1
                _instancedData[_selectedInstanceIndex].World.M11 = 1;
                _instancedData[_selectedInstanceIndex].World.M22 = 1;
                _instancedData[_selectedInstanceIndex].World.M33 = 1;

                _selectedInstanceIndex = -1;
            }
        }



        private InstanceData[] CreateInstancesData(Point3D center, Size3D size, int xCount, int yCount, int zCount)
        {
            var instancedData = new InstanceData[xCount * yCount * zCount];

            float xStep = xCount <= 1 ? 0 : (float)(size.X / (xCount - 1));
            float yStep = yCount <= 1 ? 0 : (float)(size.Y / (yCount - 1));
            float zStep = zCount <= 1 ? 0 : (float)(size.Z / (zCount - 1));

            int i = 0;
            for (int y = 0; y < yCount; y++)
            {
                float yPos = (float)(center.Y - (size.Y / 2.0) + (y * yStep));

                for (int z = 0; z < zCount; z++)
                {
                    float zPos = (float)(center.Z - (size.Z / 2.0) + (z * zStep));

                    for (int x = 0; x < xCount; x++)
                    {
                        float xPos = (float)(center.X - (size.X / 2.0) + (x * xStep));

                        instancedData[i].World = SharpDX.Matrix.Translation(xPos, yPos, zPos);

                        // If we would also need to scale the mesh, we could create the matrix with the following code:
                        //instancedData[i].World = new SharpDX.Matrix(xScale, 0, 0, 0,
                        //                                            0, yScale, 0, 0,
                        //                                            0, 0, zScale, 0,
                        //                                            xPos, yPos, zPos, 1);

                        //instancedData[i].DiffuseColor = new SharpDX.Color4(new SharpDX.Color3(0.3f + ((float)x / (float)xCount) * 0.7f, 
                        //                                                                      0.3f + ((float)y / (float)yCount) * 0.7f,
                        //                                                                      0.3f + ((float)y / (float)yCount) * 0.7f));

                        instancedData[i].DiffuseColor = new SharpDX.Color4(new SharpDX.Color3(0.1f + ((float)y / (float)yCount) * 0.9f,
                                                                                              (float)Math.Abs(0.5 - ((float)x / (float)xCount)) * 2.0f,
                                                                                              0.1f + ((float)(yCount - y) / (float)yCount) * 0.9f));

                        // Use WPF's Orange color:
                        //instancedData[i].Color = Colors.Orange.ToColor4();

                        i++;
                    }
                }
            }

            return instancedData;
        }

        private void WpfHitTestingRadioButton_OnClick(object sender, RoutedEventArgs e)
        {
            _useDXEngineHitTesting = false;

            // If _instancedMeshGeometryVisual3D is null after the next frame is rendered,
            // the CreateScene method with _useDXEngineHitTesting parameter will be called.
            // This way the scene will be immediately cleared and when this will be show a new scene can be created.
            _instancedMeshGeometryVisual3D = null;

            ObjectsPlaceholder.Children.Clear();
        }

        private void DXEngineTestingRadioButton_OnClick(object sender, RoutedEventArgs e)
        {
            _useDXEngineHitTesting = true;

            // If _instancedMeshGeometryVisual3D is null after the next frame is rendered,
            // the CreateScene method with _useDXEngineHitTesting parameter will be called.
            // This way the scene will be immediately cleared and when this will be show a new scene can be created.
            _instancedMeshGeometryVisual3D = null;

            ObjectsPlaceholder.Children.Clear();
        }
    }
}
