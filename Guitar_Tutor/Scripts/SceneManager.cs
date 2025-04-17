using Godot;
using System;

namespace GuitarTutor.Management
{
    public partial class SceneManager : CanvasLayer
    {
        // Called when the node enters the scene tree for the first time.
        public override void _Ready()
        {
            MicPermission();
            GetNode<Button>("Control/ColorRect/MarginContainer/VBoxContainer/Tuner_button").Pressed += OnTunerButtonPressed;
            GetNode<Button>("Control/ColorRect/MarginContainer/VBoxContainer/Scale_button").Pressed += OnScaleButtonPressed;
            GetNode<Button>("Control/ColorRect/MarginContainer/VBoxContainer/Help_button").Pressed += OnHelpButtonPressed;
        }

        // Ask android for microphone permission
        private void MicPermission()
        {
            if (OS.GetName() == "Android")
            {
                OS.RequestPermission("RECORD_AUDIO");
            }
        }

        // Change to Tuner scene
        private void OnTunerButtonPressed()
        {
            GetTree().ChangeSceneToFile("res://Guitar_Tutor/Scenes/Tuner_Main.tscn");
        }

        // Change to Scale Scene
        private void OnScaleButtonPressed()
        {
            GetTree().ChangeSceneToFile("res://Guitar_Tutor/Scenes/scale_main.tscn");
        }

        // Change to Help scene (Instructions)
        private void OnHelpButtonPressed()
        {
            GetTree().ChangeSceneToFile("res://Guitar_Tutor/Scenes/Help_Main.tscn");
        }
    }
}