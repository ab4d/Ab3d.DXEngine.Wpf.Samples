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
    public class MeshNormalEffect : Ab3d.DirectX.Effect
    {
        /// <summary>
        /// EffectName
        /// </summary>
        public const string EffectName = "MeshNormalEffect";

        /// <summary>
        /// Gets the input layout that is required to render this effect.
        /// </summary>
        public override InputLayoutType RequiredInputLayoutType
        {
            get { return InputLayoutType.Position | InputLayoutType.Normal; }
        }

        #region PerFrameCameraConstantBuffer
        // See MeshNormalShader.vs.txt:
        // cbuffer cbPerFrameCamera
        // {
        //
        //   row_major float4x4 gViewProjection;// Offset:    0 Size:    64
        //   float3 gEyePosW;                   // Offset:   64 Size:    12 [unused]
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
        //
        // }
        [StructLayout(LayoutKind.Explicit, Size = PerFrameLightsConstantBuffer.SizeInBytes)]
        private struct PerFrameLightsConstantBuffer
        {
            [FieldOffset(0)]
            public Color3 AmbientColor;

            [FieldOffset(12)]
            private float _dummy; // to meet the 16 bytes stride

            public const int SizeInBytes = 16;
        }
        #endregion

        #region PerObjectConstantBuffer
        // cbuffer cbPerObject
        // {
        //
        //   float3 gColorMask;                 // Offset:    0 Size:    12
        //   row_major float4x4 gWorld;         // Offset:   16 Size:    64 [unused]
        //   row_major float4x4 gWorldInverseTranspose;// Offset:   80 Size:    64 [unused]
        //
        // }
        [StructLayout(LayoutKind.Explicit, Size = PerObjectConstantBuffer.SizeInBytes)]
        private struct PerObjectConstantBuffer
        {
            [FieldOffset(0)]
            public Color3 ColorMask;

            [FieldOffset(12)]
            private float _dummy; // to meet the 16 bytes stride

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
        private SharedDXResourceWrapper<VertexShader> _vertexShaderSharedResource;
        private SharedDXResourceWrapper<InputLayout> _inputLayoutSharedResource;
        private SharedDXResourceWrapper<PixelShader> _pixelShaderSharedResource;


        /// <summary>
        /// Constructor
        /// </summary>
        public MeshNormalEffect()
            : base(EffectName)
        {
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



            // The recommended way to create shaders is to use GetShaders method on EffectsManager (or GetVertexShader, GetPixelShader or other methods - see below).
            // In order for EffectsManager to get the shader resources, we need to RegisterShaderResource before the shaders can be created.
            // In this case the code in CustomShaderMaterialSample.xaml.cs does that. The following code is used:
            //var directoryShaderBytecodeProvider = new DirectoryShaderBytecodeProvider(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources\\Shaders"));
            //dxDevice.EffectsManager.RegisterShaderResource(directoryShaderBytecodeProvider);

            var vertexLayoutDesc = InputElementFactory.GetInputElementsArray(InputLayoutType.Position | InputLayoutType.Normal);

            dxDevice.EffectsManager.GetShaders(
                "MeshNormalShader.vs", "MeshNormalShader.ps", vertexLayoutDesc,
                out _vertexShaderSharedResource, out _pixelShaderSharedResource, out _inputLayoutSharedResource, 
                throwExceptionIfShadersNotFound: true);


            // You could also use GetVertexShader and GetPixelShader methods:
            //_vertexShaderSharedResource = dxDevice.EffectsManager.GetVertexShader("MeshNormalShader.vs", vertexLayoutDesc, out _inputLayoutSharedResource, throwExceptionIfNotFound: true);
            //_pixelShaderSharedResource = dxDevice.EffectsManager.GetPixelShader("MeshNormalShader.ps", throwExceptionIfNotFound: true);


            // You could also use simple shader constructors to create the shaders:
            //// Create VertexShader
            //byte[] vertexShaderBytecode = System.IO.File.ReadAllBytes(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources\\Shaders\\MeshNormalShader.vs"));
            //_vertexShader = new VertexShader(parentDXDevice.Device, vertexShaderBytecode);

            //// Create InputLayout
            //_inputLayout = new InputLayout(parentDXDevice.Device, vertexShaderBytecode, vertexLayoutDesc);

            //// Create Pixel shader
            //byte[] pixelShaderBytecode = System.IO.File.ReadAllBytes(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources\\Shaders\\MeshNormalShader.ps"));
            //_pixelShader = new PixelShader(parentDXDevice.Device, pixelShaderBytecode);

            //// If we are using debug device we set DebugName - so we can see the correct name of shaders in Graphic debugging
            //if (parentDXDevice.IsDebugDevice)
            //{
            //    _vertexShader.DebugName = EffectName + "_VertexShader";
            //    _inputLayout.DebugName = EffectName + "_InputLayout";
            //    _pixelShader.DebugName = EffectName + "_PixelShader";
            //}


            _perFrameCameraConstantsBufferData = new PerFrameCameraConstantBuffer();
            _perFrameCameraConstantsBuffer = parentDXDevice.CreateConstantBuffer(PerFrameCameraConstantBuffer.SizeInBytes, EffectName + "_perFrameCameraConstantsBuffer");

            _perFrameLightsConstantsBufferData = new PerFrameLightsConstantBuffer();
            _perFrameLightsConstantsBuffer = parentDXDevice.CreateConstantBuffer(PerFrameLightsConstantBuffer.SizeInBytes, EffectName + "_perFrameLightsConstantsBuffer");

            _perObjectConstantsBufferData = new PerObjectConstantBuffer();
            _perObjectConstantsBuffer = parentDXDevice.CreateConstantBuffer(PerObjectConstantBuffer.SizeInBytes, EffectName + "_perObjectConstantsBuffer");

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
            _isConstantBufferDirty = false;

            if (_renderingContext == null)
                throw new DXEngineException("SetMaterialConstantBuffer called without calling ApplyPerFrameSettings. This is usually caused by not registering the effect to EffectsManager.");

            DeviceContext deviceContext = _renderingContext.DeviceContext;

            // Material data:
            var colorMaskMaterial = material as IColorMaskMaterial;

            if (colorMaskMaterial != null)
                _perObjectConstantsBufferData.ColorMask = colorMaskMaterial.ColorMask;
            else
                _perObjectConstantsBufferData.ColorMask = Color3.White; // No mask - show full normal color


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


            // If your material support alpha blending, you can set it here:
            //if (hasAlphaBlend)
            //    _contextStatesManager.BlendState = _renderingContext.CommonStates.PremultipliedAlphaBlend;
            //else
            //    _contextStatesManager.BlendState = _renderingContext.CommonStates.Opaque;


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


            if (_vertexShaderSharedResource == null || _pixelShaderSharedResource == null || _inputLayoutSharedResource == null)
                throw new Exception("Shaders not initialized.");


            _contextStatesManager.InputLayout = _inputLayoutSharedResource.Resource;
            _contextStatesManager.GeometryShader = null;

            bool isVertexShaderChanged = _contextStatesManager.SetVertexShader(_vertexShaderSharedResource.Resource);
            bool isPixelShaderChanged = _contextStatesManager.SetPixelShader(_pixelShaderSharedResource.Resource);

            // Apply constant buffer only if vertex / pixel shader is changed or if constant buffer was changed
            // To see which constant buffers are bound to which slots see the debug file created by fxc:

            // For vertex shader:
            //
            // Resource Bindings:
            //
            // Name                                 Type  Format         Dim      HLSL Bind  Count
            // ------------------------------ ---------- ------- ----------- -------------- ------
            // cbPerFrameCamera                  cbuffer      NA          NA            cb0      1 
            // cbPerObject                       cbuffer      NA          NA            cb2      1 
            if (isVertexShaderChanged)
                _contextStatesManager.SetVertexShaderConstantBuffer(_perFrameCameraConstantsBuffer, 0, forceOverrideExistingBuffer: false);

            if (isVertexShaderChanged || _isConstantBufferDirty)
                _contextStatesManager.SetVertexShaderConstantBuffer(_perObjectConstantsBuffer, 2);


            // For pixel shader:
            // Resource Bindings:
            //
            // Name                                 Type  Format         Dim      HLSL Bind  Count
            // ------------------------------ ---------- ------- ----------- -------------- ------
            // cbPerFrameLights                  cbuffer      NA          NA            cb1      1 
            // cbPerObject                       cbuffer      NA          NA            cb2      1 
            if (isPixelShaderChanged)
                _contextStatesManager.SetPixelShaderConstantBuffer(_perFrameLightsConstantsBuffer, 1, forceOverrideExistingBuffer: false);

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

                    Ab3d.DirectX.DisposeHelper.DisposeAndNullify(ref _perFrameCameraConstantsBuffer);
                    Ab3d.DirectX.DisposeHelper.DisposeAndNullify(ref _perFrameLightsConstantsBuffer);
                    Ab3d.DirectX.DisposeHelper.DisposeAndNullify(ref _perObjectConstantsBuffer);

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
            Ab3d.DirectX.DisposeHelper.DisposeAndNullify(ref _vertexShaderSharedResource);

            // This is the same as:
            if (_vertexShaderSharedResource != null)
            {
                _vertexShaderSharedResource.Dispose();
                _vertexShaderSharedResource = null;

            }

            Ab3d.DirectX.DisposeHelper.DisposeAndNullify(ref _inputLayoutSharedResource);
            Ab3d.DirectX.DisposeHelper.DisposeAndNullify(ref _pixelShaderSharedResource);
        }
    }
}