using Godot;
using System;

namespace GuitarTutor.UI
{
    /// <summary>
    /// Controls the pause menu functionality
    /// to different parts of the application.
    /// </summary>
    public partial class PauseMenu : CanvasLayer
    {
        /// <summary>
        /// Called when the node enters the scene tree for the first time.
        /// Sets up signal connections for all navigation buttons in the pause menu.
        /// </summary>
        public override void _Ready()
        {
            // Connect each button's Pressed signal to its corresponding handler method
            GetNode<Button>("Control/ColorRect/MarginContainer/VBoxContainer/Tuner_button").Pressed += OnTunerButtonPressed;
            GetNode<Button>("Control/ColorRect/MarginContainer/VBoxContainer/Scale_button").Pressed += OnScaleButtonPressed;
            GetNode<Button>("Control/ColorRect/MarginContainer/VBoxContainer/Help_button").Pressed += OnHelpButtonPressed;
            GetNode<Button>("Control/ColorRect/MarginContainer/VBoxContainer/Exit_button").Pressed += OnExitButtonPressed;
        }

        /// <summary>
        /// Handler for the Tuner button press event.
        /// Navigates the user to the Guitar Tuner scene.
        /// </summary>
        private void OnTunerButtonPressed()
        {
            // Change the current scene to the Tuner scene
            GetTree().ChangeSceneToFile("res://Guitar_Tutor/Scenes/Tuner_Main.tscn");
        }

        /// <summary>
        /// Handler for the Scale button press event.
        /// Navigates the user to the Scale Practice scene.
        /// </summary>
        private void OnScaleButtonPressed()
        {
            // Change the current scene to the Scale scene
            GetTree().ChangeSceneToFile("res://Guitar_Tutor/Scenes/scale_main.tscn");
        }

        /// <summary>
        /// Handler for the Help button press event.
        /// Navigates the user to the Help documentation scene.
        /// </summary>
        private void OnHelpButtonPressed()
        {
            // Change the current scene to the Help scene
            GetTree().ChangeSceneToFile("res://Guitar_Tutor/Scenes/Help_Main.tscn");
        }

        /// <summary>
        /// Handler for the Exit button press event.
        /// Returns the user to the Main Menu scene.
        /// </summary>
        private void OnExitButtonPressed()
        {
            // Change the current scene to the Main Menu
            GetTree().ChangeSceneToFile("res://Guitar_Tutor/Scenes/main_menu.tscn");
        }
    }
}