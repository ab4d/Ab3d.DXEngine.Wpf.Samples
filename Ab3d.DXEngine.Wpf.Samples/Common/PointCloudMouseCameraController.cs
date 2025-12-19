using System.Windows.Media.Media3D;
using Ab3d.Controls;
using Ab3d.DirectX;
using Point = System.Windows.Point;

#if SHARPDX
using SharpDX;
#endif

namespace Ab3d.DXEngine.Wpf.Samples.Common
{
    public class PointCloudMouseCameraController : MouseCameraController
    {
        public DXScene DXScene { get; set; }
        public OptimizedPointMesh<Vector3> OptimizedPointMesh { get; set; }

        /// <summary>
        /// When set to a value bigger then 0, then the distance from the ray (from mouse position)
        /// to the closest position need to be smaller that the specified amount for this to be a valid position.
        /// This prevents getting positions that are far from the ray.
        /// </summary>
        public float MaxDistanceToAnyPosition { get; set; }

        public PointCloudMouseCameraController()
        {
        }

        public PointCloudMouseCameraController(DXScene dxScene, OptimizedPointMesh<Vector3> optimizedPointMesh)
        {
            OptimizedPointMesh = optimizedPointMesh;
            DXScene            = dxScene;
        }

        protected override Point3D? GetRotationCenterPositionFromMousePosition(Point mousePosition, bool calculatePositionWhenNoObjectIsHit)
        {
            if (OptimizedPointMesh == null || DXScene == null)
                return base.GetRotationCenterPositionFromMousePosition(mousePosition, calculatePositionWhenNoObjectIsHit);

            var mouseRay = DXScene.GetRayFromCamera((int)mousePosition.X, (int)mousePosition.Y);

            float distance;
            var   closestPositionIndex = OptimizedPointMesh.GetClosestPositionIndex(mouseRay, out distance);

            if (closestPositionIndex != -1 && MaxDistanceToAnyPosition > 0 && distance < MaxDistanceToAnyPosition)
                return OptimizedPointMesh.PositionsArray[closestPositionIndex].ToWpfPoint3D();

            return null;
        }
    }
}