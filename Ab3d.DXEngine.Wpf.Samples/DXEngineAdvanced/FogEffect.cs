using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Ab3d.DirectX;
using Ab3d.DirectX.Lights;
using Ab3d.DirectX.Materials;
using SharpDX;
using SharpDX.Direct3D11;
using Buffer = SharpDX.Direct3D11.Buffer;

namespace Ab3d.DXEngine.Wpf.Samples.DXEngineAdvanced
{
    public class FogEffect : Ab3d.DirectX.Effect
    {
        /// <summary>
        /// EffectName
        /// </summary>
        public const string EffectName = "FogEffect";

        /// <summary>
        /// Gets the input layout that is required to render this effect.
        /// </summary>
        public override InputLayoutType RequiredInputLayoutType
        {
            get { return InputLayoutType.Position | InputLayoutType.Normal; }
        }

        #region PerFrameCameraConstantBuffer
        // cbuffer cbPerFrameCamera
        // {
        //
        //   row_major float4x4 gViewProjection;// Offset:    0 Size:    64 [unused]
        //   float3 gEyePosW;                   // Offset:   64 Size:    12
        //
        // }
        [StructLayout(LayoutKind.Explicit, Size = PerFrameCameraConstantBuffer.SizeInBytes)]
        private struct PerFrameCameraConstantBuffer
        {
            [FieldOffset(0)]
            public Matrix ViewProjection;

            [FieldOffset(64)]
            public Vector3 EyePosW;

            [FieldOffset(76)]
            private float _dummy; // to meet the 16 bytes stride

            public const int SizeInBytes = 80;
        }
        #endregion

        #region PerFrameLightsConstantBuffer
        // cbuffer cbPerFrameLights
        // {
        //
        //   float3 gAmbientColor;              // Offset:    0 Size:    12
        //   float3 gLightDirection;            // Offset:   16 Size:    12
        //   float3 gLightColor;                // Offset:   32 Size:    12 [unused]
        //   float gFogStartW;                  // Offset:   44 Size:     4
        //   float gFogEndW;                    // Offset:   48 Size:     4
        //   float3 gFogColor;                  // Offset:   52 Size:    12
        //
        // }
        [StructLayout(LayoutKind.Explicit, Size = PerFrameLightsConstantBuffer.SizeInBytes)]
        private struct PerFrameLightsConstantBuffer
        {
            [FieldOffset(0)]
            public Color3 AmbientColor;

            [FieldOffset(16)]
            public Vector3 LightDirection;

            [FieldOffset(32)]
            public Color3 LightColor;

            [FieldOffset(44)]
            public float FogStart;

            [FieldOffset(48)]
            public float FogFullColorStart;

            [FieldOffset(52)]
            public Color3 FogColor;

            public const int SizeInBytes = 64;
        }
        #endregion

        #region PerObjectConstantBuffer
        // cbuffer cbPerObject
        // {
        //
        //   float4 gDiffuseColor;              // Offset:    0 Size:    16
        //   row_major float4x4 gWorld;         // Offset:   16 Size:    64 
        //   row_major float4x4 gWorldInverseTranspose;// Offset:   80 Size:    64
        //
        // }
        [StructLayout(LayoutKind.Explicit, Size = PerObjectConstantBuffer.SizeInBytes)]
        private struct PerObjectConstantBuffer
        {
            [FieldOffset(0)]
            public Color4 DiffuseColor;

            [FieldOffset(16)]
            public Matrix World;

            [FieldOffset(80)]
            public Matrix WorldInverseTranspose;

            public const int SizeInBytes = 144;
        }
        #endregion

        private PerFrameCameraConstantBuffer _perFrameCameraConstantsBufferData;
        private Buffer _perFrameCameraConstantsBuffer;

        private PerFrameLightsConstantBuffer _perFrameLightsConstantsBufferData;
        private Buffer _perFrameLightsConstantsBuffer;

        private PerObjectConstantBuffer _perObjectConstantsBufferData;
        private Buffer _perObjectConstantsBuffer;

        private bool _isLastWorldMatrixIdentity;
        private bool _isConstantBufferDirty;

        private RenderingContext _renderingContext;
        private ContextStatesManager _contextStatesManager;

        // If the shader is also used by other effects it is recommended to use the SharedDXResourceWrapper:
        //private SharedDXResourceWrapper<VertexShader> _vertexShaderResource;
        //private SharedDXResourceWrapper<PixelShader> _pixelShaderResource;
        //private SharedDXResourceWrapper<InputLayout> _inputLayoutResource;

        // But in our case the shaders are used only by this effect
        private VertexShader _vertexShader;
        private PixelShader _pixelShader;
        private InputLayout _inputLayout;



        /// <summary>
        /// Constructor
        /// </summary>
        public FogEffect()
            : base(EffectName)
        {
        }


        public void SetShaders(byte[] vertexShaderBytecode, byte[] pixelShaderBytecode)
        {
            // Dispose existing shaders
            DisposeShaders();


            // Create VertexShader
            _vertexShader = new VertexShader(parentDXDevice.Device, vertexShaderBytecode);


            // Create InputLayout
            var vertexLayoutDesc = InputElementFactory.GetInputElementsArray(InputLayoutType.Position | InputLayoutType.Normal | InputLayoutType.TextureCoordinate);
            _inputLayout = new InputLayout(parentDXDevice.Device, vertexShaderBytecode, vertexLayoutDesc);


            // Create Pixel shader
            _pixelShader = new PixelShader(parentDXDevice.Device, pixelShaderBytecode);


            // If we are using debug device we set DebugName - so we can see the correct name of shaders in Graphic debugging
            if (parentDXDevice.IsDebugDevice)
            {
                _vertexShader.DebugName = EffectName + "_VertexShader";
                _pixelShader.DebugName = EffectName + "_PixelShader";
                _inputLayout.DebugName = EffectName + "_InputLayout";
            }
        }

        /// <summary>
        /// SetFogData
        /// </summary>
        /// <param name="fogStart">distance from the camera where the fog starts</param>
        /// <param name="fogFullColorStart">distance from the camera when the fog hides all other objects and shows only fog color</param>
        /// <param name="fogColor">color of the fog</param>
        public void SetFogData(float fogStart, float fogFullColorStart, Color3 fogColor)
        {
            if (fogFullColorStart < fogStart)
                fogFullColorStart = fogStart;

            _perFrameLightsConstantsBufferData.FogStart = fogStart;
            _perFrameLightsConstantsBufferData.FogFullColorStart = fogFullColorStart;
            _perFrameLightsConstantsBufferData.FogColor = fogColor;
        }

        /// <summary>
        /// Initializes this effect.
        /// </summary>
        /// <param name="dxDevice">parent DXDevice</param>
        protected override void OnInitializeResources(DXDevice dxDevice)
        {
            if (IsInitialized)
                return; // Already initialized

            parentDXDevice = dxDevice;

            _perFrameCameraConstantsBufferData = new PerFrameCameraConstantBuffer();
            _perFrameCameraConstantsBuffer = parentDXDevice.CreateConstantBuffer(PerFrameCameraConstantBuffer.SizeInBytes, "SimpleDirectionalLightEffect_perFrameCameraConstantsBuffer");

            // Do not reset the PerFrameLightsConstantBuffer struct data - in case the SetFogData was called before this method
            //_perFrameLightsConstantsBufferData = new PerFrameLightsConstantBuffer();
            _perFrameLightsConstantsBuffer = parentDXDevice.CreateConstantBuffer(PerFrameLightsConstantBuffer.SizeInBytes, "SimpleDirectionalLightEffect_perFrameLightsConstantsBuffer");

            _perObjectConstantsBufferData = new PerObjectConstantBuffer();
            _perObjectConstantsBuffer = parentDXDevice.CreateConstantBuffer(PerObjectConstantBuffer.SizeInBytes, "SimpleDirectionalLightEffect_perObjectConstantsBuffer");

            base.OnInitializeResources(dxDevice);
        }

        /// <summary>
        /// Sets per frame settings for this effect (this sets camera, lights and other per frame settings).
        /// </summary>
        /// <param name="camera">camera</param>
        /// <param name="lights">list of lights</param>
        /// <param name="renderingContext">RenderingContext</param>
        protected override void OnApplyPerFrameSettings(ICamera camera, IList<ILight> lights, RenderingContext renderingContext)
        {
            _renderingContext = renderingContext;
            _contextStatesManager = renderingContext.ContextStatesManager;


            // Update per frame Camera constant buffer
            _perFrameCameraConstantsBufferData.ViewProjection = camera.GetViewProjection();
            _perFrameCameraConstantsBufferData.EyePosW = camera.GetCameraPosition();

            renderingContext.DeviceContext.UpdateSubresource(ref _perFrameCameraConstantsBufferData, _perFrameCameraConstantsBuffer);


            // Update per frame Lights constant buffer
            _perFrameLightsConstantsBufferData.AmbientColor = SetupAmbientLight(lights);

            var firstDirectionalLight = lights.OfType<IDirectionalLight>().FirstOrDefault();
            if (firstDirectionalLight == null)
            {
                // No directional light
                _perFrameLightsConstantsBufferData.LightColor = Color3.Black;
                _perFrameLightsConstantsBufferData.LightDirection = Vector3.Zero;
            }
            else
            {
                _perFrameLightsConstantsBufferData.LightColor = firstDirectionalLight.DiffuseColor;

                _perFrameLightsConstantsBufferData.LightDirection = firstDirectionalLight.Direction;
                _perFrameLightsConstantsBufferData.LightDirection.Normalize();
            }

            renderingContext.DeviceContext.UpdateSubresource(ref _perFrameLightsConstantsBufferData, _perFrameLightsConstantsBuffer);


            _perObjectConstantsBufferData.World = Matrix.Zero; // Set World so the next time the object's worldMatrix will be compared to this value it will not be threted as equal

            _isLastWorldMatrixIdentity = false;
            _isConstantBufferDirty = true;
        }

        /// <summary>
        /// Applies the material and object's world matrix to this effect.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>ApplyMaterial</b> applies the material and object's world matrix to this effect.
        /// </para>
        /// <para>
        /// Usually effects define two constant buffers:<br/>
        /// - one that is can be changed only once per frame and contains data about lights,<br/>
        /// - one that is different for each material and object.
        /// </para>
        /// <para>
        /// The first constant buffer is set by calling <see cref="OnApplyPerFrameSettings(ICamera, IList{ILight}, RenderingContext)"/> method.
        /// </para>
        /// <para>
        /// The second constant buffer can be set by calling <see cref="ApplyMaterial(Material, RenderablePrimitiveBase)"/> method.
        /// This sets properties defined in Material.
        /// It also sets projection matrixes like world_view_projection and others.
        /// The device states (blend state, rasterizer state, etc.) are also set there.
        /// </para>
        /// </remarks>
        /// <param name="material">Material</param>
        /// <param name="renderablePrimitive">object that the material is applied for (usually RenderablePrimitive).</param>
        public override void ApplyMaterial(Material material, RenderablePrimitiveBase renderablePrimitive)
        {
            bool hasAlphaBlend = false;

            _isConstantBufferDirty = false;

            if (_renderingContext == null)
                throw new DXEngineException("ApplyMaterial called without calling ApplyPerFrameSettings. This is usually caused by not registering the effect to EffectsManager.");

            DeviceContext deviceContext = _renderingContext.DeviceContext;
            
            // Material data:
            var diffuseMaterial = material as IDiffuseMaterial;
            Color4 newDiffuseColor;

            if (diffuseMaterial != null)
            {
                newDiffuseColor = new Color4(diffuseMaterial.DiffuseColor.Red, diffuseMaterial.DiffuseColor.Green,
                                             diffuseMaterial.DiffuseColor.Blue, diffuseMaterial.Alpha);

                if (diffuseMaterial.HasTransparency)
                    hasAlphaBlend = true;
            }
            else
            {
                newDiffuseColor = Color.Black;
            }

            if (_perObjectConstantsBufferData.DiffuseColor != newDiffuseColor)
            {
                _isConstantBufferDirty = true;
                _perObjectConstantsBufferData.DiffuseColor = newDiffuseColor;
            }

            // Working with matrixes is very slow - especially in .Net because we do not have hi perf SIMD instructions and 
            // Here we optimize the code for cases when the world matrix is identity 
            if (renderablePrimitive.IsWorldMatrixIdentity)
            {
                if (!_isLastWorldMatrixIdentity)
                {
                    _perObjectConstantsBufferData.World = Matrix.Identity;
                    _perObjectConstantsBufferData.WorldInverseTranspose = Matrix.Identity;

                    _isLastWorldMatrixIdentity = true;
                    _isConstantBufferDirty = true;
                }
                // else - this object has identity as world matrix and also last object had identity => no need to change anything
            }
            else
            {
                // Normal un-optimized code (world matrix is not identity)

                Matrix world = renderablePrimitive.WorldMatrix;

                if (_perObjectConstantsBufferData.World != world || _isLastWorldMatrixIdentity)
                {
                    _perObjectConstantsBufferData.World = world;

                    Matrix invertedWorldMatrix;
                    Matrix.Invert(ref world, out invertedWorldMatrix);
                    invertedWorldMatrix = Matrix.Transpose(invertedWorldMatrix);

                    _perObjectConstantsBufferData.WorldInverseTranspose = invertedWorldMatrix;

                    _isConstantBufferDirty = true;
                }

                _isLastWorldMatrixIdentity = false;
            }



            if (hasAlphaBlend)
                _contextStatesManager.BlendState = _renderingContext.CommonStates.PremultipliedAlphaBlend;
            else
                _contextStatesManager.BlendState = _renderingContext.CommonStates.Opaque;


            if (_isConstantBufferDirty)
                deviceContext.UpdateSubresource(ref _perObjectConstantsBufferData, _perObjectConstantsBuffer);


            // Set RasterizerState
            // The most important setting in RasterizerState is IsFrontCounterClockwise
            // Here is a place where we have all the info to set it.
            // On one side we can get the orientation of triangles in mesh (object's IsFrontCounterClockwise) and on the other the IBackFaceMaterial.IsBackFaceMaterial
            // Set RasterizerState based on object's IsFrontCounterClockwise (tells orientation of the triangles in the mesh) and IBackFaceMaterial.IsBackFaceMaterial
            bool isFrontCounterClockwise = renderablePrimitive.IsFrontCounterClockwise;

            // If IsBackFaceMaterial than flip the IsFrontCounterClockwise setting from the object
            if (renderablePrimitive.IsBackFaceMaterial)
                isFrontCounterClockwise = !isFrontCounterClockwise;

            var rasterizerState = isFrontCounterClockwise ? parentDXDevice.CommonStates.CullClockwise : parentDXDevice.CommonStates.CullCounterClockwise;
            _contextStatesManager.SetRasterizerState(rasterizerState, isFrontCounterClockwise);


            if (_vertexShader == null || _pixelShader == null || _inputLayout == null)
                throw new Exception("Shaders not initialized.");


            _contextStatesManager.InputLayout = _inputLayout;
            _contextStatesManager.GeometryShader = null;

            bool isVertexShaderChanged = _contextStatesManager.SetVertexShader(_vertexShader);
            bool isPixelShaderChanged = _contextStatesManager.SetPixelShader(_pixelShader);

            // Apply constant buffer only if vertex / pixel shader is changed or if constant buffer was changed
            // To see which constant buffers are bound to which slots see the debug file created by fxc:
            
            // For vertex shader:
            //
            // Resource Bindings:
            //
            // Name                                 Type  Format         Dim      HLSL Bind  Count
            // ------------------------------ ---------- ------- ----------- -------------- ------
            // cbPerFrameCamera                  cbuffer      NA          NA            cb0      1 
            // cbPerObject                       cbuffer      NA          NA            cb1      1 
            if (isVertexShaderChanged)
                _contextStatesManager.SetVertexShaderConstantBuffer(_perFrameCameraConstantsBuffer, 0, forceOverrideExistingBuffer: false);

            if (isVertexShaderChanged || _isConstantBufferDirty)
                _contextStatesManager.SetVertexShaderConstantBuffer(_perObjectConstantsBuffer, 2);


            // For pixel shader:
            // Resource Bindings:
            //
            // Name                                 Type  Format         Dim      HLSL Bind  Count
            // ------------------------------ ---------- ------- ----------- -------------- ------
            // cbPerFrameCamera                  cbuffer      NA          NA            cb0      1 
            // cbPerFrameLights                  cbuffer      NA          NA            cb1      1 
            // cbPerObject                       cbuffer      NA          NA            cb2      1 
            if (isPixelShaderChanged)
            {
                _contextStatesManager.SetPixelShaderConstantBuffer(_perFrameCameraConstantsBuffer, 0, forceOverrideExistingBuffer: false);
                _contextStatesManager.SetPixelShaderConstantBuffer(_perFrameLightsConstantsBuffer, 1, forceOverrideExistingBuffer: false);
            }

            if (isPixelShaderChanged || _isConstantBufferDirty)
                _contextStatesManager.SetPixelShaderConstantBuffer(_perObjectConstantsBuffer, 2);
        }

        /// <summary>
        /// PreloadShaders can be called to load the shaders in advance before they are used.
        /// Calling this method increases the startup time, but when the 3D object needs to be shown, it is shown faster because all the shaders have already been created.
        /// </summary>
        public override void PreloadShaders()
        {
            // Nothing to do here because the shaders are manually created by calling SetShaders method
        }

        private Color3 SetupAmbientLight(IList<ILight> lights)
        {
            Color3 ambientColor = Color3.Black;

            if (lights == null)
                return ambientColor;

            foreach (ILight oneLight in lights)
            {
                if (oneLight is IAmbientLight && oneLight.IsEnabled)
                    ambientColor += ((IAmbientLight)oneLight).Color;
            }

            return ambientColor;
        }

        /// <summary>
        /// Dispose
        /// </summary>
        /// <param name="disposing">disposing</param>
        protected override void Dispose(bool disposing)
        {
            try
            {
                if (disposing)
                {
                    DisposeShaders();
                    _contextStatesManager = null;
                }
            }
            finally
            {
                base.Dispose(disposing);
            }
        }


        private void DisposeShaders()
        {
            if (_vertexShader != null)
            {
                _vertexShader.Dispose();
                _vertexShader = null;
            }

            if (_pixelShader != null)
            {
                _pixelShader.Dispose();
                _pixelShader = null;
            }

            if (_inputLayout != null)
            {
                _inputLayout.Dispose();
                _inputLayout = null;
            }
        }
    }
}