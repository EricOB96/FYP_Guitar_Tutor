using Godot;
using System;

public partial class QuestController : Node
{
    // Path to Camera_Follow node
    [Export]
    private NodePath _scaleMenuFollowPath = "../../../Camera_Follow/ScaleMenu";

    [Export]
    private NodePath _pauseMenuPath = "../../../Camera_Follow/PauseMenu";

    [Export]
    private NodePath _FeedbackPath = "../../../Camera_Follow/Feedback";
    // ScaleMenu node reference
    private Node _scaleMenuFollow;

    // PauseMenu Reference
    private Node _pauseMenu;

    // Feedback Reference
    private Node _feedback;

    public override void _Ready()
    {

        // Get the ScaleMenu node
        _scaleMenuFollow = GetNode(_scaleMenuFollowPath);

        // Get the PauseMenu node
        _pauseMenu = GetNode(_pauseMenuPath);

        // Get the Feedback node
        _feedback = GetNode(_FeedbackPath);

        // Debug
        if (_scaleMenuFollow != null)
        {
            GD.Print($"Found ScaleMenu node: {_scaleMenuFollow.Name}");
        }
        else
        {
            GD.PushError($"Failed to find ScaleMenu at path: {_scaleMenuFollowPath}");
        }

        if (_pauseMenu != null)
        {
            GD.Print($"Found PauseMenu node: {_pauseMenu.Name}");
        }
        else
        {
            GD.PushError($"Failed to find PauseMenu at path: {_pauseMenuPath}");
        }

        if (_feedback != null)
        {
            GD.Print($"Found PauseMenu node: {_feedback.Name}");
        }
        else
        {
            GD.PushError($"Failed to find PauseMenu at path: {_FeedbackPath}");
        }
    }

    // Signal for toggling scale menu
    private void _on_button_pressed(StringName button)
    {
        GD.Print($"Button Pressed: {button}"); // Debug

        if (button == "ax_button")
        {
            ToggleScaleMenu();
        }
    }

    // Signal for toggling pause menu

    private void _on_button_pressed_left(StringName button)
    {
        GD.Print($"Button Pressed: {button}"); // Debug

        if (button == "menu_button")
        {
            TogglePauseMenu();
        }
    }

    // Signal for toggling feeback
    private void _on_button_pressed_feedback(StringName button)
    {
        GD.Print($"Button Pressed: {button}"); // Debug

        if (button == "by_button")
        {
            ToggleFeedback();
        }
    }

    // Check if scale menu is visible
    public void ToggleScaleMenu()
    {
        if (_scaleMenuFollow != null)
        {
            bool isVisible = (bool)_scaleMenuFollow.Call("is_visible");
            _scaleMenuFollow.Call("set_visible", !isVisible);
            GD.Print($"Camera Follow visibility toggled to: {!isVisible}");
        }
    }


    // Check if pause menu is visible
    public void TogglePauseMenu()
    {
        if (_pauseMenu != null)
        {
            bool isVisible = (bool)_pauseMenu.Call("is_visible");
            _pauseMenu.Call("set_visible", !isVisible);
            GD.Print($"PauseMenu visibility toggled to: {!isVisible}");
        }
    }

    // Check if feedback is visible
    public void ToggleFeedback()
    {
        if (_feedback != null)
        {
            bool isVisible = (bool)_feedback.Call("is_visible");
            _feedback.Call("set_visible", !isVisible);
            GD.Print($"Feedback visibility toggled to: {!isVisible}");
        }
    }

}