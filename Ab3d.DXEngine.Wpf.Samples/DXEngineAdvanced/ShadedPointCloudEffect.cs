using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.InteropServices;
using Ab3d.DirectX;
using Ab3d.DirectX.Lights;

#if SHARPDX
using SharpDX;
using SharpDX.Direct3D;
using SharpDX.Direct3D11;
using Buffer = SharpDX.Direct3D11.Buffer;
#endif

namespace Ab3d.DXEngine.Wpf.Samples.DXEngineAdvanced
{
    public class ShadedPointCloudEffect : Effect
    {
        /// <summary>
        /// Gets or sets a Size value that specifies the size of each pixel in world space (not in 2D screen space).
        /// </summary>
        public float PointSize;

        /// <summary>
        /// Gets or sets a Color4 value that specifies the global pixel color. This color is multiplied with the color specified for each pixel.
        /// This value also specifies the alpha color value for all pixels.
        /// </summary>
        public Color4 DiffuseColor { get; set; }

        public Color3 SpecularColor { get; set; }

        public float SpecularPower { get; set; }

        /// <summary>
        /// EffectName
        /// </summary>
        public const string EffectName = "ShadedPointCloudEffect";

        // From: ShadedPointCloud.vs.txt
        // cbuffer cbVertexBuffer
        // {
        //
        //   row_major float4x4 gWorld;         // Offset:    0 Size:    64
        //   row_major float4x4 gWorldViewProjection;// Offset:   64 Size:    64
        //   row_major float4x4 gWorldInverseTranspose;// Offset:  128 Size:    64
        //   float3 gEyePosW;                   // Offset:  192 Size:    12
        //   float4 gDiffuseColor;              // Offset:  208 Size:    16
        //   float4 gSpecularColorPower;        // Offset:  224 Size:    16
        //   float3 gAmbientColor;              // Offset:  240 Size:    12
        //   float3 gLightDirection;            // Offset:  256 Size:    12
        //   float3 gLightColor;                // Offset:  272 Size:    12
        //
        // }
        [StructLayout(LayoutKind.Explicit, Size = VertexShaderConstantBuffer.SizeInBytes)]
        private struct VertexShaderConstantBuffer
        {
            [FieldOffset(0)]
            public Matrix World;

            [FieldOffset(64)]
            public Matrix WorldViewProjection;

            [FieldOffset(128)]
            public Matrix WorldInverseTranspose;

            [FieldOffset(192)]
            public Vector3 EyePos;

            [FieldOffset(208)]
            public Color4 DiffuseColor;

            [FieldOffset(224)]
            public Vector4 SpecularColorPower;

            [FieldOffset(240)]
            public Color3 AmbientColor;

            [FieldOffset(256)]
            public Vector3 LightDirection;

            [FieldOffset(272)]
            public Color3 LightColor;

            public const int SizeInBytes = 284;
        }

        private struct GeometryShaderConstantBuffer
        {
            public float HalfPixelSizeXPerViewport;
            public float HalfPixelSizeYPerViewport;

            public const int SizeInBytes = 8;
        }


        #region Private fields
        private VertexShaderConstantBuffer _vertexShaderConstantBufferData;
        private Buffer _vertexShaderConstantBuffer;

        private GeometryShaderConstantBuffer _geometryShaderConstantBufferData;
        private Buffer _geometryShaderConstantBuffer;

        private DXDevice _dxDevice;

        private Matrix _frameViewProjection;

        private RenderingContext _renderingContext;

        private SharedDXResourceWrapper<VertexShader> _vertexShaderSharedResource;
        private SharedDXResourceWrapper<InputLayout> _inputLayoutSharedResource;

        private SharedDXResourceWrapper<VertexShader> _vertexShaderPerPointColorSharedResource;
        private SharedDXResourceWrapper<InputLayout> _inputLayoutPerPointColorSharedResource;

        private SharedDXResourceWrapper<GeometryShader> _geometryShaderSharedResource;
        private SharedDXResourceWrapper<PixelShader> _pixelShaderSharedResource;

        #endregion

        /// <summary>
        /// Gets the input layout that is required to render this effect.
        /// </summary>
        public override InputLayoutType RequiredInputLayoutType
        {
            get { return InputLayoutType.Position | InputLayoutType.Normal; }
        }

        #region Constructor, OnInitializeResources
        /// <summary>
        /// Constructor
        /// </summary>
        public ShadedPointCloudEffect()
            : this(EffectName)
        {
        }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="effectName">effectName</param>
        protected ShadedPointCloudEffect(string effectName)
            : base(effectName)
        {
            // Set default values
            PointSize = 1;
            DiffuseColor = Color4.White;
            SpecularColor = Color3.White;
            SpecularPower = 0;

            _vertexShaderConstantBufferData = new VertexShaderConstantBuffer();
            _geometryShaderConstantBufferData = new GeometryShaderConstantBuffer();
        }

        /// <summary>
        /// Initializes resources.
        /// </summary>
        /// <param name="dxDevice">Parent DXDevice used to initialize resources</param>
        protected override void OnInitializeResources(DXDevice dxDevice)
        {
            if (IsInitialized)
                return; // Already initialized

            _dxDevice = dxDevice;

            EnsureShaders(dxDevice, usePerPointColor: true);
            EnsureConstantBuffers(dxDevice);
        }

        private void EnsureShaders(DXDevice dxDevice, bool usePerPointColor)
        {
            if (usePerPointColor)
            {
                if (_vertexShaderPerPointColorSharedResource == null)
                {
                    // InputLayoutType.Color3 will be supported in the next version of DXEngine. Then it will be possible to use:
                    var vertexLayoutDesc = InputElementFactory.GetInputElementsArray(InputLayoutType.Position | InputLayoutType.Normal | InputLayoutType.Color3);
                    
                    _vertexShaderPerPointColorSharedResource = dxDevice.EffectsManager.GetVertexShader("ShadedPointCloudPerPointColor.vs",
                        vertexLayoutDesc,
                        out _inputLayoutPerPointColorSharedResource,
                        throwExceptionIfNotFound: true);
                }
            }
            else
            {
                if (_vertexShaderSharedResource == null)
                {
                    var vertexLayoutDesc = InputElementFactory.GetInputElementsArray(InputLayoutType.Position | InputLayoutType.Normal);

                    _vertexShaderSharedResource = dxDevice.EffectsManager.GetVertexShader("ShadedPointCloud.vs",
                        vertexLayoutDesc,
                        out _inputLayoutSharedResource,
                        throwExceptionIfNotFound: true);
                }
            }

            if (_geometryShaderSharedResource == null)
                _geometryShaderSharedResource = dxDevice.EffectsManager.GetGeometryShader("ShadedPointCloud.gs", throwExceptionIfNotFound: true);

            if (_pixelShaderSharedResource == null)
                _pixelShaderSharedResource = dxDevice.EffectsManager.GetPixelShader("ShadedPointCloud.ps", throwExceptionIfNotFound: true);
        }

        private void EnsureConstantBuffers(DXDevice dxDevice)
        {
            if (_vertexShaderConstantBuffer == null)
            {
                _vertexShaderConstantBufferData = new VertexShaderConstantBuffer();
                _vertexShaderConstantBuffer = dxDevice.CreateConstantBuffer(VertexShaderConstantBuffer.SizeInBytes, "PointCloud_VertexShaderConstantBuffer");
            }

            if (_geometryShaderConstantBuffer == null)
            {
                _geometryShaderConstantBufferData = new GeometryShaderConstantBuffer();
                _geometryShaderConstantBuffer = dxDevice.CreateConstantBuffer(GeometryShaderConstantBuffer.SizeInBytes, "PointCloud_GeometryShaderConstantBuffer");
            }
        }
        #endregion

        #region ApplyPerFrameSettings

        /// <summary>
        /// Sets per frame settings for this effect (this sets camera, lights and other per frame settings).
        /// </summary>
        /// <param name="camera">camera</param>
        /// <param name="lights">list of lights</param>
        /// <param name="renderingContext">RenderingContext</param>
        protected override void OnApplyPerFrameSettings(ICamera camera, IList<ILight> lights, RenderingContext renderingContext)
        {
            _renderingContext = renderingContext;

            // Do the calculation here to optimize calculating worldViewProjection in ApplyMaterial
            _frameViewProjection = camera.GetViewProjection();

            // _viewportWidthFactor and _viewportHeightFactor are used to convert LineThickness into normalized viewport units (1.0 = width; 0.5 = half width; etc)
            float viewportWidthFactor = renderingContext.DXScene.DpiScaleX / renderingContext.CurrentViewport.Width;
            float viewportHeightFactor = renderingContext.DXScene.DpiScaleY / renderingContext.CurrentViewport.Height;

            float newHalfPixelSizeXPerViewport = PointSize * viewportWidthFactor;
            float newHalfPixelSizeYPerViewport = PointSize * viewportHeightFactor;

            if (_geometryShaderConstantBufferData.HalfPixelSizeXPerViewport != newHalfPixelSizeXPerViewport ||
                _geometryShaderConstantBufferData.HalfPixelSizeYPerViewport != newHalfPixelSizeYPerViewport)
            {
                _geometryShaderConstantBufferData.HalfPixelSizeXPerViewport = newHalfPixelSizeXPerViewport;
                _geometryShaderConstantBufferData.HalfPixelSizeYPerViewport = newHalfPixelSizeYPerViewport;

                _renderingContext.DeviceContext.UpdateSubresource(ref _geometryShaderConstantBufferData, _geometryShaderConstantBuffer);
            }


            _vertexShaderConstantBufferData.EyePos = camera.GetCameraPosition();


            // Update per frame Lights constant buffer
            _vertexShaderConstantBufferData.AmbientColor = CollectAmbientLight(lights);

            var firstDirectionalLight = lights.OfType<IDirectionalLight>().FirstOrDefault();
            if (firstDirectionalLight == null)
            {
                // No directional light
                _vertexShaderConstantBufferData.LightColor = Color3.Black;
                _vertexShaderConstantBufferData.LightDirection = Vector3.Zero;
            }
            else
            {
                _vertexShaderConstantBufferData.LightColor = firstDirectionalLight.DiffuseColor;

                _vertexShaderConstantBufferData.LightDirection = firstDirectionalLight.Direction;
                _vertexShaderConstantBufferData.LightDirection.Normalize();
            }
        }

        private Color3 CollectAmbientLight(IList<ILight> lights)
        {
            Color3 ambientColor = Color3.Black;

            foreach (ILight oneLight in lights)
            {
                if (oneLight is IAmbientLight && oneLight.IsEnabled)
                    ambientColor += ((IAmbientLight)oneLight).Color;
            }

            return ambientColor;
        }
        #endregion

        #region ApplyMaterial

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
        /// The first constant buffer is set by calling <see cref="Effect.ApplyPerFrameSettings(ICamera, IList{ILight}, RenderingContext)"/> method.
        /// </para>
        /// <para>
        /// The second constant buffer can be set by calling <see cref="ApplyMaterial(Material, RenderablePrimitiveBase)"/> method.
        /// This sets properties defined in Material.
        /// It also sets projection matrices like world_view_projection and others.
        /// The device states (blend state, rasterizer state, etc.) are also set there.
        /// </para>
        /// </remarks>
        /// <param name="material">Material</param>
        /// <param name="renderableGeometry">object that the material is applied for (usually RenderablePrimitive).</param>
        [SuppressMessage("ReSharper", "CompareOfFloatsByEqualityOperator")]
        public override void ApplyMaterial(Material material, RenderablePrimitiveBase renderableGeometry)
        {
            if (_renderingContext == null)
                throw new DXEngineException("ApplyMaterial called without calling ApplyPerFrameSettings. This is usually caused by not registering the effect to EffectsManager.");


            _vertexShaderConstantBufferData.DiffuseColor = this.DiffuseColor;
            _vertexShaderConstantBufferData.SpecularColorPower = new Vector4(this.SpecularColor.Red, this.SpecularColor.Green, this.SpecularColor.Blue, this.SpecularPower);

            if (renderableGeometry.IsWorldMatrixIdentity)
            {
                _vertexShaderConstantBufferData.World = Matrix.Identity;
                _vertexShaderConstantBufferData.WorldInverseTranspose = Matrix.Identity;
                _vertexShaderConstantBufferData.WorldViewProjection = _frameViewProjection; // Note: we use row_major declaration for matrix in HLSL
            }
            else
            {
                // Note: we use row_major declaration for matrix in HLSL so we do not need to transpose the matrix here
                _vertexShaderConstantBufferData.World = renderableGeometry.WorldMatrix;                                 
                _vertexShaderConstantBufferData.WorldInverseTranspose = renderableGeometry.WorldInverseTransposeMatrix;
                _vertexShaderConstantBufferData.WorldViewProjection = renderableGeometry.WorldMatrix * _frameViewProjection; 
            }

            _renderingContext.DeviceContext.UpdateSubresource(ref _vertexShaderConstantBufferData, _vertexShaderConstantBuffer);


            var statesManager = _renderingContext.ContextStatesManager;


            var renderablePrimitive = renderableGeometry as RenderablePrimitive;
            if (renderablePrimitive != null && (renderablePrimitive.InputLayoutType & (InputLayoutType.Color3 | InputLayoutType.Color4)) != 0)
            {
                // Per point color
                EnsureShaders(_renderingContext.DXDevice, usePerPointColor: true);

                statesManager.InputLayout = _inputLayoutPerPointColorSharedResource.Resource;
                statesManager.SetVertexShader(_vertexShaderPerPointColorSharedResource.Resource);
            }
            else
            {
                EnsureShaders(_renderingContext.DXDevice, usePerPointColor: false);

                statesManager.InputLayout = _inputLayoutSharedResource.Resource;
                statesManager.SetVertexShader(_vertexShaderSharedResource.Resource);
            }

            statesManager.SetVertexShaderConstantBuffer(_vertexShaderConstantBuffer, 2); // Set third slot - see Resource Bindings in PointCloud.vs.txt

            statesManager.SetGeometryShader(_geometryShaderSharedResource.Resource);
            statesManager.SetGeometryShaderConstantBuffer(_geometryShaderConstantBuffer, 0);

            statesManager.SetPixelShader(_pixelShaderSharedResource.Resource);

            // We can use No culling because the rendered quads are always oriented towards the camera
            // Theoretically this should be faster then Culling, because GPU does not need to check the orientation of the triangle, but in practice the perf. is the same.
            statesManager.SetRasterizerState(parentDXDevice.CommonStates.CullNone, statesManager.IsFrontCounterClockwise);

            statesManager.PrimitiveTopology = PrimitiveTopology.PointList;
            
            if (this.DiffuseColor.Alpha < 1.0f)
                statesManager.BlendState = _dxDevice.CommonStates.NonPremultipliedAlphaBlend;
            else
                statesManager.BlendState = _dxDevice.CommonStates.Opaque;
        }

        #endregion

        /// <summary>
        /// PreloadShaders can be called to load the shaders in advance before they are used.
        /// Calling this method increases the startup time, but when the 3D object needs to be shown, it is shown faster because all the shaders have already been created.
        /// </summary>
        public override void PreloadShaders()
        {
            if (_dxDevice == null)
                return;

            EnsureShaders(_dxDevice, usePerPointColor: false);
            EnsureShaders(_dxDevice, usePerPointColor: true);
            EnsureConstantBuffers(_dxDevice);
        }

        #region Dispose

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
                    {
                        DisposeHelper.DisposeAndNullify(ref _vertexShaderConstantBuffer);
                        DisposeHelper.DisposeAndNullify(ref _geometryShaderConstantBuffer);

                        DisposeHelper.DisposeAndNullify(ref _vertexShaderSharedResource);
                        DisposeHelper.DisposeAndNullify(ref _inputLayoutSharedResource);
                        DisposeHelper.DisposeAndNullify(ref _vertexShaderPerPointColorSharedResource);
                        DisposeHelper.DisposeAndNullify(ref _inputLayoutPerPointColorSharedResource);
                        DisposeHelper.DisposeAndNullify(ref _geometryShaderSharedResource);
                        DisposeHelper.DisposeAndNullify(ref _pixelShaderSharedResource);

                        _dxDevice = null;
                        _renderingContext = null;
                    }
                }
            }
            finally
            {
                base.Dispose(disposing);
            }
        }

        #endregion
    }
}
