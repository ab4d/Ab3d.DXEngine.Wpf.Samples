using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
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
using Ab3d.DirectX;
using Ab3d.DirectX.Effects;
using SharpDX;

namespace Ab3d.DXEngine.Wpf.Samples.DXEngineHitTesting
{
    // TODO: P3: S: Show how to create rectangular object selection.

    // This sample shows how to render each object with a color defined by its ObjectIds to a bitmap.
    // The ObjectId is actually an index of the RenderingQueue and an index of the object inside the RenderingQueue.
    // This way it is possible to get the rendered RenderablePrimitive from the color and 
    // from RenderablePrimitive you can get the SceneNode from the OriginalObject property.
    // This way the bitmap can be used for complex hit-testing and box or lasso selection.
    //
    // Limitations:
    // - only standard 3D objects can be rendered
    // - lines are rendered with thickness 1

    /// <summary>
    /// Interaction logic for ObjectIdRendering.xaml
    /// </summary>
    public partial class ObjectIdRendering : Page
    {
        private DisposeList _disposables;

        private SolidColorEffect _solidColorEffect;
        private CustomActionRenderingStep _objectIdRenderingStep;

        public ObjectIdRendering()
        {
            InitializeComponent();

            var boxMesh = new Ab3d.Meshes.BoxMesh3D(new Point3D(0, 0, 0), new Size3D(1, 1, 1), 1, 1, 1).Geometry;
            int modelsXCount = 20;
            int modelsYCount = 1;
            int modelsZCount = 20;

            var model3DGroup = CreateModel3DGroup(boxMesh, new Point3D(0, 5, 0), new Size3D(500, modelsYCount * 10, 500), 10, modelsXCount, modelsYCount, modelsZCount);

            MainViewport.Children.Add(model3DGroup.CreateModelVisual3D());


            _disposables = new DisposeList();

            MainDXViewportView.DXSceneDeviceCreated += MainDxViewportViewOnDxSceneDeviceCreated;

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

        private void MainDxViewportViewOnDxSceneDeviceCreated(object sender, EventArgs e)
        {
            // Create a SolidColorEffect that will be used to render each objects with a color from object's id
            _solidColorEffect = new SolidColorEffect();
            _solidColorEffect.OverrideModelColor = true; // We will overwrite the object's color with color specified in SolidColorEffect.Color

            _disposables.Add(_solidColorEffect);

            MainDXViewportView.DXScene.DXDevice.EffectsManager.RegisterEffect(_solidColorEffect);


            // Create a custom rendering step that will be used instead of standard rendering step
            _objectIdRenderingStep = new CustomActionRenderingStep("ObjectIdRenderingStep")
            {
                CustomAction = ObjectIdRenderingAction,
                IsEnabled = false
            };

            MainDXViewportView.DXScene.RenderingSteps.AddAfter(MainDXViewportView.DXScene.DefaultRenderObjectsRenderingStep, _objectIdRenderingStep);

            // In this sample we render Object ids to a custom bitmap,
            // so for standard rendering, we disable our custom rendering.
            // But if you went you can enable it and disabled the standard rendering - this will always render objects ids:
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Color4 GetObjectIdColor4(int renderingQueueIndex, int objectIndex)
        {
            // Encode renderingQueueIndex and objectIndex into 3 colors (rendering is done in 32 bits, so each color have 8 bits; but the Color4 requires float values for color (this is also what the shader gets as parameter)
            // renderingQueueIndex is written to the 4 highest bits of the red color
            // objectIndex is written to lower 4 bit in red and 8 bits in green and blue (max written index is 1.048.575).
            // Note that in the current version of DXEngine we cannot use alpha color because
            // if it is less than 1, the alpha blending is used (and also the color is premultiplied with alpha).
            // In the next version, it will be possible to use all 4 color attributes and prevent alpha blending.
            //
            // If you already need more ids, then you may increase the available objects ids to 16.777.215 with using all 3 colors for objectIndex and not writing renderingQueueIndex (for example for rendering only objects in dxScene.StandardGeometryRenderingQueue)

            float red   = (float)((renderingQueueIndex << 4) + ((objectIndex >> 16) & 0x0F)) / 255f;
            float green = (float)                              ((objectIndex >> 8)  & 0xFF)  / 255f;
            float blue  = (float)                              ( objectIndex        & 0xFF)  / 255f;

            return new Color4(red, green, blue, 1f);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void GetObjectId(Color4 idColor, out int renderingQueueIndex, out int objectIndex)
        {
            byte red   = (byte)(idColor.Red   * 255);
            byte green = (byte)(idColor.Green * 255);
            byte blue  = (byte)(idColor.Blue  * 255);

            renderingQueueIndex = red >> 4;
            objectIndex = ((red & 0x0F) << 16) + (green << 8) + blue;
        }

        private void CreateObjectIdBitmapButton_OnClick(object sender, RoutedEventArgs e)
        {
            if (_objectIdRenderingStep == null)
                return;

            _objectIdRenderingStep.IsEnabled = true;
            MainDXViewportView.DXScene.DefaultRenderObjectsRenderingStep.IsEnabled = false;

            try
            {
                var renderToBitmap = MainDXViewportView.RenderToBitmap(MainDXViewportView.DXScene.Width, MainDXViewportView.DXScene.Height, preferedMultisampling: 0);

                string fileName = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "ObjectIds.png");

                SaveBitmap(renderToBitmap, fileName);

                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(fileName) { UseShellExecute = true }); // For .Net CORE projects we need to set UseShellExecute to true 
            }
            finally
            {
                _objectIdRenderingStep.IsEnabled = false;
                MainDXViewportView.DXScene.DefaultRenderObjectsRenderingStep.IsEnabled = true;
            }
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


        public static Model3DGroup CreateModel3DGroup(MeshGeometry3D mesh, Point3D center, Size3D size, float modelScaleFactor, int xCount, int yCount, int zCount)
        {
            var model3DGroup = new Model3DGroup();

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

                        var model3D = new GeometryModel3D(mesh, material);
                        model3D.Transform = new MatrixTransform3D(matrix);

                        model3DGroup.Children.Add(model3D);

                        i++;
                    }
                }
            }

            return model3DGroup;
        }
    }
}

