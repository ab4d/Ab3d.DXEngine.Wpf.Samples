using System;
using System.Windows;
using System.Windows.Media.Imaging;
using Ab3d.Controls;
using Ab3d.DirectX;
using Ab3d.DirectX.Controls;
using Ab3d.DirectX.Materials;

#if SHARPDX
using SharpDX;
using SharpDX.Direct3D11;
#endif

namespace Ab3d.DXEngine.Wpf.Samples.DXEngineOther
{
    public class WpfElementOverlay : IDisposable
    {
        private readonly DXViewportView _mainDXViewportView;

        private readonly FrameworkElement _wpfElement;

        private SpriteBatch _spriteBatch;
        private ShaderResourceView _shaderResourceView;

        private double _dpiScaleX, _dpiScaleY;
        private RenderTargetBitmap _wpfElementBitmap;

        public FrameworkElement ParentElement;

        public WpfElementOverlay(FrameworkElement wpfElement, DXViewportView mainDXViewportView)
        {
            _wpfElement = wpfElement;
            _mainDXViewportView = mainDXViewportView;

            _mainDXViewportView.DXSceneInitialized += (sender, args) =>
            {
                // When the DXScene is initialized and have a valid size, we can setup the overlay controls

                DXView.GetDpiScale(mainDXViewportView, out _dpiScaleX, out _dpiScaleY);

                SetupOverlay();
            };

            // When some overlay controls are right or bottom aligned, then we need to update their positions when the size of the view is changed
            _mainDXViewportView.SizeChanged += (sender, args) =>
            {
                UpdateSpriteBatch();
            };
        }


        // The following method will set up a sprite that will show a static MouseCameraControllerInfo control.
        // The control is rendered by WPF to a bitmap and then converted to a ShaderResourceView that can be used as a Sprite's texture.
        private void SetupOverlay()
        {
            var dxScene = _mainDXViewportView.DXScene;
            if (dxScene == null)
                return;


            // Before rendering we need to call Update to generate the inner controls
            var mouseCameraControllerInfo = _wpfElement as MouseCameraControllerInfo;
            if (mouseCameraControllerInfo != null)
                mouseCameraControllerInfo.Update();
            

            // Render to a bigger bitmap when using DPI scale and Super-sampling
            int bitmapDpi = (int)(96 * _dpiScaleX * _mainDXViewportView.DXScene.SupersamplingFactor);
            
            // IMPORTANT:
            // If the control is changing and you call RenderToBitmap multiple times to render the same control,
            // then pass the previously rendered RenderTargetBitmap as a parameter to reuse the RenderTargetBitmap.
            // Otherwise a significant amount of unmanaged memory may be used by the bitmaps (GC is not aware of that and may not clear it).
            _wpfElementBitmap = Ab3d.Utilities.BitmapRendering.RenderToBitmap(_wpfElement, null, bitmapDpi);

            // Then we convert WPF's bitmap to DirectX ShaderResourceView that can be used as a Sprite's texture
            _shaderResourceView = WpfMaterial.CreateTexture2D(dxScene.DXDevice, _wpfElementBitmap);

            _wpfElementBitmap.Clear(); // This will help release the native memory for the RenderTargetBitmap


            _spriteBatch = dxScene.CreateSpriteBatch(_wpfElement.GetType().Name + "OverlaySprite");
            _spriteBatch.UseDeviceIndependentUnits = true; // Do not use pixel units but the same units as WPF (pixels scaled by dpi scale) when defining the destination rectangle

            // Position and draw the sprite
            UpdateSpriteBatch();
        }

        public void Update()
        {
            var dxScene = _mainDXViewportView.DXScene;

            if (_wpfElementBitmap == null || _shaderResourceView == null || dxScene == null)
            {
                SetupOverlay();
                return;
            }


            // Render to a bigger bitmap when using DPI scale and Super-sampling
            int bitmapDpi = (int)(96 * _dpiScaleX * _mainDXViewportView.DXScene.SupersamplingFactor);
            
            // If we do not call UpdateLayout, then initially CameraNavigationCircles is not visible (only after changing the camera)
            _wpfElement.UpdateLayout();

            _wpfElementBitmap = Ab3d.Utilities.BitmapRendering.RenderToBitmap(_wpfElement, null, bitmapDpi, renderTargetBitmapToReuse: _wpfElementBitmap);

            _shaderResourceView.Dispose();
            _shaderResourceView = WpfMaterial.CreateTexture2D(dxScene.DXDevice, _wpfElementBitmap);

            UpdateSpriteBatch();

            dxScene.NotifyChange(DXScene.ChangeNotifications.SpriteBatchChanged);
        }

        private void UpdateSpriteBatch()
        {
            if (_spriteBatch == null)
                return;

            var positionedElement = ParentElement ?? _wpfElement;

            var destinationRectangle = Viewport3DObjectOverlay.GetWpfObjectPosition(positionedElement, parentWpfElement: _mainDXViewportView);
            UpdateSpriteBatch(_spriteBatch, _shaderResourceView, destinationRectangle);
        }

        private void UpdateSpriteBatch(SpriteBatch spriteBatch, ShaderResourceView shaderResourceView, RectangleF destination)
        {
            // Use non-premultiplied alpha blend
            spriteBatch.Begin(_mainDXViewportView.DXScene.DXDevice.CommonStates.NonPremultipliedAlphaBlend);
            spriteBatch.Draw(shaderResourceView, destination, isDestinationRelative: false);
            spriteBatch.End();
        }

        private void DisposeTexturesAndShaderResourceViews()
        {
            if (_shaderResourceView != null)
            {
                _shaderResourceView.Dispose();
                _shaderResourceView = null;
            }
        }

        public void Dispose()
        {
            DisposeTexturesAndShaderResourceViews();
        }
    }
}