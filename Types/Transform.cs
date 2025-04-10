using System.Numerics;

namespace RenderStorm.Types;

public class Transform {
    public Vector3 Position;
    public Vector3 Scale = Vector3.One;
    public Quaternion Rotation = Quaternion.Identity;

    public Matrix4x4 Matrix => Matrix4x4.CreateFromQuaternion( Rotation )
                                  * Matrix4x4.CreateScale( Scale )
                                  * Matrix4x4.CreateTranslation( Position );

    public Matrix4x4 MatrixInverse => Matrix4x4.CreateTranslation( -Position )
                                           * Matrix4x4.CreateScale( 1 / Scale.X, 1 / Scale.Y, 1 / Scale.Z )
                                           * Matrix4x4.CreateFromQuaternion( Quaternion.Inverse(Rotation) );
}