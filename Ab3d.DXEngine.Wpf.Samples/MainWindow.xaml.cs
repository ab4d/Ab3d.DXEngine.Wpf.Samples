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
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;
using Ab3d.DirectX;
using Ab3d.DirectX.Client.Diagnostics;
using Ab3d.DirectX.Client.Settings;
using Ab3d.DirectX.Controls;

namespace Ab3d.DXEngine.Wpf.Samples
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        // Uncomment the _startupPage declaration to always start the samples with the specified page
        //private string _startupPage = "DXEngineVisuals/TwoDimensionalCameraLineEditor.xaml";
        private string _startupPage = null;

        private DXViewportView _lastShownDXViewportView;

        private string _rejectedGraphicProfilesReasons;

        private DiagnosticsWindow _diagnosticsWindow;

        private double _selectedDpiScale = double.NaN;

        public int MaxBackgroundThreadsCount { get; set; }

        private BitmapImage _diagnosticsDisabledImage;
        private BitmapImage _diagnosticsEnabledImage;

        public MainWindow()
        {
            // When using the DXEngine from NuGet and when you have purchase a commercial version,
            // then uncomment the following line to activate the license uncomment (see your User Account web page for more info):
            //Ab3d.Licensing.DXEngine.LicenseHelper.SetLicense(licenseOwner: "[CompanyName]", 
            //                                                 licenseType: "[LicenseType]", 
            //                                                 license: "[LicenseText]");
            

            // The following is a sample global exception handler that can be used 
            // to get system info (with details about graphics card and drivers)
            // in case of exception in DXEngine.
            // You can use similar code to improve your error reporting data.
            AppDomain.CurrentDomain.UnhandledException += delegate(object sender, UnhandledExceptionEventArgs e)
            {
                if (e.ExceptionObject is DXEngineException || 
                    e.ExceptionObject is SharpDX.SharpDXException) // SharpDXException is also related with using DirectX
                {
                    string fullSystemInfo;

                    try
                    {
                        // Note that using SystemInfo requires a reference to System.Management
                        fullSystemInfo = Ab3d.DirectX.Client.Diagnostics.SystemInfo.GetFullSystemInfo();
                    }
                    catch
                    {
                        fullSystemInfo = null;
                    }

                    // Here we just show a MessageBox with some exception info.
                    // In a real application it is recommended to report or store full exception and system info (fullSystemInfo)
                    MessageBox.Show(string.Format("Unhandled {0} occured while running the sample:\r\n{1}\r\n\r\nIf this is not expected, please report that to support@ab4d.com.", 
                        e.ExceptionObject.GetType().Name,
                        ((Exception) e.ExceptionObject).Message),
                        "Ab3d.DXEngine exception", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            };


            // Initialize the DXEngineSettings helper class
            // First create DXEngineSettingsStorage that is used to read and save user settings and can read overridden settings from application config file
            // After the DXEngineSettings is initialized we can use the DXEngineSettings.Current property
            var dxEngineSettingsStorage = new DXEngineSettingsStorage();
            DXEngineSettings.Initialize(dxEngineSettingsStorage);

            // Set default value for MaxBackgroundThreadsCount (the value is set in the same way as in DXScene).
            // When we have only 1 core, then MaxBackgroundThreadsCount is set to 0 - no multi-threading
            // When we have 8 processors or less, then use all the processors (one for main thread and (ProcessorCount - 1) for background threads
            // When we have more the 8 processors, use 7 background threads. When using more threads the gains are very small.
            this.MaxBackgroundThreadsCount = Math.Min(7, Environment.ProcessorCount - 1);

            InitializeComponent();

            DisableDiagnosticsButton();



            this.Loaded += OnLoaded;
            this.Unloaded += delegate(object sender, RoutedEventArgs args)
            {
                CloseDiagnosticsWindow(); // If DiagnosticsWindow is not closed, then close it with closing main samples window

                if (DXEngineSettings.Current.SystemCapabilities != null)
                    DXEngineSettings.Current.SystemCapabilities.Dispose();
            };

            ContentFrame.LoadCompleted += delegate (object o, NavigationEventArgs args)
            {
                // When content of ContentFrame is changed, we try to find the DXViewportView control
                // that is defined by the newly shown content.

                // First unsubscribe all previously subscribed events
                UnsubscribeLastShownDXViewportView();

                // Find DXViewportView
                var foundDXViewportView = FindDXViewportView(ContentFrame.Content);
                SubscribeDXViewportView(foundDXViewportView);
            };


            // SelectionChanged event handler is used to start the samples with the page set with _startupPage field.
            // SelectionChanged is used because SelectItem cannot be set from this.Loaded event.
            SampleList.SelectionChanged += delegate (object sender, SelectionChangedEventArgs args)
            {
                if (_startupPage != null)
                {
                    string savedStartupPage = _startupPage;
                    _startupPage = null;

                    SelectItem(savedStartupPage);
                }
            };
        }

        private void OnLoaded(object sender, RoutedEventArgs routedEventArgs)
        {
            bool isSupportedOS = DXEngineSettings.Current.CheckIfSupportedOperatingSystem();

            if (isSupportedOS)
            {
                DXEngineSettings.Current.InitializeGraphicProfiles();

                if (DXEngineSettings.Current.GraphicsProfiles != null && DXEngineSettings.Current.GraphicsProfiles.Length > 0)
                    ShowGraphicsProfile(DXEngineSettings.Current.GraphicsProfiles[0], dxViewportView: null);
            }
            else
            {
                // If we are running on unsupported system we can still use WPF 3D for rendering
                DXEngineSettings.Current.GraphicsProfiles = new GraphicsProfile[] { GraphicsProfile.Wpf3D };
            }
        }

        private void GraphicsSettingsButton_OnClick(object sender, RoutedEventArgs e)
        {
            // Show DXEngineSettingsWindow where user can change graphics adapter and quality settings
            // DXEngineSettingsWindow is defined in Ab3d.DirectX.Client.Settings project that is available with full source code
            var dxEngineSettingsWindow = new DXEngineSettingsWindow();
            dxEngineSettingsWindow.Owner = this;

            if (DXEngineSettings.Current.GraphicsProfiles != null && DXEngineSettings.Current.GraphicsProfiles.Length > 0)
                dxEngineSettingsWindow.SelectedGraphicsProfile = DXEngineSettings.Current.GraphicsProfiles[0];
            else
                dxEngineSettingsWindow.SelectedGraphicsProfile = null;

            dxEngineSettingsWindow.SelectedDpiScale = _selectedDpiScale;

            dxEngineSettingsWindow.SelectedMaxBackgroundThreadsCount = MaxBackgroundThreadsCount;

            dxEngineSettingsWindow.ShowDialog();


            GraphicsProfile selectedGraphicsProfile = dxEngineSettingsWindow.SelectedGraphicsProfile;

            ShowGraphicsProfile(selectedGraphicsProfile, dxViewportView: null);

            // Save the selected GraphicsProfile to application settings
            DXEngineSettings.Current.SaveGraphicsProfile(selectedGraphicsProfile);

            _selectedDpiScale = dxEngineSettingsWindow.SelectedDpiScale;
            MaxBackgroundThreadsCount = dxEngineSettingsWindow.SelectedMaxBackgroundThreadsCount;


            // Now create an array of GraphicsProfile from selectedGraphicsProfiles
            // If selectedGraphicsProfiles is hardware GraphicProfile, than we will also add software and WPF 3D rendering as fallback to the array
            DXEngineSettings.Current.GraphicsProfiles = DXEngineSettings.Current.SystemCapabilities.CreateArrayOfRecommendedGraphicsProfiles(selectedGraphicsProfile);

            if (ContentFrame.Content != null)
                ContentFrame.Refresh(); // This will reload the current sample with the new graphics settings
        }

        private void DiagnosticsButton_OnClick(object sender, RoutedEventArgs e)
        {
            if (_lastShownDXViewportView == null || _lastShownDXViewportView.IsDisposed)
            {
                _lastShownDXViewportView = FindDXViewportView(ContentFrame.Content);

                if (_lastShownDXViewportView == null)
                    return;
            }

            OpenDiagnosticsWindow();
        }

        // OnDXSceneInitialized is called when the DXScene and DirectX device has been created and when UsedGraphicsProfile is set
        // In this method we can read what type of rendering will be used by DXEngine
        private void OnDXSceneInitialized(object sender, EventArgs eventArgs)
        {
            if (_lastShownDXViewportView.DXScene != null && MaxBackgroundThreadsCount >= 0)
                _lastShownDXViewportView.DXScene.MaxBackgroundThreadsCount = MaxBackgroundThreadsCount;

            

            if (_lastShownDXViewportView != null && _lastShownDXViewportView.UsedGraphicsProfile != null)
            {
                UpdateUsedGraphicsProfileDescription(_lastShownDXViewportView.UsedGraphicsProfile, _lastShownDXViewportView);

                if (_lastShownDXViewportView.UsedGraphicsProfile.DriverType == GraphicsProfile.DriverTypes.Wpf3D)
                    Wpf3DRenderingWarningPanel.Visibility = Visibility.Visible;
            }

            SetRejectedGraphicsProfileWarningImageToolTip(_rejectedGraphicProfilesReasons);
        }

        private void ShowGraphicsProfile(GraphicsProfile graphicsProfile, DXViewportView dxViewportView)
        {
            if (dxViewportView == null)
            {
                GraphicsProfileTypeTextBlock.Text = "Selected graphics profile:";
                UpdateUsedGraphicsProfileDescription(graphicsProfile, null);
            }
            else
            {
                GraphicsProfileTypeTextBlock.Text = "Used graphics profile:";
                UpdateUsedGraphicsProfileDescription(graphicsProfile, dxViewportView);
            }
        }

        public void UpdateUsedGraphicsProfileDescription(GraphicsProfile usedGraphicsProfile, DXViewportView dxViewportView)
        {
            string graphicsInfoText;

            string graphicsProfileName = usedGraphicsProfile.DisplayName ?? usedGraphicsProfile.Name;

            if (usedGraphicsProfile.DriverType == GraphicsProfile.DriverTypes.Wpf3D ||
                dxViewportView == null || dxViewportView.DXScene == null)
            {
                // Show only profile name for WPF 3D or when we do not have dxViewportView.DXScene
                graphicsInfoText = graphicsProfileName + Environment.NewLine; 
            }
            else
            {
                string adapterInfo = usedGraphicsProfile.DefaultAdapterDescription ?? dxViewportView.DXScene.DXDevice.Adapter.Description1.Description;

                string samplingDescription;
                int multiSamplingCount, superSamplingCount;

                if (_lastShownDXViewportView != null && _lastShownDXViewportView.DXScene != null)
                {
                    multiSamplingCount = _lastShownDXViewportView.DXScene.UsedMultisamplingDescription.Count;
                    superSamplingCount = _lastShownDXViewportView.DXScene.SupersamplingCount;
                }
                else
                {
                    multiSamplingCount = 1;
                    superSamplingCount = 1;
                }

                if (multiSamplingCount <= 1 && superSamplingCount <= 1)
                    samplingDescription = "";
                else if (multiSamplingCount > 1 && superSamplingCount <= 1)
                    samplingDescription = string.Format(" ({0}xMSAA)", multiSamplingCount);
                else if (multiSamplingCount <= 1 && superSamplingCount > 1)
                    samplingDescription = string.Format(" ({0}xSSAA)", superSamplingCount);
                else
                    samplingDescription = string.Format(" ({0}xMSAA {1}xSSAA)", multiSamplingCount, superSamplingCount);

                graphicsInfoText = graphicsProfileName + " on \r\n" + adapterInfo + samplingDescription;
            }

            SelectedGraphicInfoTextBlock.Text = graphicsInfoText;
        }

        // OnGraphicsProfileRejected is called when a GraphicsProfile is rejected 
        // The reason why the specified GraphicsProfile cannot be used is written in the event arguments passed to this method
        private void OnGraphicsProfileRejected(object sender, GraphicsProfileRejectedEventArgs e)
        {
            if (_rejectedGraphicProfilesReasons == null)
                _rejectedGraphicProfilesReasons = "The following graphics profiles were rejected:\r\n\r\n";
            else
                _rejectedGraphicProfilesReasons += "\r\n\r\n";

            _rejectedGraphicProfilesReasons += string.Format("- {0}\r\n  Rejected reason: {1}", e.RejectedGraphicsProfile.DisplayName ?? e.RejectedGraphicsProfile.Name, e.RejectedReason);

            if (e.Exception != null)
                _rejectedGraphicProfilesReasons += string.Format("\r\n  Exception: {0}", e.Exception.Message);
        }

        private void OnDXViewportViewUnloaded(object sender, RoutedEventArgs routedEventArgs)
        {
            UnsubscribeLastShownDXViewportView();
        }

        private void SetRejectedGraphicsProfileWarningImageToolTip(string toolTipText)
        {
            RejectedGraphicsProfileWarningImage.ToolTip = toolTipText;
            RejectedGraphicsProfileWarningImage.Visibility = string.IsNullOrEmpty(toolTipText) ? Visibility.Collapsed : Visibility.Visible;
        }

        public void SubscribeDXViewportView(DXViewportView dxViewportView)
        {
            if (dxViewportView == null)
            {
                DisableDiagnosticsButton();
                CloseDiagnosticsWindow();
                return;
            }


            // Apply user settings

            // First set the GraphicsProfiles that should be used by DXEngine
            // If the GraphicsProfiles were not explicitly set in the loaded sample, set the GraphicsProfiles to settings defined by user
            // The GraphicsProfiles are stored by DXEngineSettings - stores GraphicsProfiles based on user choice or by recommended graphic profile
            if (ReferenceEquals(dxViewportView.GraphicsProfiles, DXView.DefaultGraphicsProfiles))
                dxViewportView.GraphicsProfiles = DXEngineSettings.Current.GraphicsProfiles;

            // If UseDirectXOverlay is set to true, then force DirectXOverlay for all DXView controls (this can be used to enable graphical debugging)
            if (DXEngineSettings.Current.UseDirectXOverlay)
                dxViewportView.PresentationType = DXView.PresentationTypes.DirectXOverlay;

            // Subscribe events that will save any rejected GraphicsProfile and the used GraphicsProfile
            dxViewportView.GraphicsProfileRejected += OnGraphicsProfileRejected;
            dxViewportView.DXSceneInitialized += OnDXSceneInitialized;
            dxViewportView.Unloaded += OnDXViewportViewUnloaded;

            _rejectedGraphicProfilesReasons = null;
            _lastShownDXViewportView = dxViewportView;

            // We are now showing "Used graphics profile:" instead of "Selected graphics profile:"
            GraphicsProfileTypeTextBlock.Text = "Used graphics profile:";


            // When we have a DXViewportView we can enable diagnostics button
            EnableDiagnosticsButton();

            if (_diagnosticsWindow != null)
                _diagnosticsWindow.DXView = dxViewportView;
        }

        public void UnsubscribeLastShownDXViewportView()
        {
            if (_lastShownDXViewportView == null)
                return;

            _lastShownDXViewportView.GraphicsProfileRejected -= OnGraphicsProfileRejected;
            _lastShownDXViewportView.DXSceneInitialized -= OnDXSceneInitialized;
            _lastShownDXViewportView.Unloaded -= OnDXViewportViewUnloaded;

            _lastShownDXViewportView = null;

            SetRejectedGraphicsProfileWarningImageToolTip(null);

            // If no DXViewportView will be shown, then we show the "Selected graphics profile:" instead of "Used graphics profile:"
            GraphicsProfileTypeTextBlock.Text = "Selected graphics profile:";

            if (DXEngineSettings.Current.GraphicsProfiles != null && DXEngineSettings.Current.GraphicsProfiles[0] != null)
                ShowGraphicsProfile(DXEngineSettings.Current.GraphicsProfiles[0], dxViewportView: null);

            Wpf3DRenderingWarningPanel.Visibility = Visibility.Collapsed;
        }

        private void EnableDiagnosticsButton()
        {
            if (_diagnosticsEnabledImage == null)
                _diagnosticsEnabledImage = new BitmapImage(new Uri("pack://application:,,,/Ab3d.DXEngine.Wpf.Samples;component/Resources/Diagnostics.png"));

            DiagnosticsImage.Source = _diagnosticsEnabledImage;
            DiagnosticsButton.IsEnabled = true;

            DiagnosticsButton.ToolTip = null;
        }

        private void DisableDiagnosticsButton()
        {
            if (_diagnosticsDisabledImage == null)
                _diagnosticsDisabledImage = new BitmapImage(new Uri("pack://application:,,,/Ab3d.DXEngine.Wpf.Samples;component/Resources/Diagnostics-gray.png"));

            DiagnosticsImage.Source = _diagnosticsDisabledImage;
            DiagnosticsButton.IsEnabled = false;

            DiagnosticsButton.ToolTip = "Diagnostics button is disabled because there is no shown DXViewportView control.";
            ToolTipService.SetShowOnDisabled(DiagnosticsButton, true);
        }

        private void OpenDiagnosticsWindow()
        {
            CloseDiagnosticsWindow();

            _diagnosticsWindow = new DiagnosticsWindow(_lastShownDXViewportView);
            _diagnosticsWindow.Closing += delegate (object o, CancelEventArgs args)
            {
                _diagnosticsWindow = null;
            };

            // Position DiagnosticsWindow to the top-left corner of our window
            double left = this.Left + this.ActualWidth;
            double maxLeft = left + DiagnosticsWindow.InitialWindowWidth;

            if (maxLeft > SystemParameters.VirtualScreenWidth)
            {
                if (this.Left > DiagnosticsWindow.InitialWindowWidth)
                    left = this.Left - DiagnosticsWindow.InitialWindowWidth;
                else
                    left -= (maxLeft - SystemParameters.VirtualScreenWidth);
            }

            _diagnosticsWindow.Left = left;
            _diagnosticsWindow.Top = this.Top;

            _diagnosticsWindow.Show();
        }

        private void CloseDiagnosticsWindow()
        {
            if (_diagnosticsWindow != null)
            {
                try
                {
                    _diagnosticsWindow.Close();
                }
                catch
                { }
            }
        }

        private void SelectItem(string pageName)
        {
            if (string.IsNullOrEmpty(pageName))
            {
                SampleList.SelectedItem = null;
                return;
            }

            var supportPageElement = SampleList.Items.OfType<System.Xml.XmlElement>()
                                                     .First(x => x.Attributes["Page"] != null && x.Attributes["Page"].Value == pageName);

            SampleList.SelectedItem = supportPageElement;

            SampleList.ScrollIntoView(supportPageElement);
        }

        // Searches the logical controls tree and returns the first instance of DXViewportView if found
        private DXViewportView FindDXViewportView(object element)
        {
            DXViewportView foundDViewportView = element as DXViewportView;

            if (foundDViewportView != null)
                return foundDViewportView;

            if (element is ContentControl)
            {
                // Check the element's Content
                foundDViewportView = FindDXViewportView(((ContentControl)element).Content);
            }
            else if (element is Decorator) // for example Border
            {
                // Check the element's Child
                foundDViewportView = FindDXViewportView(((Decorator)element).Child);
            }
            else if (element is Page)
            {
                // Page is not ContentControl so handle it specially (note: Window is ContentControl)
                foundDViewportView = FindDXViewportView(((Page)element).Content);
            }
            else if (element is Panel)
            {
                Panel panel = (Panel)element;

                // Check each child of a Panel
                foreach (UIElement oneChild in panel.Children)
                {
                    foundDViewportView = FindDXViewportView(oneChild);

                    if (foundDViewportView != null)
                        break;
                }
            }

            return foundDViewportView;
        }

        private void LogoImage_OnMouseUp(object sender, MouseButtonEventArgs e)
        {
            // For CORE3 project we need to set UseShellExecute to true,
            // otherwise a "The specified executable is not a valid application for this OS platform" exception is thrown.
            System.Diagnostics.Process.Start(new ProcessStartInfo("https://www.ab4d.com") { UseShellExecute = true });
        }

        private void DiagnosticsInfoImage_OnMouseDown(object sender, MouseButtonEventArgs e)
        {
            // For CORE3 project we need to set UseShellExecute to true,
            // otherwise a "The specified executable is not a valid application for this OS platform" exception is thrown.
            System.Diagnostics.Process.Start(new ProcessStartInfo("https://www.ab4d.com/DirectX/3D/Diagnostics.aspx") { UseShellExecute = true });
        }

        private void ContentFrame_OnNavigated(object sender, NavigationEventArgs e)
        {
            // Prevent navigation (for example clicking back button) because our ListBox is not updated when this navigation occurs
            // We prevent navigation with clearing the navigation history each time navigation item changes
            ContentFrame.NavigationService.RemoveBackEntry();
        }


        public void ReloadCurrentSample()
        {
            var savedItem = SampleList.SelectedItem;

            SampleList.SelectedItem = null;

            Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(delegate
            {
                SampleList.SelectedItem = savedItem;
            }));
        }

        public void ShowFullScreen()
        {
            Grid.SetColumn(RightSideBorder, 0);
            Grid.SetColumnSpan(RightSideBorder, 2);
            Grid.SetRow(RightSideBorder, 0);
            Grid.SetRowSpan(RightSideBorder, 2);

            RightSideBorder.Margin = new Thickness(0);
            RightSideBorder.Padding = new Thickness(0);

            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize; // This will also covert the taskbar
            WindowState = WindowState.Maximized;

            // Allow hitting escape to exit full screen
            this.PreviewKeyDown += OnPreviewKeyDown;
        }

        private void OnPreviewKeyDown(object sender, KeyEventArgs keyEventArgs)
        {
            if (keyEventArgs.Key == Key.Escape)
                ExitFullScreen();
        }

        public void ExitFullScreen()
        {
            this.PreviewKeyDown -= OnPreviewKeyDown;

            WindowState = WindowState.Normal;
            WindowStyle = WindowStyle.SingleBorderWindow;
            ResizeMode = ResizeMode.CanResize;

            Grid.SetColumn(RightSideBorder, 1);
            Grid.SetColumnSpan(RightSideBorder, 1);
            Grid.SetRow(RightSideBorder, 1);
            Grid.SetRowSpan(RightSideBorder, 1);

            RightSideBorder.Margin = new Thickness(5);
            RightSideBorder.Padding = new Thickness(10);
        }
    }
}
