using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using GuitarTutor.Audio;
using GuitarTutor.Data;
using GuitarTutor.Visual;

namespace GuitarTutor.UI
{
    /// <summary>
    /// Handles note detection, scale visualization, and user feedback for guitar practice.
    /// Manages both regular scale mode and pattern mode with visual and audio feedback.
    /// </summary>
    public partial class FeedbackSystem : Node
    {
        // UI Components
        [Export] private NodePath _scaleNotesLabelPath = "../Camera_Follow/Feedback/Viewport/CanvasLayer/FeedbackMenu/ColorRect/MarginContainer3/ScaleNotes";
        [Export] private NodePath _playedNoteLabelPath = "../Camera_Follow/Feedback/Viewport/CanvasLayer/FeedbackMenu/ColorRect/MarginContainer4/Note";
        [Export] private NodePath _patternDisplayLabelPath = "../Camera_Follow/Feedback/Viewport/CanvasLayer/FeedbackMenu/ColorRect/MarginContainer5/PatternDisplay";

        // Audio feedback - Sounds played when correct notes are detected
        [Export] private AudioStream _correctNoteSound;    // Sound for individual correct notes
        [Export] private AudioStream _completeScaleSound;  // Sound for completing the entire scale/pattern

        // References to other components
        [Export] private NodePath _patternHighlighterPath; // Visual element that highlights pattern positions

        // Configuration
        [Export] private float _detectionThreshold = 0.7f; // Minimum confidence level required for note detection
        [Export] private float _displayDuration = 1.5f;    // How long detected notes are displayed
        [Export] private bool _ascendingOnly = true;       // Whether scale practice is ascending-only or includes descending

        // Pattern Mode configuration
        [Export] private bool _patternModeActive = false;  // Whether practicing scales or specific patterns

        // Private component references obtained during runtime
        private RichTextLabel _scaleNotesLabel;            // Displays all notes in the scale/pattern
        private RichTextLabel _playedNoteLabel;            // Displays the currently played note
        private RichTextLabel _patternDisplayLabel;        // Optional dedicated display for pattern practice
        private AudioStreamPlayer _audioPlayer;            // Plays feedback sounds
        private YinPitchDetector _yinDetector;             // Detects pitch from audio input
        private PatternHighlighter _patternHighlighter;    // Visual component that shows pattern positions
        private ScaleLibrary _scaleLibrary;                // Repository of scale definitions

        // Scale data
        private List<string> _scaleNotes = new List<string>();     // The unique notes in the current scale
        private List<bool> _playedNotes = new List<bool>();        // Tracks which notes have been played
        private string _currentRootNote = "A";                     // Root note of the current scale
        private string _currentScaleType = "major";                // Type of scale (major, minor, etc.)

        // Specific to pattern practice mode
        private List<string> _patternNoteSequence = new List<string>(); // Full sequence of notes in pattern order
        private int _patternCurrentIndex = 0;                           // Current position in the pattern sequence

        // Tracking current status during practice
        private string _lastDetectedNote = "";             // Most recently detected note
        private float _detectionCooldown = 0f;             // Prevents rapid re-detection of the same note
        private float _displayTimer = 0f;                  // Timer for displaying detected notes
        private bool _scaleCompleted = false;              // Whether the current scale/pattern is completed

        /// <summary>
        /// Initializes the FeedbackSystem, setting up UI components, audio player, and connecting to other systems.
        /// </summary>
        public override void _Ready()
        {
            // Get UI components from the scene tree
            _scaleNotesLabel = GetNode<RichTextLabel>(_scaleNotesLabelPath);
            _playedNoteLabel = GetNode<RichTextLabel>(_playedNoteLabelPath);
            if (!string.IsNullOrEmpty(_patternDisplayLabelPath.ToString()))
            {
                _patternDisplayLabel = GetNodeOrNull<RichTextLabel>(_patternDisplayLabelPath);
                if (_patternDisplayLabel != null)
                {
                    _patternDisplayLabel.BbcodeEnabled = true; // Enable rich text formatting
                }
            }

            if (_scaleNotesLabel != null)
            {
                _scaleNotesLabel.BbcodeEnabled = true; // Enable rich text formatting for scale notes
            }

            // Create audio player for feedback sounds
            _audioPlayer = new AudioStreamPlayer();
            AddChild(_audioPlayer);
            _audioPlayer.Bus = "SoundEffects"; // Route to audio bus 

            // Get references to other system components
            _yinDetector = GetNode<YinPitchDetector>("/root/YinPitchDetector"); // Pitch detection singleton
            _patternHighlighter = GetNode<PatternHighlighter>(_patternHighlighterPath); // Visual pattern guide
            _scaleLibrary = GetNodeOrNull<ScaleLibrary>("../ScaleLibrary"); // Scale definitions repository

            // Connect to ScaleLibrary signals to receive updates when selected scale changes
            if (_scaleLibrary != null)
            {
                _scaleLibrary.RootNoteChanged += OnRootNoteChanged;
                _scaleLibrary.ScaleTypeChanged += OnScaleTypeChanged;
                GD.Print("Connected to ScaleLibrary signals");
            }

            // Initialize UI with empty state
            if (_playedNoteLabel != null)
            {
                _playedNoteLabel.Text = "";
            }

            // Debug info
            GD.Print("FeedbackSystem._Ready() called");
            GD.Print($"Pattern mode active: {_patternModeActive}");

            // Generate initial scale based on current settings
            UpdateCurrentScale();
        }

        /// <summary>
        /// Called every frame. Handles pitch detection, note recognition, and updates timers.
        /// </summary>
        public override void _Process(double delta)
        {
            // Update timers for display duration and detection cooldown
            if (_displayTimer > 0) _displayTimer -= (float)delta;
            if (_detectionCooldown > 0) _detectionCooldown -= (float)delta;

            // Check for played notes if pitch detector is available and cooldown has expired
            if (_yinDetector != null && _detectionCooldown <= 0)
            {
                float pitch = _yinDetector.GetLastPitch();
                float confidence = _yinDetector.GetConfidence();

                // Only process notes that meet our confidence threshold
                if (pitch > 0 && confidence > _detectionThreshold)
                {
                    string detectedNote = FrequencyToNoteName(pitch);
                    string noteOnly = StripOctave(detectedNote); // Remove octave number for comparison

                    // Only process when a new note is detected (not sustained notes)
                    if (detectedNote != _lastDetectedNote)
                    {
                        _lastDetectedNote = detectedNote;

                        // Update the UI with the detected note
                        if (_playedNoteLabel != null)
                        {
                            _playedNoteLabel.Text = detectedNote;
                        }

                        // Check if the played note is part of the exercise
                        CheckPlayedNote(noteOnly);

                        // Reset timers for display duration and detection cooldown
                        _displayTimer = _displayDuration;
                        _detectionCooldown = 0.2f; // Short cooldown to prevent rapid re-triggering
                    }
                }
            }
        }

        /// <summary>
        /// Handles root note changes from the ScaleLibrary.
        /// </summary>
        private void OnRootNoteChanged(string newRootNote)
        {
            GD.Print($"FeedbackSystem detected root note change to: {newRootNote}");
            if (newRootNote != _currentRootNote)
            {
                _currentRootNote = newRootNote;
                UpdateCurrentScale(); // Regenerate scale with new root note
            }
        }

        /// <summary>
        /// Handles scale type changes from the ScaleLibrary.
        /// </summary>
        private void OnScaleTypeChanged(string newScaleType)
        {
            GD.Print($"FeedbackSystem detected scale type change to: {newScaleType}");
            if (newScaleType != _currentScaleType)
            {
                _currentScaleType = newScaleType;
                UpdateCurrentScale(); // Regenerate scale with new type
            }
        }

        /// <summary>
        /// Updates the current scale based on the selected root note and scale type.
        /// Regenerates scale notes and resets practice status.
        /// </summary>
        private void UpdateCurrentScale()
        {
            if (_scaleLibrary != null)
            {
                // Get current scale info from ScaleLibrary
                _currentRootNote = _scaleLibrary.CurrentRootNote;
                _currentScaleType = _scaleLibrary.CurrentScaleType;

                GD.Print($"Updated scale from ScaleLibrary: {_currentRootNote} {_currentScaleType}");
            }
            else
            {
                // Fallback if ScaleLibrary not found
                GD.Print("ScaleLibrary not found, using default values");
                _currentRootNote = "A";
                _currentScaleType = "major";
            }

            // Generate notes for the current scale and reset practice state
            GenerateScaleNotes();
            ResetPractice();

            // Force immediate UI update with new scale information
            if (_scaleNotesLabel != null)
            {
                GD.Print("Forcing UI update with new scale notes");
                UpdateScaleNotesDisplay();
            }
        }

        /// <summary>
        /// Generates the notes for the current scale based on root note and scale type.
        /// Also generates pattern sequence if pattern mode is active.
        /// </summary>
        private void GenerateScaleNotes()
        {
            _scaleNotes.Clear();
            _patternNoteSequence.Clear();

            if (_scaleLibrary != null)
            {
                // Get scale definition from library
                ScaleDefinition scale = _scaleLibrary.GetScale(_currentScaleType);
                if (scale != null)
                {
                    // Standard chromatic scale in Western music
                    string[] allNotes = { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };
                    int rootIndex = Array.IndexOf(allNotes, _currentRootNote);

                    if (rootIndex >= 0)
                    {
                        // Generate the basic scale notes by applying intervals to the root
                        foreach (int step in scale.Intervals)
                        {
                            _scaleNotes.Add(allNotes[(rootIndex + step) % allNotes.Length]);
                        }

                        // Make sure we include the octave root note at the top if not already there
                        if (_scaleNotes.Count > 0 && _scaleNotes[0] == _currentRootNote &&
                            (_scaleNotes[_scaleNotes.Count - 1] != _currentRootNote))
                        {
                            _scaleNotes.Add(_currentRootNote);
                        }

                        // For pattern mode, generate the full note sequence in the proper order
                        if (_patternModeActive)
                        {
                            GeneratePatternSequence(allNotes, rootIndex, scale.Intervals);
                        }

                        GD.Print($"Generated scale notes: {string.Join(", ", _scaleNotes)}");
                        return;
                    }
                }
            }

            // Fallback to major scale if anything fails
            GenerateDefaultScaleNotes();
        }

        /// <summary>
        /// Generates a default major scale when scale library is unavailable or scale lookup fails.
        /// </summary>
        private void GenerateDefaultScaleNotes()
        {
            // Standard chromatic scale
            string[] allNotes = { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };
            int rootIndex = Array.IndexOf(allNotes, _currentRootNote);
            if (rootIndex == -1) rootIndex = 9; // Default to A if root note not found

            // Major scale intervals
            int[] majorIntervals = { 0, 2, 4, 5, 7, 9, 11 };
            foreach (int step in majorIntervals)
            {
                _scaleNotes.Add(allNotes[(rootIndex + step) % allNotes.Length]);
            }

            // Add the octave root note for completeness
            _scaleNotes.Add(allNotes[rootIndex]);
            GD.Print($"Generated default scale notes: {string.Join(", ", _scaleNotes)}");

            // Generate pattern sequence if in pattern mode
            if (_patternModeActive)
            {
                GeneratePatternSequence(allNotes, rootIndex, majorIntervals);
            }
        }

        /// <summary>
        /// Generates a sequence of notes for pattern practice based on the scale type.
        /// Different scale types have specific pattern sequences designed for guitar practice.
        /// </summary>
        private void GeneratePatternSequence(string[] allNotes, int rootIndex, int[] intervals)
        {
            // Clear the existing sequence
            _patternNoteSequence.Clear();

            // Debug the scale type for troubleshooting
            GD.Print($"Generating pattern sequence for scale type: {_currentScaleType} with root {_currentRootNote}");

            if (_currentScaleType.Contains("pentatonic_minor") || _currentScaleType.Contains("Minor Pentatonic"))
            {
                // Minor pentatonic has intervals: [0, 3, 5, 7, 10]
                // These intervals correspond to the scale degrees: 1, b3, 4, 5, b7

                // Calculate the actual notes based on the root
                string root = _currentRootNote;
                string minorThird = allNotes[(rootIndex + 3) % 12];    // Flat 3rd
                string fourth = allNotes[(rootIndex + 5) % 12];        // 4th
                string fifth = allNotes[(rootIndex + 7) % 12];         // 5th
                string minorSeventh = allNotes[(rootIndex + 10) % 12]; // Flat 7th

                GD.Print($"Minor Pentatonic Notes: {root}, {minorThird}, {fourth}, {fifth}, {minorSeventh}");

                // The scale degrees sequence for minor pentatonic pattern (based on the pattern layout) is:
                // This sequence is designed for guitar practice, covering common patterns across the fretboard
                // 1 b3 4 5 b7 1 b3 4 5 b7 1 b3

                // Add the notes in this sequence regardless of the root note
                _patternNoteSequence.Add(root);          // Root
                _patternNoteSequence.Add(minorThird);    // Minor 3rd
                _patternNoteSequence.Add(fourth);        // 4th
                _patternNoteSequence.Add(fifth);         // 5th
                _patternNoteSequence.Add(minorSeventh);  // Minor 7th
                _patternNoteSequence.Add(root);          // Root (octave)
                _patternNoteSequence.Add(minorThird);    // Minor 3rd
                _patternNoteSequence.Add(fourth);        // 4th
                _patternNoteSequence.Add(fifth);         // 5th
                _patternNoteSequence.Add(minorSeventh);  // Minor 7th
                _patternNoteSequence.Add(root);          // Root (octave)
                _patternNoteSequence.Add(minorThird);    // Minor 3rd
            }
            else if (_currentScaleType.Contains("pentatonic_major") || _currentScaleType.Contains("Major Pentatonic"))
            {
                // Major pentatonic has intervals: [0, 2, 4, 7, 9]
                // These intervals correspond to the scale degrees: 1, 2, 3, 5, 6

                // Calculate the actual notes based on the root
                string root = _currentRootNote;
                string second = allNotes[(rootIndex + 2) % 12];      // 2nd
                string majorThird = allNotes[(rootIndex + 4) % 12];  // Major 3rd
                string fifth = allNotes[(rootIndex + 7) % 12];       // 5th
                string sixth = allNotes[(rootIndex + 9) % 12];       // 6th

                GD.Print($"Major Pentatonic Notes: {root}, {second}, {majorThird}, {fifth}, {sixth}");

                // The scale degrees sequence for major pentatonic pattern (based on the pattern layout) is:
                // This sequence is designed for guitar practice, covering common patterns across the fretboard
                // 1 2 3 5 6 1 2 3 5 6 1 2

                // Add the notes in this sequence regardless of the root note
                _patternNoteSequence.Add(root);          // Root
                _patternNoteSequence.Add(second);        // 2nd
                _patternNoteSequence.Add(majorThird);    // Major 3rd
                _patternNoteSequence.Add(fifth);         // 5th
                _patternNoteSequence.Add(sixth);         // 6th
                _patternNoteSequence.Add(root);          // Root (octave)
                _patternNoteSequence.Add(second);        // 2nd
                _patternNoteSequence.Add(majorThird);    // Major 3rd
                _patternNoteSequence.Add(fifth);         // 5th
                _patternNoteSequence.Add(sixth);         // 6th
                _patternNoteSequence.Add(root);          // Root (octave)
                _patternNoteSequence.Add(second);        // 2nd
            }
            else
            {
                // For other scales, use a simpler approach that just repeats the pattern twice
                // Start with the root note
                _patternNoteSequence.Add(_currentRootNote);

                // Add all other scale notes in order
                for (int i = 1; i < intervals.Length; i++)
                {
                    _patternNoteSequence.Add(allNotes[(rootIndex + intervals[i]) % 12]);
                }

                // Add octave root note
                _patternNoteSequence.Add(_currentRootNote);

                // Repeat pattern for a complete exercise
                for (int i = 1; i < intervals.Length; i++)
                {
                    _patternNoteSequence.Add(allNotes[(rootIndex + intervals[i]) % 12]);
                }

                // Final root note
                _patternNoteSequence.Add(_currentRootNote);
            }

            // Debug output
            GD.Print($"Generated pattern sequence ({_patternNoteSequence.Count} notes): {string.Join(", ", _patternNoteSequence)}");
        }

        /// <summary>
        /// Updates the display showing which scale notes have been played.
        /// Displays notes differently based on normal mode or pattern mode.
        /// </summary>
        private void UpdateScaleNotesDisplay()
        {
            if (_scaleNotesLabel == null) return;

            string displayText = "";

            if (!_patternModeActive)
            {
                // Regular mode - show which unique notes have been played
                // Green color for played notes, default color for unplayed
                for (int i = 0; i < _scaleNotes.Count; i++)
                {
                    if (i > 0) displayText += " ";
                    displayText += _playedNotes[i] ? $"[color=green]{_scaleNotes[i]}[/color]" : _scaleNotes[i];
                }
            }
            else
            {
                // In pattern mode, show the full sequence with progress
                // Green for completed notes, yellow for current, default for upcoming
                for (int i = 0; i < _patternNoteSequence.Count; i++)
                {
                    if (i > 0) displayText += " ";

                    if (i < _patternCurrentIndex)
                    {
                        // Completed notes - green
                        displayText += $"[color=green]{_patternNoteSequence[i]}[/color]";
                    }
                    else if (i == _patternCurrentIndex)
                    {
                        // Next note to play (highlighted) - yellow
                        displayText += $"[color=yellow]{_patternNoteSequence[i]}[/color]";
                    }
                    else
                    {
                        // Upcoming notes - default color
                        displayText += _patternNoteSequence[i];
                    }
                }
            }

            _scaleNotesLabel.Text = displayText;
            GD.Print($"Updated scale notes display: {displayText}");
        }

        /// <summary>
        /// Updates the dedicated pattern display if available, showing pattern progress.
        /// Falls back to using the regular scale display if no dedicated pattern display exists.
        /// </summary>
        private void UpdatePatternDisplay()
        {
            // If a dedicated pattern display label, use it
            if (_patternDisplayLabel != null && _patternModeActive)
            {
                string patternText = "";

                // Show the full sequence with progress indicators
                // Green for completed, yellow for current, default for upcoming
                for (int i = 0; i < _patternNoteSequence.Count; i++)
                {
                    if (i > 0) patternText += " ";

                    if (i < _patternCurrentIndex)
                    {
                        // Completed notes - green
                        patternText += $"[color=green]{_patternNoteSequence[i]}[/color]";
                    }
                    else if (i == _patternCurrentIndex)
                    {
                        // Next note to play (highlighted) - yellow
                        patternText += $"[color=yellow]{_patternNoteSequence[i]}[/color]";
                    }
                    else
                    {
                        // Upcoming notes - default color
                        patternText += _patternNoteSequence[i];
                    }
                }

                _patternDisplayLabel.Text = patternText;
                GD.Print($"Updated pattern display: {patternText}");
            }
            else if (_patternModeActive)
            {
                // If no dedicated display, use the scale notes label for both
                string fullText = $"Pattern Mode: {_currentRootNote} {_currentScaleType}\n";

                // Add abbreviated pattern sequence with progress
                fullText += $"Progress: {_patternCurrentIndex}/{_patternNoteSequence.Count} notes";

                // If we have the regular label, update it
                if (_scaleNotesLabel != null)
                {
                    _scaleNotesLabel.Text = fullText;
                }

                GD.Print($"Pattern display updated in scale label: {fullText}");
            }
        }

        /// <summary>
        /// Checks if a played note matches the expected note in the scale or pattern.
        /// Updates progress tracking and provides audio feedback.
        /// </summary>
        private void CheckPlayedNote(string noteName)
        {
            // If scale is already completed, reset for a new attempt
            if (_scaleCompleted)
            {
                ResetPractice();
                return;
            }

            // Different checking logic based on mode (pattern vs regular)
            if (_patternModeActive && _patternNoteSequence.Count > 0)
            {
                // In pattern mode, notes must be played in the exact sequence
                if (_patternCurrentIndex < _patternNoteSequence.Count &&
                    noteName == _patternNoteSequence[_patternCurrentIndex])
                {
                    // Correct note in sequence
                    PlayNoteSound(_correctNoteSound);

                    // Advance in the pattern sequence
                    _patternCurrentIndex++;

                    // Update the display
                    UpdateScaleNotesDisplay();

                    // Check if completed the full pattern
                    if (_patternCurrentIndex >= _patternNoteSequence.Count)
                    {
                        _scaleCompleted = true;
                        PlayNoteSound(_completeScaleSound);
                        GD.Print("Full pattern completed!");
                    }
                }
                else if (_patternNoteSequence.Contains(noteName))
                {
                    // Note is in the scale but out of sequence
                    GD.Print($"Note {noteName} is in scale but out of sequence. Expected: {_patternNoteSequence[_patternCurrentIndex]}");
                }
            }
            else
            {
                // Original behavior for non-pattern mode
                // In regular mode, just need to play all notes, order doesn't matter
                if (_scaleNotes.Contains(noteName))
                {
                    // Mark the note as played if it's in the scale and hasn't been played yet
                    for (int i = 0; i < _scaleNotes.Count; i++)
                    {
                        if (!_playedNotes[i] && _scaleNotes[i] == noteName)
                        {
                            _playedNotes[i] = true;
                            PlayNoteSound(_correctNoteSound);
                            break;
                        }
                    }

                    // Update the visual display
                    UpdateScaleNotesDisplay();

                    // Check if all notes have been played to complete the scale
                    if (_playedNotes.All(played => played))
                    {
                        _scaleCompleted = true;
                        PlayNoteSound(_completeScaleSound);
                    }
                }
            }
        }

        /// <summary>
        /// Resets the practice session, clearing played notes and progress indicators.
        /// </summary>
        public void ResetPractice()
        {
            // Reset tracking arrays to initial state
            _playedNotes = new List<bool>(new bool[_scaleNotes.Count]);
            _scaleCompleted = false;
            _lastDetectedNote = "";
            _patternCurrentIndex = 0; // Reset the pattern sequence index

            // Update UI to show the reset state
            UpdateScaleNotesDisplay();
            GD.Print($"Reset practice with scale notes: {string.Join(", ", _scaleNotes)}");

            if (_patternModeActive)
            {
                GD.Print($"Pattern mode active with sequence length: {_patternNoteSequence.Count}");
            }
        }

        /// <summary>
        /// Plays a sound effect for feedback when a note is played correctly.
        /// </summary>
        private void PlayNoteSound(AudioStream sound)
        {
            if (_audioPlayer == null || sound == null) return;
            _audioPlayer.Stream = sound;
            _audioPlayer.Play();
        }

        /// <summary>
        /// Converts a frequency in Hz to a note name with octave.
        /// Uses the standard A4 = 440Hz reference for calculation.
        /// </summary>
        private string FrequencyToNoteName(float frequency)
        {
            if (frequency <= 0) return "-"; // Invalid frequency

            // A4 reference frequency (440Hz)
            float a4 = 440.0f;

            // All possible note names in Western music
            string[] allNotes = { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };

            // Calculate semitones from A4 using the formula: 12 * log2(f/440)
            float semitoneFromA4 = 12 * Mathf.Log(frequency / a4) / Mathf.Log(2);

            // Convert to note index (A4 is at index 9 in the 4th octave)
            int noteIndex = (int)Mathf.Round(semitoneFromA4) + 9;

            // Calculate octave (A4 is in octave 4)
            int octave = 4 + (noteIndex / 12);

            // Calculate position in the current octave
            noteIndex %= 12;
            if (noteIndex < 0) noteIndex += 12; // Handle negative indices

            // Combine note name and octave
            return allNotes[noteIndex] + octave.ToString();
        }

        /// <summary>
        /// Removes the octave number from a note name.
        /// </summary>
        private string StripOctave(string noteWithOctave)
        {
            if (noteWithOctave == "-") return noteWithOctave;
            // Take all characters until the first digit
            return new string(noteWithOctave.TakeWhile(c => !char.IsDigit(c)).ToArray());
        }

        /// <summary>
        /// Sets whether scale practice should be in ascending order only.
        /// </summary>
        public void SetAscendingOnly(bool ascending)
        {
            _ascendingOnly = ascending;
            ResetPractice(); // Reset practice with new setting
        }

        /// <summary>
        /// Toggles between regular scale mode and pattern mode.
        /// </summary>
        public void SetPatternMode(bool enabled)
        {
            if (_patternModeActive != enabled)
            {
                _patternModeActive = enabled;
                GD.Print($"Pattern mode {(enabled ? "enabled" : "disabled")}");
                UpdateCurrentScale(); // This will regenerate the notes with the new mode
                ResetPractice();
            }
        }
    }
}