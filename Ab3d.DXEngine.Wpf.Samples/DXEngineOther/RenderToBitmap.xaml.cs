using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
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
using Ab3d.Cameras;
using Ab3d.Common.Cameras;
using Ab3d.Common.Models;
using Ab3d.DirectX;
using Ab3d.DirectX.Controls;
using Ab3d.Visuals;

namespace Ab3d.DXEngine.Wpf.Samples.DXEngineOther
{

    //
    // This sample sample shows how to create a DXViewportView object without showing it on the UI 
    // and use RenderToBitmap method to render the scene to a WPF's bitmap
    //
    // Note that you can also use the RenderToBitmap method when the DXViewportView is shown on the UI
    //


    /// <summary>
    /// Interaction logic for RenderToBitmap.xaml
    /// </summary>
    public partial class RenderToBitmap : Page
    {
        private const bool ForceSoftwareRendering = false;

        private int BitmapWidth  = 512; // NOTE: Max size for DirectX feature level 11 and 11.1 is 16384 x 16384 (for feature level 10 the max size is 8192 x 8192)
        private int BitmapHeight = 512;

        //private const int BitmapWidth  = 16384;
        //private const int BitmapHeight = 16384;

        private const int BitmapDpiX = 96;
        private const int BitmapDpiY = 96;



        private DXViewportView _dxViewportView;

        private Viewport3D _viewport3D;

        private TargetPositionCamera _camera;

        private bool _isFirstRender;

        public RenderToBitmap()
        {
            InitializeComponent();

            SetupDXEngine();
            SetupContent();

            this.Unloaded += delegate(object sender, RoutedEventArgs args)
            {
                if (_dxViewportView != null)
                {
                    _dxViewportView.Dispose();
                    _dxViewportView = null;
                }
            };
        }

        private void SetupDXEngine()
        {
            _dxViewportView = new DXViewportView();

            // For this sample we force software rendering (for example to be used on server or other computer without graphics card)
            if (ForceSoftwareRendering)
                _dxViewportView.GraphicsProfiles = new GraphicsProfile[] { GraphicsProfile.HighQualitySoftwareRendering };

            // Because the DXViewportView is not shown in the UI, we need to manually sets its size (without this the current version will not be initialized correctly - this will be improved in the future)
            _dxViewportView.Width = 128;
            _dxViewportView.Height = 128;

            _viewport3D = new Viewport3D();

            _camera = new TargetPositionCamera()
            {
                //Heading = HeadingSlider.Value,
                Attitude = -20,
                Distance = 200,
                ShowCameraLight = ShowCameraLightType.Always,
                TargetViewport3D = _viewport3D
            };

            UpdateCamera();

            _dxViewportView.Viewport3D = _viewport3D;


            // Initialize the scene with creating DirectX device and required resources
            try
            {
                _dxViewportView.InitializeScene();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error initializing DXEngine:\r\n" + ex.Message);

                RenderButton.IsEnabled = false;
            }

            _isFirstRender = true;
        }

        private void SetupContent()
        {
            var boxVisual3D = new Ab3d.Visuals.BoxVisual3D()
            {
                Size     = new Size3D(50, 20, 30),
                Material = new DiffuseMaterial(Brushes.Green)
            };

            _viewport3D.Children.Add(boxVisual3D);
        }   

        private void UpdateCamera()
        {
            _camera.Heading = HeadingSlider.Value;
            _camera.Refresh();
        }

        private void RenderButton_OnClick(object sender, RoutedEventArgs e)
        {
            UpdateCamera();

            var stopwatch = new Stopwatch();
            stopwatch.Start();


            BitmapWidth  = (int)ImageBorder.ActualWidth;
            BitmapHeight = (int)ImageBorder.ActualHeight;

            _viewport3D.Width = BitmapWidth;
            _viewport3D.Height = BitmapHeight;

            _viewport3D.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            _viewport3D.Arrange(new Rect(0, 0, _viewport3D.DesiredSize.Width, _viewport3D.DesiredSize.Height));

            _camera.Refresh();

            Ab3d.Utilities.LinesUpdater.Instance.Refresh();

            BitmapSource renderBitmap = _dxViewportView.RenderToBitmap(width: BitmapWidth, 
                                                                       height: BitmapHeight, 
                                                                       preferedMultisampling: 2, // when -1 is used, then the currently used multisampling is used
                                                                       dpiX: BitmapDpiX, // 96 is the default value and can be changed if needed
                                                                       dpiY: BitmapDpiY);

            // Display render time (note that the first time the scene is rendered the time is bigger because of additional initializations)
            stopwatch.Stop();
            InfoTextBlock.Text = string.Format("Render time: {0:0.0}ms", stopwatch.Elapsed.TotalMilliseconds);

            if (_isFirstRender)
            {
                InfoTextBlock.Text += " (note: render time of the first image is longer then the time to render other images)";
                _isFirstRender = false;
            }

            RenderedImage.Source = renderBitmap;
            
            //SaveBitmap(renderBitmap, @"c:\temp\DXEngineRender.png");
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

    }
}
