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
using System.Windows.Navigation;
using System.Windows.Shapes;
using Ab3d.DirectX;
using Ab3d.DirectX.Materials;
using SharpDX;
using SharpDX.Direct3D11;
using Matrix = System.Windows.Media.Matrix;
using Point = System.Windows.Point;

namespace Ab3d.DXEngine.Wpf.Samples.DXEngineOther
{
    /// <summary>
    /// Interaction logic for SpritesSample.xaml
    /// </summary>
    public partial class SpritesSample : Page
    {
        private SpriteBatch _animatedSpriteBatch;
        private ShaderResourceView _animatedSpriteShaderResourceView;
        
        private ShaderResourceView _backgroundSpriteShaderResourceView;

        private DateTime _animationStartTime;
        private SharpDX.Point _animatedSpritePosition;
        private float _animatedRotationAngle;
        private SpriteBatch _bottomRightSpriteBatch;
        private ShaderResourceView _logoShaderResourceView;
        private int _bottomRightSpriteSize;
        
        private DisposeList _disposables;

        public SpritesSample()
        {
            InitializeComponent();

            _disposables = new DisposeList();

            MainDXViewportView.DXSceneInitialized += delegate(object sender, EventArgs args)
            {
                // When the DXScene is initialized, we can call SetupTestSpritesScene
                // We could also use DXSceneDeviceCreated to add sprites, but at that time the size of the view is not yet set 
                // but we require the size to correctly position a sprite at bottom right corner. This size is available in DXSceneInitialized.
                SetupTestSpritesScene();
            };

            MainDXViewportView.SceneRendered += delegate(object sender, EventArgs args)
            {
                if (_animationStartTime == DateTime.MinValue)
                    return;

                var elapsedTime = DateTime.Now - _animationStartTime;

                _animatedSpritePosition.X = (int)((Math.Cos(elapsedTime.TotalSeconds) + 1) * 200.0) + 100;
                _animatedRotationAngle = (float)elapsedTime.TotalSeconds * 90f;

                UpdateAnimatedSprite();
            };

            this.Unloaded += delegate(object sender, RoutedEventArgs args)
            {
                _disposables.Dispose();
                MainDXViewportView.Dispose();
            };
        }

        private void SetupTestSpritesScene()
        {
            var dxScene = MainDXViewportView.DXScene;
            if (dxScene == null) // For example when we are using Wpf 3d
                return;

            // First we need to create a new SpriteBatch object that will be able to render many sprites
            var spriteBatch = dxScene.CreateSpriteBatch("SpriteBatch1");

            // By default the coordinates and size are defined in pixels,
            // but we can enable using device independent units to use the same units as in WPF (for example in an overlay WPF element)
            spriteBatch.UseDeviceIndependentUnits = true;

            // When the SpriteBatch is created or updated, we need to fist call the Begin method.
            // Then many draw or other methods can be called. This is completed by calling End method.
            // Begin method can be also called with transformation and shader states as parameters.
            // The default shader states are:
            // blendState:        dxScene.DXDevice.CommonStates.PremultipliedAlphaBlend
            // samplerState:      dxScene.DXDevice.CommonStates.ClampSampler
            // depthStencilState: dxScene.DXDevice.CommonStates.DepthNone
            // rasterizerState:   dxScene.DXDevice.CommonStates.CullNone
            spriteBatch.Begin();

            // Load png file and create a DirectX 11 ShaderResourceView
            var textureFileName = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"Resources\TreeTexture.png");
            var textureShaderResourceView = WpfMaterial.LoadTexture2D(dxScene.DXDevice, textureFileName);

            _disposables.Add(textureShaderResourceView);

            // Draw the textureShaderResourceView to specified destination: at coordinates (20, 20) and with destination size (128 x 128 pixels)
            spriteBatch.Draw(textureShaderResourceView, new RectangleF(20, 70, 100, 150)); // Image size is: 256 x 415



            // Use custom source rectangle to render from a part of source texture.
            textureFileName = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"Resources\10x10-texture.png");
            textureShaderResourceView = WpfMaterial.LoadTexture2D(dxScene.DXDevice, textureFileName);

            _disposables.Add(textureShaderResourceView);

            spriteBatch.Draw(textureShaderResourceView,
                sourceRectangle: new RectangleF(0.2f, 0.4f, 0.4f, 0.5f), // note that source rectangle is always specified in relative coordinates
                destinationRectangle: new RectangleF(20, 250, 128, 128),
                color: Color4.White,
                isDestinationRelative: false);



            // Load the texture from a DDS file
            textureFileName = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"Resources\ab4d-logo-220x220.DDS");
            _logoShaderResourceView = Ab3d.DirectX.TextureLoader.LoadShaderResourceView(dxScene.Device, textureFileName);

            _disposables.Add(_logoShaderResourceView);

            // Draw another sprite but use relative coordinates this time (show at the right size; size is 50% of the width and 50% of the height of the view)
            // Note that when window is resized, the sprite size will also change to preserve the relative size.
            spriteBatch.Draw(_logoShaderResourceView, new RectangleF(0.55f, 0.0f, 0.4f, 0.2f), isDestinationRelative: true);


            var textBlock = new TextBlock()
            {
                Text = "Rendered TEXT",
                Foreground = Brushes.Green,
                FontFamily = new FontFamily("Impact")
            };

            var renderedBitmap = Ab3d.Utilities.BitmapRendering.RenderToBitmap(textBlock, 256, 0);
            var renderedSpriteShaderResourceView = WpfMaterial.CreateTexture2D(dxScene.DXDevice, renderedBitmap);

            _disposables.Add(renderedSpriteShaderResourceView);

            spriteBatch.Draw(renderedSpriteShaderResourceView, new RectangleF(20, 400, 256, 64));



            // Use red color mask
            var redColor = new Color4(1, 0, 0, 1); // RGBA
            spriteBatch.Draw(_logoShaderResourceView, new RectangleF(0.75f, 0.2f, 0.2f, 0.2f), isDestinationRelative: true, color: redColor);




            // Use non-transparent rendering (change from PremultipliedAlphaBlend to Opaque BlendState)
            spriteBatch.SetBlendState(dxScene.DXDevice.CommonStates.Opaque);
            
            spriteBatch.Draw(_logoShaderResourceView, new RectangleF(0.75f, 0.4f, 0.2f, 0.2f), isDestinationRelative: true);



            // Use additive blending:
            spriteBatch.SetBlendState(dxScene.DXDevice.CommonStates.PremultipliedAdditiveBlend);

            spriteBatch.Draw(_logoShaderResourceView, new RectangleF(0.75f, 0.6f, 0.2f, 0.2f), isDestinationRelative: true);
            spriteBatch.Draw(_logoShaderResourceView, new RectangleF(0.76f, 0.6f, 0.2f, 0.2f), isDestinationRelative: true);
            spriteBatch.Draw(_logoShaderResourceView, new RectangleF(0.77f, 0.6f, 0.2f, 0.2f), isDestinationRelative: true);

            // We could also create a new sprite batch for additive blending:
            //
            //var additiveSpriteBatch = dxScene.CreateSpriteBatch("AdditiveSpriteBatch");
            //
            //additiveSpriteBatch.Begin(dxScene.DXDevice.CommonStates.PremultipliedAdditiveBlend);
            //additiveSpriteBatch.Draw(_logoShaderResourceView, new RectangleF(0.2f, 0.5f, 0.5f, 0.5f), isDestinationRelative: true);
            //additiveSpriteBatch.End();


            // See code and comments in UpdateAnimatedSprite method below to see how to use spriteBatch.SetTransform method


            // Complete this sprite batch
            spriteBatch.End();



            // Create a separate sprite batch for a sprite in the lower right corner.
            // The reason for this is that this sprite batch will need to be updated (sprite will be repositioned)
            // when the view size is changed. See UpdateBottomRightSprite method for more info.
            _bottomRightSpriteBatch = dxScene.CreateSpriteBatch("BottomRightSpriteBatch");
            _bottomRightSpriteSize = 128;

            UpdateBottomRightSprite();

            dxScene.SizeChanged += delegate (object sender, EventArgs args)
            {
                UpdateBottomRightSprite();
            };



            // Create a separate sprite batch for animated sprites (this sprite batch will be updated after each rendering):
            _animatedSpriteBatch = dxScene.CreateSpriteBatch("AnimatedSpriteBatch");

            // Load texture from Resources
            // This is done with first loading the texture into a WPF's BitmapImage object...
            var bitmap = new BitmapImage(new Uri("pack://application:,,,/Ab3d.DXEngine.Wpf.Samples;component/Resources/info_orange_icon.png"));
            // ... and then converting that to a DirectX 11 ShaderResourceView
            _animatedSpriteShaderResourceView = WpfMaterial.CreateTexture2D(dxScene.DXDevice, bitmap);

            _disposables.Add(_animatedSpriteShaderResourceView);

            _animatedSpritePosition = new SharpDX.Point(100, 180);
            UpdateAnimatedSprite();


            // Start animation
            _animationStartTime = DateTime.Now;
        }

        private void UpdateAnimatedSprite()
        {
            _animatedSpriteBatch.Begin(MainDXViewportView.DXScene.DXDevice.CommonStates.PremultipliedAlphaBlend);

            _animatedSpriteBatch.Draw(_animatedSpriteShaderResourceView, new RectangleF(_animatedSpritePosition.X, _animatedSpritePosition.Y, 16, 16));

            // To rotate the sprite around its center, make sure to define its destination rectangle so that the center of the sprite is at 0.5, 0.5
            // Then you will be able to move the sprite after rotating it with using Matrix.Translation.
            // Note that rotation is done in relative coordinates: x is in range from 0 to +2 (0 is left, 2 is right); y is in range from 0 to -2 (0 is top, -2 is bottom)
            _animatedSpriteBatch.SetTransform(SharpDX.Matrix.RotationZ(MathUtil.DegreesToRadians(_animatedRotationAngle)) *
                                              SharpDX.Matrix.Translation(0.1f, 0.85f, 0.0f));

            // Use red color mask
            // To rotate the sprite, make sure that its center is positioned at (0, 0):
            _animatedSpriteBatch.Draw(_logoShaderResourceView, new RectangleF(-0.1f, -0.1f, 0.2f, 0.2f), isDestinationRelative: true);

            _animatedSpriteBatch.End();
        }

        private void UpdateBottomRightSprite()
        {
            // To update the sprite batch we need to call Begin again
            _bottomRightSpriteBatch.Begin();

            // Update the position of the sprite
            _bottomRightSpriteBatch.Draw(_logoShaderResourceView, new RectangleF((float)MainDXViewportView.DXFinalPixelSize.Width - _bottomRightSpriteSize, (float)MainDXViewportView.DXFinalPixelSize.Height - _bottomRightSpriteSize, _bottomRightSpriteSize, _bottomRightSpriteSize));

            // End sprite batch
            _bottomRightSpriteBatch.End();
        }

        private void CompositionTargetOnRendering(object sender, EventArgs e)
        {
            if (_animationStartTime == DateTime.MinValue)
                return;

            var elapsedTime = DateTime.Now - _animationStartTime;

            _animatedSpritePosition.X = (int)((Math.Cos(elapsedTime.TotalSeconds) + 1) * 200.0) + 100;
            _animatedRotationAngle = (float)elapsedTime.TotalSeconds * 90f;

            UpdateAnimatedSprite();
        }
        
        private void SetBackgroundImageButton_OnClick(object sender, RoutedEventArgs e)
        {
            if (_backgroundSpriteShaderResourceView != null) // Background already set?
                return;

            var dxScene = MainDXViewportView.DXScene;

            // First create a new RenderingStep that will render sprites
            var renderSpritesRenderingStep = new RenderSpritesRenderingStep("BackgroundSpritesRenderingStep");

            // This step is added before other objects are rendered
            dxScene.RenderingSteps.AddBefore(dxScene.DefaultRenderObjectsRenderingStep, renderSpritesRenderingStep);


            // Create a new SpriteBatch (note that we specify the renderSpritesRenderingStep: if this is omitted then DefaultRenderSpritesRenderingStep is used) 
            var backgroundSpriteBatch = dxScene.CreateSpriteBatch(renderSpritesRenderingStep, "BackgroundSpriteBatch");


            // Load bitmap
            var fileName = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"Resources\SemiTransparentFrame.png");
            var backgroundBitmap = new BitmapImage(new Uri(fileName));

            // ... and then converting that to a DirectX 11 ShaderResourceView
            _backgroundSpriteShaderResourceView = WpfMaterial.CreateTexture2D(dxScene.DXDevice, backgroundBitmap);

            _disposables.Add(_backgroundSpriteShaderResourceView);

            // Render the sprite
            backgroundSpriteBatch.Begin();
            backgroundSpriteBatch.Draw(_backgroundSpriteShaderResourceView, new RectangleF(0, 0, 1, 1), isDestinationRelative: true); // render to the whole background
            backgroundSpriteBatch.End();
        }
    }
}
