using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using System.Windows.Threading;
using Ab3d.Common.Cameras;
using Ab3d.DirectX;

#if SHARPDX
using SharpDX;
#endif

namespace Ab3d.DXEngine.Wpf.Samples.PowerToysOther
{
    // This sample shows how to efficiently show materials with VisualBrushes in DXEngine.

    // 1) 
    // Small and Big rectangles show how to specify different sizes for rendered bitmaps. This is done with using SetDXAttribute extension method.
    //
    // 2)
    // Three rectangles with green border show how to update the material with VisualBrush.
    // The first rectangle does not get updated when VisualBrush is changed (WPF does not provide a way to inform any subscriber that the content of VisualBrush is changed).
    // The second rectangle is updated with calling Refresh method on the DXEngine's material that is used to show the WPF's Material.
    // The third rectangle is updated with resetting the Visual property of the VisualBrush (setting the property to null and then back to its previous value).
    // This way DXEngine can get a change notification and it can update the texture.
    //
    // 3)
    // Rectangle with orange border shows how to dynamically set the size of rendered bitmap based on the size of rendered rectangle on the screen. 
    // Move the camera around and zoom with the camera to change the size of the rendered bitmap.

    /// <summary>
    /// Interaction logic for VisualBrushTest.xaml
    /// </summary>
    public partial class VisualBrushTest : Page
    {
        private Size2 _currentBigPlaneBitmapSize;

        public VisualBrushTest()
        {
            InitializeComponent();

            // To show materials with VisualBrush materials, DXEngine needs to render the VisualBrush to bitmap.
            // By default VisualBrushes and DrawingImages are rendered to 512 x 512 bitmap (by default gradients are rendered to 1 x 128 or 128 x 128).
            //
            // Because rendered bitmaps can take a lot of memory, it is possible to control the size of the rendered bitmaps.
            // The easiest way to do that is used to SetDXAttribute extension method and specify the bitmap size with setting the value of the CachedBitmapSize attribute.

            // SmallPlane is farther away from the camera and small, so 64 x 32 bitmap will be enough.
            SmallPlane.Material.SetDXAttribute(DXAttributeType.CachedBitmapSize, new Size2(64, 32));

            // BigPlane is close to the camera and big and therefore we want to render it to a high detailed bitmap.
            _currentBigPlaneBitmapSize = new Size2(512, 256);
            BigPlane.Material.SetDXAttribute(DXAttributeType.CachedBitmapSize, _currentBigPlaneBitmapSize);

            // The value of the CachedBitmapSize attribute can be set to Size2 or System.Windows.Size type;
            // or to Int32, double or float - in this case the value specify both width and height of the bitmap.

            // You can also change the value of CachedBitmapSize after the scene has been rendered.
            // This can be used to dynamically update the size of bitmaps in a dynamic scene.
            // To prevent many bitmap rendering, it is recommended not to change this value too ofter.



            // For this sample we will setup a simple timer that will change the content of VisualBrush on every second.
            var dispatcherTimer = new DispatcherTimer();
            dispatcherTimer.Interval = TimeSpan.FromSeconds(1);
            dispatcherTimer.Tick += OnDispatcherTimerOnTick;

            dispatcherTimer.Start();


            Camera1.CameraChanged += delegate(object sender, CameraChangedRoutedEventArgs args)
            {
                // On each camera change, we check if we need to adjust the size of rendered bitmap
                AdjustBitmapSizeFromScreenSize();
            };

            this.SizeChanged += delegate(object sender, SizeChangedEventArgs args)
            {
                // We also need to call AdjustBitmapSizeFromScreenSize on each change of the window size
                AdjustBitmapSizeFromScreenSize();
            };


            // Another option to update VisualBrush is to enable rendering of VisualBrush before every frame rendering.
            // This option can have huge performance impact so it should be used with caution.
            // The following commented code can be used to show how to enable it.
            // To get the WpfMaterial we need to subscribe to DXSceneInitialized event - here the WpfMaterial should be already created.
            //
            //MainDXViewportView.DXSceneInitialized += delegate(object sender, EventArgs args)
            //{
            //    var usedDXMaterial = Plane2.Material.GetUsedDXMaterial(MainDXViewportView.DXScene.DXDevice) as WpfMaterial;

            //    if (usedDXMaterial != null)
            //    {
            //        // Enable rendering VisualBrus on each frame
            //        usedDXMaterial.RenderToBitmapOnEveryFrame = true;

            //        // Because this can have huge performance impact, it is adviced to lower the resolution of bitmap (from default 512 x 512).
            //        // Because we have an instance of WpfMaterial, we can set the RenderedBrushTextureWidth and RenderedBrushTextureHeight directly (without using SetDXAttribute).
            //        usedDXMaterial.RenderedBrushTextureWidth = 256;
            //        usedDXMaterial.RenderedBrushTextureHeight = 256;
            //    }
            //};
            //
            // When this is uncommented, you can comment the manual updating of Plane2 with commenting the call to ManuallyRefreshMaterial inside the OnDispatcherTimerOnTick method


            this.Unloaded += delegate(object sender, RoutedEventArgs args)
            {
                dispatcherTimer.Stop();
                dispatcherTimer.Tick -= OnDispatcherTimerOnTick;

                MainDXViewportView.Dispose();
            };
        }

        private void OnDispatcherTimerOnTick(object sender, EventArgs args)
        {
            // Change the VisualBrush
            ChangeVisualBrush();

            // Manually update only second material
            ManuallyRefreshMaterial(Plane2.Material);
        }

        private void RefreshMaterialsButton_Click(object sender, RoutedEventArgs e)
        {
            // Update all materials
            ManuallyRefreshMaterial(Plane1.Material);
            ManuallyRefreshMaterial(Plane2.Material);
            ManuallyRefreshMaterial(Plane3.Material);
        }

        private void ManuallyRefreshMaterial(System.Windows.Media.Media3D.Material wpfMaterialToUpdate)
        {
            if (MainDXViewportView.DXScene == null) // Using WPF 3D rendering
                return;

            // GetUsedDXMaterial returns the DXEngine's material that is used to show the wpfMaterialToUpdate
            var usedDXMaterial = wpfMaterialToUpdate.GetUsedDXMaterial(MainDXViewportView.DXScene.DXDevice);

            if (usedDXMaterial != null)
                usedDXMaterial.Refresh(); // Regenerate the textures from VisualBrush
        }

        private void ChangeVisualBrush()
        {
            // Get VisualBrush and change its Background color and text
            var visualBrush1 = this.FindResource("UsedVisualBrush1") as VisualBrush;
            ChangeVisualBrush(visualBrush1);

            // Change another VisualBrush
            var visualBrush2 = this.FindResource("UsedVisualBrush2") as VisualBrush;
            ChangeVisualBrush(visualBrush2);

            // We can notify the Ab3d.DXEngine that the VisualBrush is changed by resetting the Visual property to the already set value:
            var savedVisual = visualBrush2.Visual;
            visualBrush2.Visual = null;
            visualBrush2.Visual = savedVisual;
        }

        private void ChangeVisualBrush(VisualBrush visualBrush)
        {
            if (visualBrush == null) // in case the UsedVisualBrush resource was changed in XAML
                return;

            // We need to get the Border and TextBlock from VisualBrush.Visual (setting Name in XAML does not create the elements in code)

            var border = visualBrush.Visual as Border;
            if (border == null)
                return;

            if (border.Background != Brushes.LightGreen) // If background is not Yellow yet, set it to Yellow
                border.Background = Brushes.LightGreen;

            var textBlock = border.Child as TextBlock;
            if (textBlock == null)
                return;

            // Increase number by one
            textBlock.Text = string.Format("{0}", (Int32.Parse(textBlock.Text) + 1) % 10);
        }

        private void ChangeRenderSizeButton_OnClick(object sender, RoutedEventArgs e)
        {
            var currentSize = BigPlane.Material.GetDXAttributeOrDefault<Size2>(DXAttributeType.CachedBitmapSize);

            Size2 newSize;

            if (currentSize.Width == 512)
                newSize = new Size2(64, 32);
            else
                newSize = new Size2(512, 256);

            _currentBigPlaneBitmapSize = newSize;
            BigPlane.Material.SetDXAttribute(DXAttributeType.CachedBitmapSize, newSize);
        }


        // This method sets the rendered bitmap size based on the size of the Plane's 3D model on the screen
        private void AdjustBitmapSizeFromScreenSize()
        {
            var geometryModel3D = DynamicPlane.Content as GeometryModel3D;

            if (geometryModel3D == null)
                return;


            var meshGeometry3D = (MeshGeometry3D)geometryModel3D.Geometry;

            // Get the transformation of the geometryModel3D
            // If the geometryModel3D is also transformed by the parent ModelVisual3D objects, we also need to add that transformation to the transform (using new Transform3DGroup)
            var transform = geometryModel3D.Transform;


            // Now we can calculate the size of the geometryModel3D on the screen (the GetPositionsSizeOnScreen is defined below).
            var sizeOnScreen = GetPositionsSizeOnScreen(meshGeometry3D.Positions, transform, Camera1);


            // In DirectX it is recommended to use texture sizes that are power of 2 (64, 128, 256, 512, etc.)
            // RoundToPowerOf2 will round the size to power of 2 values so that the sizeOnScreen will be smaller than the returned value.
            // This will also prevent too many changes of bitmap size (prevent too many rendering of VisualBrush).
            var sizeOnScreen2 = RoundToPowerOf2(sizeOnScreen);

            //System.Diagnostics.Debug.WriteLine($"SizeOnScreen: {sizeOnScreen} => {sizeOnScreen2}");

            // When using antialiasing, we can divide the size by 2 and still get very good results
            sizeOnScreen2.Width /= 2;
            sizeOnScreen2.Height /= 2;


            // Clip the size to be between 64 and 2048
            int minBitmapSize = 64;
            int maxBitmapSize = 2048;

            if (sizeOnScreen2.Width < minBitmapSize) sizeOnScreen2.Width = minBitmapSize;
            if (sizeOnScreen2.Height < minBitmapSize) sizeOnScreen2.Height = minBitmapSize;

            if (sizeOnScreen2.Width > maxBitmapSize) sizeOnScreen2.Width = maxBitmapSize;
            if (sizeOnScreen2.Height > maxBitmapSize) sizeOnScreen2.Height = maxBitmapSize;


            if (_currentBigPlaneBitmapSize != sizeOnScreen2)
            {
                // If the size is changed, we change the text in the VisualBrush to the new size
                DynamicInfoTextBlock.Text = string.Format("{0} x {1}", sizeOnScreen2.Width, sizeOnScreen2.Height);

                // Change the size of rendered bitmap
                DynamicPlane.Material.SetDXAttribute(DXAttributeType.CachedBitmapSize, sizeOnScreen2);
                _currentBigPlaneBitmapSize = sizeOnScreen2;
            }
        }

        public static Size2 GetPositionsSizeOnScreen(Point3DCollection positions, Transform3D transform, Ab3d.Cameras.BaseCamera camera)
        {
            double minX = double.MaxValue;
            double maxX = double.MinValue;
            double minY = double.MaxValue;
            double maxY = double.MinValue;

            var positionsCount = positions.Count;

            bool useTransform = transform != null && !transform.Value.IsIdentity;

            for (int i = 0; i < positionsCount; i++)
            {
                var onePoint = positions[i];
                if (useTransform)
                    onePoint = transform.Transform(onePoint);

                var pointOnScreen = camera.Point3DTo2D(onePoint);

                if (pointOnScreen.X < minX) minX = pointOnScreen.X;
                if (pointOnScreen.X > maxX) maxX = pointOnScreen.X;

                if (pointOnScreen.Y < minY) minY = pointOnScreen.Y;
                if (pointOnScreen.Y > maxY) maxY = pointOnScreen.Y;
            }

            var sizeOnScreen = new Size2((int)(maxX - minX), (int)(maxY - minY));

            return sizeOnScreen;
        }

        // Round Size2 value to power of 2 where the returned Size2 is bigger than the Size2 in parameter.
        public static Size2 RoundToPowerOf2(Size2 size)
        {
            return new Size2(RoundToPowerOf2(size.Width), RoundToPowerOf2(size.Height));
        }

        // Round int value to power of 2 where the returned value is bigger than the value in parameter.
        public static int RoundToPowerOf2(int value)
        {
            if (value <= 0)
                return 0;

            int powerOfTwo = 1;
            while (value > powerOfTwo)
            {
                powerOfTwo *= 2;
            }

            return powerOfTwo; // Return next value so the value is smaller then the returned value
        }
    }
}
