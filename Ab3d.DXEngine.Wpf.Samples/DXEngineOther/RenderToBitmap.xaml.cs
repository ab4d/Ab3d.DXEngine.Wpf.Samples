using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
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
using Ab3d.DirectX.PostProcessing;
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
        private int BitmapWidth  = 512; // NOTE: Max size for DirectX feature level 11 and 11.1 is 16384 x 16384 (for feature level 10 the max size is 8192 x 8192)
        private int BitmapHeight = 512;

        private DXViewportView _dxViewportView;

        private Viewport3D _viewport3D;

        private TargetPositionCamera _camera;

        private bool _isFirstRender;
        private WriteableBitmap _writeableBitmap;
        private BitmapSource _renderBitmap;

        public RenderToBitmap()
        {
            InitializeComponent();

            ConvertToNonPremultipliedAlphaInfoControl.InfoText =
                @"Ab3d.DXEngine and internally WPF are using pre-multiplied alpha textures. This means that color values (red, green, blue) are multiplied by alpha value. See internet for reasons why this is better.

When rendering bitmap that is will be shown in a WPF application, then it is recommended to preserve the pre-multiplied alpha.
When rendering bitmap that will be saved to png file and when there are transparent pixels in the texture, then it is better to convert the rendered image into non pre-multiplied bitmap because png files do not support pre-multiplied alpha.

To test the difference when saving to png, uncomment the code in the SetupContent method.";

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

            // To use render to bitmap on a server, it is possible to use software rendering with the following line:
            //_dxViewportView.GraphicsProfiles = new GraphicsProfile[] { GraphicsProfile.HighQualitySoftwareRendering };

            // Because the DXViewportView is not shown in the UI, we need to manually sets its size (without this the current version will not be initialized correctly - this will be improved in the future)
            _dxViewportView.Width = 128;
            _dxViewportView.Height = 128;

            // By default the BackgroundColor is set to transparent.
            // We set that to white so that the saved bitmap has a white background.
            _dxViewportView.BackgroundColor = Colors.White;

            _viewport3D = new Viewport3D();

            _camera = new TargetPositionCamera()
            {
                //Heading = HeadingSlider.Value,
                Attitude = -15,
                Distance = 300,
                TargetPosition = new Point3D(0, 10, 0),
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
            var box1 = new Ab3d.Visuals.BoxVisual3D()
            {
                CenterPosition = new Point3D(0, 5, 0),
                Size           = new Size3D(40, 10, 40),
                Material       = new DiffuseMaterial(Brushes.Orange)
            };

            _viewport3D.Children.Add(box1);
            
            var box2 = new Ab3d.Visuals.BoxVisual3D()
            {
                CenterPosition = new Point3D(0, 20, 0),
                Size           = new Size3D(32, 20, 32),
                Material       = new DiffuseMaterial(Brushes.Aqua)
            };

            _viewport3D.Children.Add(box2);


            var wireGridVisual3D = new Ab3d.Visuals.WireGridVisual3D()
            {
                CenterPosition      = new Point3D(0, -0.1, 0),
                Size                = new Size(160, 160),
                WidthCellsCount     = 16,
                HeightCellsCount    = 16,
                MajorLinesFrequency = 4,

                LineColor      = Colors.LightGray,
                LineThickness  = 1,

                MajorLineColor     = Colors.DimGray,
                MajorLineThickness = 1.5,
            };

            _viewport3D.Children.Add(wireGridVisual3D);

            // Uncomment this code to add 11 boxes with different transparency levels.
            // This can be used to test saving to png files with using pre-multiplied or non pre-multiplied alpha color.
            //for (int i = 0; i <= 10; i++)
            //{
            //    var box = new Ab3d.Visuals.BoxVisual3D()
            //    {
            //        CenterPosition = new Point3D(-100 + i * 20, -30, 0),
            //        Size = new Size3D(15, 15, 15),
            //        Material = new DiffuseMaterial(new SolidColorBrush(Color.FromArgb((byte)(25.5 * i), (byte)255, (byte)255,(byte)0)))
            //    };

            //    _viewport3D.Children.Add(box);
            //}
        }   

        private void UpdateCamera()
        {
            _camera.Heading = HeadingSlider.Value;
            _camera.Refresh();
        }

        private void RenderButton_OnClick(object sender, RoutedEventArgs e)
        {
            // Update camera based on the HeadingSlider value
            UpdateCamera();

            var stopwatch = new Stopwatch();
            stopwatch.Start();


            BitmapWidth  = (int)ImageBorder.ActualWidth;
            BitmapHeight = (int)ImageBorder.ActualHeight;

            double dpiScaleX, dpiScaleY;
            DXView.GetDpiScale(this, out dpiScaleX, out dpiScaleY);

            double dpiX = 96.0 * dpiScaleX; // 96 is the default dpi value without any scaling
            double dpiY = 96.0 * dpiScaleY;


            // The following 6 lines are not needed because the DXViewportView is not shown:

            _viewport3D.Width = BitmapWidth;
            _viewport3D.Height = BitmapHeight;

            // Because DXViewportView is not actually show, we need to call Measure and Arrange
            _viewport3D.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            _viewport3D.Arrange(new Rect(0, 0, _viewport3D.DesiredSize.Width, _viewport3D.DesiredSize.Height));

            // Update camera
            _camera.Refresh();

            // Update 3D lines
            Ab3d.Utilities.LinesUpdater.Instance.Refresh();


            int multiSamplingCount, superSamplingCount;
            GetAntiAliasingSettings(out multiSamplingCount, out superSamplingCount);


            var convertToNonPreMultipledAlpha = ConvertToNonPremultipliedAlphaCheckBox.IsChecked ?? false;

            if (ReuseWriteableBitmapCheckBox.IsChecked ?? false)
            {
                // Create only one instance of WriteableBitmap, then reuse it with other calls to RenderToBitmap
                
                int finalBitmapWidth  = (int)(BitmapWidth  * dpiScaleX);
                int finalBitmapHeight = (int)(BitmapHeight * dpiScaleY);

                // Except in case when the size is changed
                if (_writeableBitmap != null && (_writeableBitmap.PixelWidth != finalBitmapWidth || _writeableBitmap.PixelHeight != finalBitmapHeight))
                    _writeableBitmap = null; // recreate the WriteableBitmap


                if (_writeableBitmap == null) // we would also need to recreate the WriteableBitmap in case the size changes
                {
                    // When using WriteableBitmap, the RenderToBitmap will use pre-multiplied alpha
                    // or non pre-multiplied alpha based on the pixel format (Pbgra32 for pre-multiplied alpha).
                    //
                    // When the rendered image is shown in WPF, then it is recommended to use Pbgra32 (pre-multiplied alpha)
                    // because this is also what is internally used by WPF.
                    //
                    // But when the image is saved to png image, then the image should be non pre-multiplied
                    // because png does not support pre-multiplied image.

                    var pixelFormat = convertToNonPreMultipledAlpha ? PixelFormats.Bgra32 : PixelFormats.Pbgra32;

                    _writeableBitmap = new WriteableBitmap(finalBitmapWidth, finalBitmapHeight, 96, 96, pixelFormat, null); // always use 96 as dpi for WriteableBitmap otherwise the bitmap will be shown too small
                }

                _dxViewportView.RenderToBitmap(_writeableBitmap, multiSamplingCount, superSamplingCount);

                _renderBitmap = _writeableBitmap;
            }
            else
            {
                // When we do not need to reuse the WriteableBitmap, we can use the following RenderToBitmap method:
                _renderBitmap = _dxViewportView.RenderToBitmap(width:  (int)(BitmapWidth * dpiScaleX),
                                                               height: (int)(BitmapHeight * dpiScaleY),
                                                               preferedMultisampling: multiSamplingCount, // when -1 is used, then the currently used multisampling is used
                                                               supersamplingCount: superSamplingCount,
                                                               dpiX: dpiX,                          
                                                               dpiY: dpiY,
                                                               convertToNonPreMultipledAlpha: convertToNonPreMultipledAlpha); // pixel format of the returned image will be Pbgra32 (when convertToNonPreMultipledAlpha is false) or Bgra32 when true.
            }


            // Display render time (note that the first time the scene is rendered the time is bigger because of additional initializations)
            stopwatch.Stop();
            InfoTextBlock.Text = string.Format("Render time: {0:0.0}ms", stopwatch.Elapsed.TotalMilliseconds);

            if (_isFirstRender)
            {
                InfoTextBlock.Text += " (note: render time of the first image is longer then the time to render other images)";
                _isFirstRender = false;
            }

            RenderedImage.Source = _renderBitmap;

            TipTextBlock.Visibility = Visibility.Collapsed;

            SaveButton.Visibility = Visibility.Visible;
        }

        private void GetAntiAliasingSettings(out int multiSamplingCount, out int superSamplingCount)
        {
            if (!this.IsLoaded)
            {
                multiSamplingCount = 4;
                superSamplingCount = 4;
                return;
            }

            var selectedComboBoxItem = (ComboBoxItem)AntiAliasingComboBox.SelectedValue;

            var regex = new Regex(@"(\d)xMSAA\s(\d)xSSAA");
            var match = regex.Match((string)selectedComboBoxItem.Content);

            if (match.Success)
            {
                multiSamplingCount = Int32.Parse(match.Groups[1].Value);
                superSamplingCount = Int32.Parse(match.Groups[2].Value);
            }
            else
            {
                multiSamplingCount = 4;
                superSamplingCount = 4;
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

        private void SaveButton_OnClick(object sender, RoutedEventArgs e)
        {
            var saveFileDialog = new Microsoft.Win32.SaveFileDialog
            {
                OverwritePrompt = true,
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                FileName = "DXEngineRender.png",
                DefaultExt = "png",
                Filter = "png image (*.png)|*.png",
                Title = "Select file name to save rendered bitmap"
            };

            if (saveFileDialog.ShowDialog() ?? false)
            {
                try
                {
                    SaveBitmap(_renderBitmap, saveFileDialog.FileName);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error saving bitmap:\r\n" + ex.Message);
                }
            }

            
        }
    }
}
