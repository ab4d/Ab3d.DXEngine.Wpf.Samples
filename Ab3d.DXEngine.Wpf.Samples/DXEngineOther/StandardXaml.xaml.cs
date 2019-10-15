using System;
using System.Collections.Generic;
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
using System.Windows.Navigation;
using System.Windows.Shapes;
using Ab3d.DirectX;
using Path = System.IO.Path;

namespace Ab3d.DXEngine.Wpf.Samples.DXEngineOther
{
    /// <summary>
    /// Interaction logic for StandardXaml.xaml
    /// </summary>
    public partial class StandardXaml : Page
    {
        public StandardXaml()
        {
            InitializeComponent();


            // Code snippets:
            
            // Force using WPF 3D rendering (useful for comparison of performance)
            //MainDXViewportView.GraphicsProfiles = new GraphicsProfile[] { GraphicsProfile.Wpf3D };


            // Force using Software rendering (for example for server side rendering)
            //MainDXViewportView.GraphicsProfiles = new GraphicsProfile[] { GraphicsProfile.NormalQualitySoftwareRendering };


            // Enable collecting rendering statistics (times required for each rendering phase). Statistics can be then read from MainDXViewportView.DXScene.Statistics object.
            //Ab3d.DirectX.DXDiagnostics.IsCollectingStatistics = true;


            // Enable logging (no info and trace level logging is compiled into relase build - only warnings and errors)
            //Ab3d.DirectX.DXDiagnostics.LogLevel = DXDiagnostics.LogLevels.Warn;
            //Ab3d.DirectX.DXDiagnostics.LogFileName = @"C:\temp\DXEngine.log";
            //Ab3d.DirectX.DXDiagnostics.IsWritingLogToOutput = true;


            // AfterFrameRendered event is triggered after each frame is rendered
            //MainDXViewportView.DXScene.AfterFrameRendered += delegate(object sender, EventArgs args) {  };


            // DXSceneDeviceCreated event is triggered after the DirectX device and DXScene objects were created (see help for more info).
            // This can be used to check the actually used GraphicsProfile and to set some specific graphics settings on DXScene.
            //MainDXViewportView.DXSceneDeviceCreated += delegate(object sender, EventArgs args)
            //{
            //    if (MainDXViewportView.UsedGraphicsProfile.DriverType != GraphicsProfile.DriverTypes.Wpf3D)
            //    {
            //        // Render polylines as simple diconnected lines with DirectX hardware acceleration (when using small line thickness the missing connection between the lines is not visible) - see help for more info
            //        MainDXViewportView.DXScene.RenderConnectedLinesAsDisconnected = true;
            //    }
            //};


            // DXSceneInitialized event is triggered after all the 3D objects have been initialized (see help for more info).
            // It can be used to get lower lever DXEngine's SceneNode objects that are created from WPF's objects.
            //MainDXViewportView.DXSceneInitialized += delegate(object sender, EventArgs args)
            //{
            //    // Get the SceneNode that was created from the WPF's Box1Visual3D object
            //    var sceneNode = MainDXViewportView.GetSceneNodeForWpfObject(Box1Visual3D) as Ab3d.DirectX.Models.WpfModelVisual3DNode;
            //};




            // Sample specific code: add the sample title and button to copy xaml
            AddSampleTitle();

            this.Unloaded += (sender, args) => MainDXViewportView.Dispose();
        }



        /*******************************************************************

            You do not need to copy the code below to your project.
            It just adds the Title TextBlock and Button to copy the XAML.

        *******************************************************************/

        private void AddSampleTitle()
        {
            var stackPanel = new StackPanel()
            {
                Orientation = Orientation.Vertical
            };


            // titleTextBlock is defined here because it is not part of standard XAML
            var titleTextBlock = new TextBlock()
            {
                FontWeight = FontWeights.Bold,
                FontSize = 16,
                Foreground = Brushes.DimGray,
                Margin = new Thickness(10, 10, 10, 10),
                VerticalAlignment = VerticalAlignment.Top,
                HorizontalAlignment = HorizontalAlignment.Left,
                TextWrapping = TextWrapping.Wrap
            };

            titleTextBlock.Text =
@"Copy the XAML from this sample as the standard boilerplate for your 3D content.
Also check the C# part for common code snippets (see StandardXaml.xaml.cs).";

            stackPanel.Children.Add(titleTextBlock);


            var button = new Button()
            {
                Content = "Copy XAML to clipboard",
                FontSize = 16,
                Margin = new Thickness(10, 0, 10, 10),
                Padding = new Thickness(10, 3, 10, 3),
                HorizontalAlignment = HorizontalAlignment.Left
            };

            button.Click += ButtonOnClick;

            stackPanel.Children.Add(button);


            RootGrid.Children.Add(stackPanel);
        }

        private void ButtonOnClick(object sender, RoutedEventArgs routedEventArgs)
        {
            try
            {
                string fileName = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\DXEngine\StandardXaml.xaml");

                string xaml = File.ReadAllText(fileName);

                Clipboard.SetText(xaml);

                MessageBox.Show("Successfully copied template XAML to clipboard");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error getting XAML to clipboard\r\n" + ex.Message);
            }
        }
    }
}
