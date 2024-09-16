using Silk.NET.Maths;
using YASV.Helpers;

public class Camera
{
    public enum Direction
    {
        Forward,
        Left,
        Backward,
        Right
    }

    private const float MouseSensitivity = 0.1f;
    private const float MovementSpeed = 0.1f;

    private Vector3D<float> _position = new(0.0f, 0.0f, 3.0f);
    private Vector3D<float> _worldUp = new(0.0f, 1.0f, 0.0f);
    private Vector3D<float> _front = new(0.0f, 0.0f, 0.0f);
    private Vector3D<float> _right = new(0.0f, 0.0f, 0.0f);
    private Vector3D<float> _up = new(0.0f, 0.0f, 0.0f);

    private float _yaw = -90.0f;
    private float _pitch = 0.0f;

    public Camera()
    {
        UpdateCameraVectors();
    }

    public void ProcessKeyboard(Direction direction)
    {
        switch (direction)
        {
            case Direction.Forward:
                _position += _front * MovementSpeed;
                break;
            case Direction.Left:
                _position -= _right * MovementSpeed;
                break;
            case Direction.Backward:
                _position -= _front * MovementSpeed;
                break;
            case Direction.Right:
                _position += _right * MovementSpeed;
                break;
        }
    }

    public void ProcessMouseMotion(float xOffset, float yOffset)
    {
        xOffset *= MouseSensitivity;
        yOffset *= MouseSensitivity;

        _yaw += xOffset;
        _pitch += yOffset;

        if (_pitch > 89.0f)
        {
            _pitch = 89.0f;
        }
        if (_pitch < -89.0f)
        {
            _pitch = -89.0f;
        }

        UpdateCameraVectors();
    }

    public void ProcessMouseWheel(float direction)
    {
        if (direction > 0)
        {
            _position += _front * MovementSpeed;
        }
        else
        {
            _position -= _front * MovementSpeed;
        }
    }

    public Matrix4X4<float> GetViewMatrix()
    {
        return Matrix4X4.CreateLookAt(_position, _position + _front, _up);
    }

    private void UpdateCameraVectors()
    {
        var front = new Vector3D<float>
        {
            X = (float)(Math.Cos(MathHelpers.DegreesToRadians(_yaw)) * Math.Cos(MathHelpers.DegreesToRadians(_pitch))),
            Y = (float)Math.Sin(MathHelpers.DegreesToRadians(_pitch)),
            Z = (float)(Math.Sin(MathHelpers.DegreesToRadians(_yaw)) * Math.Cos(MathHelpers.DegreesToRadians(_pitch)))
        };
        _front = Vector3D.Normalize(front);
        _right = Vector3D.Normalize(Vector3D.Cross(_front, _worldUp));
        _up = Vector3D.Normalize(Vector3D.Cross(_right, _front));
    }
}
