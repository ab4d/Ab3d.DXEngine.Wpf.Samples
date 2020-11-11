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

            MainDXViewportView.DXSceneDeviceCreated += OnDxSceneDeviceCreated;

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

        private void CreateObjectIdBitmapButton_OnClick(object sender, RoutedEventArgs e)
        {
            if (_objectIdRenderingStep == null)
                return;

            // Now enable our custom rendering step that will be used only for RenderToBitmap
            _objectIdRenderingStep.IsEnabled = true;
            MainDXViewportView.DXScene.DefaultRenderObjectsRenderingStep.IsEnabled = false;

            try
            {
                // Render bitmap but do not use super-sampling (so divide by SupersamplingFactor)
                var supersamplingFactor = MainDXViewportView.DXScene.SupersamplingFactor;
                int width               = (int)(MainDXViewportView.DXScene.Width / supersamplingFactor);
                int height              = (int)(MainDXViewportView.DXScene.Height / supersamplingFactor);

                var renderToBitmap = MainDXViewportView.RenderToBitmap(width, height, preferedMultisampling: 0, supersamplingCount: 1);

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
            float alpha = (float)(0xF0 + (renderingQueueIndex & 0x0F)) / 255f; // preserve the high 4 bits of alpha value so that the colors are visible and write renderingQueueIndex into the low 4 bits (0...15 possible values)

            return new Color4(red, green, blue, alpha);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void GetObjectId(Color4 idColor, out int renderingQueueIndex, out int objectIndex)
        {
            byte red   = (byte)(idColor.Red   * 255);
            byte green = (byte)(idColor.Green * 255);
            byte blue  = (byte)(idColor.Blue  * 255);
            byte alpha = (byte)(idColor.Alpha * 255);

            renderingQueueIndex = alpha & 0x0F;
            objectIndex         = (red << 16) + (green << 8) + blue;
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

