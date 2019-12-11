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
using System.Windows.Threading;
using Ab3d.DirectX;
using Ab3d.DirectX.Client.Diagnostics;
using Ab3d.DirectX.Controls;
using Ab3d.DXEngine.Wpf.Samples.Common;

namespace Ab3d.DXEngine.Wpf.Samples.DXEnginePerformance
{
    /// <summary>
    /// Interaction logic for PerformanceTest.xaml
    /// </summary>
    public partial class PerformanceTest : Page
    {
        private const bool OptimizeReadModel = true;

        // Change this value to set for how much the Camera's Heading is changed after each rendered frame
        private const double HeadingIncreaseOnEachFrame = 0.5;

        private readonly string StartupFileName = @"Resources\Models\ship_boat.obj";

        private Stopwatch _stopwatch;

        private int _lastSecond;

        private int _framesCount;

        private PerformanceAnalyzer _performanceAnalyzer;
        private string _savedWindowTitle;

        public PerformanceTest()
        {
            InitializeComponent();

            // To use RenderAsManyFramesAsPossible we need DirectXOverlay presentation type
            MainDXViewportView.PresentationType = DXView.PresentationTypes.DirectXOverlay;

            InfoTextBlock.Text = string.Format(InfoTextBlock.Text, HeadingIncreaseOnEachFrame);

            MainDXViewportView.DXSceneInitialized += delegate(object sender, EventArgs args)
            {
                if (MainDXViewportView.DXScene == null) // Probably WPF 3D rendering
                    return;

                string testName = string.Format("PerformanceTest with '{0}' ({1})", 
                    System.IO.Path.GetFileName(StartupFileName),
                    OptimizeReadModel ? "optimized" : "not optimized");

                _performanceAnalyzer = new PerformanceAnalyzer(MainDXViewportView, testName, initialCapacity: 10000);
                _performanceAnalyzer.StartCollectingStatistics();
            };


            this.Loaded += OnLoaded;


            // FPS will be displayed in Window Title - save the title so we can restore it later
            _savedWindowTitle = Application.Current.MainWindow.Title;

            this.Unloaded += delegate(object sender, RoutedEventArgs args)
            {
                StopTest(showResults: false);

                MainDXViewportView.Dispose();
            };
        }

        private void OnLoaded(object sender, RoutedEventArgs routedEventArgs)
        {
            string startupFileName;

            if (System.IO.Path.IsPathRooted(StartupFileName))
                startupFileName = StartupFileName;
            else
                startupFileName = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, StartupFileName);

            LoadObjFile(startupFileName);

            StartRenderingAsCrazy();
        }

        private void StartRenderingAsCrazy()
        {
            // Set static RenderAsManyFramesAsPossible field to true to render as many frames as possible.
            // This way DXEngine does not wait until WPF's Render event but starts rendering new frame immediately after one scene was rendered.
            // This mode works only with PresentationType set to DirectXOverlay (this way DXEngine has its own part of the screen where it can render)
            // Note that this mode is used only for testing the performance and cannot be used in production because it significantly affects WPF's responsiveness
            // For example user interface is not updated - therefore FPS is rendered in title that gets updated by Windows.
            Ab3d.DirectX.Controls.D3DHost.RenderAsManyFramesAsPossible = true;

            // Also not that to utilize the increased rendering performance we need to make changes to our scene after every rendered scene
            // and not on WPF's CompositionTarget.Rendering event (for example this is used by Ab3d.PowerToys cameras to rotate the camera)
            MainDXViewportView.SceneRendered += OnSceneRendered;

            _stopwatch = new Stopwatch();
            _stopwatch.Start();
        }

        private void OnSceneRendered(object sender, EventArgs args)
        {
            Camera1.Heading += HeadingIncreaseOnEachFrame;

            if (_stopwatch.Elapsed.Seconds != _lastSecond)
            {
                UpdateFps();

                _lastSecond = _stopwatch.Elapsed.Seconds;
                _framesCount = 0;
            }
            else
            {
                _framesCount++;
            }
        }

        private void StopTest(bool showResults)
        {
            Ab3d.DirectX.Controls.D3DHost.RenderAsManyFramesAsPossible = false;

            MainDXViewportView.SceneRendered -= OnSceneRendered;

            if (_stopwatch != null)
                _stopwatch.Stop();

            Application.Current.MainWindow.Title = _savedWindowTitle;

            if (_performanceAnalyzer == null)
                return;


            _performanceAnalyzer.StopCollectingStatistics();

            var resultsText = _performanceAnalyzer.GetResultsText();

            if (showResults)
            {
                ResultsTextBox.Text = resultsText;
                ResultsTextBox.Visibility = Visibility.Visible;

                MainDXViewportView.Visibility = Visibility.Collapsed;
            }
        }

        private void UpdateFps()
        {
            // While the performance test is running WPF is not able to always update its controls
            // Therefore we cannot display the FPS in WPF's TextBlock but need to show that in Window Title

            Application.Current.MainWindow.Title = "FPS: " + _framesCount.ToString();
        }

        private void LoadObjFile(string fileName)
        {
            var readerObj = new Ab3d.ReaderObj();
            var readModel = readerObj.ReadModel3D(fileName);

            if (OptimizeReadModel)
            {
                readModel = Ab3d.Utilities.ModelOptimizer.OptimizeAll(readModel);

                readModel.Freeze();
            }

            var modelVisual3D = new ModelVisual3D();
            modelVisual3D.Content = readModel;

            MainViewport.Children.Add(modelVisual3D);


            double readObjectSize = Math.Sqrt(readModel.Bounds.SizeX * readModel.Bounds.SizeX + readModel.Bounds.SizeY * readModel.Bounds.SizeY + readModel.Bounds.SizeZ + readModel.Bounds.SizeZ);
            Camera1.Distance = readObjectSize * 1.5;

            var objectsCenter = new Point3D(readModel.Bounds.X + readModel.Bounds.SizeX / 2,
                readModel.Bounds.Y + readModel.Bounds.SizeY / 2,
                readModel.Bounds.Z + readModel.Bounds.SizeZ / 2);

            Camera1.TargetPosition = objectsCenter;
            Camera1.Heading = 0;
        }

        private void StopButton_OnClick(object sender, RoutedEventArgs e)
        {
            StopTest(showResults: true);

            StopButton.IsEnabled = false;
        }
    }
}
