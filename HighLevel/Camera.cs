using System.Numerics;
using RenderStorm.Other;

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
        Vector3 forward = Vector3.Transform(-Vector3.UnitZ, rotation);
        Vector3 up = Vector3.Transform(Vector3.UnitY, rotation);
        return Matrix4x4.CreateLookAt(Origin, Origin + forward, up);
    }

    public Matrix4x4 GetProjection(float aspectRatio)
    {
        return Matrix4x4.CreatePerspectiveFieldOfView(Util.Deg2Rad(FOV), aspectRatio, NearPlane, FarPlane);
    }
}