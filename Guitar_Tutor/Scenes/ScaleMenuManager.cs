using Godot;
using System;

public partial class ScaleMenuManager : CanvasLayer
{
    // Ui elements
    private OptionButton _noteButton;
    private OptionButton _scaleButton;

    // Reference scale node script

    // Arrays for note and scale options
    private readonly string[] _noteOptions = { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };
    private readonly string[] _scaleOptions = { "major", "minor", "pentatonic_major", "pentatonic_minor", "blues", "dorian", "mixolydian", "phrygian" };

    public override void _Ready()
    {
        // Get ui elements
        _noteButton = GetNode<OptionButton>("CanvasLayer/ScaleMenu/ColorRect/MarginContainer/note_button");
        _scaleButton = GetNode<OptionButton>("CanvasLayer/ScaleMenu/ColorRect/MarginContainer2/scale_button");

        // Get scale node script from FretboardV3
        /* insert here */

        // Connect signals
        _noteButton.ItemSelected += _on_note_button_item_selected;
        _scaleButton.ItemSelected += _on_scale_button_item_selected;

        // Initialize the dropdowens with options
        InitializeDropdowns();

        // Initalize highlight with default values
        UpdateHighlighting();
    }
    private void InitializeDropdowns()
    {
        // clear existing items
        _noteButton.Clear();
        _scaleButton.Clear();

        // Set Default
        _noteButton.Selected = 0; // C 
        _scaleButton.Selected = 0; // Major
    }

    private void _on_note_button_item_selected()
    {
        UpdateHighlighting();
    }

    private void _on_scale_button_item_selected()
    {
        UpdateHighlighting();
    }

    private void UpdateHighlighting()
    {
        throw new NotImplementedException();
    }


}
