using System.Numerics;

namespace RenderStorm.HighLevel;

public struct Camera
{
    public Vector3 Origin = Vector3.Zero;
    public Vector3 Angle = Vector3.Zero;

    public float FOV = 90;
    public float FarPlane = 1024;
    public float NearPlane = 0.1f;

    public Camera()
    {
    }

    public Matrix4x4 GetView()
    {
        Matrix4x4 rotation = Matrix4x4.CreateFromYawPitchRoll(Angle.Y, Angle.X, Angle.Z);

        // Calculate forward vector based on camera rotation
        Vector3 forward = Vector3.Transform(new Vector3(0, 0, 1), rotation);
        Vector3 up = Vector3.Transform(Vector3.UnitY, rotation);

        // Create look-at matrix from camera position to position + forward
        return Matrix4x4.CreateLookAt(Origin, Origin + forward, Vector3.UnitY);
    }

    public Matrix4x4 GetProjection(float aspectRatio)
    {
        return Matrix4x4.CreatePerspectiveFieldOfView(FOV * (float.Pi / 180f), aspectRatio, NearPlane, FarPlane);
    }
}