
using Godot;
using System;

public partial class ScaleMenuManager : CanvasLayer
{
    // UI elements
    private OptionButton _noteButton;
    private OptionButton _scaleButton;

    // References to needed nodes
    private ScaleHighlighter _scaleHighlighter;
    private ScaleLibrary _scaleLibrary;

    // Arrays for note options
    private readonly string[] _noteOptions = { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };

    // Export variables for node paths
    [Export]
    private NodePath _scaleHighlighterPath = "../../../../FretBoard/ScaleHighlighter";
    [Export]
    private NodePath _scaleLibraryPath = "../../../../ScaleLibrary";

    // Added flag to track initialization status
    private bool _isInitialized = false;

    public override void _Ready()
    {
        // Get UI elements - adjust these paths to match your UI structure
        _noteButton = GetNode<OptionButton>("ScaleMenu/ColorRect/MarginContainer/note_button");
        _scaleButton = GetNode<OptionButton>("ScaleMenu/ColorRect/MarginContainer2/scale_button");

        // Get references to required nodes
        _scaleHighlighter = GetNode<ScaleHighlighter>(_scaleHighlighterPath);
        _scaleLibrary = GetNode<ScaleLibrary>(_scaleLibraryPath);

        GD.Print("ScaleMenuManager: Ready");
        GD.Print($"Note button found: {_noteButton != null}");
        GD.Print($"Scale button found: {_scaleButton != null}");
        GD.Print($"ScaleHighlighter found: {_scaleHighlighter != null}");
        GD.Print($"ScaleLibrary found: {_scaleLibrary != null}");

        if (_noteButton == null || _scaleButton == null || _scaleHighlighter == null || _scaleLibrary == null)
        {
            GD.PushError("Failed to find required nodes. Check the paths in ScaleMenuManager.");
            return;
        }

        // Connect signals
        _noteButton.ItemSelected += OnNoteButtonItemSelected;
        _scaleButton.ItemSelected += OnScaleButtonItemSelected;

        // Initialize the dropdowns with options
        InitializeDropdowns();
    }

    private void InitializeDropdowns()
    {
        GD.Print("Initializing dropdowns...");

        // Clear existing items
        _noteButton.Clear();
        _scaleButton.Clear();

        // Add note options
        foreach (string note in _noteOptions)
        {
            _noteButton.AddItem(note);
        }
        GD.Print($"Added {_noteButton.ItemCount} notes to note dropdown");

        // Set defaults for notes
        if (_noteButton.ItemCount > 0)
        {
            _noteButton.Selected = 9; // A (index 9)
        }

        // We'll initialize scales in a deferred call
        CallDeferred(nameof(InitializeScalesDropdown));
    }

    private void InitializeScalesDropdown()
    {
        // Check if ScaleLibrary is now initialized
        if (_scaleLibrary == null)
        {
            GD.PushError("ScaleLibrary reference is still null");
            return;
        }

        // Get scale options
        string[] scaleNames = _scaleLibrary.GetScaleDisplayNames();
        GD.Print($"Got scale names: {string.Join(", ", scaleNames)}");

        // If no scales returned, try again later
        if (scaleNames.Length == 0)
        {
            GD.Print("No scales found, retrying in 0.5 seconds");
            GetTree().CreateTimer(0.5f).Timeout += () => InitializeScalesDropdown();
            return;
        }

        // Add scales to dropdown
        foreach (string scaleName in scaleNames)
        {
            _scaleButton.AddItem(scaleName);
            GD.Print($"Added scale: {scaleName}");
        }

        GD.Print($"Added {_scaleButton.ItemCount} scales to scale dropdown");

        // Set default scale
        if (_scaleButton.ItemCount > 0)
        {
            _scaleButton.Selected = 0; // Major
            GD.Print($"Selected scale: {_scaleButton.GetItemText(_scaleButton.Selected)}");

            // Now that we have scales, we can set initialized flag
            _isInitialized = true;

            // Perform initial highlighting
            UpdateHighlighting();
        }
    }

    private void OnNoteButtonItemSelected(long index)
    {
        if (!_isInitialized) return;

        GD.Print($"Note selected: {_noteButton.GetItemText((int)index)}");
        UpdateHighlighting();
    }

    private void OnScaleButtonItemSelected(long index)
    {
        if (!_isInitialized) return;

        GD.Print($"Scale selected: {_scaleButton.GetItemText((int)index)}");
        UpdateHighlighting();
    }

    private void UpdateHighlighting()
    {
        if (!_isInitialized) return;

        if (_scaleHighlighter != null && _scaleLibrary != null &&
            _noteButton.ItemCount > 0 && _scaleButton.ItemCount > 0)
        {
            // Get the selected note and scale from the dropdown text
            string selectedNote = _noteButton.GetItemText(_noteButton.Selected);
            string selectedScaleDisplay = _scaleButton.GetItemText(_scaleButton.Selected);

            GD.Print($"Getting highlight for: Note={selectedNote}, Scale={selectedScaleDisplay}");

            // Verify we have valid selections
            if (string.IsNullOrEmpty(selectedNote) || string.IsNullOrEmpty(selectedScaleDisplay))
            {
                GD.PushError("Empty selection detected in UpdateHighlighting");
                return;
            }

            // Convert display name to internal scale key
            string selectedScale = _scaleLibrary.GetScaleKeyFromDisplayName(selectedScaleDisplay);

            GD.Print($"Highlighting scale: {selectedNote} {selectedScale}");

            // Highlight the scale on the fretboard
            _scaleHighlighter.HighlightScale(selectedNote, selectedScale);
        }
        else
        {
            GD.PushError("Cannot update highlighting: Missing references or empty dropdowns");
            GD.Print($"ScaleHighlighter: {_scaleHighlighter != null}");
            GD.Print($"ScaleLibrary: {_scaleLibrary != null}");
            GD.Print($"Note button count: {_noteButton?.ItemCount ?? 0}");
            GD.Print($"Scale button count: {_scaleButton?.ItemCount ?? 0}");
        }
    }
}