using System.Numerics;

namespace RenderStorm.HighLevel;

public struct Camera
{
    public Vector3 Origin = Vector3.Zero;
    public Vector3 Angle = Vector3.Zero; // Angles in degrees!

    public float FOV = 90;
    public float FarPlane = 1024;
    public float NearPlane = 0.1f;

    public Camera() { }

    public Matrix4x4 GetView()
    {
        float yawRad = Angle.Y * (MathF.PI / 180f);
        float pitchRad = Angle.X * (MathF.PI / 180f);
        float rollRad = Angle.Z * (MathF.PI / 180f);

        Matrix4x4 rotation = Matrix4x4.CreateFromYawPitchRoll(yawRad, pitchRad, rollRad);
        Vector3 forward = Vector3.Transform(new Vector3(0, 0, 1), rotation);
        Vector3 up = Vector3.Transform(Vector3.UnitY, rotation);
        return Matrix4x4.CreateLookAt(Origin, Origin + forward, up);
    }

    public Matrix4x4 GetProjection(float aspectRatio)
    {
        float fovRadians = FOV * (MathF.PI / 180f);
        return Matrix4x4.CreatePerspectiveFieldOfView(fovRadians, aspectRatio, NearPlane, FarPlane);
    }

    public Vector2 Project(Vector3 origin, int width, int height)
    {
        Matrix4x4 view = GetView();
        Matrix4x4 proj = GetProjection((float)width / height);
        Matrix4x4 viewProj = view * proj;

        Vector4 worldPos = new Vector4(origin, 1.0f);
        Vector4 clipSpace = Vector4.Transform(worldPos, viewProj);

        if (clipSpace.W == 0)
            return new Vector2(float.NaN, float.NaN);

        Vector3 ndc = new Vector3(clipSpace.X, clipSpace.Y, clipSpace.Z) / clipSpace.W;
        float screenX = (ndc.X + 1f) * 0.5f * width;
        float screenY = (1f - ndc.Y) * 0.5f * height;

        return new Vector2(screenX, screenY);
    }
}