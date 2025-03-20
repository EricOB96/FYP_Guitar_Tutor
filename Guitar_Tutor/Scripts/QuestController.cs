using Godot;
using System;

public partial class QuestController : Node
{
    // Path to Camera_Follow node
    [Export]
    private NodePath _cameraFollowPath = "../../../Camera_Follow";

    // Camera_Follow node reference
    private Node _cameraFollow;

    public override void _Ready()
    {
        // Get the Camera_Follow node
        _cameraFollow = GetNode(_cameraFollowPath);

        if (_cameraFollow != null)
        {
            GD.Print($"Found Camera_Follow node: {_cameraFollow.Name}");
        }
        else
        {
            GD.PushError($"Failed to find Camera_Follow at path: {_cameraFollowPath}");
        }
    }

    // This method matches your signal name
    private void _on_button_pressed(StringName button)
    {
        GD.Print($"Button Pressed: {button}"); // Debug

        if (button == "ax_button")
        {
            ToggleCameraFollow();
        }
    }


    public void ToggleCameraFollow()
    {
        if (_cameraFollow != null)
        {
            bool isVisible = (bool)_cameraFollow.Call("is_visible");
            _cameraFollow.Call("set_visible", !isVisible);
            GD.Print($"Camera Follow visibility toggled to: {!isVisible}");
        }
    }
}