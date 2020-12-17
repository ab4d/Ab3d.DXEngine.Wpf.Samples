using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;
using System.Xml;
using Ab3d.Common;
using Ab3d.DirectX.Cameras;
using Ab3d.DirectX.Controls;
using Ab3d.DirectX.Effects;
using Ab3d.DirectX.Lights;
using Ab3d.DirectX.Models;
using Microsoft.Win32;
using SharpDX;
using SharpDX.Direct3D11;
using Color = System.Windows.Media.Color;
using Exception = System.Exception;

namespace Ab3d.DirectX.Client.Diagnostics
{
    /// <summary>
    /// Interaction logic for DiagnosticsWindow.xaml
    /// </summary>
    public partial class DiagnosticsWindow : Window
    {
        public DXView DXView
        {
            get { return _dxView; }
            set
            {
                if (ReferenceEquals(_dxView, value))
                    return;

                RegisterDxView(value);
                _dxView = value;
            }
        }

        public const double InitialWindowWidth = 310;

        public bool IsDXEngineDebugBuild { get; private set; }

        public bool ShowProcessCpuUsage { get; set; }

        public string DumpFileName { get; set; }

        private static readonly int UpdateStatisticsInterval = 100; // 100 ms = update statistics 10 times per second

        private bool _isManuallyEnabledCollectingStatistics;

        private WpfPreviewWindow _wpfPreviewWindow;
        private DXView _dxView;

        private DateTime _lastStatisticsUpdate;
        private DateTime _lastPerfCountersReadTime;

        private bool _hasHitTestingTime;

        private bool _showRenderingStatistics = true;

        private Queue<double> _fpsQueue;

        private List<Tuple<DXDiagnostics.LogLevels, string>> _logMessages;
        private int _deletedLogMessagesCount;
        private const int MaxLogMessages = 200;

        private string _logMessagesString;

        private LogMessagesWindow _logMessagesWindow;

        private SettingsEditorWindow _settingsEditorWindow;
        private RenderingFilterWindow _renderingFilterWindow;

        private PerformanceAnalyzer _performanceAnalyzer;
        private bool _isOnSceneRenderedSubscribed;

        private PerformanceCounter _cpuCounter;
        private PerformanceCounter _processorFrequencyCounter;

        private SolidColorEffect _solidColorEffect;
        private CustomActionRenderingStep _objectIdRenderingStep;
        private List<MaterialSortedRenderingQueue> _alteredRenderingQueues;

        private float _lastCpuUsage;

        private int _processorsCount = 1;
        private DispatcherTimer _updateStatisticsTimer;

        public DiagnosticsWindow(DXView dxView)
            : this()
        {
            this.DXView = dxView;
        }


        public DiagnosticsWindow()
        {
            InitializeComponent();

            ShowProcessCpuUsage = false;

            this.Width = InitialWindowWidth;


            string dumpFolder;
            if (System.IO.Directory.Exists(@"C:\temp"))
                dumpFolder = @"C:\temp\";
            else
                dumpFolder = System.IO.Path.GetTempPath();

            DumpFileName = System.IO.Path.Combine(dumpFolder, "DXEngineDump.txt");


            // Set DXEngine assembly version
            var version = typeof(DXDevice).Assembly.GetName().Version;

            // IsDebugVersion field is defined only in Debug version
            var fieldInfo = typeof(DXDevice).GetField("IsDebugVersion");
            IsDXEngineDebugBuild = fieldInfo != null;

            DXEngineInfoTextBlock.Text = string.Format("Ab3d.DXEngine v{0}.{1}.{2}{3}",
                version.Major, version.Minor, version.Build,
                IsDXEngineDebugBuild ? " (debug build)" : "");

            Ab3d.DirectX.DXDiagnostics.LogLevel = DXDiagnostics.LogLevels.Warn;
            Ab3d.DirectX.DXDiagnostics.LogAction = DXEngineLogAction;

            bool isCaptureFrameSupported;
            try
            {
                isCaptureFrameSupported = CheckIsCaptureFrameSupported();
            }
            catch (MissingMethodException)
            {
                // In case of an old DXEngine
                isCaptureFrameSupported = false;
            }
            
            if (!isCaptureFrameSupported)
            {
                CaptureFrameMenuItem.IsEnabled = false;
                CaptureFrameMenuItem.ToolTip = "Capture frame from runtime is not available.\r\n\r\nCapture is supported on Windows 8.1 and newer operating system,\r\nwith installed Windows SDK and\r\nONLY when the Visual Studio Graphics Debugging is running.";
                CaptureFrameMenuItem.SetValue(ToolTipService.ShowOnDisabledProperty, true);
                CaptureFrameMenuItem.SetValue(ToolTipService.ShowDurationProperty, 30000);
            }


            // When the window is shown start showing statistics
            Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(StartShowingStatistics));


            this.Loaded += OnLoaded;
            this.Closing += OnClosing;
        }

        private void OnLoaded(object sender, RoutedEventArgs routedEventArgs)
        {
            UpdateEnabledMenuItems();
        }

        private void UpdateEnabledMenuItems()
        {
            var menuItemsThatRequireDXScene = new MenuItem[]
            {
                DumpSceneNodesMenuItem,
                DumpRenderingQueuesMenuItem,
                DumpRenderingStepsMenuItem,
                DumpBackBufferChangesMenuItem,
                SaveToBitmapMenuItem,
                StartPerformanceAnalyzerMenuItem,
                DumpResourcesMenuItem
            };

            if (DXView != null && DXView.DXScene != null)
            {
                foreach (var menuItem in menuItemsThatRequireDXScene)
                {
                    menuItem.IsEnabled = true;
                    menuItem.ToolTip = null;
                }
            }
            else
            {
                foreach (var menuItem in menuItemsThatRequireDXScene)
                {
                    menuItem.IsEnabled = false;
                    menuItem.ToolTip = "This action require that DXScene object is created (not supported in WPF 3D rendering)";
                    menuItem.SetValue(ToolTipService.ShowOnDisabledProperty, true);
                }

                if (DXView != null)
                    DXView.DXSceneDeviceCreated += (sender, args) => UpdateEnabledMenuItems();
            }

            // Show DumpBackBufferChangesMenuItem only with debug device (because we need resource names otherwise we will not see anything)
            if (DXView != null && DXView.DXScene != null && DXView.DXScene.DXDevice != null && DXView.DXScene.DXDevice.IsDebugDevice)
                DumpBackBufferChangesMenuItem.Visibility = Visibility.Visible;
            else
                DumpBackBufferChangesMenuItem.Visibility = Visibility.Collapsed;

            // On each new scene reset the StartStopCameraRotationMenuItem text
            StartStopCameraRotationMenuItem.Header = "Toggle camera rotation";
        }

        private bool CheckIsCaptureFrameSupported()
        {
            bool isCaptureFrameSupported = DXDiagnostics.IsCaptureFrameSupported(); // IsCaptureFrameSupported method is not available in some older versions of DXEngine so use catch that case and set isCaptureFrameSupported to false

            return isCaptureFrameSupported;
        }

        private void DXEngineLogAction(DXDiagnostics.LogLevels logLevels, string message)
        {
            if (_logMessages == null)
                _logMessages = new List<Tuple<DXDiagnostics.LogLevels, string>>();

            if (_deletedLogMessagesCount > _logMessages.Count)
                _deletedLogMessagesCount = 0; // This means that the messages were deleted in LogMessagesWindow

            if (_logMessages.Count >= MaxLogMessages)
            {
                // remove first 1/10 of messages
                int logMessagesToDelete = (int) (MaxLogMessages/10); 
                _logMessages.RemoveRange(0, logMessagesToDelete);

                _deletedLogMessagesCount += logMessagesToDelete;
            }

            _logMessages.Add(new Tuple<DXDiagnostics.LogLevels, string>(logLevels, message));

            var numberOfWarnings = _logMessages.Count(t => t.Item1 >= DXDiagnostics.LogLevels.Warn);
            if (numberOfWarnings > 0)
            {
                WarningsCountTextBlock.Text = (numberOfWarnings + _deletedLogMessagesCount).ToString();
                LogWarningsPanel.Visibility = Visibility.Visible;
            }

            if (_logMessagesWindow != null)
            {
                _logMessagesWindow.MessageStartIndex = _deletedLogMessagesCount + 1;
                _logMessagesWindow.UpdateLogMessages();
            }
        }

        private void OnClosing(object sender, CancelEventArgs cancelEventArgs)
        {
            if (_performanceAnalyzer != null)
            {
                _performanceAnalyzer.StopCollectingStatistics();
                _performanceAnalyzer = null;
            }

            DisposePerformanceCounters();

            if (_logMessagesWindow != null)
            {
                try
                {
                    _logMessagesWindow.Close();
                }
                catch
                {
                    // Maybe the window was already closed
                }

                _logMessagesWindow = null;
            }

            Ab3d.DirectX.DXDiagnostics.LogAction = null;
            UnregisterCurrentDXView();
        }

        private void StartShowingStatistics()
        {
            if (DXView != null && DXView.DXScene != null)
            {
                ResultsTitleTextBlock.Visibility = Visibility.Visible;
                ResultsTitleTextBlock.Text = _showRenderingStatistics ? "Rendering statistics:" : "Camera info:";
            }
            else
            {
                ResultsTitleTextBlock.Visibility = Visibility.Collapsed;
            }

            AnalyerResultsTextBox.Visibility = Visibility.Collapsed;
            StatisticsTextBlock.Visibility = Visibility.Visible;


            SubscribeOnSceneRendered();

            // Setup PerformanceCounter
            if (ShowProcessCpuUsage)
                SetupPerformanceCounters();

            if (!Ab3d.DirectX.DXDiagnostics.IsCollectingStatistics)
            {
                DXDiagnostics.IsCollectingStatistics = true;
                _isManuallyEnabledCollectingStatistics = true;

                if (_dxView != null)
                    _dxView.Refresh(); // Force render so we get on statistics and are not showing empty data
            }
        }

        private void EndShowingStatistics()
        {
            StatisticsTextBlock.Visibility = Visibility.Collapsed;
            ResultsTitleTextBlock.Visibility = Visibility.Collapsed;

            UnsubscribeOnSceneRendered();
            DisposePerformanceCounters();

            if (_isManuallyEnabledCollectingStatistics)
            {
                DXDiagnostics.IsCollectingStatistics = false;
                _isManuallyEnabledCollectingStatistics = true;
            }
        }

        private void RegisterDxView(DXView dxView)
        {
            UnregisterCurrentDXView();

            _dxView = dxView;

            if (dxView == null)
                return;

            dxView.Disposing += OnDXViewDisposing;

            DeviceInfoControl.DXView = dxView;

            StartShowingStatistics();

            if (Ab3d.DirectX.DXDiagnostics.IsCollectingStatistics)
            {
                if (dxView.DXScene != null && dxView.DXScene.Statistics != null)
                {
                    ResultsTitleTextBlock.Visibility = Visibility.Visible;
                    UpdateStatistics(dxView.DXScene.Statistics);
                }
            }

            UpdateEnabledMenuItems();
        }

        private void OnDXViewDisposing(object sender, EventArgs args)
        {
            UnregisterCurrentDXView();
        }

        private void UnregisterCurrentDXView()
        {
            if (_dxView == null)
                return;

            UnsubscribeOnSceneRendered();
            DisposePerformanceCounters();

            _dxView.Disposing -= OnDXViewDisposing;

            _dxView = null;

            if (_wpfPreviewWindow != null)
            {
                try
                {
                    _wpfPreviewWindow.Close();
                }
                catch
                {
                    // Maybe the window was already closed
                }

                _wpfPreviewWindow = null;
            }

            if (_settingsEditorWindow != null)
            {
                try
                {
                    _settingsEditorWindow.Close();
                }
                catch
                {
                    // Maybe the window was already closed
                }

                _settingsEditorWindow = null;
            }
            
            if (_renderingFilterWindow != null)
            {
                try
                {
                    _renderingFilterWindow.Close();
                }
                catch
                {
                    // Maybe the window was already closed
                }

                _renderingFilterWindow = null;
            }

            DeviceInfoControl.DXView = null;

            EndShowingStatistics();
            StopPerformanceAnalyzer();
        }

        private void SubscribeOnSceneRendered()
        {
            if (_isOnSceneRenderedSubscribed || _dxView == null)
                return;

            if (_dxView.UsedGraphicsProfile == null && _dxView.DXScene != null) // In case we manually created the DXScene, then the _dxView.SceneRendered will not be called. Instead we need to subscribe to DXScene.AfterFrameRendered
                _dxView.DXScene.AfterFrameRendered += DXViewOnSceneRendered;
            else
                _dxView.SceneRendered += DXViewOnSceneRendered;

            _isOnSceneRenderedSubscribed = true;
        }

        private void UnsubscribeOnSceneRendered()
        {
            if (!_isOnSceneRenderedSubscribed || _dxView == null)
                return;

            if (_updateStatisticsTimer != null)
            {
                _updateStatisticsTimer.Stop();
                _updateStatisticsTimer = null;
            }

            if (_dxView.UsedGraphicsProfile == null && _dxView.DXScene != null)
                _dxView.DXScene.AfterFrameRendered -= DXViewOnSceneRendered;
            else
                _dxView.SceneRendered -= DXViewOnSceneRendered;

            _isOnSceneRenderedSubscribed = false;
        }

        private void SetupPerformanceCounters()
        {
            if (_processorsCount == 0)
            {
                try
                {
                    _processorsCount = Environment.ProcessorCount;
                }
                catch
                {
                    _processorsCount = 1;
                }
            }

            if (_cpuCounter == null)
            {
                try
                {
                    string processName = Process.GetCurrentProcess().ProcessName;
                    _cpuCounter = new PerformanceCounter("Process", "% Processor Time", processName);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error creating PerformanceCounter:\r\n" + ex.Message);
                    _cpuCounter = null;
                }

                // Try to get processor relative frequency counter
                try
                {
                    _processorFrequencyCounter = new PerformanceCounter("Processor information", "% of Maximum Frequency", "_Total");
                }
                catch
                {
                    _processorFrequencyCounter = null;
                }
            }

            // When do not render we still need to update cpu usage statistics - do that every seconds
            if (_updateStatisticsTimer == null)
            {
                _updateStatisticsTimer = new DispatcherTimer();
                _updateStatisticsTimer.Interval = TimeSpan.FromSeconds(1);
                _updateStatisticsTimer.Tick += CheckToUpdateStatistics;
                _updateStatisticsTimer.Start();
            }
        }

        private void DisposePerformanceCounters()
        {
            if (_updateStatisticsTimer != null)
            {
                _updateStatisticsTimer.Stop();
                _updateStatisticsTimer = null;
            }

            if (_cpuCounter != null)
            {
                _cpuCounter.Dispose();
                _cpuCounter = null;
            }

            if (_processorFrequencyCounter != null)
            {
                _processorFrequencyCounter.Dispose();
                _processorFrequencyCounter = null;
            }
        }

        private void CheckToUpdateStatistics(object sender, EventArgs eventArgs)
        {
            var elapsed = (DateTime.Now - _lastStatisticsUpdate).TotalMilliseconds;

            if (elapsed > 950 && DXView != null && DXView.DXScene != null && DXView.DXScene.Statistics != null)
                UpdateStatistics(DXView.DXScene.Statistics);
        }

        private void DXViewOnSceneRendered(object sender, EventArgs eventArgs)
        {
            // We also support scenario when the rendering is done on non-UI thread.
            // In this case we use Dispatcher.BeginInvoke to update the shown data on the UI thread

            RenderingStatistics statistics;

            if (Ab3d.DirectX.DXDiagnostics.IsCollectingStatistics && DXView != null && DXView.DXScene != null)
                statistics = DXView.DXScene.Statistics;
            else
                statistics = null;

            if (this.CheckAccess()) // Check if we are on UI thread
            {
                if (statistics != null)
                    UpdateStatistics(statistics);
                else
                    StatisticsTextBlock.Visibility = Visibility.Collapsed;
            }
            else
            {
                // Non-UI thread - use Dispatcher.BeginInvoke
                if (statistics != null)
                {
                    statistics = DXView.DXScene.Statistics.Clone();
                    this.Dispatcher.BeginInvoke(new Action(delegate { UpdateStatistics(statistics); }));
                }
                else
                {
                    this.Dispatcher.BeginInvoke(new Action(delegate { StatisticsTextBlock.Visibility = Visibility.Collapsed; }));
                }
            }
        }

        private void GetSceneXamlMenuItem_OnClick(object sender, RoutedEventArgs args)
        {   
            // Show WPF preview only if DXSceneView is showing DXViewport3D with some content
            var dxViewport3D = GetDXViewportView();
            if (dxViewport3D != null && dxViewport3D.Viewport3D != null)
            {
                string xaml;

                try
                {
                    xaml = GetXaml(dxViewport3D.Viewport3D);
                }
                catch (Exception ex)
                {
                    xaml = "Exception saving Viewport3D to XAML:\r\n" + ex.Message;
                }

                ShowInfoText(xaml);
            }
        }

        private void ShowWpfPreviewMenuItem_OnClick(object sender, RoutedEventArgs args)
        {   
            // Show WPF preview only if DXSceneView is showing DXViewport3D with some content
            var dxViewport3D = GetDXViewportView();
            if (dxViewport3D != null && dxViewport3D.Viewport3D != null && dxViewport3D.Viewport3D.Children.Count > 0)
                ShowWpfPreviewWindow();
        }

        private void StartStopCameraRotationMenuItem_OnClick(object sender, RoutedEventArgs args)
        {
            var powerToysCamera = GetPowerToysCamera();

            if (powerToysCamera == null)
            {
                MessageBox.Show("Cannot find Ab3d.PowerToys camera");
                return;
            }

            try
            {
                var propertyInfo = powerToysCamera.GetType().GetProperty("IsRotating");

                bool success = false;
                if (propertyInfo != null)
                {
                    var propertyValue = propertyInfo.GetValue(powerToysCamera, null);

                    if (propertyValue != null)
                    {
                        bool isAnimating = (bool)propertyValue;

                        if (isAnimating)
                        {
                            var stopRotationMethodInfo = powerToysCamera.GetType().GetMethod("StopRotation", new Type[] {});
                            if (stopRotationMethodInfo != null)
                            {
                                stopRotationMethodInfo.Invoke(powerToysCamera, null);
                                success = true;

                                StartStopCameraRotationMenuItem.Header = "Start camera rotation";
                            }
                        }
                        else
                        {
                            var startRotationMethodInfo = powerToysCamera.GetType().GetMethod("StartRotation", new[] { typeof(double), typeof(double) });
                            if (startRotationMethodInfo != null)
                            {
                                startRotationMethodInfo.Invoke(powerToysCamera, new object[] {45.0, 0.0});
                                success = true;

                                StartStopCameraRotationMenuItem.Header = "Stop camera rotation";
                            }
                        }
                    }
                }

                if (!success)
                    MessageBox.Show("Cannot start camera animation");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error starting or stopping camera animation:\r\n" + ex.Message);
            }
        }

        private void SaveToBitmapMenuItem_OnClick(object sender, RoutedEventArgs args)
        {
            if (DXView.DXScene == null || DXView.DXScene.RenderingContext == null)
                return;

            DXView.DXScene.RenderingContext.RegisterBackBufferMapping(StagingBackBufferMappedCallback);

            // After we have subscribed to capture next frame, we can force rendering that frame
            DXView.Refresh();
        }

        private void StagingBackBufferMappedCallback(object s, BackBufferReadyEventArgs e)
        {
            try
            {
                var renderedBitmap = CreatedRenderedBitmap(e);

                DXView.DXScene.RenderingContext.UnregisterBackBufferMapping(StagingBackBufferMappedCallback);

                // We do not want to show SaveFileDialog inside a rendering pipeline.
                // So once we have the image in main memory in WriteableBitmap, we delay the invoke of saving bitmap and exit this callback 
                Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(() => { SaveRenderedBitmap(renderedBitmap); }));
            }
            catch
            {
                // Do not crash DXEngine in case of exception
            }
        }

        private void DumpSceneNodesMenuItem_OnClick(object sender, RoutedEventArgs e)
        {
            DumpSceneNodes();
        }

        private void DumpRenderingQueuesMenuItem_OnClick(object sender, RoutedEventArgs e)
        {
            DumpRenderingQueues();
        }

        private void DumpResourcesMenuItem_OnClick(object sender, RoutedEventArgs e)
        {
            DumpResources();
        }

        private void DumpRenderingStepsMenuItem_OnClick(object sender, RoutedEventArgs e)
        {
            DumpRenderingSteps();
        }

        private void DumpBackBufferChangesMenuItem_OnClick(object sender, RoutedEventArgs e)
        {
            BackBufferChanges();
        }

        private void DumpSystemInfoMenuItem_OnClick(object sender, RoutedEventArgs e)
        {
            DumpSystemInfo();
        }

        private void DumpDXEngineSettingsMenuItem_OnClick(object sender, RoutedEventArgs e)
        {
            DumpDXEngineSettings();
        }
        
        private void EditDXEngineSettingsMenuItem_OnClick(object sender, RoutedEventArgs e)
        {
            EditDXEngineSettings();
        }

        private void DumpCurrentSceneMenuItem_OnClick(object sender, RoutedEventArgs e)
        {
            ShowFullSceneDump();
        }
        
        private void RenderObjectIdMenuItemMenuItem_OnClick(object sender, RoutedEventArgs e)
        {
            RenderObjectIdBitmap();
        }
        
        private void RenderingFilterMenuItem_OnClick(object sender, RoutedEventArgs e)
        {
            ShowRenderingFilterWindow();
        }

        private void CaptureFrameMenuItem_OnClick(object sender, RoutedEventArgs e)
        {
            DXDiagnostics.CaptureNextFrame(_dxView.DXScene);
        }

        private void GarbageCollectMenuItem_OnClick(object sender, RoutedEventArgs e)
        {
            GC.Collect();
            GC.WaitForFullGCComplete();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }

        private void StartPerformanceAnalyzerMenuItem_OnClick(object sender, RoutedEventArgs e)
        {
            if (_performanceAnalyzer != null)
                return; // Already started (this should not happen)

            EndShowingStatistics();
            AnalyerResultsTextBox.Visibility = Visibility.Collapsed;

            ResultsTitleTextBlock.Visibility = Visibility.Collapsed;

            // Start new test
            _performanceAnalyzer = new PerformanceAnalyzer(DXView, "DXEngine Snoop performance test", initialCapacity: 10000);
            _performanceAnalyzer.StartCollectingStatistics();

            ActionsMenu.Visibility = Visibility.Collapsed;

            ShowButtons(showStopPerformanceAnalyzerButton: true, showShowStatisticsButton: false);
        }

        private void StopPerformanceAnalyzerButton_OnClick(object sender, RoutedEventArgs e)
        {
            StopPerformanceAnalyzer();
        }

        private void StopPerformanceAnalyzer()
        {
            if (_performanceAnalyzer == null)
                return;

            ActionsMenu.Visibility = Visibility.Visible;
            ActionsMenu.IsEnabled = true;

            _performanceAnalyzer.StopCollectingStatistics();

            string resultsText = _performanceAnalyzer.GetResultsText();
            resultsText = "DXEngine performance analysis\r\n" + resultsText;

            _performanceAnalyzer = null;

            // Also write results to Visual Studio output window
            System.Diagnostics.Debug.WriteLine(resultsText);


            // Show results in AnalyerResultsTextBox (shown instead of StatisticsTextBlock)
            AnalyerResultsTextBox.Text = resultsText;

            if (StatisticsTextBlock.ActualHeight > 0)
                AnalyerResultsTextBox.MinHeight = StatisticsTextBlock.ActualHeight; // If StatisticsTextBlock is shown use the same height as it has
            else
                AnalyerResultsTextBox.MinHeight = 400;

            StatisticsTextBlock.Visibility = Visibility.Collapsed;
            AnalyerResultsTextBox.Visibility = Visibility.Visible;

            ResultsTitleTextBlock.Visibility = Visibility.Visible;
            ResultsTitleTextBlock.Text = "Performance analyzer results:";

            SaveAnalyzerResultsMenuItem.IsEnabled = true;

            ShowStatisticsButton.Content = _showRenderingStatistics ? "Show rendering statistics" : "Show camera info";
            
            ShowButtons(showStopPerformanceAnalyzerButton: false, showShowStatisticsButton: true);
        }

        private void SaveAnalyzerResultsMenuItem_OnClick(object sender, RoutedEventArgs e)
        {
            var saveFileDialog = new Microsoft.Win32.SaveFileDialog();
            saveFileDialog.AddExtension = false;
            saveFileDialog.CheckFileExists = false;
            saveFileDialog.CheckPathExists = true;
            saveFileDialog.OverwritePrompt = false;
            saveFileDialog.ValidateNames = false;

            saveFileDialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);

            saveFileDialog.FileName = string.Format("DXEngine performance {0:HH}_{0:mm}.txt", DateTime.Now);
            saveFileDialog.DefaultExt = "txt";
            saveFileDialog.Filter = "Text file (*.txt)|*.txt";
            saveFileDialog.Title = "Select file name to store DXEngine Performance Analyzer report";

            if (saveFileDialog.ShowDialog() ?? false)
            {
                try
                {
                    System.IO.File.WriteAllText(saveFileDialog.FileName, AnalyerResultsTextBox.Text);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error saving report:\r\n" + ex.Message);
                }
            }
        }

        private void LogWarningsPanel_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (_logMessagesWindow != null)
                return;

            _logMessagesWindow = new LogMessagesWindow();

            _logMessagesWindow.MessageStartIndex = _deletedLogMessagesCount + 1;
            _logMessagesWindow.LogMessages = _logMessages;

            _logMessagesWindow.Closing += delegate(object o, CancelEventArgs args)
            {
                _logMessagesWindow = null;

                // Check if user cleared the list of warnings
                if (_logMessages == null || _logMessages.Count == 0)
                {
                    WarningsCountTextBlock.Text = null;
                    LogWarningsPanel.Visibility = Visibility.Collapsed;
                }
            };

            _logMessagesWindow.Show();
        }

        private void IsResourceTrackingEnabledCheckBoxChanged(object sender, RoutedEventArgs e)
        {
            try
            {
                SetDXEngineResourceTracking(IsResourceTrackingEnabledCheckBox.IsChecked ?? false);
            }
            catch
            {
                IsResourceTrackingEnabledCheckBox.IsChecked = false;
            }

            // Close the menu
            ActionsRootMenuItem.IsSubmenuOpen = false;
        }

        private void IsSharpDxResourceTrackingEnabledCheckBoxChanged(object sender, RoutedEventArgs e)
        {
            try
            {
                SetSharpDXObjectTracking(IsSharpDxResourceTrackingEnabledCheckBox.IsChecked ?? false);
            }
            catch
            {
                IsSharpDxResourceTrackingEnabledCheckBox.IsChecked = false;
            }

            // Close the menu
            ActionsRootMenuItem.IsSubmenuOpen = false;
        }

        private void OnShowCpuUsageCheckBoxCheckedChanged(object sender, RoutedEventArgs e)
        {
            ShowProcessCpuUsage = ShowCpuUsageCheckBox.IsChecked ?? false;

            // Close the menu
            ActionsRootMenuItem.IsSubmenuOpen = false;

            if (ShowProcessCpuUsage)
                SetupPerformanceCounters();
            else
               DisposePerformanceCounters();
        }

        private void ShowStatisticsButton_OnClick(object sender, RoutedEventArgs e)
        {
            StartShowingStatistics();
            ShowButtons(showStopPerformanceAnalyzerButton: false, showShowStatisticsButton: false);
        }

        private void AlwaysOnTopCheckBoxChanged(object sender, RoutedEventArgs e)
        {
            this.Topmost = (AlwaysOnTopCheckBox.IsChecked ?? false);

            // Close the menu
            ActionsRootMenuItem.IsSubmenuOpen = false;
        }

        private void GetCameraDetailsMenuItem_OnClick(object sender, RoutedEventArgs e)
        {
            ShowCameraDetails();
        }

        private void ShowButtons(bool showStopPerformanceAnalyzerButton, bool showShowStatisticsButton)
        {
            StopPerformanceAnalyzerButton.Visibility = showStopPerformanceAnalyzerButton ? Visibility.Visible : Visibility.Collapsed;
            ShowStatisticsButton.Visibility          = showShowStatisticsButton ? Visibility.Visible : Visibility.Collapsed;

            ButtonsPanel.Visibility = showStopPerformanceAnalyzerButton || showShowStatisticsButton ? Visibility.Visible : Visibility.Collapsed;
        }


        private string GetRenderingStatisticsDetails(RenderingStatistics renderingStatistics, string fpsText)
        {
            string shadowMappingStatistics;

            try
            {
                if (renderingStatistics.RenderShadowsMs > 0)
                    shadowMappingStatistics = string.Format(System.Globalization.CultureInfo.InvariantCulture, "RenderShadows: {0:0.00}ms\r\n", renderingStatistics.RenderShadowsMs);
                else
                    shadowMappingStatistics = "";
            }
            catch (MissingMethodException)
            {
                // In case of an old DXEngine that do not yet have the RenderShadowsMs property
                shadowMappingStatistics = "";
            }

            string postProcessingTimeText;
            if (renderingStatistics.PostProcessingRenderTimeMs > 0)
                postProcessingTimeText = string.Format(System.Globalization.CultureInfo.InvariantCulture, "PostProcessingRenderTime: {0:0.00} ms\r\n", renderingStatistics.PostProcessingRenderTimeMs);
            else
                postProcessingTimeText = "";

            string hitTestingTimeText;
            try
            {
                hitTestingTimeText = GetHitTestingTime(renderingStatistics);
            }
            catch (MissingMethodException)
            {
                // In case of an old DXEngine that do not yet have the HitTestingTimeMs property
                hitTestingTimeText = "";
            }

            string usedBackgroundThreadsCount;
            try
            {
                usedBackgroundThreadsCount = GetUsedBackgroundThreadsCount(renderingStatistics);
            }
            catch (MissingMethodException)
            {
                // In case of an old DXEngine that do not yet have the HitTestingTimeMs property
                usedBackgroundThreadsCount = "";
            }


            string cachedCommandListsStatistics;
            try
            {
                cachedCommandListsStatistics = GetCachedCommandListsStatistics(renderingStatistics);
            }
            catch (MissingMethodException)
            {
                // In case of an old DXEngine that do not yet have the CreatedCommandListsCount and CachedCommandListsCount properties
                cachedCommandListsStatistics = "";
            }


            if (fpsText == null)
                fpsText = "";

            if (fpsText.Length > 0 && !fpsText.Contains("("))
                fpsText = '(' + fpsText + ')';

            string statisticsText = string.Format(
                System.Globalization.CultureInfo.InvariantCulture,
@"Frame number: {0:#,##0}
Frame time: {1:0.00}ms {2}
UpdateTime: {3:0.00} ms
PrepareRenderTime: {4:0.00} ms
DrawRenderTime: {5:0.00} ms
{6}CompleteRenderTime: {7:0.00} ms
{8}{9}DrawCallsCount: {10:#,##0}
DrawnIndicesCount: {11:#,##0}
ShaderChangesCount: {12:#,##0}
VertexBuffersChangesCount: {13:#,##0}
IndexBuffersChangesCount: {14:#,##0}
ConstantBufferChangesCount: {15:#,##0}
StateChangesCount: {16:#,##0}{17}{18}",
                renderingStatistics.FrameNumber,
                renderingStatistics.UpdateTimeMs + renderingStatistics.TotalRenderTimeMs,
                fpsText,
                renderingStatistics.UpdateTimeMs,
                renderingStatistics.PrepareRenderTimeMs,
                renderingStatistics.DrawRenderTimeMs,
                shadowMappingStatistics,
                renderingStatistics.CompleteRenderTimeMs,
                postProcessingTimeText,
                hitTestingTimeText,
                renderingStatistics.DrawCallsCount,
                renderingStatistics.DrawnIndicesCount,
                renderingStatistics.ShaderChangesCount,
                renderingStatistics.VertexBuffersChangesCount,
                renderingStatistics.IndexBuffersChangesCount,
                renderingStatistics.ConstantBufferChangesCount,
                renderingStatistics.StateChangesCount,
                cachedCommandListsStatistics,
                usedBackgroundThreadsCount
                );

            return statisticsText;
        }

        // Read HitTestingTimeMs in a separate method so in case Diagnostics window is opened with and older version of DXEngine (for example from DXEngineSnoop) 
        // that does not yet define this property, we can catch this as MissingMethodException (the exception is thrown when entering the method that is using this property).
        [MethodImpl(MethodImplOptions.NoInlining)] // Do not inline so we get MissingMethodException when calling this method and not the calling method
        private string GetCachedCommandListsStatistics(RenderingStatistics renderingStatistics)
        {
            string cachedCommandListsStatistics;
            if (renderingStatistics.CachedCommandListsCount > 0)
            {
                cachedCommandListsStatistics = string.Format("\r\nCachedCommandLists: {0} / {1}\r\nCachedRenderedObjects: {2:#,##0}",
                    renderingStatistics.CachedCommandListsCount, renderingStatistics.CreatedCommandListsCount + renderingStatistics.CachedCommandListsCount,
                    renderingStatistics.CachedRenderedObjectsCount);
            }
            else
            {
                cachedCommandListsStatistics = "";
            }

            return cachedCommandListsStatistics;
        }


        // Read HitTestingTimeMs in a separate method so in case Diagnostics window is opened with and older version of DXEngine (for example from DXEngineSnoop) 
        // that does not yet define this property, we can catch this as MissingMethodException (the exception is thrown when entering the method that is using this property).
        [MethodImpl(MethodImplOptions.NoInlining)] // Do not inline so we get MissingMethodException when calling this method and not the calling method
        private string GetHitTestingTime(RenderingStatistics renderingStatistics)
        {
            double hitTestingTime = renderingStatistics.HitTestingTimeMs;

            if (hitTestingTime > 0)
            {
                // We need to manually reset the time to zero. See remarks in HitTestingTimeMs for more info.
                renderingStatistics.HitTestingTimeMs = 0;
                _hasHitTestingTime = true;
            }

            // We do not show HitTestingTime until there is actually one valid hit testing time bugger then 0
            // After the we show the time (even if it is zero) to avoid "flickering".
            if (hitTestingTime <= 0 && !_hasHitTestingTime)
                return "";

            return string.Format(System.Globalization.CultureInfo.InvariantCulture, "HitTestingTime: {0:0.##} ms\r\n", hitTestingTime);
        }

        // Read UsedBackgroundThreadsCount in a separate method so in case Diagnostics window is opened with and older version of DXEngine (for example from DXEngineSnoop) 
        // that does not yet define this property, we can catch this as MissingMethodException (the exception is thrown when entering the method that is using this property).
        [MethodImpl(MethodImplOptions.NoInlining)] // Do not inline so we get MissingMethodException when calling this method and not the calling method
        private string GetUsedBackgroundThreadsCount(RenderingStatistics renderingStatistics)
        {
            return "\r\nUsedBackgroundThreadsCount: " + renderingStatistics.UsedBackgroundThreadsCount.ToString();
        }

        private void UpdateStatistics(RenderingStatistics renderingStatistics)
        {
            var now = DateTime.Now;

            if (UpdateStatisticsInterval > 0 && _lastStatisticsUpdate != DateTime.MinValue)
            {
                double elapsed = (now - _lastStatisticsUpdate).TotalMilliseconds;

                if (elapsed < UpdateStatisticsInterval) // Check if the required elapsed time has already passed
                    return;
            }


            string statisticsText;

            if (_showRenderingStatistics)
            {
                statisticsText = GetRenderingStatisticsText(renderingStatistics, now);
            }
            else
            {
                var sb = new StringBuilder();

                try
                {
                    AddCameraInfo(sb);
                }
                catch (Exception ex)
                {
                    sb.AppendLine("Error getting camera info:")
                      .AppendLine(ex.Message);

                    if (ex.InnerException != null)
                        sb.AppendLine(ex.InnerException.Message);
                }

                statisticsText = sb.ToString();
            }


            StatisticsTextBlock.Text = statisticsText;

            _lastStatisticsUpdate = now;

            ResultsTitleTextBlock.Visibility = Visibility.Visible;
            StatisticsTextBlock.Visibility = Visibility.Visible;
        }

        private string GetRenderingStatisticsText(RenderingStatistics renderingStatistics, DateTime now)
        {
            double frameTime = renderingStatistics.UpdateTimeMs + renderingStatistics.TotalRenderTimeMs;
            double fps       = 1000 / frameTime;


            // Update average fps
            int averageResultsCount = 2000 / UpdateStatisticsInterval; // 2 seconds for default update interval (100) => averageResultsCount = 20 - every 20 statistical results we calculate an average
            if (averageResultsCount <= 1)
                averageResultsCount = 1;

            if (_fpsQueue == null)
                _fpsQueue = new Queue<double>(averageResultsCount);

            if (_fpsQueue.Count == averageResultsCount)
                _fpsQueue.Dequeue(); // dump the result that is farthest away

            _fpsQueue.Enqueue(fps);

            string averageFpsText;

            if (_fpsQueue.Count >= 10)
            {
                double averageFps = _fpsQueue.Average();
                averageFpsText = string.Format(System.Globalization.CultureInfo.InvariantCulture, "; avrg: {0:0.0}", averageFps);
            }
            else
            {
                averageFpsText = "";
            }

            string fpsText = String.Format(System.Globalization.CultureInfo.InvariantCulture, "{0:0.0} FPS{1}", fps, averageFpsText);


            string statisticsText;

            try
            {
                statisticsText = GetRenderingStatisticsDetails(renderingStatistics, fpsText);
            }
            catch (Exception ex)
            {
                statisticsText = "Error getting rendering statistics:\r\n" + ex.Message;
                if (ex.InnerException != null)
                    statisticsText += Environment.NewLine + ex.InnerException.Message;
            }

            if (ShowProcessCpuUsage)
            {
                float cpuUsage;

                if (_cpuCounter != null)
                {
                    var elapsed = (now - _lastPerfCountersReadTime).TotalMilliseconds;

                    if (elapsed >= 950) // To get accurate results we need to wait one second between reading perf values
                    {
                        try
                        {
                            cpuUsage = _cpuCounter.NextValue() / (float) _processorsCount;
                        }
                        catch
                        {
                            cpuUsage = _lastCpuUsage;
                        }

                        if (_processorFrequencyCounter != null)
                        {
                            try
                            {
                                float processorFrequency = _processorFrequencyCounter.NextValue(); // Get relative CPU frequency (from 0 to 100)

                                if (processorFrequency > 0)
                                    cpuUsage *= processorFrequency * 0.01f; // Adjust the cpu usage by relative frequency (this gives the same results as in Device Monitor)
                            }
                            catch
                            {
                            }
                        }

                        _lastCpuUsage             = cpuUsage;
                        _lastPerfCountersReadTime = now;
                    }
                    else
                    {
                        cpuUsage = _lastCpuUsage;
                    }
                }
                else
                {
                    cpuUsage = 0;
                }

                statisticsText += "\r\nProcess CPU usage:";

                if (cpuUsage > 0)
                    statisticsText += string.Format(System.Globalization.CultureInfo.InvariantCulture, " {0:0.0}%", cpuUsage);
            }

            return statisticsText;
        }

        private void DumpSceneNodes()
        {
            if (_dxView == null || _dxView.DXScene == null) return;

            string dumpText;
            try
            {
                dumpText = GetSceneNodesDumpString(_dxView.DXScene);
            }
            catch (Exception ex)
            {
                dumpText = "Exception occured when calling DXScene.GetSceneNodesDumpString:\r\n" + ex.Message;
            }

            dumpText += "\r\n\r\nLights:\r\n";

            foreach (var light in _dxView.DXScene.Lights)
            {
                dumpText += "  " + light.ToString();
                var shadowCastingLight = light as IShadowCastingLight;
                if (shadowCastingLight != null && shadowCastingLight.IsCastingShadow)
                    dumpText += " IsCastingShadow: True";

                dumpText += Environment.NewLine;
            }

            ShowInfoText(dumpText);
        }

        private string GetSceneNodesDumpString(DXScene dxScene)
        {
            return dxScene.GetSceneNodesDumpString(showBounds: true, showDirtyFlags: true);
        }

        private void DumpRenderingQueues()
        {
            if (_dxView == null || _dxView.DXScene == null) return;

            string dumpText;

            try
            {
                dumpText = GetRenderingQueuesDumpString(_dxView.DXScene);
            }
            catch (Exception ex)
            {
                dumpText = "Exception occured when calling DXScene.GetRenderingQueuesDumpString:\r\n" + ex.Message;
            }

            ShowInfoText(dumpText);
        }

        private string GetRenderingQueuesDumpString(DXScene dxScene)
        {
            return dxScene.GetRenderingQueuesDumpString(dumpEmptyRenderingQueues: false);
        }

        private void DumpResources()
        {
            var sb = new StringBuilder();

            try
            {
                string dxEngineReport = DXResourceBase.ResourcesTracker.GetTrackedResourcesReport();

                if (!string.IsNullOrEmpty(dxEngineReport))
                {
                    sb.Append("\r\nDXEngine report:\r\n").Append(dxEngineReport).AppendLine();
                }
                else
                {
                    if (!DXDiagnostics.IsResourceTrackingEnabled)
                        sb.AppendLine("DXEngine's resource tracking is disabled");
                    else
                        sb.AppendLine("No live objects reported by DXEngine");
                }
            }
            catch (Exception ex)
            {
                sb.Append("Error getting DXEngine resources: ").AppendLine(ex.Message).AppendLine();
            }

            try
            {
                AppendSharpDXResources(sb);
            }
            catch (Exception ex)
            {
                sb.Append("Error getting SharpDX resources: ").AppendLine(ex.Message).AppendLine();
            }

            // Do not call ReportLiveDeviceObjects because this can throw exception on next frame rendering
            //try
            //{
            //    ReportLiveDeviceObjects(sb);
            //}
            //catch (Exception ex)
            //{
            //    sb.Append("Error getting reporting live DirectX objects: ").AppendLine(ex.Message).AppendLine();
            //}

            ShowInfoText(sb.ToString());
        }

        // Note: The code in this method is moved from DumpResources so that in case when there are any incompatibilities in SharpDX assemblies
        // (can happen in case of DXEngineSnoop that is compiled with different SharpDX assemblies than the running application).
        // With moving code into another method, we can try...catch the call to this method. If the code is in DumpResources, the the call to DumpResources fails
        private void AppendSharpDXResources(StringBuilder sb)
        {
            string sharpDxReport = SharpDX.Diagnostics.ObjectTracker.ReportActiveObjects();
            if (!string.IsNullOrEmpty(sharpDxReport) && sharpDxReport != "\r\nCount per Type:\r\n")
                sb.Append("\r\nSharpDX report:\r\n").Append(sharpDxReport).AppendLine();
        }

        // Note: The code in this method is moved from DumpResources so that in case when there are any incompatibilities in SharpDX assemblies
        // (can happen in case of DXEngineSnoop that is compiled with different SharpDX assemblies than the running application)
        // With moving code into another method, we can try...catch the call to this method. If the code is in DumpResources, the the call to DumpResources fails
        private void ReportLiveDeviceObjects(StringBuilder sb)
        {
            if (DXView != null && DXView.DXScene != null)
            {
                var device = DXView.DXScene.Device;

                if (device != null && DXView.DXScene.DXDevice.IsDebugDevice)
                {
                    try
                    {
                        using (var deviceDebug = new DeviceDebug(device))
                        {
                            deviceDebug.ReportLiveDeviceObjects(ReportingLevel.Detail);

                            sb.AppendLine("DirectX live objects report written to Visual Studio's Output window");
                        }
                    }
                    catch
                    { }
                }
            }
        }

        private void DumpRenderingSteps()
        {
            if (DXView == null || DXView.DXScene == null)
                return;

            var sb = new StringBuilder();
            foreach (var renderingStepBase in DXView.DXScene.RenderingSteps)
                sb.AppendLine(renderingStepBase.ToString());

            ShowInfoText(sb.ToString());
        }
        
        private void BackBufferChanges()
        {
            if (DXView == null || DXView.DXScene == null)
                return;

            DXView.DXScene.RenderingContext.IsCollectingBackBufferChanges = true;

            DXView.Refresh();
            var backBufferChangesReport = DXView.DXScene.RenderingContext.GetBackBufferChangesReport();

            DXView.DXScene.RenderingContext.IsCollectingBackBufferChanges = false;

            ShowInfoText(backBufferChangesReport);
        }

        private void DumpDXEngineSettings()
        {
            var dxEngineSettingsDump = GetDXEngineSettingsDump();
            ShowInfoText(dxEngineSettingsDump);
        }
        
        private void EditDXEngineSettings()
        {
            if (DXView == null || DXView.DXScene == null)
                return;

            if (_settingsEditorWindow != null)
            {
                if (_settingsEditorWindow.CurrentDXScene == DXView.DXScene)
                {
                    _settingsEditorWindow.Focus();
                    return;
                }

                // DXScene changed - close opened window
                try
                {
                    _settingsEditorWindow.Close();
                }
                catch
                {
                    // Maybe the window was already closed
                }
            }

            _settingsEditorWindow = new SettingsEditorWindow(DXView.DXScene);
            _settingsEditorWindow.Owner = this;
            _settingsEditorWindow.Left = this.Left;
            _settingsEditorWindow.Top = this.Top + 80;

            _settingsEditorWindow.ValueChanged += delegate(object sender, EventArgs args)
            {
                DXView.Refresh(); // Render the scene again
            };

            _settingsEditorWindow.Closing += delegate(object sender, CancelEventArgs args)
            {
                _settingsEditorWindow = null;
            };

            _settingsEditorWindow.Show();
        }
        
        private void ShowRenderingFilterWindow()
        {
            if (DXView == null || DXView.DXScene == null)
                return;

            if (_renderingFilterWindow != null)
            {
                if (_renderingFilterWindow.CurrentDXScene == DXView.DXScene)
                {
                    _renderingFilterWindow.Focus();
                    return;
                }

                // DXScene changed - close opened window
                try
                {
                    _renderingFilterWindow.Close();
                }
                catch
                {
                    // Maybe the window was already closed
                }
            }

            _renderingFilterWindow = new RenderingFilterWindow(DXView.DXScene);
            _renderingFilterWindow.Owner = this;
            _renderingFilterWindow.Left = this.Left;
            _renderingFilterWindow.Top = this.Top + 80;

            _renderingFilterWindow.ValueChanged += delegate(object sender, EventArgs args)
            {
                DXView.Refresh(); // Render the scene again
            };

            _renderingFilterWindow.Closing += delegate(object sender, CancelEventArgs args)
            {
                _renderingFilterWindow = null;
            };

            _renderingFilterWindow.Show();
        }

        private string GetDXEngineSettingsDump()
        {
            if (DXView == null || DXView.DXScene == null)
                return "";

            var sb = new StringBuilder();

            DumpObjectProperties(DXView, sb, "  ");
            sb.AppendLine();

            sb.Append("  DXView.GraphicsProfiles: ");
            if (DXView.GraphicsProfiles != null && DXView.GraphicsProfiles.Length > 0)
            {
                sb.Append(string.Join(", ", DXView.GraphicsProfiles.Select(p => p.Name)));
                sb.AppendLine();
            }
            else
            {
                sb.AppendLine("null");
            }

            sb.Append("  DXView.UsedGraphicsProfile:\r\n  ");
            DumpObjectProperties(DXView.UsedGraphicsProfile, sb, "    ");
            sb.AppendLine();


            DumpObjectProperties(DXView.DXScene, sb, "  ");
            sb.AppendLine();


            if (DXView.DXScene.ShadowRenderingProvider != null)
            {
                sb.Append("  DXScene.ShadowRenderingProvider:\r\n  ");
                DumpObjectProperties(DXView.DXScene.ShadowRenderingProvider, sb, "    ");
            }

            if (DXView.DXScene.VirtualRealityProvider != null)
            {
                sb.Append("  DXScene.VirtualRealityProvider:\r\n  ");
                DumpObjectProperties(DXView.DXScene.VirtualRealityProvider, sb, "    ");
            }


            return sb.ToString();
        }

        private void DumpObjectProperties(object objectToDump, StringBuilder sb, string indent)
        {
            if (objectToDump == null)
            {
                sb.AppendLine("null");
                return;
            }

            var type = objectToDump.GetType();

            sb.AppendLine(type.Name + " properties:");

            try
            {
                var allProperties = type.GetProperties().OrderBy(p => p.Name).ToList();

                foreach (var propertyInfo in allProperties)
                {
                    if (propertyInfo.PropertyType.IsValueType || 
                        propertyInfo.PropertyType == typeof(string) ||
                        (propertyInfo.DeclaringType != null && propertyInfo.DeclaringType.Assembly.FullName.StartsWith("Ab3d."))) // Only show referenced objects for types that are declared in this class
                    {
                        string valueText;

                        try
                        {
                            var propertyValue = propertyInfo.GetValue(objectToDump, null);

                            if (propertyValue == null)
                                valueText = "<null>";
                            else
                                valueText = string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0}", propertyValue);
                        }
                        catch (Exception e)
                        {
                            valueText = "ERROR: " + e.Message;
                        }

                        sb.AppendLine(indent + propertyInfo.Name + ": " + valueText);
                    }
                }
            }
            catch (Exception ex)
            {
                sb.Append(indent).Append("Error: ").AppendLine(ex.Message);
            }
        }

        private void DumpSystemInfo()
        {
            string systemInfoText;
            try
            {
                systemInfoText = SystemInfo.GetFullSystemInfo();
            }
            catch (Exception ex)
            {
                systemInfoText = "Error getting system info: \r\n" + ex.Message;
            }

            ShowInfoText(systemInfoText);
        }

        private void ShowInfoText(string infoText)
        {
            System.IO.File.WriteAllText(DumpFileName, infoText);

            StartProcess(DumpFileName);
        }

        private DXViewportView GetDXViewportView()
        {
            return DXView as DXViewportView;
        }

        private static void StartProcess(string fileName)
        {
            // For CORE3 project we need to set UseShellExecute to true,
            // otherwise a "The specified executable is not a valid application for this OS platform" exception is thrown.
            System.Diagnostics.Process.Start(new ProcessStartInfo(fileName) { UseShellExecute = true });
        }

        private void UpdateWpfPreviewWindowContent()
        {
            if (_wpfPreviewWindow == null)
                return;

            string xaml = null;

            Mouse.OverrideCursor = Cursors.Wait;

            try
            {
                Color backgroundColor = Colors.White;

                var dxViewport3D = GetDXViewportView();

                if (dxViewport3D != null)
                {
                    backgroundColor = DXView.BackgroundColor;

                    if (dxViewport3D.Viewport3D != null)
                        xaml = GetViewport3DXaml(dxViewport3D.Viewport3D);
                }

                if (!string.IsNullOrEmpty(xaml))
                {
                    _wpfPreviewWindow.SetBackgroundColor(backgroundColor);
                    _wpfPreviewWindow.SetXaml(xaml);
                }
                else
                {
                    Mouse.OverrideCursor = null;
                    MessageBox.Show("XAML empty");
                }
            }
            finally
            {
                Mouse.OverrideCursor = null;
            }
        }

        private string GetViewport3DXaml(Viewport3D viewport3D)
        {
            var newViewport3D = new Viewport3D();
            newViewport3D.Camera = viewport3D.Camera.Clone();

            foreach (var visual3D in viewport3D.Children)
            {
                // We need to clone Visual3D objects because they are already part of another VisualTree
                // We also simplify all Ab3d.PowerToys visuals - we simply use their Content's Model3D
                var clonedChildVisual = CloneModelVisual3D(visual3D as ModelVisual3D);

                if (clonedChildVisual != null)
                    newViewport3D.Children.Add(clonedChildVisual);
            }

            object savedTag = newViewport3D.Tag; // Do not write Tag (=DXScene) to xaml
            newViewport3D.ClearValue(Viewport3D.TagProperty);

            string xaml = GetXaml(newViewport3D);

            newViewport3D.Tag = savedTag;

            return xaml;
        }

        private ModelVisual3D CloneModelVisual3D(ModelVisual3D modelVisual3D)
        {
            if (modelVisual3D == null)
                return null;

            var newModelVisual3D = new ModelVisual3D();

            if (modelVisual3D.Content != null)
            {
                var model3D = modelVisual3D.Content.Clone();
                newModelVisual3D.Content = model3D;
            }

            if (modelVisual3D.Children.Count > 0)
            {
                foreach (Visual3D childVisual3D in modelVisual3D.Children)
                {
                    var clonedChild = CloneModelVisual3D(childVisual3D as ModelVisual3D);

                    if (clonedChild != null)
                        newModelVisual3D.Children.Add(clonedChild);
                }
            }

            if (modelVisual3D.Transform != null && !modelVisual3D.Transform.Value.IsIdentity)
                newModelVisual3D.Transform = modelVisual3D.Transform;

            return newModelVisual3D;
        }

        private string GetXaml(object wpfObject)
        {
            var sb = new StringBuilder();

            var xmlWriterSettings = new XmlWriterSettings()
            {
                Indent = true
            };

            var xmlWriter = XmlWriter.Create(sb, xmlWriterSettings);

            // On older version of .Net the XamlWriter.Save can lead to stackoverflow when there is a circular reference in the objects.
            // In case of Viewport3D this can happen, because DXEngine sets a DXViewportView.DXViewportViewProperty to the Viewport3D to mark it as being used inside DXEngine.
            // Because teh value of this property is set to DXViewportView this produces a circular reference.
            // So in case we are serializing Viewport3D, we need to clear this value before calling XamlWriter.Save.
            // To prevent that, we could also use System.Windows.Markup.XamlWriter.Save(wpfObject, xmlWriter). But this produces more complex xaml (with all properties) and also has problems with MaterialTypeConverter in older versions of Ab3d.PowerToys.
            var viewport3D = wpfObject as Viewport3D;
            object savedDXViewportViewValue;

            if (viewport3D != null)
            {
                viewport3D.Tag = null; // Older version of DXEngine used Tag property insetad of DXViewportViewProperty
                savedDXViewportViewValue = viewport3D.GetValue(DXViewportView.DXViewportViewProperty);
                viewport3D.ClearValue(DXViewportView.DXViewportViewProperty);
            }
            else
            {
                savedDXViewportViewValue = null;
            }


            // Write XAML
            try
            {
                Mouse.OverrideCursor = Cursors.Wait;

                XamlWriter.Save(wpfObject, xmlWriter);
            }
            catch (Exception ex)
            {
                if (viewport3D != null)
                {
                    sb.AppendLine()
                        .Append("Error saving object to XAML: ").AppendLine(ex.Message)
                        .AppendLine()
                        .AppendLine("Using custom xaml writter:")
                        .AppendLine();

                    sb.AppendLine("<Viewport3D>");

                    var alreadyVisitedObjects = new HashSet<object>();

                    foreach (var viewport3DChild in viewport3D.Children)
                        AppendVisual3DToXaml(viewport3DChild, sb, "    ", xmlWriter, alreadyVisitedObjects);

                    sb.AppendLine("\r\n</Viewport3D>").AppendLine();
                }
            }
            finally
            {
                Mouse.OverrideCursor = null;
            }


            if (viewport3D != null)
                viewport3D.SetValue(DXViewportView.DXViewportViewProperty, savedDXViewportViewValue);


            xmlWriter.Close();

            string xaml = sb.ToString();

            return xaml;
        }

        private void AppendVisual3DToXaml(Visual3D rootVisual3D, StringBuilder sb, string indent, XmlWriter xmlWriter, HashSet<object> alreadyVisitedObjects)
        {
            if (alreadyVisitedObjects.Contains(rootVisual3D)) // Prevent iternal recursion
                return;


            alreadyVisitedObjects.Add(rootVisual3D);


            var rootType = rootVisual3D.GetType();

            bool useSimpleTypeWriter = false;

            if (!rootType.IsPublic)
            {
                useSimpleTypeWriter = true; // XamlWriter.Save cannot serialize non public classes
            }
            else
            {
                try
                {
                    XamlWriter.Save(rootVisual3D, xmlWriter);
                }
                catch
                {
                    useSimpleTypeWriter = true;
                }
            }

            if (useSimpleTypeWriter)
            {
                sb.AppendLine().Append(indent).Append('<').Append(rootType.FullName).AppendLine(">");

                var allProperties = rootType.GetProperties();
                var visual3DProperties = allProperties.Where(p => p.PropertyType.IsSubclassOf(typeof(Visual3D)));
                var model3DProperties  = allProperties.Where(p => p.PropertyType.IsSubclassOf(typeof(Visual3D)));

                foreach (var model3DProperty in model3DProperties)
                {
                    // Model3D classes are sealted so it should be safe to use XamlWriter.Save on them
                    try
                    {
                        XamlWriter.Save(model3DProperty, xmlWriter);
                    }
                    catch
                    { }
                }

                string newIndent1 = indent + "    ";
                string newIndent2 = newIndent1 + "    ";
                foreach (var visual3DProperty in visual3DProperties)
                {
                    try
                    {
                        var visual3D = visual3DProperty.GetValue(rootVisual3D, null) as Visual3D;

                        if (visual3D != null)
                        {
                            sb.Append(newIndent1)
                                .Append('<')
                                .Append(rootType.Name)
                                .Append('.')
                                .Append(visual3DProperty.Name)
                                .AppendLine(">");

                            AppendVisual3DToXaml(visual3D, sb, newIndent2, xmlWriter, alreadyVisitedObjects);

                            sb.Append(newIndent1)
                                .Append("</")
                                .Append(rootType.Name)
                                .Append('.')
                                .Append(visual3DProperty.Name)
                                .AppendLine(">");
                        }
                    }
                    catch
                    {
                    }
                }

                var modelVisual3D = rootVisual3D as ModelVisual3D;
                if (modelVisual3D != null && modelVisual3D.Children.Count > 0)
                {
                    newIndent1 = indent + "    ";

                    sb.Append(newIndent1)
                        .Append('<')
                        .Append(rootType.Name)
                        .AppendLine(".Children>");

                    foreach (var visual3DProperty in modelVisual3D.Children)
                    {
                        try
                        {
                            AppendVisual3DToXaml(visual3DProperty, sb, newIndent1, xmlWriter, alreadyVisitedObjects);
                        }
                        catch
                        {
                        }
                    }

                    sb.Append(newIndent1)
                        .Append("</")
                        .Append(rootType.Name)
                        .AppendLine(".Children>");
                }
            }
        }

        private void RemoveXamlNamespaceDeclarations(ref string xaml)
        {
            // Remove initial <?xml version="1.0" encoding="utf-16"?>
            if (xaml.StartsWith("<?xml"))
            {
                int pos = xaml.IndexOf("<", 5); // get start of first element after <?xml
                if (pos != -1)
                    xaml = xaml.Substring(pos);
            }

            // Remove namespace declarations
            xaml = xaml.Replace(" xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\"", "");
            xaml = xaml.Replace(" xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\"", "");
        }

        private void ShowWpfPreviewWindow()
        {
            if (_wpfPreviewWindow == null)
            {
                _wpfPreviewWindow = new WpfPreviewWindow();
                _wpfPreviewWindow.Closed += delegate (object o, EventArgs args)
                {
                    _wpfPreviewWindow = null; // This will create a new window next time
                };

                _wpfPreviewWindow.UpdateButtonClicked += delegate (object o, EventArgs args)
                {
                    UpdateWpfPreviewWindowContent();
                };

                _wpfPreviewWindow.Show();
            }
            else
            {
                _wpfPreviewWindow.Activate();
            }

            UpdateWpfPreviewWindowContent();
        }

        private WriteableBitmap CreatedRenderedBitmap(BackBufferReadyEventArgs e)
        {
            // We will copy rendered image into a new WriteableBitmap
            var renderedBitmap = new WriteableBitmap(e.Width, e.Height, 96, 96, PixelFormats.Bgra32, null);

            // delegate used by RenderToBitmap method - it is called when the scene is rendered to back buffer and it is available in main CPU memory
            renderedBitmap.Lock();

            var viewportRect = new Int32Rect(0, 0, e.Width, e.Height);

            // Copy bitmap from e.Data.DataPointer to writeableBitmap
            renderedBitmap.WritePixels(viewportRect, e.Data.DataPointer, e.Data.SlicePitch, e.Data.RowPitch);

            renderedBitmap.AddDirtyRect(viewportRect);
            renderedBitmap.Unlock();

            return renderedBitmap;
        }

        private void SaveRenderedBitmap(BitmapSource renderedBitmap, bool openSavedImage = true, string initialFileName = null, string dialogTitle = null)
        {
            if (renderedBitmap == null)
            {
                MessageBox.Show("No rendered image");
                return;
            }

            var saveFileDialog = new Microsoft.Win32.SaveFileDialog()
            {
                AddExtension = true,
                CheckFileExists = false,
                CheckPathExists = true,
                OverwritePrompt = true,
                ValidateNames = false,
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                FileName = initialFileName ?? "DXEngineRender.png",
                DefaultExt = "txt",
                Filter = "png Image (*.png)|*.png",
                Title = dialogTitle ?? "Select file name to store the rendered image"
            };

            if (saveFileDialog.ShowDialog() ?? false)
            {
                // write the bitmap to a file
                using (var imageStream = new FileStream(saveFileDialog.FileName, FileMode.Create))
                {
                    //JpegBitmapEncoder enc = new JpegBitmapEncoder();
                    var enc = new PngBitmapEncoder();
                    var bitmapImage = BitmapFrame.Create(renderedBitmap);
                    enc.Frames.Add(bitmapImage);
                    enc.Save(imageStream);
                }

                try
                {
                    if (openSavedImage)
                        StartProcess(saveFileDialog.FileName);
                }
                catch
                { }
            }
        }

        private void ShowFullSceneDump()
        {
            // Start with empty DumpFile 
            System.IO.File.WriteAllText(DumpFileName, "Ab3d.DXEngine FULL SCENE DUMP\r\n\r\n");

            try
            {

                string systemInfoText;
                try
                {
                    systemInfoText = SystemInfo.GetFullSystemInfo();
                }
                catch (Exception ex)
                {
                    systemInfoText = "Error getting system info: \r\n" + ex.Message;
                }

                AppendDumpText("System info:", systemInfoText);


                var dxEngineSettingsDump = GetDXEngineSettingsDump();
                AppendDumpText("DXView settings:", dxEngineSettingsDump);


                var cameraInfo = new StringBuilder();
                AddCameraDetails(cameraInfo);

                AppendDumpText("DXScene.Camera matrices:", cameraInfo.ToString());

                if (DXView.DXScene != null)
                {
                    string lights = string.Join(Environment.NewLine, DXView.DXScene.Lights.Select(r => r.ToString()));
                    AppendDumpText("DXScene.Lights:", lights);


                    string renderingSteps = string.Join(Environment.NewLine, DXView.DXScene.RenderingSteps.Select(r => r.ToString()));
                    AppendDumpText("DXScene.RenderingSteps:", renderingSteps);


                    string sceneNodesText;
                    try
                    {
                        sceneNodesText = GetSceneNodesDumpString(DXView.DXScene);
                    }
                    catch (Exception ex)
                    {
                        sceneNodesText = "Exception occured when calling DXScene.GetSceneNodesDumpString:\r\n" + ex.Message;
                    }

                    AppendDumpText("Scene nodes:", sceneNodesText);


                    string renderingQueuesText;
                    try
                    {
                        renderingQueuesText = GetRenderingQueuesDumpString(DXView.DXScene);
                    }
                    catch (Exception ex)
                    {
                        renderingQueuesText = "Exception occured when calling DXScene.GetRenderingQueuesDumpString:\r\n" + ex.Message;
                    }

                    AppendDumpText("DXScene.RenderingQueues:", renderingQueuesText);


                    // Add XAML of the scene:
                    var dxViewport3D = GetDXViewportView();
                    if (dxViewport3D != null && dxViewport3D.Viewport3D != null)
                    {
                        string xaml;

                        try
                        {
                            xaml = GetXaml(dxViewport3D.Viewport3D);
                        }
                        catch (Exception ex)
                        {
                            xaml = "Exception saving Viewport3D to XAML:\r\n" + ex.Message;
                        }

                        AppendDumpText("Viewport3D XAML:", xaml);


                        try
                        {
                            xaml = GetViewport3DXaml(dxViewport3D.Viewport3D);
                        }
                        catch (Exception ex)
                        {
                            xaml = "Exception creating cleaned Viewport3D XAML:\r\n" + ex.Message;
                        }

                        AppendDumpText("Cleaned Viewport3D XAML:", xaml);
                    }


                    // Finally we add rendered bitmap as base64 string.
                    // This is done 
                    string renderedBitmapBase64String = null;

                    // To get a copy of next rendered scene, we can use the RegisterBackBufferMapping method that is called when the rendered scene's bitmap is ready to be copied to main memory
                    DXView.DXScene.RenderingContext.RegisterBackBufferMapping(delegate (object s, BackBufferReadyEventArgs args)
                    {
                        try
                        {
                            var renderedBitmap = CreatedRenderedBitmap(args);
                            renderedBitmapBase64String = GetRenderedBitmapBase64String(renderedBitmap);
                        }
                        catch
                        {
                            // Do not crash DXEngine in case of exception
                        }
                    });

                    // After we have subscribed to capture next frame, we can force rendering that frame
                    try
                    {
                        var savedLogLevels = DXDiagnostics.LogLevel;
                        var savedLogAction = DXDiagnostics.LogAction;

                        _logMessagesString = "";

                        DXDiagnostics.LogAction = OnLogAction;
                        DXDiagnostics.LogLevel = DXDiagnostics.LogLevels.Warn;


                        // Render the scene again
                        DXView.Refresh();


                        DXDiagnostics.LogLevel = savedLogLevels;
                        DXDiagnostics.LogAction = savedLogAction;

                        if (DXView.DXScene != null && DXView.DXScene.Statistics != null)
                            AppendDumpText("RenderingStatistics:", GetRenderingStatisticsDetails(DXView.DXScene.Statistics, fpsText: null));


                        if (_logMessagesString.Length > 0)
                            AppendDumpText("Log messages:", _logMessagesString);


                        if (renderedBitmapBase64String != null)
                        {
                            Dispatcher.Invoke(DispatcherPriority.Background, new Action(() =>
                            {
                                // Show as html with embedded image so this can be easily shown in browser
                                AppendDumpText("Rendered bitmap:", "<html><body>\r\n<img src=\"data:image/png;base64,\r\n" +
                                                                   renderedBitmapBase64String +
                                                                   "\" />\r\n</body></html>");
                            }));
                        }
                    }
                    catch
                    {
                    }
                }

            }
            catch (Exception ex)
            {
                AppendDumpText("Error writing scene dump:", ex.Message);
            }

            StartProcess(DumpFileName);
        }

        private void RenderObjectIdBitmap()
        {
            try
            {
                SetupObjectIdRendering();

                // Render bitmap but do not use super-sampling (so divide by SupersamplingFactor)
                var supersamplingFactor = DXView.DXScene.SupersamplingFactor;
                int width               = (int)(DXView.DXScene.Width / supersamplingFactor);
                int height              = (int)(DXView.DXScene.Height / supersamplingFactor);

                var renderToBitmap = DXView.RenderToBitmap(width, height, preferedMultisampling: 0, supersamplingCount: 1);

                SaveRenderedBitmap(renderToBitmap, openSavedImage: true, initialFileName: "ObjectIds.png");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error rendering ObjectId bitmap:\r\n" + ex.Message);
            }
            finally
            {
                DisposeObjectIdRendering();
            }
        }
        
        private void DisposeObjectIdRendering()
        {
            if (_objectIdRenderingStep != null)
            {
                DXView.DXScene.RenderingSteps.Remove(_objectIdRenderingStep);

                _objectIdRenderingStep.Dispose();
                _objectIdRenderingStep = null;
            }

            if (_solidColorEffect != null)
            {
                _solidColorEffect.Dispose();
                _solidColorEffect = null;
            }

            if (_alteredRenderingQueues != null)
            {
                foreach (var materialSortedRenderingQueue in _alteredRenderingQueues)
                    materialSortedRenderingQueue.IsRenderingEnabled = !materialSortedRenderingQueue.IsRenderingEnabled;

                _alteredRenderingQueues = null;
            }

            DXView.DXScene.DefaultRenderObjectsRenderingStep.IsEnabled = true;
        }
        
        private void SetupObjectIdRendering()
        {
            if (DXView.DXScene == null)
                return; // WPF 3d rendering


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

            _alteredRenderingQueues = new List<MaterialSortedRenderingQueue>();

            foreach (var materialSortedRenderingQueue in DXView.DXScene.RenderingQueues.OfType<MaterialSortedRenderingQueue>())
            {
                if (materialSortedRenderingQueue.IsSortingEnabled)
                {
                    materialSortedRenderingQueue.IsSortingEnabled = false;
                    _alteredRenderingQueues.Add(materialSortedRenderingQueue);
                }
            }
            


            // Create a SolidColorEffect that will be used to render each objects with a color from object's id
            _solidColorEffect = new SolidColorEffect
            {
                // We will overwrite the object's color with color specified in SolidColorEffect.Color
                OverrideModelColor = true,

                // Always use Opaque blend state even if alpha is less then 1 (usually PremultipliedAlphaBlend is used in this case).
                // This will allow us to also use alpha component for the object id (in our case RenderingQueue id)
                OverrideBlendState = DXView.DXScene.DXDevice.CommonStates.Opaque,

                // By default for alpha values less then 1, the color components are multiplied with alpha value to produce pre-multiplied colors.
                // This will allow us to also use alpha component for the object id (in our case RenderingQueue id)
                PremultiplyAlphaColors = false
            };

            DXView.DXScene.DXDevice.EffectsManager.RegisterEffect(_solidColorEffect);


            // Create a custom rendering step that will be used instead of standard rendering step
            _objectIdRenderingStep = new CustomActionRenderingStep("ObjectIdRenderingStep")
            {
                CustomAction = ObjectIdRenderingAction,
            };

            DXView.DXScene.RenderingSteps.AddAfter(DXView.DXScene.DefaultRenderObjectsRenderingStep, _objectIdRenderingStep);

            DXView.DXScene.DefaultRenderObjectsRenderingStep.IsEnabled = false;
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

#if NET45 || NETCOREAPP
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        private static Color4 GetObjectIdColor4(int renderingQueueIndex, int objectIndex)
        {
            // Encode renderingQueueIndex and objectIndex into 4 colors components (rendering is done in 32 bits, so each color have 8 bits; but the Color4 requires float values for color (this is also what the shader gets as parameter)
            // renderingQueueIndex is written to the 4 low bits of the alpha color component
            // objectIndex is written to red, green and blue (max written index is 16.777.215)
            //
            // This way it is possible to write ids for 16.777.215 objects in each rendering queue (so 16M solid + 16M lines + 16M transparent objects).
            // If you need more ids, then you can move the renderingQueueIndex into 4 high bits of alpha and use lower 4 bits for extra index increasing the ids count by 16.

            float red   = (float)((objectIndex >> 16) & 0xFF) / 255f;
            float green = (float)((objectIndex >> 8) & 0xFF) / 255f;
            float blue  = (float)(objectIndex & 0xFF) / 255f;

            // Write renderingQueueIndex
            // We increase the index by 1 so that value 0 represents an invalid index that is used for areas where there is no 3D object
            // We preserve the high 4 bits of alpha value so that the colors are visible and write renderingQueueIndex into the low 4 bits (0...15 possible values)
            float alpha = (float)(0xF0 + ((renderingQueueIndex + 1) & 0x0F)) / 255f;

            return new Color4(red, green, blue, alpha);
        }

#if NET45 || NETCOREAPP
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        public static void GetObjectId(Color4 idColor, out int renderingQueueIndex, out int objectIndex)
        {
            byte red   = (byte)(idColor.Red   * 255);
            byte green = (byte)(idColor.Green * 255);
            byte blue  = (byte)(idColor.Blue  * 255);
            byte alpha = (byte)(idColor.Alpha * 255);

            renderingQueueIndex = (alpha & 0x0F) - 1;
            objectIndex         = (red << 16) + (green << 8) + blue;
        }


        private void OnLogAction(DXDiagnostics.LogLevels logLevel, string logMessage)
        {
            _logMessagesString += logLevel.ToString() + ": " + logMessage + Environment.NewLine;
        }

        private void AppendDumpText(string title, string content)
        {
            System.IO.File.AppendAllText(DumpFileName, title + "\r\n\r\n" + content + "\r\n##########################\r\n\r\n");
        }

        private string GetRenderedBitmapBase64String(WriteableBitmap renderedBitmap)
        {
            if (renderedBitmap == null)
                return "RenderedBitmap is null";

            byte[] bitmapBytes;

            // write bitmap to a MemoryStream
            using (var imageStream = new MemoryStream())
            {
                //JpegBitmapEncoder enc = new JpegBitmapEncoder();
                PngBitmapEncoder enc = new PngBitmapEncoder();
                BitmapFrame bitmapImage = BitmapFrame.Create(renderedBitmap);
                enc.Frames.Add(bitmapImage);
                enc.Save(imageStream);

                imageStream.Seek(0, SeekOrigin.Begin);

                bitmapBytes = new byte[imageStream.Length];
                imageStream.Read(bitmapBytes, 0, bitmapBytes.Length);
            }

            string bitmapString;

            if (bitmapBytes != null)
            {
                bitmapString = Convert.ToBase64String(bitmapBytes);

                // Format base64 string with adding new line chars after each 128 chars
                int stringLength = bitmapString.Length;
                if (stringLength > 500)
                {
                    int segmentLength = 128;
                    var sb = new StringBuilder((int)(stringLength + ((stringLength * 2) / segmentLength)));
                    for (int i = 0; i < bitmapString.Length; i += segmentLength)
                    {
                        if (i + segmentLength > stringLength)
                            sb.AppendLine(bitmapString.Substring(i));
                        else
                            sb.AppendLine(bitmapString.Substring(i, segmentLength));
                    }

                    bitmapString = sb.ToString();
                }
            }
            else
            {
                bitmapString = null;
            }

            return bitmapString;
        }

        // This is not used any more because it is replaced by GetDXEngineSettingsDump
        //private string GetDXViewDetails()
        //{
        //    string dxViewSettings = null;

        //    try
        //    {
        //        dxViewSettings = string.Format(CultureInfo.InvariantCulture,
        //            "{0} ({1} x {2})\r\n    DPI Scale: {3}, {4}\r\n    PresentationType: {5}\r\n    IsAutomaticallyUpdatingDXScene: {6}\r\n    UsedGraphicsProfile: {7}\r\n",
        //            DXView.GetType().Name, DXView.ActualWidth, DXView.ActualHeight,
        //            DXView.DpiScaleX, DXView.DpiScaleY,
        //            DXView.PresentationType,
        //            DXView.IsAutomaticallyUpdatingDXScene,
        //            DXView.UsedGraphicsProfile == null ? "<null>" : DXView.UsedGraphicsProfile.Name);

        //        if (DXView.GraphicsProfiles == null)
        //        {
        //            dxViewSettings += "    DXView.GraphicsProfiles == null\r\n";
        //        }
        //        else
        //        {
        //            dxViewSettings += "    DXView.GraphicsProfiles: { " + string.Join(", ", DXView.GraphicsProfiles.Select(p => p.Name)) + " }\r\n";
        //        }

        //        var dxScene = DXView.DXScene;
        //        if (dxScene == null)
        //        {
        //            dxViewSettings += "    DXView.DXScene == null\r\n";
        //        }
        //        else
        //        {
        //            dxViewSettings += string.Format(CultureInfo.InvariantCulture,
        //                "    MultisamplingCount: {0} ({1})\r\n    ShaderQuality: {2}\r\n    HardwareAccelerate3DLines: {3}\r\n    UseGeometryShaderFor3DLines: {4}\r\n    IsAutomaticallyUpdatingBeforeEachRender: {5}\r\n    OptimizeNearAndFarCameraPlanes: {6}\r\n    IsMaterialSortingEnabled: {7}\r\n    UseSharedWpfTexture: {8}\r\n",
        //                dxScene.UsedMultisamplingDescription,
        //                dxScene.MultisampleCount,
        //                dxScene.ShaderQuality,
        //                dxScene.HardwareAccelerate3DLines,
        //                dxScene.UseGeometryShaderFor3DLines,
        //                dxScene.IsAutomaticallyUpdatingBeforeEachRender,
        //                dxScene.OptimizeNearAndFarCameraPlanes,
        //                dxScene.IsMaterialSortingEnabled,
        //                dxScene.UseSharedWpfTexture);

        //            if (dxScene.ShadowRenderingProvider != null)
        //                dxViewSettings += string.Format("    ShadowRenderingProvider: {0}\r\n", dxScene.ShadowRenderingProvider.GetType().Name);

        //            if (dxScene.VirtualRealityProvider != null)
        //                dxViewSettings += string.Format("    VirtualRealityProvider: {0}\r\n", dxScene.VirtualRealityProvider.GetType().Name);
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        if (dxViewSettings == null)
        //            dxViewSettings = "";

        //        dxViewSettings += "Error writing DXView settings: " + ex.Message;
        //    }

        //    return dxViewSettings;
        //}

        private Camera GetWpfCamera()
        {
            var dxViewport3D = GetDXViewportView();
            if (dxViewport3D == null || dxViewport3D.Viewport3D == null)
                return null;

            return dxViewport3D.Viewport3D.Camera;
        }

        private object GetPowerToysCamera()
        {
            var dxViewport3D = GetDXViewportView();
            if (dxViewport3D == null || dxViewport3D.Viewport3D == null)
                return null;

            Type baseCameraType = Type.GetType("Ab3d.Cameras.BaseCamera, Ab3d.PowerToys", throwOnError: false);
            if (baseCameraType == null)
                return null; // Ab3d.PowerToys library is not loaded because BaseCamera class does not exist


            var viewport3D = dxViewport3D.Viewport3D;
            var targetViewport3DPropertyInfo = baseCameraType.GetProperty("TargetViewport3D");

            if (targetViewport3DPropertyInfo == null)
                return null;


            var rootElement = GetRootControl(dxViewport3D);

            if (rootElement == null)
                return null;

            var foundElement = FindElement(rootElement, baseCameraType, delegate (object foundCamera)
            {
                // Check that the found camera is using our Viewport3D
                try
                {
                    var targetViewport3D = targetViewport3DPropertyInfo.GetValue(foundCamera, null);
                    return ReferenceEquals(targetViewport3D, viewport3D);
                }
                catch
                { }

                return true; // If we cannot check the TargetViewport3D property, then just get the first camera
            });

            return foundElement;
        }

        private static FrameworkElement GetRootControl(FrameworkElement element)
        {
            if (element == null)
                return null;


            var currentElement = element.Parent as FrameworkElement;
            if (currentElement == null) // start element has no parent
                return null;

            for (; ; )
            {
                var previousElement = currentElement.Parent as FrameworkElement;

                if (previousElement == null || currentElement is UserControl || currentElement is Window || currentElement is Page)
                    break;

                currentElement = previousElement;
            }

            return currentElement;
        }

        private static object FindElement(object startElement, Type typeToFind, Predicate<object> isCorrectCameraPredicate)
        {
            object foundElement = null;

            if (typeToFind.IsInstanceOfType(startElement))
            {
                foundElement = startElement;

                if (isCorrectCameraPredicate != null)
                {
                    if (!isCorrectCameraPredicate(foundElement))
                        foundElement = null;
                }
            }
            else if (startElement is ContentControl)
            {
                // Check the element's Content
                foundElement = FindElement(((ContentControl)startElement).Content, typeToFind, isCorrectCameraPredicate);
            }
            else if (startElement is Decorator) // for example Border
            {
                // Check the element's Content
                foundElement = FindElement(((Decorator)startElement).Child, typeToFind, isCorrectCameraPredicate);
            }
            else if (startElement is Page)
            {
                // Page is not ContentControl so handle it specially (note: Window is ContentControl)
                foundElement = FindElement(((Page)startElement).Content, typeToFind, isCorrectCameraPredicate);
            }
            else if (startElement is Panel)
            {
                Panel panel = startElement as Panel;

                // Check each child of a Panel
                foreach (UIElement oneChild in panel.Children)
                {
                    foundElement = FindElement(oneChild, typeToFind, isCorrectCameraPredicate);

                    if (foundElement != null)
                        break;
                }
            }

            return foundElement;
        }



        private void ShowCameraDetails()
        {
            var sb = new StringBuilder();

            try
            {
                AddCameraDetails(sb);
            }
            catch (Exception ex)
            {
                sb.AppendLine("Error getting camera details:")
                  .AppendLine(ex.Message);

                if (ex.InnerException != null)
                    sb.AppendLine(ex.InnerException.Message);
            }

            ShowInfoText(sb.ToString());
        }

        private void AddCameraInfo(StringBuilder sb)
        {
            var viewport3DCamera = GetWpfCamera();

            if (viewport3DCamera == null)
                return;


            string singleDigitFormatString = "{0}: {1:0.#}\r\n";

            var powerToysCamera = GetPowerToysCamera();

            if (powerToysCamera != null)
            {
                AppendCameraPropertyValue(powerToysCamera, "Heading", sb, singleDigitFormatString);
                AppendCameraPropertyValue(powerToysCamera, "Attitude", sb, singleDigitFormatString);

                string bankText = AppendCameraPropertyValue(powerToysCamera, "Bank", null, singleDigitFormatString);
                if (bankText != null && bankText != "0")
                    AppendCameraPropertyValue(powerToysCamera, "Bank", sb, singleDigitFormatString);

                string cameraType = AppendCameraPropertyValue(powerToysCamera, "CameraType", null, singleDigitFormatString);
                if (cameraType == "OrthographicCamera")
                {
                    sb.Append("\r\nCameraType: OrthographicCamera\r\n");
                    AppendCameraPropertyValue(powerToysCamera, "CameraWidth", sb, singleDigitFormatString);
                }
                else
                {
                    AppendCameraPropertyValue(powerToysCamera, "Distance", sb, singleDigitFormatString);
                    AppendCameraPropertyValue(powerToysCamera, "FieldOfView", sb, singleDigitFormatString);
                }

                sb.AppendLine();

                AppendCameraPropertyValue(powerToysCamera, "TargetPosition", sb, singleDigitFormatString);
                AppendCameraPropertyValue(powerToysCamera, "RotationCenterPosition", sb, singleDigitFormatString);


                string offsetText = AppendCameraPropertyValue(powerToysCamera, "Offset", null, singleDigitFormatString);
                if (offsetText != "0, 0, 0")
                    AppendCameraPropertyValue(powerToysCamera, "Offset", sb, singleDigitFormatString);
            }

            if (viewport3DCamera != null)
            {
                sb.AppendLine();

                AppendCameraPropertyValue(viewport3DCamera, "Position",      sb, "CameraPosition: {1:0.#}\r\n");
                AppendCameraPropertyValue(viewport3DCamera, "LookDirection", sb, "LookDirection:  {1:0.##}\r\n");
                //AppendCameraPropertyValue(viewport3DCamera, "UpDirection", sb, directionFormatString);
            }

            var dxViewport3D = GetDXViewportView();
            if (dxViewport3D != null && 
                dxViewport3D.DXScene != null && 
                dxViewport3D.DXScene.Camera != null &&
                dxViewport3D.DXScene.OptimizeNearAndFarCameraPlanes)
            {
                sb.AppendFormat(System.Globalization.CultureInfo.InvariantCulture,
                                "\r\nNearPlaneDistance: {0}\r\n" +
                                "FarPlaneDistance:  {1}\n\n\n", 
                                dxViewport3D.DXScene.Camera.NearPlaneDistance, 
                                dxViewport3D.DXScene.Camera.FarPlaneDistance);

                sb.Append(GetDXSceneCameraMatricesString(dxViewport3D.DXScene.Camera));
            }           
        }

        private void AddCameraDetails(StringBuilder sb)
        {
            var viewport3DCamera = GetWpfCamera();

            if (viewport3DCamera == null)
                return;

            if (viewport3DCamera != null)
            {
                string xaml = GetXaml(viewport3DCamera);
                RemoveXamlNamespaceDeclarations(ref xaml);

                sb.AppendLine("Viewport3D.Camera:");
                sb.AppendLine(xaml);
                sb.AppendLine();
            }

            //var ab3dCamera = FindControlsHelper.FindFirstElement<Ab3d.Cameras.BaseCamera>(dxViewport3D.Viewport3D);

            var powerToysCamera = GetPowerToysCamera();

            if (powerToysCamera != null)
            {
                sb.AppendFormat("<{0}\r\n", powerToysCamera.GetType().Name);

                string formatString = "    {0}=\"{1}\"\r\n";

                AppendCameraPropertyValue(powerToysCamera, "Name", sb, formatString);
                AppendCameraPropertyValue(powerToysCamera, "Heading", sb, formatString);
                AppendCameraPropertyValue(powerToysCamera, "Attitude", sb, formatString);
                AppendCameraPropertyValue(powerToysCamera, "Bank", sb, formatString);
                AppendCameraPropertyValue(powerToysCamera, "Distance", sb, formatString);
                AppendCameraPropertyValue(powerToysCamera, "TargetPosition", sb, formatString);
                AppendCameraPropertyValue(powerToysCamera, "RotationCenterPosition", sb, formatString);
                AppendCameraPropertyValue(powerToysCamera, "Position", sb, formatString);
                AppendCameraPropertyValue(powerToysCamera, "CameraPosition", sb, formatString);
                AppendCameraPropertyValue(powerToysCamera, "RotationUpAxis", sb, formatString);
                AppendCameraPropertyValue(powerToysCamera, "Offset", sb, formatString);
                AppendCameraPropertyValue(powerToysCamera, "FieldOfView", sb, formatString);
                AppendCameraPropertyValue(powerToysCamera, "IsDistancePercent", sb, formatString);
                AppendCameraPropertyValue(powerToysCamera, "ShowCameraLights", sb, formatString);

                var cameraTypeString = AppendCameraPropertyValue(powerToysCamera, "CameraType", sb, formatString);
                if (cameraTypeString == "OrthographicCamera")
                    AppendCameraPropertyValue(powerToysCamera, "CameraWidth", sb, formatString);

                sb.AppendLine("/>");
            }

            var dxViewport3D = GetDXViewportView();
            if (dxViewport3D != null && dxViewport3D.DXScene != null && dxViewport3D.DXScene.Camera != null)
            {
                sb.AppendFormat(System.Globalization.CultureInfo.InvariantCulture,
                                "\r\n\r\nNearPlaneDistance: {0}\r\n" +
                                "FarPlaneDistance: {1}\r\n" +
                                "OptimizeNearAndFarCameraPlanes: {2}\r\n\r\n", 
                                dxViewport3D.DXScene.Camera.NearPlaneDistance, 
                                dxViewport3D.DXScene.Camera.FarPlaneDistance,
                                dxViewport3D.DXScene.OptimizeNearAndFarCameraPlanes);

                sb.AppendLine(GetDXSceneCameraMatricesString(dxViewport3D.DXScene.Camera));
            }           
        }


        private void SetDXEngineResourceTracking(bool newValue)
        {
            Ab3d.DirectX.DXDiagnostics.IsResourceTrackingEnabled = newValue;
        }

        private void SetSharpDXObjectTracking(bool newValue)
        {
            // In SharpDX v3.0+ the call stack is get with throwing an exception and then getting the CallStack from it
            // The reason for this is that this is the only way to get call stack in PCL.
            // This kill performance totally (displaying exceptions in VS output) and can also break you into VS when break on exceptions is enabled.
            // Usually we do not need to store stack trace so we just override the StackTraceProvider to always return empty string.
            SharpDX.Diagnostics.ObjectTracker.StackTraceProvider = new Func<string>(() => "");

            SharpDX.Configuration.EnableObjectTracking = newValue;
        }

        // formatString = "    {0}=\"{1:0.##}\"\r\n"
        // Returns propertyValue as string (without any special formatting)
        private string AppendCameraPropertyValue(object cameraObject, string propertyName, StringBuilder sb, string formatString)
        {
            var propertyInfo = cameraObject.GetType().GetProperty(propertyName);

            if (propertyInfo == null)
                return null; // Not defined for this camera type

            var propertyValue = propertyInfo.GetValue(cameraObject, null);
            
            if (propertyValue != null)
            {
                var propertyValueString = string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0}", propertyValue);

                propertyValueString = propertyValueString.Replace(",", ", "); // Add space after comma

                if (sb != null)
                {
                    string finalString = string.Format(System.Globalization.CultureInfo.InvariantCulture, formatString, propertyName, propertyValue);
                    finalString = finalString.Replace(",", ", "); // Add space after comma

                    sb.AppendFormat(finalString);
                }

                return propertyValueString;
            }

            return null;
        }

        private string GetDXSceneCameraMatricesString(ICamera dxCamera)
        {
            var sb = new StringBuilder();

            sb.AppendFormat("View matrix:\r\n{0}\r\n\r\n",
                GetMatrix3DText(dxCamera.View));

            sb.AppendFormat("Projection matrix:\r\n{0}\r\n\r\n",
                GetMatrix3DText(dxCamera.Projection));

            sb.AppendFormat("ViewProjection matrix:\r\n{0}\r\n",
                GetMatrix3DText(dxCamera.GetViewProjection()));

            return sb.ToString();
        }

#region GetMatrix3DText, FormatTable
        // This code is taken from Ab3d.PowerToys Ab3d.Utilities.Dumper.GetMatrix3DText

        private static string GetMatrix3DText(SharpDX.Matrix matrix)
        {
            return FormatTable(new[] {
                new string[] { matrix.M11.ToString("F2", System.Globalization.CultureInfo.InvariantCulture),
                               matrix.M12.ToString("F2", System.Globalization.CultureInfo.InvariantCulture),
                               matrix.M13.ToString("F2", System.Globalization.CultureInfo.InvariantCulture),
                               matrix.M14.ToString("F2", System.Globalization.CultureInfo.InvariantCulture) },

                new string[] { matrix.M21.ToString("F2", System.Globalization.CultureInfo.InvariantCulture),
                               matrix.M22.ToString("F2", System.Globalization.CultureInfo.InvariantCulture),
                               matrix.M23.ToString("F2", System.Globalization.CultureInfo.InvariantCulture),
                               matrix.M24.ToString("F2", System.Globalization.CultureInfo.InvariantCulture) },

                new string[] { matrix.M31.ToString("F2", System.Globalization.CultureInfo.InvariantCulture),
                               matrix.M32.ToString("F2", System.Globalization.CultureInfo.InvariantCulture),
                               matrix.M33.ToString("F2", System.Globalization.CultureInfo.InvariantCulture),
                               matrix.M34.ToString("F2", System.Globalization.CultureInfo.InvariantCulture) },

                new string[] { matrix.M41.ToString("F2", System.Globalization.CultureInfo.InvariantCulture),
                               matrix.M42.ToString("F2", System.Globalization.CultureInfo.InvariantCulture),
                               matrix.M43.ToString("F2", System.Globalization.CultureInfo.InvariantCulture),
                               matrix.M44.ToString("F2", System.Globalization.CultureInfo.InvariantCulture) }
            },
            maxColumnLength: 0,
            columnSeparator: "  ",
            headerRowSeparator: '\0',  // No header row
            indentString: "  ",
            newLineString: Environment.NewLine,
            rightAlign: true);
        }

        // Formats list of string arrays or array of string arrays (this is used in CameraRotationTrack.GetDumpString)
        // This methods formats the columns so that they have the same length
        // if maxColumnLength == 0 then do not limit column length; 
        // if headerRowSeparator is not '\0' then we add a line with that separators after the first row
        internal static string FormatTable(IList<string[]> stringTable, int maxColumnLength, string columnSeparator = "  ", char headerRowSeparator = '\0', string indentString = "", string newLineString = "\r\n", bool rightAlign = false)
        {
            var columnMaxLength = new List<int>(); // This will hold max string length for each column

            // Pass1: First measure max string length for each column
            int rowsCount = stringTable.Count;
            for (int rowIndex = 0; rowIndex < rowsCount; rowIndex++)
            {
                var oneRow = stringTable[rowIndex];

                var oneRowLength = oneRow.Length;
                if (oneRowLength > columnMaxLength.Count)
                {
                    // Resize the columnMaxLength
                    while (columnMaxLength.Count < oneRowLength)
                        columnMaxLength.Add(0);
                }

                for (int columnIndex = 0; columnIndex < oneRowLength; columnIndex++)
                {
                    string oneString = oneRow[columnIndex];
                    int oneStringLength = oneString == null ? 0 : oneString.Length;

                    if (oneStringLength > columnMaxLength[columnIndex])
                        columnMaxLength[columnIndex] = oneStringLength;
                }
            }

            // Clip max length to maxColumnLength (must be bigger then 3 so we have space for ...)
            if (maxColumnLength > 3)
            {
                for (var i = 0; i < columnMaxLength.Count; i++)
                {
                    if (columnMaxLength[i] > maxColumnLength)
                        columnMaxLength[i] = maxColumnLength;
                }
            }

            // Pass2: create string
            var sb = new StringBuilder();

            for (int rowIndex = 0; rowIndex < rowsCount; rowIndex++)
            {
                var oneRow = stringTable[rowIndex];

                var oneRowLength = oneRow.Length;


                if (!string.IsNullOrEmpty(indentString))
                    sb.Append(indentString);


                for (int columnIndex = 0; columnIndex < oneRowLength; columnIndex++)
                {
                    string oneString = oneRow[columnIndex];

                    if (oneString == null)
                        oneString = "";

                    int oneStringLength = oneString.Length;
                    int oneColumnLength = columnMaxLength[columnIndex];

                    if (oneStringLength < oneColumnLength)
                    {
                        if (rightAlign)
                            sb.Append(' ', oneColumnLength - oneStringLength).Append(oneString);
                        else
                            sb.Append(oneString).Append(' ', oneColumnLength - oneStringLength);
                    }
                    else if (oneStringLength == oneColumnLength)
                    {
                        sb.Append(oneString); // No added spaces
                    }
                    else // Clip string
                    {
                        sb.Append(oneString.Substring(0, oneColumnLength - 3)).Append("...");
                    }

                    sb.Append(columnSeparator);
                }

                sb.Append(newLineString);

                if (rowIndex == 0 && headerRowSeparator != '\0')
                {
                    for (int columnIndex = 0; columnIndex < oneRowLength; columnIndex++)
                    {
                        int oneColumnLength = columnMaxLength[columnIndex];
                        sb.Append(headerRowSeparator, oneColumnLength);
                        sb.Append(columnSeparator);
                    }

                    sb.AppendLine();
                }
            }

            return sb.ToString();
        }
#endregion

        private void StatisticsTypeRadioButton_OnChecked(object sender, RoutedEventArgs e)
        {
            if (!this.IsLoaded)
                return;

            // Store value in local field so we do not need to check the value ShowRenderingStatisticsRadioButton.IsChecked on each update (this is quite slow)
            _showRenderingStatistics = ShowRenderingStatisticsRadioButton.IsChecked ?? false;

            if (_showRenderingStatistics)
            {
                ResultsTitleTextBlock.Text = "Rendering statistics:";
                StatisticsTextBlock.ClearValue(FontFamilyProperty);
                StatisticsTextBlock.ClearValue(FontSizeProperty);
            }
            else
            {
                ResultsTitleTextBlock.Text = "Camera info:";
                StatisticsTextBlock.FontFamily = new FontFamily("Courier New");
                StatisticsTextBlock.FontSize = 11;
            }

            if (Ab3d.DirectX.DXDiagnostics.IsCollectingStatistics && DXView != null && DXView.DXScene != null && DXView.DXScene.Statistics != null)
                UpdateStatistics(DXView.DXScene.Statistics);

            // Close the menu
            ActionsRootMenuItem.IsSubmenuOpen = false;
        }
    }
}
