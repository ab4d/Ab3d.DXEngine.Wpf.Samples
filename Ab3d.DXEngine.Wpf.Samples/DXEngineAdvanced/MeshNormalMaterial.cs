using Ab3d.DirectX;

#if SHARPDX
using SharpDX;
#endif

namespace Ab3d.DXEngine.Wpf.Samples.DXEngineAdvanced
{
    public class MeshNormalMaterial : Ab3d.DirectX.Material, IColorMaskMaterial
    {
        public Color3 ColorMask { get; set; }

        /// <summary>
        /// Initializes resources.
        /// </summary>
        /// <param name="dxDevice">Parent DXDevice used to initialize resources</param>
        protected override void OnInitializeResources(DXDevice dxDevice)
        {
            // Set this.Effect to an instance of MyEffect. 
            // This will tell DXEngine to use MyEffect to render this material.
            // An instance of MyEffect is get by using GetEffect method on EffectsManager. 
            // This will also register MyEffect on EffectsManager if it was not registered before (using Activator to create an instance of MyEffect)
            this.Effect = dxDevice.EffectsManager.GetEffect<MeshNormalEffect>();


            // To provide custom settings when creating an instance of MeshNormalEffect, you can use the following code to customize the creation of MeshNormalEffect
            //var meshNormalEffect = dxDevice.EffectsManager.GetEffect<MeshNormalEffect>(createNewEffectInstanceIfNotFound: false);

            //if (meshNormalEffect == null)
            //{
            //    meshNormalEffect = new MeshNormalEffect(myEffectSettings);
            //    dxDevice.EffectsManager.RegisterEffect(meshNormalEffect);
            //}

            //this.Effect = meshNormalEffect;


            // Another option is to register your effect in the DXEngine initialization process.
            // This is done in the class where you create the DXViewportView:
            //MainDXViewportView.DXSceneDeviceCreated += delegate (object sender, EventArgs args)
            //{
            //    var dxScene = MainDXViewportView.DXScene;

            //    if (dxScene == null) // Probably using WPF 3D rendering
            //        return;

            //    // Create a new instance of MyEffect
            //    var meshNormalEffect = new MeshNormalEffect();

            //    // Register effect with EffectsManager - this will also initialize the effect with calling OnInitializeResources method in the MyEffect class
            //    dxScene.DXDevice.EffectsManager.RegisterEffect(meshNormalEffect);
            //};
        }

        protected override void Dispose(bool disposing)
        {
            if (Effect != null)
            {
                Effect.Dispose(); // Decrease reference counting in the Effect object
                Effect = null;
            }

            base.Dispose(disposing);
        }
    }
}