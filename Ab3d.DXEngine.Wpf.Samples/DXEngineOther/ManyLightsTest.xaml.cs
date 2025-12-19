using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace Ab3d.DXEngine.Wpf.Samples.DXEngineOther
{
    /// <summary>
    /// Interaction logic for ManyLightsTest.xaml
    /// </summary>
    public partial class ManyLightsTest : Page
    {
        // The following constants define how many lights there will be and what is the maximum light intensity
        private const int XLightsCount = 8;
        private const int YLightsCount = 8;

        private const double MaxLightIntensity = 0.5;

        private DateTime _startTime;

        // NOTES related to rendering many lights in Ab3d.DXEngine
        //
        // Ab3d.DXEngine can render ambient light and 16 other lights (Directional, Point and Spot lights) in one rendering pass
        // (maximum number of lights is defined in the following constant Ab3d.DirectX.Shaders.SuperShader.MaxLights).
        // When more lights are defined in the scene, the lights are rendered with multiple rendering passes.
        // For example this sample is rendered with 4 rendering passes (8 * 8 = 64 lights; 64 / 16 = 4 passes).
        // This means that the scene is first rendered with ambient light and first 16 lights.
        // Then all the scene objects are rendered again with the next 16 lights.
        // The pixel colors that are rendered this time are added to the pixels that were rendered in the previous pass (this is additive blending).
        // Then the next pass is rendered until all lights are used.
        //
        // This works well for most of the cases.
        // But additive blending also have some limitations:
        // 1) When multiple lights overlap the colors can become overburned (too white).
        //    This can happen because each pass just adds the colors to the previous color.
        //    For example if one object has Diffuse color set to (255, 150, 0) and is fully illuminated by two lights in two different rendering passes,
        //    the final color will be (255 + 255, 150 + 150, 0 + 0) = (255, 250, 0) which will be too bright (green = 255 instead of 150).
        // 
        //    One way to prevent that is to use only 16 lights (remove the lights that are far away or have minimal effect on the scene).
        //    You can also use the FilterLightsFunction property on RenderObjectsRenderingStep (you can get it from DXScene.RenderingSteps collection) -
        //    the FilterLightsFunction can dynamically remove the lights that are not valid for the current scene (this is more efficient than adding and removing the lights from the scene).
        // 
        //    Another way to prevented this is with manually arranging the lights so that the lights that overlay (shine on the same area) are rendered in the same
        //    rendering step (remember that 16 lights are rendered in one step). To do this you might need to add some dummy lights (PointLight with almost black color and radius 1).
        //
        //    It is also possible to increase the number of lights rendered in one rendering pass.
        //    To do this you will need to purchase the source code for the effects and shaders. Then please contact me and I will instruct you on how to
        //    change and compile the shader to accept more then 16 lights and render them in one rendering pass.
        //
        //
        // 2) Semi-transparent objects may not be rendered correctly.
        //    The problem is that multipass rendering required additive blending where pixel colors from rendering passes are added to the previous pixel color.
        //    But rendering transparent pixels require alpha blending where the final pixel color is determined by the alpha value of the pixel.
        //    Those two modes cannot be used at the same time so in some cases the transparent objects are not rendered correctly.
        //
        //
        // It is also possible to disable multi-pass rendering with setting the AllowMultipassRendering property to false.
        // The property is defined on RenderObjectsRenderingStep (you can get it from DXScene.RenderingSteps collection).

        public ManyLightsTest()
        {
            InitializeComponent();

            CreateLights();

            _startTime = DateTime.Now;
            CompositionTarget.Rendering += CompositionTargetOnRendering;

            this.Unloaded += delegate(object sender, RoutedEventArgs args)
            {
                CompositionTarget.Rendering -= CompositionTargetOnRendering;

                MainDXViewportView.Dispose();
            };
        }

        private void CompositionTargetOnRendering(object sender, EventArgs eventArgs)
        {
            double elapsedSeconds = (DateTime.Now - _startTime).TotalSeconds;
            AdjustAllLightsIntensity(elapsedSeconds);
        }

        private void CreateLights()
        {
            var lightsModelGroup = new Model3DGroup();

            double totalWidth = BoxVisual3D.Size.X;
            double totalHeight = BoxVisual3D.Size.Z;

            double usedWidth = totalWidth * 0.9;
            double usedHeight = totalHeight * 0.9;

            double halfWidth = usedWidth / 2;
            double halfHeight = usedHeight / 2;


            double xStep = usedWidth / (double)XLightsCount;
            double yStep = usedHeight / (double)YLightsCount;

            double xStart = -halfWidth + xStep / 2;
            double yStart = -halfHeight + yStep / 2;

            double lightHeight = BoxVisual3D.CenterPosition.Y + BoxVisual3D.Size.Y / 2 + 5;

            double xPos = xStart;
            for (int x = 0; x < XLightsCount; x++)
            {
                double yPos = yStart;

                for (int y = 0; y < YLightsCount; y++)
                {
                    Point3D lightPosition = new Point3D(xPos, lightHeight, yPos);

                    Color lightColor = Colors.White;
                    lightColor = Color.FromRgb((byte)(lightColor.R * MaxLightIntensity), (byte)(lightColor.G * MaxLightIntensity), (byte)(lightColor.B * MaxLightIntensity));

                    var pointLight = new System.Windows.Media.Media3D.PointLight(lightColor, lightPosition);
                    pointLight.Range = xStep * 6; // Defining the range improves performance with skipping lighting calculations for pixels that are out of range

                    lightsModelGroup.Children.Add(pointLight);

                    yPos += yStep;
                }

                xPos += xStep;
            }

            LightsModelVisual.Content = lightsModelGroup;

            AdjustAllLightsIntensity(0);
        }

        private void AdjustAllLightsIntensity(double time)
        {
            var model3DGroup = LightsModelVisual.Content as Model3DGroup;

            if (model3DGroup == null)
                return;

            foreach (var pointLight in model3DGroup.Children.OfType<System.Windows.Media.Media3D.PointLight>())
            {
                AdjustLightIntensity(pointLight, time);
            }
        }

        private void AdjustLightIntensity(System.Windows.Media.Media3D.PointLight pointLight, double time)
        {
            Point3D lightPosition = pointLight.Position;

            double distanceFromCenter = Math.Sqrt(lightPosition.X * lightPosition.X + lightPosition.Z * lightPosition.Z);

            double maxDistance = Math.Sqrt(BoxVisual3D.Size.X * BoxVisual3D.Size.X + BoxVisual3D.Size.Z * BoxVisual3D.Size.Z);

            double relativeDistance = distanceFromCenter / maxDistance;

            // Multiply relativeDistance with 2 instead of 2 to get two cos cycles on the whole range
            // device time by 2 to get one cycle per 2 seconds
            double t = relativeDistance * 3 - time * 0.5;

            // Use Cosinus to start with 1 at the center 
            // adjust to make the value between 0 and 1
            double lightIntensity = (Math.Cos(t * 2.0 * Math.PI) + 1.0) * 0.5; 

            lightIntensity *= MaxLightIntensity;

            Color lightColor = Colors.White;
            lightColor = Color.FromRgb((byte)(lightColor.R * lightIntensity), (byte)(lightColor.G * lightIntensity), (byte)(lightColor.B * lightIntensity));

            pointLight.Color = lightColor;
        }
    }
}
