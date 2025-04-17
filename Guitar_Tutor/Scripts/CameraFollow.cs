using Godot;
using System;

namespace GuitarTutor.Camera
{
    public partial class CameraFollow : Node3D
    {
        [Export]
        private NodePath _cameraPath = "../Player/Player/XRCamera3D";

        [Export]
        private float _distanceFromCamera = 2.0f;

        [Export]
        private Vector3 _positionOffset = new Vector3(0, -0.5f, 0);

        [Export]
        private bool _smoothFollow = true;

        [Export]
        private float _smoothSpeed = 10.0f;

        private Camera3D _camera;
        private bool _initialized = false;

        public override void _Ready()
        {
            // Get camera reference
            _camera = GetNode<Camera3D>(_cameraPath);

            if (_camera == null)
            {
                GD.PushError("Camera not found at path: " + _cameraPath);
                return;
            }

            _initialized = true;
            GD.Print("Follow camera initialized");
        }

        public override void _Process(double delta)
        {
            if (!_initialized || _camera == null)
                return;

            // Get camera's forward direction
            Vector3 cameraForward = -_camera.GlobalTransform.Basis.Z.Normalized();

            // Calculate target position in front of camera
            Vector3 targetPosition = _camera.GlobalPosition + (cameraForward * _distanceFromCamera) + _positionOffset;

            if (_smoothFollow)
            {
                // Smooth movement toward target position
                GlobalPosition = GlobalPosition.Lerp(targetPosition, (float)delta * _smoothSpeed);

                // Make the object face the camera
                LookAt(_camera.GlobalPosition, Vector3.Up);
            }
            else
            {
                // Immediate positioning
                GlobalPosition = targetPosition;

                // Make the menu face the camera
                LookAt(_camera.GlobalPosition, Vector3.Up);
            }
        }
    }
}
