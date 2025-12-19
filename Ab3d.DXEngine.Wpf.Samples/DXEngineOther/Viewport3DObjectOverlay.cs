using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Ab3d.DirectX;
using Ab3d.DirectX.Controls;

#if SHARPDX
using SharpDX;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
#endif

namespace Ab3d.DXEngine.Wpf.Samples.DXEngineOther
{
    public class Viewport3DObjectOverlay : IDisposable
    {
        private readonly DXViewportView _mainDXViewportView;

        private readonly FrameworkElement _wpfControlWithViewport3D;

        private SpriteBatch _spriteBatch;

        private DXViewportView _secondaryDXViewportView;
        private Texture2D _texture2D;
        private ShaderResourceView _shaderResourceView;

        private double _dpiScaleX, _dpiScaleY;

        private bool _isRenderingSubscribed;


        // When CopyRenderTargetToSeparateTexture is set to true, then the RenderTargets (contains the rendered 3D scene)
        // from the CameraAxisPanel and from CameraNavigationCircles are copied to a separate 2D texture before they are
        // shown in the main 3D scene as sprites.
        // When false, then the RenderedTarget is directly used as a sprite's texture.
        // The later option is faster because it skips one texture copying, but can theoretically produce flickering
        // in case when the DirectX is rendering the sprites in the main scene (note that in DirectXOverlay rendering 
        // is happening in the background) while the rendering of CameraAxisPanel and from ViewCubeControllers is also happening.
        //
        // Because in the textures for rendered CameraAxisPanel and from CameraNavigationCircles are small and the copying is
        // happening in GPU memory, this is very fast and therefore this sample is using it by default.
        public bool CopyRenderTargetToSeparateTexture = true;


        public Viewport3DObjectOverlay(FrameworkElement wpfControlWithViewport3D, DXViewportView mainDXViewportView)
        {
            _wpfControlWithViewport3D = wpfControlWithViewport3D;
            _mainDXViewportView = mainDXViewportView;
            
            _mainDXViewportView.DXSceneInitialized += (sender, args) =>
            {
                // When the DXScene is initialized and have a valid size, we can setup the overlay controls

                DXView.GetDpiScale(mainDXViewportView, out _dpiScaleX, out _dpiScaleY);

                SetupOverlay();

                // Because the secondary DXViewportView controls (used to render CameraAxisPanel and ViewCubeCameraController)
                // are not actually visible (are not loaded to WPF tree view), they do not automatically check for updates 
                // and render the 3D scene in case of changes (for example when camera is changed).
                // Therefore we need to manually call RenderScene - this is done in the Rendering event handler.
                StartAutomaticUpdating();
            };

            // When some overlay controls are right or bottom aligned, then we need to update their positions when the size of the view is changed
            _mainDXViewportView.SizeChanged += (sender, args) =>
            {
                Update();
            };
        }

        public void StartAutomaticUpdating()
        {
            if (!_isRenderingSubscribed)
            {
                CompositionTarget.Rendering += OnCompositionTargetOnRendering;
                _isRenderingSubscribed = true;
            }
        }
        
        public void StopAutomaticUpdating()
        {
            if (_isRenderingSubscribed)
            {
                CompositionTarget.Rendering -= OnCompositionTargetOnRendering;
                _isRenderingSubscribed = false;
            }
        }

        private void OnCompositionTargetOnRendering(object sender, EventArgs args)
        {
            // Because the secondary DXViewportView controls (used to render CameraAxisPanel and ViewCubeCameraController)
            // are not actually visible (are not loaded to WPF tree view), they do not automatically check for updates 
            // and render the 3D scene in case of changes (for example when camera is changed).
            // Therefore we need to manually call RenderScene - this is done in the Rendering event handler.

            if (_secondaryDXViewportView != null)
                _secondaryDXViewportView.RenderScene(forceRender: false, forceUpdate: false);
        }

        private void SetupOverlay()
        {
            var dxScene = _mainDXViewportView.DXScene;
            if (dxScene == null)
                return;

            _spriteBatch = dxScene.CreateSpriteBatch(_wpfControlWithViewport3D.GetType().Name + "OverlaySprite");
            _spriteBatch.UseDeviceIndependentUnits = true; // Do not use pixel units but the same units as WPF (pixels scaled by dpi scale) when defining the destination rectangle

            _secondaryDXViewportView = CreateSecondaryDXViewportView(_wpfControlWithViewport3D, _mainDXViewportView, out _texture2D, out _shaderResourceView);

            if (_secondaryDXViewportView != null)
            {
                _secondaryDXViewportView.SceneRendered += (sender, args) =>
                {
                    Update();
                };

                // Render the 3D scene with CameraAxisPanel
                // This will also call UpdateCameraAxisImageSprite (from SceneRendered event handler)
                _secondaryDXViewportView.Refresh();
            }
        }

        public void Update()
        {
            if (_spriteBatch == null)
                return;

            if (CopyRenderTargetToSeparateTexture)
            {
                // Copy rendered scene from RenderTarget to out Texture2D object
                _secondaryDXViewportView.DXScene.DXDevice.ImmediateContext.CopyResource(_secondaryDXViewportView.DXScene.BackBuffer, _texture2D);
            }

            var destinationRectangle = GetWpfObjectPosition(_wpfControlWithViewport3D, parentWpfElement: _mainDXViewportView);
            UpdateSecondaryDXViewportView(_secondaryDXViewportView, _spriteBatch, _shaderResourceView, destinationRectangle);
        }
        
        private DXViewportView CreateSecondaryDXViewportView(FrameworkElement wpfElement, DXViewportView primaryDXViewportView, out Texture2D texture2D, out ShaderResourceView shaderResourceView)
        {
            // We need a Viewport3D to be able to create a secondary DXViewportView
            // Find it inside the ViewCubeCameraController1
            var viewport3D = FindViewport3D(wpfElement);

            if (viewport3D == null)
            {
                shaderResourceView = null;
                texture2D = null;
                return null;
            }

            float supersamplingFactor = primaryDXViewportView.DXScene.SupersamplingFactor;

            int wpfElementWidth = (int)(wpfElement.ActualWidth * _dpiScaleX * supersamplingFactor);
            int wpfElementHeight = (int)(wpfElement.ActualHeight * _dpiScaleY * supersamplingFactor);


            DXDevice dxDevice = primaryDXViewportView.DXScene.DXDevice;
            var secondaryDXViewportView = CreateSecondaryDXViewportView(viewport3D, dxDevice, wpfElementWidth, wpfElementHeight);


            // See comment at the definition of CopyRenderTargetToSeparateTexture
            if (CopyRenderTargetToSeparateTexture)
            {
                // When copying texture, we need to create a new texture that will contain the copied rendered scene
                texture2D = dxDevice.CreateTexture2D(wpfElementWidth, wpfElementHeight, new SampleDescription(1, 0), false, true, false);
                shaderResourceView = new ShaderResourceView(dxDevice.Device, texture2D);
            }
            else
            {
                // We using the RenderedTarget, we just save the BackBufferShaderResourceView from the DXScene
                texture2D = null;
                shaderResourceView = secondaryDXViewportView.DXScene.BackBufferShaderResourceView;
            }

            return secondaryDXViewportView;
        }

        private DXViewportView CreateSecondaryDXViewportView(Viewport3D viewport3D, DXDevice dxDevice, int width, int height)
        {
            if (dxDevice == null || viewport3D == null)
                return null;

            // Create DXScene from the existing DirectX 11 device.
            // This way, we will be able to share the rendered image as a texture in the final 3D scene.
            // Note that dpi scale and supersamplingCount are set to 1 here; when using bigger dpi scale and supersamplingCount, then the width and height will be bigger (this is setup in the calling method)
            var dxScene = dxDevice.CreateDXSceneWithBackBuffer(width, height,
                                                               preferedMultisampleCount: 4,
                                                               supersamplingCount: 1,
                                                               dpiScaleX: 1, dpiScaleY: 1,
                                                               createBackBufferShaderResourceView: !CopyRenderTargetToSeparateTexture);

            // After we have DXScene and Viewport3D objects, we can create a DXViewportView.
            // It is used to render 3D scene defined by Viewport3D.
            return new DXViewportView(dxScene, viewport3D);
        }

        private void UpdateSecondaryDXViewportView(DXViewportView dxViewportView, SpriteBatch spriteBatch, ShaderResourceView shaderResourceView, RectangleF destination)
        {
            if (dxViewportView == null)
                return;

            UpdateSpriteBatch(spriteBatch, shaderResourceView, destination);
        }

        private void UpdateSpriteBatch(SpriteBatch spriteBatch, ShaderResourceView shaderResourceView, RectangleF destination)
        {
            // Use non-premultiplied alpha blend
            spriteBatch.Begin(_mainDXViewportView.DXScene.DXDevice.CommonStates.NonPremultipliedAlphaBlend);
            spriteBatch.Draw(shaderResourceView, destination, isDestinationRelative: false);
            spriteBatch.End();
        }

        public static RectangleF GetWpfObjectPosition(FrameworkElement wpfElement, FrameworkElement parentWpfElement)
        {
            float parentWidth = (float)parentWpfElement.ActualWidth;
            float parentHeight = (float)parentWpfElement.ActualHeight;

            float elementWidth = (float)wpfElement.ActualWidth;
            float elementHeight = (float)wpfElement.ActualHeight;

            var wpfElementMargin = wpfElement.Margin;

            float left = (float)wpfElementMargin.Left;
            float right = (float)wpfElementMargin.Right;
            float top = (float)wpfElementMargin.Top;
            float bottom = (float)wpfElementMargin.Bottom;


            float elementX, elementY;

            switch (wpfElement.HorizontalAlignment)
            {
                case HorizontalAlignment.Center:
                    elementX = parentWidth - (left - right - elementWidth) / 2 + left;
                    break;

                case HorizontalAlignment.Right:
                    elementX = parentWidth - elementWidth - right;
                    break;

                case HorizontalAlignment.Left:
                case HorizontalAlignment.Stretch:
                default:
                    elementX = left;
                    break;
            }

            switch (wpfElement.VerticalAlignment)
            {
                case VerticalAlignment.Center:
                    elementY = ((float)parentHeight - top - bottom - elementHeight) / 2 + top;
                    break;

                case VerticalAlignment.Bottom:
                    elementY = (float)parentHeight - (float)elementHeight - bottom;
                    break;

                case VerticalAlignment.Top:
                case VerticalAlignment.Stretch:
                default:
                    elementY = top;
                    break;
            }

            var position = new RectangleF(elementX, elementY, elementWidth, elementHeight);

            return position;
        }

        private static Viewport3D FindViewport3D(DependencyObject wpfObject)
        {
            int childrenCount = VisualTreeHelper.GetChildrenCount(wpfObject);

            Viewport3D foundViewport3D = null;
            for (int i = 0; i < childrenCount; i++)
            {
                var oneChild = VisualTreeHelper.GetChild(wpfObject, i);

                foundViewport3D = oneChild as Viewport3D;

                if (foundViewport3D == null)
                    foundViewport3D = FindViewport3D(oneChild);

                if (foundViewport3D != null)
                    break;
            }

            return foundViewport3D;
        }
        

        private void DisposeTexturesAndShaderResourceViews()
        {
            if (_shaderResourceView != null)
            {
                _shaderResourceView.Dispose();
                _shaderResourceView = null;
            }

            if (_texture2D != null)
            {
                _texture2D.Dispose();
                _texture2D = null;
            }
        }

        private void DisposeSecondaryDXViewportViews()
        {
            if (_secondaryDXViewportView != null)
            {
                _secondaryDXViewportView.Dispose();
                _secondaryDXViewportView = null;
            }
        }

        public void Dispose()
        {
            StopAutomaticUpdating();

            if (CopyRenderTargetToSeparateTexture)
                DisposeTexturesAndShaderResourceViews(); // Call this only when the Texture2D and ShaderResourceView are created in this sample (and not in DXScene)

            DisposeSecondaryDXViewportViews();
        }
    }
}