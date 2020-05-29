using System;
using System.Collections.Generic;
using System.Linq;
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
using System.Windows.Threading;
using Ab3d.DirectX.Common;
using Ab3d.Meshes;
using Ab3d.Visuals;
using SharpDX;
using SharpDX.DXGI;
using Color = System.Windows.Media.Color;
using RenderingEventArgs = Ab3d.DirectX.RenderingEventArgs;

namespace Ab3d.DXEngine.Wpf.Samples.DXEngineOther
{
    // This handle shows a simple example on how to handle the DeviceRemoved event.
    //
    // The DeviceRemoved event can be triggered when the DirectX device is removed or reset.
    // This usually happen when the graphics driver is updated while the application is running.
    // This event does not happen when windows are restored from sleep or hibernation
    // or when the window is minimized and then opened again.
    // This event can also happen in case when there is some problem with the driver (very rare).
    //
    // In case when this event is triggered, all the DirectX resources that are created
    // with DXEngine are invalid. This means that the 3D scene and all the DirectX and DXEngine
    // objects need to be created again.
    // 
    // See more:
    // https://gamedev.stackexchange.com/questions/126534/dx11-handle-device-removed
    // https://docs.microsoft.com/en-us/windows/uwp/gaming/handling-device-lost-scenarios

    /// <summary>
    /// Interaction logic for DeviceRemovedHandlerSample.xaml
    /// </summary>
    public partial class DeviceRemovedHandlerSample : Page
    {
        private struct SphereInfo
        {
            public Point3D CenterPosition;
            public Color Color;

            public SphereInfo(Point3D centerPosition, Color color)
            {
                CenterPosition = centerPosition;
                Color = color;
            }
        }

        private List<SphereInfo> _shownSphereData;

        // Static fields that will store the scene data when this sample is unloaded and loaded again
        private static List<SphereInfo> _savedSphereData;

        private static double _savedCameraHeading;
        private static double _savedCameraAttitude;
        private static double _savedCameraDistance;
        private static Point3D _savedCameraTargetPosition;

        public DeviceRemovedHandlerSample()
        {
            InitializeComponent();


            List<SphereInfo> usedSphereData;

            // If we have stored data, then use then
            if (_savedSphereData != null)
            {
                usedSphereData = _savedSphereData;

                Camera1.Heading        = _savedCameraHeading;
                Camera1.Attitude       = _savedCameraAttitude;
                Camera1.Distance       = _savedCameraDistance;
                Camera1.TargetPosition = _savedCameraTargetPosition;

                _savedSphereData = null;

                SampleReloadedIntoTextBlock.Visibility = Visibility.Visible;
            }
            else
            {
                // ... else generate new data
                usedSphereData = CreateRandomSphereData(20);
            }

            ShowSpheres(usedSphereData);


            // To subscribe to DeviceRemoved we need to wait until DXScene is created
            MainDXViewportView.DXSceneDeviceCreated += delegate(object sender, EventArgs args)
            {
                MainDXViewportView.DXScene.DeviceRemoved += DXSceneOnDeviceRemoved;
            };

            this.Unloaded += delegate(object sender, RoutedEventArgs args)
            {
                MainDXViewportView.Dispose();
            };
        }

        // This event handler is called when DirectX device is removed or reset.
        private void DXSceneOnDeviceRemoved(object sender, DeviceRemovedEventArgs e)
        {
            // If we do not handle the device removal, we just return from the handler (e.IsHandled stays false)
            if (!(HandleDeviceRemovedCheckBox.IsChecked ?? false))
                return;


            // Save the 3D scene data so we will be able to regenerate the 3D scene
            // after this sample is loaded again.
            SaveSceneData();

            // Set IsHandled to true.
            // This will prevent throwing DXEngineException because it is a sign that we will handle this use case.
            e.IsHandled = true;

            // Reload the current sample
            Dispatcher.BeginInvoke(new Action(delegate
            {
                var parentWindow = Window.GetWindow(this) as MainWindow;
                if (parentWindow != null)
                    parentWindow.ReloadCurrentSample();
            }));
        }

        private void SaveSceneData()
        {
            _savedSphereData = _shownSphereData;

            _savedCameraHeading        = Camera1.Heading;
            _savedCameraAttitude       = Camera1.Attitude;
            _savedCameraDistance       = Camera1.Distance;
            _savedCameraTargetPosition = Camera1.TargetPosition + Camera1.Offset;
        }

        private List<SphereInfo> CreateRandomSphereData(int count)
        {
            var rnd = new Random();
            var spheres = new List<SphereInfo>(count);

            for (int i = 0; i < count; i++)
            {
                var centerPosition = new Point3D(rnd.NextDouble() * 100 - 50, rnd.NextDouble() * 30 + 10, rnd.NextDouble() * 100 - 50);
                var color = Color.FromRgb((byte) rnd.Next(255), (byte) rnd.Next(255), (byte) rnd.Next(255));

                spheres.Add(new SphereInfo(centerPosition, color));
            }

            return spheres;
        }

        private void ShowSpheres(List<SphereInfo> spheres)
        {
            RootVisual3D.Children.Clear();

            foreach (var sphereInfo in spheres)
            {
                var sphereVisual3D = new SphereVisual3D()
                {
                    CenterPosition = sphereInfo.CenterPosition,
                    Radius         = 3,
                    Material       = new DiffuseMaterial(new SolidColorBrush(sphereInfo.Color))
                };

                RootVisual3D.Children.Add(sphereVisual3D);
            }

            _shownSphereData = spheres;
        }

        private void RecreateSceneButton_OnClick(object sender, RoutedEventArgs e)
        {
            var usedSphereData = CreateRandomSphereData(20);
            ShowSpheres(usedSphereData);

            SampleReloadedIntoTextBlock.Visibility = Visibility.Collapsed;
        }

        private void RemoveDeviceButton_OnClick(object sender, RoutedEventArgs e)
        {
            // NOTE:
            // You can also simulate a device removed error with starting "dxcap -forcetdr" from admin visual studio command prompt
            // See "Testing Device Removed Handling" - https://docs.microsoft.com/en-us/windows/uwp/gaming/handling-device-lost-scenarios#testing-device-removed-handling

            MessageBox.Show("To simulate a DeviceRemoval an exception will be thrown.\r\n\r\nIf you have break on exceptions turned on in Visual Studio\r\njust click on Continue or press F5 when Visual Studio is shown.", "", MessageBoxButton.OK, MessageBoxImage.Stop);

            if (MainDXViewportView.DXScene != null)
            {
                // Simulate device removed event when calling DirectX Present method
                MainDXViewportView.DXScene.DefaultCompleteRenderingStep.BeforeRunningStep += delegate(object o, RenderingEventArgs args)
                {
                    //
                    // WHEN VISUALS STUDIO STOPS HERE JUST CLICK ON CONTINUE OR PRESS F5
                    //
                    throw new SharpDXException(ResultCode.DeviceRemoved);
                };

                
                // Render the scene again to trigger the exception
                MainDXViewportView.Refresh();
            }
        }
    }
}
