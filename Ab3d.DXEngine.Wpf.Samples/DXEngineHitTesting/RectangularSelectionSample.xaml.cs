using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using Ab3d.Controls;
using Ab3d.DirectX;
using Ab3d.DirectX.Controls;
using Ab3d.DirectX.Effects;
using Ab3d.DirectX.Models;
using Ab3d.Visuals;
using Color = System.Windows.Media.Color;
using Material = System.Windows.Media.Media3D.Material;

#if SHARPDX
using SharpDX;
#endif

namespace Ab3d.DXEngine.Wpf.Samples.DXEngineHitTesting
{
    /// <summary>
    /// Interaction logic for RectangularSelectionSample.xaml
    /// </summary>
    public partial class RectangularSelectionSample : Page
    {
        private DisposeList _disposables;

        private SolidColorEffect _solidColorEffect;
        private CustomActionRenderingStep _objectIdRenderingStep;

        private bool _isMouseSelectionStarted;
        private System.Windows.Point _startMousePosition;
        
        private byte[] _objectIdPixelsArray;
        private int _objectIdBitmapWidth;
        private int _objectIdBitmapHeight;

        private double _dpiScaleX, _dpiScaleY;

        private HashSet<uint> _selectedIds;
        private HashSet<DependencyObject> _selectedObjects;
        private Dictionary<GeometryModel3D, System.Windows.Media.Media3D.Material> _savedMaterials;
        private Dictionary<BaseLineVisual3D, System.Windows.Media.Color> _savedLineColors;

        private DiffuseMaterial _selectedDiffuseMaterial;
        private Model3DGroup _objectsModel3DGroup;
        private List<GeometryModel3D> _modelsToRemove;
        private List<BaseLineVisual3D> _linesToRemove;

#if DEBUG
        private List<int> _renderingQueueObjectsCounts;
#endif

        public RectangularSelectionSample()
        {
            InitializeComponent();


            BoundsIn2DInfoControl.InfoText =
@"The simplest technique to do rectangular selection is to convert the object's 3D bounds (axis aligned bounding box) into a 2D rectangle that represents the bounds on the screen. Then we can simply call IntersectsWith method that checks if the two 2D rectangles intersect.

Advantages:
- Very simple and fast when there is not a lot of 3D objects.
- Also selects the objects that are behind the objects closer to the camera.
- Can be used with only Ab3d.PowerToys (without Ab3d.DXEngine).

Disadvantages:
- Not accurate - the bounds of 3D objects and its bounds in 2D world are bigger then the the actual 3D object - selection is done before the user actually touches the 3D object.
- Slow when checking a lot of 3D objects.
- Cannot be used to select 3D lines.";
            

            ObjectIdMapInfoControl.InfoText =
                @"With Ab3d.DXEngine it is possible to render objects to a bitmap in such a way that each object is rendered with a different color where the color represents the object's id. When such a bitmap is rendered it is possible to get individual pixel colors and from that get the original object that was used to render the pixel.

Advantages:
- Pixel perfect accuracy.
- Fast when rendering a lot of objects.
- Can be used to select 3D lines.
- Can be extended to support some other selection types and not only rectangular selection.

Disadvantages:
- More complex (using custom rendering steps) than using simple bounding boxes.
- Slower when using simple 3D scene (DXEngine needs to set up the DirectX resources for another rendering pass; also much more memory is required).
- We need to find original WPF 3D object from DXEngine's RenderablePrimitive object.
- Cannot select objects that are behind some other objects that are closer to the camera.";
            

            MouseCameraControllerInfo1.AddCustomInfoLine(0, MouseCameraController.MouseAndKeyboardConditions.LeftMouseButtonPressed, "Rectangular selection");

            this.Cursor          = Cursors.Cross;
            OptionsBorder.Cursor = Cursors.Arrow;
            

            _selectedDiffuseMaterial = new DiffuseMaterial(Brushes.Red);

            _savedMaterials  = new Dictionary<GeometryModel3D, Material>();
            _savedLineColors = new Dictionary<BaseLineVisual3D, Color>();
            
            _disposables = new DisposeList();


            CreateTestScene();


            // Setup mouse events that will be used to show rectangular selection
            this.MouseLeftButtonDown += OnMouseLeftButtonDown;
            this.MouseMove += OnMouseMove;
            this.MouseLeftButtonUp += OnMouseLeftButtonUp;
            

            MainDXViewportView.DXSceneDeviceCreated += OnDxSceneDeviceCreated;

            this.Loaded += delegate (object sender, RoutedEventArgs args)
            {
                DXView.GetDpiScale(this, out _dpiScaleX, out _dpiScaleY);
            };

            this.Unloaded += delegate(object sender, RoutedEventArgs args)
            {
                if (_disposables != null)
                {
                    _disposables.Dispose();
                    _disposables = null;
                }

                MainDXViewportView.Dispose();
            };
        }


        private void OnDxSceneDeviceCreated(object sender, EventArgs e)
        {
            //
            // !!! IMPORTANT !!!
            //
            // Some rendering queues are being sorted by material. 
            // This improves performance by rendering objects with similar material one after another and
            // this reduces number of DirectX state changes.
            // But when using ObjectId map, we need to disable rendering queues by material.
            // If this is not done, the objects rendered in the standard rendering pass and 
            // objects rendered for object id map will be rendered in different order.
            // Because of this we would not be able to get the original object id from the back.
            //
            // Therefore go through all MaterialSortedRenderingQueue and disable sorting.
            //
            // Note the we do not need to disable sorting by camera distance (TransparentRenderingQueue)
            // because the object order does not change when rendering object id map.

            foreach (var materialSortedRenderingQueue in MainDXViewportView.DXScene.RenderingQueues.OfType<MaterialSortedRenderingQueue>())
                materialSortedRenderingQueue.IsSortingEnabled = false;



            // Create a SolidColorEffect that will be used to render each objects with a color from object's id
            _solidColorEffect = new SolidColorEffect
            {
                // We will overwrite the object's color with color specified in SolidColorEffect.Color
                OverrideModelColor = true,

                // Always use Opaque blend state even if alpha is less then 1 (usually PremultipliedAlphaBlend is used in this case).
                // This will allow us to also use alpha component for the object id (in our case RenderingQueue id)
                OverrideBlendState = MainDXViewportView.DXScene.DXDevice.CommonStates.Opaque,

                // By default for alpha values less then 1, the color components are multiplied with alpha value to produce pre-multiplied colors.
                // This will allow us to also use alpha component for the object id (in our case RenderingQueue id)
                PremultiplyAlphaColors = false
            };


            _disposables.Add(_solidColorEffect);

            MainDXViewportView.DXScene.DXDevice.EffectsManager.RegisterEffect(_solidColorEffect);


            // Create a custom rendering step that will be used instead of standard rendering step.
            // It will be used in the CreateObjectIdBitmapButton_OnClick method below
            _objectIdRenderingStep = new CustomActionRenderingStep("ObjectIdRenderingStep")
            {
                CustomAction = ObjectIdRenderingAction,
                IsEnabled = false                       // IMPORTANT: disable this custom rendering step - it will be enabled when rendering to bitmap
            };

            MainDXViewportView.DXScene.RenderingSteps.AddAfter(MainDXViewportView.DXScene.DefaultRenderObjectsRenderingStep, _objectIdRenderingStep);

            // In this sample we render Object ids to a custom bitmap,
            // so for standard rendering, we disable our custom rendering.
            // But if you went you can enable it and disabled the standard rendering - this will always render objects ids:
            //
            //_objectIdRenderingStep.IsEnabled = true;
            //MainDXViewportView.DXScene.DefaultRenderObjectsRenderingStep.IsEnabled = false;
        }

        private void ObjectIdRenderingAction(RenderingContext renderingContext)
        {
            var dxScene = renderingContext.DXScene;

            // Apply per frame settings
            _solidColorEffect.ApplyPerFrameSettings(renderingContext.UsedCamera, dxScene.Lights, renderingContext);

            // Go through each rendering queue ...
            var renderingQueues = dxScene.RenderingQueues;
            for (var i = 0; i < renderingQueues.Count; i++)
            {
                var oneRenderingQueue = renderingQueues[i];

                if (!oneRenderingQueue.IsRenderingEnabled)
                    continue;

                int objectsCount = oneRenderingQueue.Count;

                // ... and each object in rendering queue.
                for (int j = 0; j < objectsCount; j++)
                {
                    var oneRenderableObject = oneRenderingQueue[j];

                    _solidColorEffect.Color = GetObjectIdColor4(i, j); // because _solidColorEffect.OverrideModelColor is true, the object will be rendered with color specified here and not with actual models color (set in material)

                    // To get renderingQueueIndex and objectIndex back from color use:
                    //GetObjectId(_solidColorEffect.Color, out renderingQueueIndex, out renderingQueueIndex);

                    // Setup constant buffers and prepare shaders
                    _solidColorEffect.ApplyMaterial(oneRenderableObject.Material, oneRenderableObject);

                    // Draw the object
                    oneRenderableObject.RenderGeometry(renderingContext);
                }
            }
        }


        private BitmapSource RenderObjectIdBitmap()
        {
            if (_objectIdRenderingStep == null)
                return null;


            BitmapSource objectIdBitmap;

            // Now enable our custom rendering step that will be used only for RenderToBitmap
            _objectIdRenderingStep.IsEnabled                                       = true;
            MainDXViewportView.DXScene.DefaultRenderObjectsRenderingStep.IsEnabled = false;

            try
            {
                // Render to the same size as seen on the screen but without multi-sampling and super-sampling
                objectIdBitmap = MainDXViewportView.RenderToBitmap(MainDXViewportView.DXFinalPixelSize.Width,
                                                                   MainDXViewportView.DXFinalPixelSize.Height,
                                                                   preferedMultisampling: 0,
                                                                   supersamplingCount: 1);

                if (objectIdBitmap != null)
                {
                    int bitmapSize = objectIdBitmap.PixelHeight * objectIdBitmap.PixelWidth * 4; // 4 bytes per pixel

                    // Try to reuse the _objectIdPixelsArray
                    if (_objectIdPixelsArray == null || _objectIdPixelsArray.Length < bitmapSize)
                        _objectIdPixelsArray = new byte[bitmapSize];

                    objectIdBitmap.CopyPixels(_objectIdPixelsArray, objectIdBitmap.PixelWidth * 4, 0);

                    _objectIdBitmapWidth  = objectIdBitmap.PixelWidth;
                    _objectIdBitmapHeight = objectIdBitmap.PixelHeight;

                    SetRenderingQueueObjectsCounts();
                }
            }
            finally
            {
                _objectIdRenderingStep.IsEnabled = false;
                MainDXViewportView.DXScene.DefaultRenderObjectsRenderingStep.IsEnabled = true;
            }

            return objectIdBitmap;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Color4 GetObjectIdColor4(int renderingQueueIndex, int objectIndex)
        {
            // Encode renderingQueueIndex and objectIndex into 4 colors components (rendering is done in 32 bits, so each color have 8 bits; but the Color4 requires float values for color (this is also what the shader gets as parameter)
            // renderingQueueIndex is written to the 4 low bits of the alpha color component
            // objectIndex is written to red, green and blue (max written index is 16.777.215)
            //
            // This way it is possible to write ids for 16.777.215 objects in each rendering queue (so 16M solid + 16M lines + 16M transparent objects).
            // If you need more ids, then you can move the renderingQueueIndex into 4 high bits of alpha and use lower 4 bits for extra index increasing the ids count by 16.

            float red   = (float)((objectIndex >> 16) & 0xFF) / 255f;
            float green = (float)((objectIndex >> 8)  & 0xFF) / 255f;
            float blue  = (float)( objectIndex        & 0xFF) / 255f;

            // Write renderingQueueIndex
            // We increase the index by 1 so that value 0 represents an invalid index that is used for areas where there is no 3D object
            // We preserve the high 4 bits of alpha value so that the colors are visible and write renderingQueueIndex into the low 4 bits (0...15 possible values)
            float alpha = (float)(0xF0 + ((renderingQueueIndex + 1) & 0x0F)) / 255f; 

            return new Color4(red, green, blue, alpha);
        }

        // if renderingQueueIndex is -1, then this pixel does not have any 3D object
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void GetObjectId(Color4 idColor, out int renderingQueueIndex, out int objectIndex)
        {
            byte red   = (byte)(idColor.Red   * 255);
            byte green = (byte)(idColor.Green * 255);
            byte blue  = (byte)(idColor.Blue  * 255);
            byte alpha = (byte)(idColor.Alpha * 255);

            renderingQueueIndex = (alpha & 0x0F) - 1;
            objectIndex         = (red << 16) + (green << 8) + blue;
        }

        // if renderingQueueIndex is -1, then this pixel does not have any 3D object
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void GetObjectId(uint idColor, out int renderingQueueIndex, out int objectIndex)
        {
            renderingQueueIndex = (int)((idColor >> 24) & 0xF) - 1;
            objectIndex = (int) (idColor & 0xFFFFFF);
        }

        
        /// <summary>
        /// Saves the BitmapSource into png file with resultImageFileName
        /// </summary>
        /// <param name="image">BitmapSource</param>
        /// <param name="resultImageFileName">file name</param>
        public static void SaveBitmap(BitmapSource image, string resultImageFileName)
        {
            // write the bitmap to a file
            using (FileStream fs = new FileStream(resultImageFileName, FileMode.Create))
            {
                SaveBitmapToStream(image, fs);
            }
        }

        /// <summary>
        /// Saves the BitmapSource into imageStream with using png file encoder.
        /// </summary>
        /// <param name="image">BitmapSource</param>
        /// <param name="imageStream">Stream</param>
        public static void SaveBitmapToStream(BitmapSource image, Stream imageStream)
        {
            //JpegBitmapEncoder enc = new JpegBitmapEncoder();
            PngBitmapEncoder enc = new PngBitmapEncoder();
            BitmapFrame bitmapImage = BitmapFrame.Create(image);
            enc.Frames.Add(bitmapImage);
            enc.Save(imageStream);
        }


        private void SaveObjectIdBitmapButton_OnClick(object sender, RoutedEventArgs e)
        {
            var objectIdBitmap = RenderObjectIdBitmap();

            if (objectIdBitmap == null)
                return;

            string fileName = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "ObjectIds.png");

            SaveBitmap(objectIdBitmap, fileName);

            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(fileName) { UseShellExecute = true }); // For .Net CORE projects we need to set UseShellExecute to true 
        }

        private void ClearSelectionButton_OnClick(object sender, RoutedEventArgs e)
        {
            RestoreOriginalMaterials();
        }


        private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs args)
        {
            _isMouseSelectionStarted = false;

            SelectionRectangle.Visibility = Visibility.Collapsed;

            _objectIdPixelsArray = null; // Release _objectIdPixelsArray, so it can be collected by GC
        }

        private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs args)
        {
            _isMouseSelectionStarted = true;
            _startMousePosition = args.GetPosition(ViewportBorder);

            // It is important to restore original materials so that the ID bitmap will use the unselected objects
            RestoreOriginalMaterials();

            if (ObjectIdMapRadioButton.IsChecked ?? false)
                UpdateObjectIdBitmap();
        }

        private void OnMouseMove(object sender, MouseEventArgs args)
        {
            if (!_isMouseSelectionStarted)
                return;

            var currentMousePosition = args.GetPosition(ViewportBorder);

            Rect selectionRectangle = new Rect(Math.Min(currentMousePosition.X, _startMousePosition.X),
                                               Math.Min(currentMousePosition.Y, _startMousePosition.Y),
                                               Math.Abs(currentMousePosition.X - _startMousePosition.X),
                                               Math.Abs(currentMousePosition.Y - _startMousePosition.Y));

            Canvas.SetLeft(SelectionRectangle, selectionRectangle.X);
            Canvas.SetTop(SelectionRectangle, selectionRectangle.Y);

            SelectionRectangle.Width = selectionRectangle.Width;
            SelectionRectangle.Height = selectionRectangle.Height;

            SelectionRectangle.Visibility = Visibility.Visible;


            if (_selectedObjects == null)
                _selectedObjects = new HashSet<DependencyObject>();
            else
                _selectedObjects.Clear();
            
            if (ObjectIdMapRadioButton.IsChecked ?? false)
                UpdateSelectedObjectsWithObjectIdMap(selectionRectangle);
            else if (BoundsIn2DRadioButton.IsChecked ?? false)
                UpdateSelectedObjectsWithBoundsIn2D(selectionRectangle);

            UpdateSelectedObjects();
        }

        private void UpdateSelectedObjectsWithBoundsIn2D(Rect selectionRectangle)
        {
            // TODO: When there is many objects in the scene, then
            // when the selected is started we can convert the 3D bounds of all objects to 2D bounds.
            // Then in this method we would just call IntersectsWith without calling Rect3DTo2D.

            foreach (var model3D in _objectsModel3DGroup.Children)
            {
                var bounds = model3D.Bounds;

                // TODO: If parent Model3DGroup or parent Visual3D use any transformation,
                //       then we need to transform the bounds with that transformation.
                //       If the hierarchy of parents is not simple or not known, then you can use:
                //       Ab3d.Utilities.TransformationsHelper.GetModelTotalTransform() or GetVisual3DTotalTransform()
                
                // Convert 3D object bounds to 2D bounds on the screen
                var bounds2D = Camera1.Rect3DTo2D(bounds);

                // Check if our selection rectangle intersects with the 2D object bounds
                if (selectionRectangle.IntersectsWith(bounds2D))
                    _selectedObjects.Add(model3D);
            }
        }

        private void UpdateSelectedObjectsWithObjectIdMap(Rect selectionRectangle)
        {
            if (_objectIdPixelsArray == null || selectionRectangle.IsEmpty)
                return;

            int minX = (int)Math.Max(selectionRectangle.X * _dpiScaleX, 0);
            int maxX = (int)Math.Min((selectionRectangle.X + selectionRectangle.Width) * _dpiScaleX, _objectIdBitmapWidth);
            int width = (maxX - minX);

            int minY = (int)Math.Max(selectionRectangle.Y * _dpiScaleY, 0);
            int maxY = (int)Math.Min((selectionRectangle.Y + selectionRectangle.Height) * _dpiScaleY, _objectIdBitmapHeight);
            
            int stride = _objectIdBitmapWidth;


            // Check that the number of objects in each RenderingQueue is still the same as when we rendered the object ID bitmap.
            CheckRenderingQueueObjectsCounts();


            if (_selectedIds == null)
                _selectedIds = new HashSet<uint>();
            else
                _selectedIds.Clear();


            // Get all ids from the selected rectangle inside the bitmap byte array.
            // This is heavily performance critical so optimize it as much as possible!


            // TODO: For .Net Core and .Net 5 use Span instead of unsafe:
            //
            // #if NETCOREAPP || NET // Span<T> is supported only in .Net Core 2.1+ and .Net 5.0+
            //
            //Span<byte> pixelBytes = _objectIdPixelsArray;
            //Span<uint> pixelInts = MemoryMarshal.Cast<byte, uint>(pixelBytes);
            //
            // ...


            // Use unsafe so we can read byte array as array of integers
            unsafe
            {
                fixed (byte* pixelBytePtr = _objectIdPixelsArray)
                {
                    for (int y = minY; y <= maxY; y++)
                    {
                        uint* pixelPtr = (uint*)pixelBytePtr + y * stride + minX;

                        uint lastPixelColor = *pixelPtr;
                        _selectedIds.Add(lastPixelColor);

                        for (int x = 1; x < width; x++)
                        {
                            pixelPtr++;
                            uint pixelColor = *pixelPtr;

                            if (pixelColor != lastPixelColor)
                            {
                                _selectedIds.Add(pixelColor);
                                lastPixelColor = pixelColor;
                            }
                        }
                    }
                }
            }


            // Convert object ids to actual WPF objects and add them to _selectedObjects HashMap

            var renderingQueues = MainDXViewportView.DXScene.RenderingQueues;

            foreach (uint selectedId in _selectedIds)
            {
                int renderingQueueIndex, objectIndex;
                GetObjectId(selectedId, out renderingQueueIndex, out objectIndex);

                if (renderingQueueIndex < 0) // No 3D object
                    continue;


                var renderingQueue = renderingQueues[renderingQueueIndex];

                if (objectIndex >= renderingQueue.Count)
                    continue; // This can happen if ID bitmap was rendered with different materials (some object have both Material and BackMaterial) set than the current scene; see also exception text in the second exception in the CheckRenderingQueueObjectsCounts method.


                var renderablePrimitive = renderingQueue[objectIndex];

                var wpfGeometryModel3DNode = renderablePrimitive.OriginalObject as WpfGeometryModel3DNode;
                if (wpfGeometryModel3DNode != null)
                {
                    _selectedObjects.Add(wpfGeometryModel3DNode.GeometryModel3D);
                }
                else
                {
                    // When we hit ScreenSpaceLineNode we can get the original wpf object from the parent's node:
                    var screenSpaceLineNode = renderablePrimitive.OriginalObject as ScreenSpaceLineNode;
                    if (screenSpaceLineNode != null)
                    {
                        var baseWpfObjectNode = screenSpaceLineNode.ParentNode as BaseWpfObjectNode;
                        if (baseWpfObjectNode != null)
                        {
                            var baseLineVisual3D = baseWpfObjectNode.GetOriginalWpfObject() as BaseLineVisual3D;
                            if (baseLineVisual3D != null)
                                _selectedObjects.Add(baseLineVisual3D);
                        }
                    }
                }
            }
        }

        private void UpdateSelectedObjects()
        {
            // First remove selected objects that are not selected anymore:

            // reuse _modelsToRemove
            if (_modelsToRemove == null)
                _modelsToRemove = new List<GeometryModel3D>();
            else
                _modelsToRemove.Clear();

            foreach (var keyValuePair in _savedMaterials)
            {
                var geometryModel3D = keyValuePair.Key;
                if (!_selectedObjects.Contains(geometryModel3D))
                {
                    // Restore saved material
                    if (geometryModel3D.BackMaterial != null)
                        geometryModel3D.BackMaterial = keyValuePair.Value;
                    else
                        geometryModel3D.Material = keyValuePair.Value;

                    _modelsToRemove.Add(geometryModel3D);          // And mark to be removed from _savedMaterials (we cannot do that inside foreach)
                }
            }

            foreach (var geometryModel3D in _modelsToRemove)
                _savedMaterials.Remove(geometryModel3D);


            // reuse _linesToRemove
            if (_linesToRemove == null)
                _linesToRemove = new List<BaseLineVisual3D>();
            else
                _linesToRemove.Clear();

            foreach (var keyValuePair in _savedLineColors)
            {
                var baseLineVisual3D = keyValuePair.Key;
                if (!_selectedObjects.Contains(baseLineVisual3D))
                {
                    baseLineVisual3D.LineColor = keyValuePair.Value; // Restore saved color
                    _linesToRemove.Add(baseLineVisual3D);            // And mark to be removed from _savedLineColors (we cannot do that inside foreach)
                }
            }

            foreach (var lineToRemove in _linesToRemove)
                _savedLineColors.Remove(lineToRemove);



            // Now add newly selected objects:

            foreach (var selectedObject in _selectedObjects)
            {
                var geometryModel3D = selectedObject as GeometryModel3D;
                if (geometryModel3D != null)
                {
                    if (!_savedMaterials.ContainsKey(geometryModel3D)) // Is this geometryModel3D already selected?
                    {
                        // NO: Add it to the selection
                        if (geometryModel3D.Material == null) // Do we need to change the BackMaterial (see CheckRenderingQueueObjectsCounts method for more info about that)
                        {
                            _savedMaterials.Add(geometryModel3D, geometryModel3D.BackMaterial);
                            geometryModel3D.BackMaterial = _selectedDiffuseMaterial;
                        }
                        else
                        {
                            _savedMaterials.Add(geometryModel3D, geometryModel3D.Material);
                            geometryModel3D.Material = _selectedDiffuseMaterial;
                        }
                    }
                }
                else
                {
                    var baseLineVisual3D = selectedObject as BaseLineVisual3D;

                    if (baseLineVisual3D != null)
                    {
                        if (!_savedLineColors.ContainsKey(baseLineVisual3D)) // Is this geometryModel3D already selected?
                        {
                            // NO: Add it to selection ...
                            _savedLineColors.Add(baseLineVisual3D, baseLineVisual3D.LineColor);

                            // ... and change its material
                            baseLineVisual3D.LineColor = Colors.Red;
                        }
                    }
                }
            }
        }

        private void RestoreOriginalMaterials()
        {
            foreach (var keyValuePair in _savedMaterials)
            {
                var geometryModel3D = keyValuePair.Key;

                if (geometryModel3D.BackMaterial != null)
                    geometryModel3D.BackMaterial = keyValuePair.Value;
                else
                    geometryModel3D.Material = keyValuePair.Value;
            }

            _savedMaterials.Clear();


            foreach (var savedLineColor in _savedLineColors)
            {
                var baseLineVisual3D = savedLineColor.Key;
                baseLineVisual3D.LineColor = savedLineColor.Value;
            }

            _savedLineColors.Clear();
        }

        private void UpdateObjectIdBitmap()
        {
            var objectIdBitmap = RenderObjectIdBitmap();

            if (objectIdBitmap == null)
                return;

            int bitmapSize = objectIdBitmap.PixelHeight * objectIdBitmap.PixelWidth * 4; // 4 bytes per pixel
            _objectIdPixelsArray = new byte[bitmapSize];

            objectIdBitmap.CopyPixels(_objectIdPixelsArray, objectIdBitmap.PixelWidth * 4, 0);

            _objectIdBitmapWidth = objectIdBitmap.PixelWidth;
            _objectIdBitmapHeight = objectIdBitmap.PixelHeight;
        }
        
        // Save the current number of objects in each RenderingQueue.
        // This is used in the CheckRenderingQueueObjectsCounts method.
        // This prevents that the number of objects is changed after the object ID bitmap is rendered.
        private void SetRenderingQueueObjectsCounts()
        {
#if DEBUG
            var renderingQueues = MainDXViewportView.DXScene.RenderingQueues;

            if (_renderingQueueObjectsCounts == null)
                _renderingQueueObjectsCounts = new List<int>(renderingQueues.Count);
            else
                _renderingQueueObjectsCounts.Clear();
            
            for (var i = 0; i < renderingQueues.Count; i++)
                _renderingQueueObjectsCounts.Add(renderingQueues[i].Count);
#endif
        }

        // This method checks that the number of objects in each RenderingQueue is the now the same as it was when the object ID bitmap was rendered.
        //
        // This can prevent problems with using object ID bitmap that can happen when an object that only has BackMaterial set
        // when rendering the object ID bitmap also gets a Material set when it is selected.
        // The same thing can happen when BackMaterial and Material are set to the same material, but after the selection, they are different.
        // Both cases change the number of required objects in the RenderingQueue from one object to two objects (to render different back and front material).
        // Because object ID bitmap renders indexes in the RenderingQueue as colors, the indexes do not match anymore if the objects in the RenderingQueues are changed.
        //
        // For example, you have a 3D scene with 2 objects:
        // - 1st has BackMaterial set
        // - the 2nd has Material set.
        // Those two objects create 2 items in the RenderingQueue: 1st renders the BackMaterial for the first object, 2nd renders the Material for the Material.
        //
        // When an ID bitmap is rendered, the indexes from the RenderingQueue are converted into colors.
        // If you do a selection and assign a new material to the 1st object's Material property,
        // then that object has both Material and BackMaterial set and both are different materials.
        // This requires 2 items in RenderingQueue. So the number of items in the RenderingQueue is now 3.
        //
        // If you do a selection on the old ID bitmap and select the object with index 1 (index 1 was retrieved from the pixel's color)
        // this will now point to the first object because it is used to create the second item in the RenderingQueue.
        // This will be a mistake, because based on the ID bitmap the second object should be selected, but it now has an index 2 in the RenderingQueue.
        // 
        // There are the following possibilities to solve that:
        // 1) When doing selection: If the selected object has only Material set, then change the Material. If it has only  BackMaterial set, than change only BackMaterial. If both BackMaterial and Material are set to the same material, then change both to the same selected material.
        // 2) Render another ID bitmap after changing the materials.
        //
        //
        // To test this change the line in the UpdateSelectedObjects method from:
        // geometryModel3D.BackMaterial = _selectedDiffuseMaterial;
        // to:
        // geometryModel3D.Material = _selectedDiffuseMaterial;
        //
        // Then start the sample and check the "Use BackMaterials" checkbox
        private void CheckRenderingQueueObjectsCounts()
        {
#if DEBUG            
            var renderingQueues = MainDXViewportView.DXScene.RenderingQueues;

            if (_renderingQueueObjectsCounts.Count != renderingQueues.Count)
                throw new InvalidOperationException("Cannot use object ID bitmap because the number of RenderingQueues was changed after the object ID bitmap was render. Please render the IO bitmap again or change only existing Material and BackMaterial objects (do not add new one) to prevent changing the order of items in the RenderingQueues.");

            for (var i = 0; i < renderingQueues.Count; i++)
            {
                if (renderingQueues[i].Count != _renderingQueueObjectsCounts[i])
                    throw new InvalidOperationException(string.Format("Cannot use object ID bitmap because the number of objects in the {0} was changed from {1} to {2} after the object ID bitmap was render. This is probably caused because Material and BackMaterial property have changed. For example, if an object had only the BackMaterial property set and after rendering object ID bitmap it also got the Material set, then this objects now requires two RenderableObjects in the RenderingQueue instead of one. This breaks the indexes of the objects in the bitmap ID bitmap. If the selected object has only Material set, then change the Material. If it has only BackMaterial set, than change only BackMaterial. If both BackMaterial and Material are set to the same material, then change both. You can also render another ID bitmap after changing the selected objects.", renderingQueues[i].Name, _renderingQueueObjectsCounts[i], renderingQueues[i].Count));
            }
#endif
        }

        private void CreateTestScene()
        {
            var boxMesh    = new Ab3d.Meshes.BoxMesh3D(new Point3D(0,    0, 0), new Size3D(1, 1, 1), 1, 1, 1).Geometry;
            var sphereMesh = new Ab3d.Meshes.SphereMesh3D(new Point3D(0, 0, 0), 0.7, 30).Geometry;

            int modelsXCount = 10;
            int modelsYCount = 1;
            int modelsZCount = 10;

            _objectsModel3DGroup = new Model3DGroup();

            AddModels(_objectsModel3DGroup, boxMesh, new Point3D(0, 5, 0), new Size3D(500, modelsYCount * 10, 500), 10, modelsXCount, modelsYCount, modelsZCount, "Box", useBackMaterial: UseBackMaterialsCheckBox.IsChecked ?? false);
            AddModels(_objectsModel3DGroup, sphereMesh, new Point3D(25, 5, 25), new Size3D(500, modelsYCount * 10, 500), 10, modelsXCount, modelsYCount, modelsZCount, "Sphere");
            
            MainViewport.Children.Add(_objectsModel3DGroup.CreateContentVisual3D());

            // It would be optimal to use WireGridVisual3D to create a wire grid.
            // But because WireGridVisual3D creates a MultiLineVisual3D behind the scene, all the lines can have only a single color.
            // Therefore we create multiple lines for this sample so we can easily change color of individual lines.
            // And what is more, this way the object id map can get us the hit 3D lines (otherwise the whole MultiLineVisual3D would be hit)
            // <visuals:WireGridVisual3D CenterPosition="0 0 0" Size="500 500" WidthCellsCount="20" HeightCellsCount="20" LineColor="#555555" LineThickness="5"/>

            var contentVisual3D = CreateWireGridLines(new Point3D(0, 0, 0), new Size(500, 500), 20, 20, new Vector3D(1, 0, 0), new Vector3D(0, 0, 1), Colors.Gray, 5);
            MainViewport.Children.Add(contentVisual3D);
        }

        public static void AddModels(Model3DGroup parentModel3DGroup, MeshGeometry3D mesh, Point3D center, Size3D size, float modelScaleFactor, int xCount, int yCount, int zCount, string name, bool useBackMaterial = false)
        {
            float xStep = (float)(size.X / xCount);
            float yStep = (float)(size.Y / yCount);
            float zStep = (float)(size.Z / zCount);

            int i = 0;
            for (int z = 0; z < zCount; z++)
            {
                float zPos = (float)(center.Z - (size.Z / 2.0) + (z * zStep));

                for (int y = 0; y < yCount; y++)
                {
                    float yPos = (float)(center.Y - (size.Y / 2.0) + (y * yStep));

                    float yPercent = (float)y / (float)yCount;

                    for (int x = 0; x < xCount; x++)
                    {
                        float xPos = (float)(center.X - (size.X / 2.0) + (x * xStep));

                        var matrix = new Matrix3D(modelScaleFactor, 0, 0, 0,
                                                  0, modelScaleFactor, 0, 0,
                                                  0, 0, modelScaleFactor, 0,
                                                  xPos, yPos, zPos, 1);

                        var material = new DiffuseMaterial(new SolidColorBrush(System.Windows.Media.Color.FromRgb((byte)(255 * (float)x / (float)xCount), 255, (byte)(yPercent * 255))));

                        var model3D = new GeometryModel3D();
                        model3D.Geometry = mesh;

                        if (useBackMaterial)
                            model3D.BackMaterial = material;
                        else
                            model3D.Material = material;

                        model3D.Transform = new MatrixTransform3D(matrix);

                        model3D.SetName($"{name}_{z}_{y}_{x}");

                        parentModel3DGroup.Children.Add(model3D);

                        i++;
                    }
                }
            }
        }

        private ContentVisual3D CreateWireGridLines(Point3D centerPosition,
                                                    Size size,
                                                    int widthCellsCount,
                                                    int heightCellsCount,
                                                    Vector3D widthDirection,
                                                    Vector3D heightDirection,
                                                    Color linesColor,
                                                    double linesThickness)
        {
            var contentVisual3D = new ContentVisual3D();

            Point3D onePosition;

            double width = size.Width;
            Vector3D widthVector = new Vector3D(width * widthDirection.X,
                                                width * widthDirection.Y,
                                                width * widthDirection.Z);

            double height = size.Height;
            Vector3D heightVector = new Vector3D(height * heightDirection.X,
                                                 height * heightDirection.Y,
                                                 height * heightDirection.Z);

            var startPosition = centerPosition - (widthVector + heightVector) * 0.5;


            double oneStepFactor = 1.0 / widthCellsCount;

            for (int x = 1; x < widthCellsCount; x++)
            {
                onePosition = startPosition + x * oneStepFactor * widthVector;

                var lineVisual3D = new LineVisual3D()
                {
                    StartPosition = onePosition,
                    EndPosition = onePosition + heightVector,
                    LineColor = linesColor,
                    LineThickness = linesThickness
                };

                contentVisual3D.Children.Add(lineVisual3D);
            }


            oneStepFactor = 1.0 / heightCellsCount;

            for (int y = 1; y < heightCellsCount; y++)
            {
                onePosition = startPosition + y * oneStepFactor * heightVector;

                var lineVisual3D = new LineVisual3D()
                {
                    StartPosition = onePosition,
                    EndPosition = onePosition + widthVector,
                    LineColor = linesColor,
                    LineThickness = linesThickness
                };

                contentVisual3D.Children.Add(lineVisual3D);
            }

            return contentVisual3D;
        }

        private void OnUseBackMaterialsCheckBoxCheckedChanged(object sender, RoutedEventArgs e)
        {
            if (!this.IsLoaded)
                return;

            MainViewport.Children.Clear();
            CreateTestScene();
        }
    }
}
