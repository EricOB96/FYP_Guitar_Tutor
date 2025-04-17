using Godot;
using System;

namespace GuitarTutor.UI
{
    /// <summary>
    /// Handles the help menu UI navigation, providing buttons for users
    /// to access different parts of the Guitar Tutor application.
    /// </summary>
    public partial class Helpmenu : CanvasLayer
    {
        /// <summary>
        /// Called when the node enters the scene tree for the first time.
        /// Sets up button signal connections for the help menu.
        /// </summary>
        public override void _Ready()
        {
            // Connect the Tuner button's Pressed signal to its handler method
            GetNode<Button>("HelpUi/ColorRect/MarginContainer6/Tuner_Button").Pressed += OnTunerButtonPressed;

            // Connect the Scale button's Pressed signal to its handler method
            GetNode<Button>("HelpUi/ColorRect/MarginContainer7/Scale_Button").Pressed += OnScaleButtonPressed;
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
    }
}