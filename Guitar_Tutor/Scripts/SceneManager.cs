using Godot;
using System;

public partial class SceneManager : CanvasLayer
{
    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {
        MicPermission();
        GetNode<Button>("Control/ColorRect/MarginContainer/VBoxContainer/Tuner_button").Pressed += OnTunerButtonPressed;
        GetNode<Button>("Control/ColorRect/MarginContainer/VBoxContainer/Scale_button").Pressed += OnScaleButtonPressed;
    }

    // Ask android for microphone permission
    private void MicPermission()
    {
        if (OS.GetName() == "Android")
        {
            OS.RequestPermission("RECORD_AUDIO");
        }
    }

    // Called every frame. 'delta' is the elapsed time since the previous frame.
    public override void _Process(double delta)
    {
        // pass
    }

    private void OnTunerButtonPressed()
    {
        GetTree().ChangeSceneToFile("res://Guitar_Tutor/Scenes/Tuner_Main.tscn");
    }

    private void OnScaleButtonPressed()
    {
        GetTree().ChangeSceneToFile("res://Guitar_Tutor/Scenes/scale_main.tscn");
    }
}