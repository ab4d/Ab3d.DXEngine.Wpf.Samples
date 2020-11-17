using System;
using System.CodeDom;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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
using System.Windows.Media.Media3D;
using Ab3d.DirectX;
using Ab3d.DirectX.Controls;
using Ab3d.DirectX.Models;
using Ab3d.Utilities;
using Ab3d.Visuals;
using SharpDX;
using Material = System.Windows.Media.Media3D.Material;
using Point = System.Windows.Point;

namespace Ab3d.DXEngine.Wpf.Samples.DXEngineHitTesting
{
    // This sample is similar to EventManagerDragSample sample from Ab3d.PowerToys main samples.
    // The difference is that this sample is using DXEventManager3DWrapper that is derived 
    // from the standard EventManager3D from Ab3d.PowerToys library.
    //
    // This means that when using DXEventManager3DWrapper, you can use the same methods and events as with EventManager3D
    // and use the much faster hit testing from DXEngine.
    //
    // The source for the DXEventManager3DWrapper is available in this project.
    // 
    // Unfortunately for the DXEventManager3DWrapper to work, .Net's reflection needs to be used
    // to be able to create the RayMeshGeometry3DHitTestResult class and sets its properties.

    /// <summary>
    /// Interaction logic for DXEventManager3DWrapperSample.xaml
    /// </summary>
    public partial class DXEventManager3DWrapperSample : Page
    {
        private bool _isSelected;
        private DXEventManager3DWrapper _eventManager;

        private DiffuseMaterial _selectedMaterial;
        private DiffuseMaterial _unSelectedMaterial;

        public DXEventManager3DWrapperSample()
        {
            InitializeComponent();

            _selectedMaterial = new DiffuseMaterial(Brushes.Red);
            _unSelectedMaterial = new DiffuseMaterial(Brushes.Blue);

            LowerBoxVisual3D.SetName("LowerBoxVisual3D");
            PassageBoxVisual3D.SetName("PassageBoxVisual3D");
            UpperBoxVisual3D.SetName("UpperBoxVisual3D");
            MovableVisualParent.SetName("MovableVisualParent");
            MovableBoxVisual3D.SetName("MovableBoxVisual3D");
            ArrowLineVisual3D.SetName("ArrowLineVisual3D");
            Cylinder1.SetName("Cylinder1");
            Cylinder2.SetName("Cylinder2");
            Cylinder3.SetName("Cylinder3");
            TransparentPlaneVisual3D.SetName("TransparentPlaneVisual3D");


            this.Loaded += new RoutedEventHandler(DXEventManager3DWrapperSample_Loaded);

            // IMPORTANT:
            // It is very important to call Dispose method on DXSceneView after the control is not used any more (see help file for more info)
            this.Unloaded += (sender, args) => MainDXViewportView.Dispose();
        }

        void DXEventManager3DWrapperSample_Loaded(object sender, RoutedEventArgs e)
        {
            //_eventManager = new Ab3d.Utilities.EventManager3D(Viewport3D1);
            //_eventManager.CustomEventsSourceElement = ViewportBorder;

            _eventManager = new DXEventManager3DWrapper(MainDXViewportView);


            // Exclude TransparentPlaneVisual3D from hit testing
            _eventManager.RegisterExcludedVisual3D(TransparentPlaneVisual3D);


            var multiEventSource3D = new Ab3d.Utilities.MultiVisualEventSource3D();
            multiEventSource3D.TargetVisuals3D = new Visual3D[] { LowerBoxVisual3D, PassageBoxVisual3D, UpperBoxVisual3D };
            multiEventSource3D.IsDragSurface = true;

            _eventManager.RegisterEventSource3D(multiEventSource3D);


            var eventSource3D = new Ab3d.Utilities.VisualEventSource3D();
            eventSource3D.TargetVisual3D = MovableBoxVisual3D;
            eventSource3D.Name = "Movable";
            eventSource3D.MouseEnter += new Ab3d.Common.EventManager3D.Mouse3DEventHandler(eventSource3D_MouseEnter);
            eventSource3D.MouseLeave += new Ab3d.Common.EventManager3D.Mouse3DEventHandler(eventSource3D_MouseLeave);
            eventSource3D.MouseClick += new Ab3d.Common.EventManager3D.MouseButton3DEventHandler(movableEventSource3D_MouseClick);
            eventSource3D.MouseDrag += new Ab3d.Common.EventManager3D.MouseDrag3DEventHandler(movableEventSource3D_MouseDrag);

            _eventManager.RegisterEventSource3D(eventSource3D);
        }

        void eventSource3D_MouseLeave(object sender, Ab3d.Common.EventManager3D.Mouse3DEventArgs e)
        {
            ViewportBorder.Cursor = null;

            MovableBoxVisual3D.Material = _unSelectedMaterial;
            ArrowLineVisual3D.LineColor = Colors.Blue;
        }

        void eventSource3D_MouseEnter(object sender, Ab3d.Common.EventManager3D.Mouse3DEventArgs e)
        {
            ViewportBorder.Cursor = Cursors.Hand;

            MovableBoxVisual3D.Material = _selectedMaterial;
            ArrowLineVisual3D.LineColor = Colors.Red;
        }

        void movableEventSource3D_MouseDrag(object sender, Ab3d.Common.EventManager3D.MouseDrag3DEventArgs e)
        {
            if (e.HitSurface != null)
            {
                MovableVisualTranslate.OffsetX = e.CurrentSurfaceHitPoint.X;
                MovableVisualTranslate.OffsetY = e.CurrentSurfaceHitPoint.Y + MovableBoxVisual3D.Size.Y / 2;
                MovableVisualTranslate.OffsetZ = e.CurrentSurfaceHitPoint.Z;
            }
        }

        void movableEventSource3D_MouseClick(object sender, Ab3d.Common.EventManager3D.MouseButton3DEventArgs e)
        {
            Material newMaterial;

            if (_isSelected)
                newMaterial = new DiffuseMaterial(Brushes.Blue);
            else
                newMaterial = new DiffuseMaterial(Brushes.Gold);

            MovableBoxVisual3D.Material = newMaterial;

            _isSelected = !_isSelected;
        }
    }
}
