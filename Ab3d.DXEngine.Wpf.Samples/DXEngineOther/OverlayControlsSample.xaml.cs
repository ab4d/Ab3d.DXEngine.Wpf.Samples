using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Ab3d.DirectX;
using Ab3d.DirectX.Controls;
using Ab3d.DirectX.Materials;
using Ab3d.DirectX.PostProcessing;
using SharpDX;
using SharpDX.Direct3D11;
using SharpDX.DXGI;

// Using DirectXOverlay PresentationType has a big performance advantage because in this
// presentation mode the drivers and graphics card can render in the background and when
// the rendering is complete the graphics card can present the rendered 3D scene to its part of screen.
// A big disadvantage in this presentation type is that it is not possible to render WPF objects over the 3D scene.
//
// But with using sprites, it is possible to render static WPF elements to a texture and show that in a sprite.
// It is also possible to show dynamic WPF controls like CameraNavigationCircles. In this case the control
// is rendered to a WPF bitmap after each change and then that bitmap is rendered as a sprite by DXViewportView.
// 
// It is also possible to show dynamic WPF controls that use Viewport3D (like CameraAxisPanel)
// with rendering them by DXViewportView that use the same DirectX device as the main DXViewportView.
//
// This way, the rendered CameraAxisPanel can be shared with the main DXViewportView and shown by using sprites.
// What is more, even though the WPF controls are not shown, the mouse events are still propagated to the controls.

// This sample uses WpfElementOverlay and Viewport3DObjectOverlay classes that greatly simplifies showing
// WPF controls with DirectXOverlay PresentationType.
// You only need to create an instance of WpfElementOverlay and Viewport3DObjectOverlay and pass 
// WPF control and parent DXViewportView. At the end you need to Dispose the controls.
//
// To position WPF controls you can use HorizontalAlignment, VerticalAlignment and Margin properties.
// 
// NOTE
// WpfElementOverlay and Viewport3DObjectOverlay classes are defined with FULL SOURCE CODE in this project.

namespace Ab3d.DXEngine.Wpf.Samples.DXEngineOther
{
    /// <summary>
    /// Interaction logic for OverlayControlsSample.xaml
    /// </summary>
    public partial class OverlayControlsSample : Page
    {
        public OverlayControlsSample()
        {
            InitializeComponent();


            // CameraNavigationCircles1, CameraAxisPanel1 and CameraControllerInfo are defined in XAML.
            // Because CameraNavigationCircles1 is a dynamic control that requires mouse events,
            // it must be added to the WPF's view tree.
            // Other control could be just defined in the code as the TextBlock below.

            // Use WpfElementOverlay to render the CameraNavigationCircles1
            // Because CameraNavigationCircles1 is a dynamic control, we need to update it after each change.
            var cameraNavigationCirclesOverlay = new WpfElementOverlay(CameraNavigationCircles1, MainDXViewportView);
            cameraNavigationCirclesOverlay.ParentElement = CameraNavigationCirclesParentGrid;

            CameraNavigationCircles1.ControlUpdated += delegate(object sender, EventArgs args)
            {
                cameraNavigationCirclesOverlay.Update();
            };



            // Use Viewport3DObjectOverlay to render the Viewport3D that is used by CameraAxisPanel1
            var cameraAxisPanelOverlay = new Viewport3DObjectOverlay(CameraAxisPanel1, MainDXViewportView);


            // CameraControllerInfo is a static WPF control.
            // It is shown by WpfElementOverlay that renders the CameraControllerInfo into a 2D texture that is shown by a sprite.
            var cameraControllerInfoOverlay = new WpfElementOverlay(CameraControllerInfo, MainDXViewportView);


            // Create a title TextBlock that will be shown by WpfElementOverlay
            var titleTextBlock = new TextBlock()
            {
                Text = "Using sprites to render WPF controls on a 3D scene rendered by DirectXOverlay presentation type",
                FontSize = 20,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(60, 60, 60)),
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(10, 10, 10, 10)
            };

            var titleTextBlockInfoOverlay = new WpfElementOverlay(titleTextBlock, MainDXViewportView);


            // IMPORTANT:
            // You need to dispose the created WpfElementOverlay and Viewport3DObjectOverlay objects when they are not used anymore.

            this.Unloaded += (sender, args) =>
            {
                cameraAxisPanelOverlay.Dispose();
                cameraNavigationCirclesOverlay.Dispose();
                cameraControllerInfoOverlay.Dispose();
                titleTextBlockInfoOverlay.Dispose();

                MainDXViewportView.Dispose();
            };
        }
    }
}
