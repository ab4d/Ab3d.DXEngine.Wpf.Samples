
#if SHARPDX
using SharpDX;
#endif

namespace Ab3d.DXEngine.Wpf.Samples.DXEngineAdvanced
{
    public interface IColorMaskMaterial
    {
        Color3 ColorMask { get; }
    }
}