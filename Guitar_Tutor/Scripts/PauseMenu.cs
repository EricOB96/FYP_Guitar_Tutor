using Godot;
using System;

// 2D_XR_PauseMenu.cs - Attach to a CanvasLayer
public partial class PauseMenu : CanvasLayer
{


    public override void _Ready()
    {

        // Connect button signals
        GetNode<Button>("Control/ColorRect/MarginContainer/VBoxContainer/Tuner_button").Pressed += OnTunerButtonPressed;
        GetNode<Button>("Control/ColorRect/MarginContainer/VBoxContainer/Scale_button").Pressed += OnScaleButtonPressed;

        
    }


    private void OnTunerButtonPressed()
    {
        // Change to Tuner Scene
        GetTree().ChangeSceneToFile("res://Guitar_Tutor/Scenes/Tuner_Main.tscn");
    }

    private void OnScaleButtonPressed()
    {
        // Change to Scale Scene
        GetTree().ChangeSceneToFile("res://Guitar_Tutor/Scenes/scale_main.tscn");

    }

    // Help function
   

   // Quit Function
}