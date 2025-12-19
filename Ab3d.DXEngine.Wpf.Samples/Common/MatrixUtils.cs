#if SHARPDX
using SharpDX;
using Matrix = SharpDX.Matrix;
#endif

namespace Ab3d.DXEngine.Wpf.Samples.Common
{
    public static class MatrixUtils
    {
        public static Matrix GetMatrixFromNormalizedDirection(Vector3 normalizedDirectionVector, Vector3 position, Vector3 scale)
        {
            var orientationMatrix = Matrix.Scaling(scale) *
                                    GetMatrixFromNormalizedDirection(normalizedDirectionVector);

            orientationMatrix.M41 = position.X;
            orientationMatrix.M42 = position.Y;
            orientationMatrix.M43 = position.Z;

            return orientationMatrix;
        }

        // directionVector must be normalized
        public static Matrix GetMatrixFromNormalizedDirection(Vector3 normalizedDirectionVector)
        {
            // The first three rows in the upper left 3x3 part of the matrix represents the axes of the coordinate system defined by the matrix.
            // For example in the identity matrix the xAxis is 1,0,0; yAxis is 0,1,0; zAxis is 0,0,1
            // With this knowledge we can create a matrix that will orient the transformed positions based on the specified direction vector.
            // The direction vector represents the xAxis in the new coordinate system.
            // To get the full matrix, we need to calculate the other two axes.

            //var xAxis = normalizedDirectionVector;
            var yAxis = CalculateUpDirectionFromNormalizeDirection(normalizedDirectionVector);

            // Once we have the direction and up axis, we can get the zAxis with calculating the perpendicular axis to those two
            var zAxis = Vector3.Cross(normalizedDirectionVector, yAxis);

            var rotationMatrix = new Matrix(normalizedDirectionVector.X, normalizedDirectionVector.Y, normalizedDirectionVector.Z, 0,
                yAxis.X, yAxis.Y, yAxis.Z, 0,
                zAxis.X, zAxis.Y, zAxis.Z, 0,
                0, 0, 0, 1);

            return rotationMatrix;
        }

        public static void GetMatrixFromDirection(Vector3 normalizedDirectionVector, Vector3 position, Vector3 scale, out Matrix orientationMatrix)
        {
            //var xAxis = normalizedDirectionVector;
            var yAxis = CalculateUpDirection(normalizedDirectionVector);
            var zAxis = Vector3.Cross(normalizedDirectionVector, yAxis);

            // For more info see comments in GetRotationMatrixFromDirection
            orientationMatrix = new Matrix(normalizedDirectionVector.X * scale.X, normalizedDirectionVector.Y * scale.Y, normalizedDirectionVector.Z * scale.Z, 0,
                                                   yAxis.X * scale.X, yAxis.Y * scale.Y, yAxis.Z * scale.Z, 0,
                                                   zAxis.X * scale.X, zAxis.Y * scale.Y, zAxis.Z * scale.Z, 0,
                                                   position.X, position.Y, position.Z, 1);
        }

        public static Vector3 CalculateUpDirectionFromNormalizeDirection(Vector3 normalizedLookDirection)
        {
            // To get the up direction we need to find a vector that lies on the xz plane (horizontal plane) and is perpendicular to Up vector and lookDirection.
            // Than we just create a perpendicular vector to lookDirection and the found vector on xz plane.

            var horizontalVector = Vector3.Cross(Vector3.Up, normalizedLookDirection);

            // First we need to check for edge case - the look direction is in the same direction as UpVector - the length of horizontalVector is 0 (or almost zero)

            var length = horizontalVector.Length();

            if (length < 0.001)
                horizontalVector = Vector3.UnitZ; // Any vector on xz plane could be used
            else
                horizontalVector /= length; // Normalize

            var upDirection = Vector3.Cross(normalizedLookDirection, horizontalVector);

            return upDirection;
        }

        public static Vector3 CalculateUpDirection(Vector3 lookDirection)
        {
            // To get the up direction we need to find a vector that lies on the xz plane (horizontal plane) and is perpendicular to Up vector and lookDirection.
            // Than we just create a perpendicular vector to lookDirection and the found vector on xz plane.

            var horizontalVector = Vector3.Cross(Vector3.Up, lookDirection);

            // First we need to check for edge case - the look direction is in the UpVector direction - the length of horizontalVector is 0 (or almost zero)

            if (horizontalVector.LengthSquared() < 0.0001) // we can use LengthSquared to avoid costly sqrt
                return Vector3.UnitZ;              // Any vector on xz plane could be used


            var upDirection = Vector3.Cross(lookDirection, horizontalVector);
            upDirection.Normalize();

            return upDirection;
        }
    }
}