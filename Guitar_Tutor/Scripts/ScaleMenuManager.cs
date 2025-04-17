using Godot;
using System;
using GuitarTutor.Visual;
using GuitarTutor.Data;
using GuitarTutor.Detection;

namespace GuitarTutor.UI
{
    public partial class ScaleMenuManager : CanvasLayer
    {
        // UI elements
        private OptionButton _noteButton;
        private OptionButton _scaleButton;
        private OptionButton _positionButton;

        // References to needed nodes
        //private ScaleHighlighter _scaleHighlighter;
        private PatternHighlighter _patternHighlighter;
        private ScaleLibrary _scaleLibrary;
        private PositionLockDetector _positionLockDetector;

        // Arrays for note options
        private readonly string[] _noteOptions = { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };

        // Pattern positions
        private readonly (int, int, bool)[] _patternPositions = new[]
        {
        (0, 4, true),    // Pattern 1: Open position (includes open strings)
        (4, 8, false),   // Pattern 2: Second position
        (8, 12, false),  // Pattern 3: Third position
        (12, 16, false), // Pattern 4: Fourth position
        (16, 20, false)  // Pattern 5: Fifth position
    };

        // Export variables for node paths
        //[Export]
        // private NodePath _scaleHighlighterPath = "../../../../FretBoard/ScaleHighlighter";
        [Export]
        private NodePath _patternHighlighterPath = "../../../../FretBoard/PatternHighlighter";
        [Export]
        private NodePath _scaleLibraryPath = "../../../../ScaleLibrary";
        [Export]
        private NodePath _positionLockDetectorPath = "../../../../FretBoard/PositionLockDetector";

        // Flag to track initialization status
        private bool _isInitialized = false;

        public override void _Ready()
        {
            // Get UI elements - adjust these paths to match your UI structure
            _noteButton = GetNode<OptionButton>("ScaleMenu/ColorRect/MarginContainer/note_button");
            _scaleButton = GetNode<OptionButton>("ScaleMenu/ColorRect/MarginContainer2/scale_button");
            _positionButton = GetNode<OptionButton>("ScaleMenu/ColorRect/MarginContainer5/position_button");

            // Get references to required nodes
            //_scaleHighlighter = GetNode<ScaleHighlighter>(_scaleHighlighterPath);
            _patternHighlighter = GetNode<PatternHighlighter>(_patternHighlighterPath);
            _scaleLibrary = GetNode<ScaleLibrary>(_scaleLibraryPath);
            _positionLockDetector = GetNode<PositionLockDetector>(_positionLockDetectorPath);

            GD.Print("ScaleMenuManager: Ready");
            GD.Print($"Note button found: {_noteButton != null}");
            GD.Print($"Scale button found: {_scaleButton != null}");
            GD.Print($"Position button found: {_positionButton != null}");
            //GD.Print($"ScaleHighlighter found: {_scaleHighlighter != null}");
            GD.Print($"PatternHighlighter found: {_patternHighlighter != null}");
            GD.Print($"ScaleLibrary found: {_scaleLibrary != null}");
            GD.Print($"PositionLockDetector found: {_positionLockDetector != null}");

            if (_noteButton == null || _scaleButton == null || _positionButton == null ||
                _patternHighlighter == null || _scaleLibrary == null)
            {
                GD.PushError("Failed to find required nodes. Check the paths in ScaleMenuManager.");
                return;
            }

            // Connect signals
            _noteButton.ItemSelected += OnNoteButtonItemSelected;
            _scaleButton.ItemSelected += OnScaleButtonItemSelected;
            _positionButton.ItemSelected += OnPositionButtonItemSelected;

            // Initialize the dropdowns with options
            InitializeDropdowns();
        }

        private void InitializeDropdowns()
        {
            GD.Print("Initializing dropdowns...");

            // Clear existing items
            _noteButton.Clear();
            _scaleButton.Clear();
            _positionButton.Clear();

            // Add note options
            foreach (string note in _noteOptions)
            {
                _noteButton.AddItem(note);
            }
            GD.Print($"Added {_noteButton.ItemCount} notes to note dropdown");

            // Add position options
            _positionButton.AddItem("Pattern 1: Open Position (0-4)");
            _positionButton.AddItem("Pattern 2: 2nd Position (4-8)");
            _positionButton.AddItem("Pattern 3: 3rd Position (8-12)");
            _positionButton.AddItem("Pattern 4: 4th Position (12-16)");
            _positionButton.AddItem("Pattern 5: 5th Position (16-20)");
            GD.Print($"Added {_positionButton.ItemCount} positions to position dropdown");

            // Set defaults for notes
            if (_noteButton.ItemCount > 0)
            {
                _noteButton.Selected = 9; // A (index 9)
            }

            // Set default for position
            if (_positionButton.ItemCount > 0)
            {
                _positionButton.Selected = 0; // First pattern (index 0)
            }

            // Initialize scales in a deferred call
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

                // initialized flag
                _isInitialized = true;

                // Perform initial highlighting with pattern
                UpdatePatternHighlighting();
                UpdatePositionLock();
            }
        }

        private void OnNoteButtonItemSelected(long index)
        {
            if (!_isInitialized) return;

            GD.Print($"Note selected: {_noteButton.GetItemText((int)index)}");
            UpdatePatternHighlighting();
        }

        private void OnScaleButtonItemSelected(long index)
        {
            if (!_isInitialized) return;

            GD.Print($"Scale selected: {_scaleButton.GetItemText((int)index)}");
            UpdatePatternHighlighting();
        }

        private void OnPositionButtonItemSelected(long index)
        {
            if (!_isInitialized) return;

            GD.Print($"Position selected: {_positionButton.GetItemText((int)index)}");

            // Update the pattern highlighter with the new position
            int patternIndex = _positionButton.Selected;
            var (minFret, maxFret, includeOpen) = _patternPositions[patternIndex];
            _patternHighlighter.SetPatternRange(minFret, maxFret, includeOpen);

            // Update position lock for note detection
            UpdatePositionLock();

            // Re-highlight with the new pattern
            UpdatePatternHighlighting();
        }

        private void UpdatePositionLock()
        {
            if (!_isInitialized || _positionLockDetector == null) return;

            // Get the current pattern settings
            int patternIndex = _positionButton.Selected;
            var (minFret, maxFret, includeOpen) = _patternPositions[patternIndex];

            // Update the position lock detector
            _positionLockDetector.SetPositionLock(minFret, maxFret);
            _positionLockDetector.SetIncludeOpenStrings(includeOpen);

            GD.Print($"Position lock set to pattern {patternIndex + 1}: Frets {minFret}-{maxFret}");

            // Store current pattern in the global scene for other components
            GetNode<Node>("/root/Scale_main").Set("current_pattern", patternIndex + 1);
        }

        private void UpdatePatternHighlighting()
        {
            if (!_isInitialized) return;

            if (_patternHighlighter != null && _scaleLibrary != null &&
                _noteButton.ItemCount > 0 && _scaleButton.ItemCount > 0)
            {
                // Get the selected note and scale from the dropdown text
                string selectedNote = _noteButton.GetItemText(_noteButton.Selected);
                string selectedScaleDisplay = _scaleButton.GetItemText(_scaleButton.Selected);

                GD.Print($"Highlighting pattern for: Note={selectedNote}, Scale={selectedScaleDisplay}");

                // Verify we have valid selections
                if (string.IsNullOrEmpty(selectedNote) || string.IsNullOrEmpty(selectedScaleDisplay))
                {
                    GD.PushError("Empty selection detected in UpdatePatternHighlighting");
                    return;
                }

                // Convert display name to internal scale key
                string selectedScale = _scaleLibrary.GetScaleKeyFromDisplayName(selectedScaleDisplay);

                GD.Print($"Highlighting pattern scale: {selectedNote} {selectedScale}");

                // Highlight the pattern scale on the fretboard
                _patternHighlighter.HighlightPatternScale(selectedNote, selectedScale);

                // Store the current scale info in the global scene for other components
                if (_scaleLibrary != null)
                {
                    _scaleLibrary.CurrentRootNote = selectedNote;
                    _scaleLibrary.CurrentScaleType = selectedScale;
                    GD.Print($"Updated ScaleLibrary to: {selectedNote} {selectedScale}");
                }
            }
            else
            {
                GD.PushError("Cannot update highlighting: Missing references or empty dropdowns");
            }
        }
    }
}