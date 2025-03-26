using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class FeedbackSystem : Node
{
    // UI Components
    [Export] private NodePath _scaleNotesLabelPath = "../Camera_Follow/Feedback/Viewport/CanvasLayer/FeedbackMenu/ColorRect/MarginContainer3/ScaleNotes";
    [Export] private NodePath _playedNoteLabelPath = "../Camera_Follow/Feedback/Viewport/CanvasLayer/FeedbackMenu/ColorRect/MarginContainer4/Note";

    // Audio feedback
    [Export] private AudioStream _correctNoteSound;
    [Export] private AudioStream _completeScaleSound;

    // References to other components
    [Export] private NodePath _yinDetectorPath;
    [Export] private NodePath _patternHighlighterPath;

    // Configuration
    [Export] private float _detectionThreshold = 0.6f;
    [Export] private float _displayDuration = 1.5f;
    [Export] private bool _ascendingOnly = true;

    // Private components
    private RichTextLabel _scaleNotesLabel;
    private RichTextLabel _playedNoteLabel;
    private AudioStreamPlayer _audioPlayer;
    private YinPitchDetector _yinDetector;
    private PatternHighlighter _patternHighlighter;
    private ScaleLibrary _scaleLibrary;

    // Scale data
    private List<string> _scaleNotes = new List<string>();
    private List<bool> _playedNotes = new List<bool>();
    private string _currentRootNote = "A";
    private string _currentScaleType = "major";

    // Runtime state
    private string _lastDetectedNote = "";
    private float _detectionCooldown = 0f;
    private float _displayTimer = 0f;
    private bool _scaleCompleted = false;

    public override void _Ready()
    {
        // Get UI components
        _scaleNotesLabel = GetNode<RichTextLabel>(_scaleNotesLabelPath);
        _playedNoteLabel = GetNode<RichTextLabel>(_playedNoteLabelPath);
        _scaleNotesLabel.BbcodeEnabled = true;

        // Create audio player
        _audioPlayer = new AudioStreamPlayer();
        AddChild(_audioPlayer);
        _audioPlayer.Bus = "SoundEffects";

        // Get other components
        _yinDetector = GetNode<YinPitchDetector>(_yinDetectorPath);
        _patternHighlighter = GetNode<PatternHighlighter>(_patternHighlighterPath);
        _scaleLibrary = GetNodeOrNull<ScaleLibrary>("../ScaleLibrary");

        // Connect to ScaleLibrary signals
        if (_scaleLibrary != null)
        {
            _scaleLibrary.RootNoteChanged += OnRootNoteChanged;
            _scaleLibrary.ScaleTypeChanged += OnScaleTypeChanged;
            GD.Print("Connected to ScaleLibrary signals");
        }

        // Initialize UI
        _playedNoteLabel.Text = "";
        UpdateCurrentScale();
    }

    public override void _Process(double delta)
    {
        // Update timers
        if (_displayTimer > 0) _displayTimer -= (float)delta;
        if (_detectionCooldown > 0) _detectionCooldown -= (float)delta;

        if (_yinDetector != null && _detectionCooldown <= 0)
        {
            float pitch = _yinDetector.GetLastPitch();
            float confidence = _yinDetector.GetConfidence();

            if (pitch > 0 && confidence > _detectionThreshold)
            {
                string detectedNote = FrequencyToNoteName(pitch);
                string noteOnly = StripOctave(detectedNote);

                if (detectedNote != _lastDetectedNote)
                {
                    _lastDetectedNote = detectedNote;
                    _playedNoteLabel.Text = detectedNote;
                    CheckPlayedNote(noteOnly);
                    _displayTimer = _displayDuration;
                    _detectionCooldown = 0.2f;
                }
            }
        }
    }

    // Signal handlers for ScaleLibrary changes
    private void OnRootNoteChanged(string newRootNote)
    {
        GD.Print($"FeedbackSystem detected root note change to: {newRootNote}");
        if (newRootNote != _currentRootNote)
        {
            _currentRootNote = newRootNote;
            UpdateCurrentScale();
        }
    }

    private void OnScaleTypeChanged(string newScaleType)
    {
        GD.Print($"FeedbackSystem detected scale type change to: {newScaleType}");
        if (newScaleType != _currentScaleType)
        {
            _currentScaleType = newScaleType;
            UpdateCurrentScale();
        }
    }

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

        GenerateScaleNotes();
        ResetPractice();

        // Force immediate UI update
        if (_scaleNotesLabel != null)
        {
            GD.Print("Forcing UI update with new scale notes");
            UpdateScaleNotesDisplay();
        }
    }

    private void GenerateScaleNotes()
    {
        _scaleNotes.Clear();

        if (_scaleLibrary != null)
        {
            ScaleDefinition scale = _scaleLibrary.GetScale(_currentScaleType);
            if (scale != null)
            {
                string[] allNotes = { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };
                int rootIndex = Array.IndexOf(allNotes, _currentRootNote);

                if (rootIndex >= 0)
                {
                    foreach (int step in scale.Intervals)
                    {
                        _scaleNotes.Add(allNotes[(rootIndex + step) % allNotes.Length]);
                    }
                    if (_scaleNotes.Count > 0) _scaleNotes.Add(_currentRootNote);
                    GD.Print($"Generated scale notes: {string.Join(", ", _scaleNotes)}");
                    return;
                }
            }
        }

        // Fallback to major scale if anything fails
        GenerateDefaultScaleNotes();
    }

    private void GenerateDefaultScaleNotes()
    {
        string[] allNotes = { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };
        int rootIndex = Array.IndexOf(allNotes, _currentRootNote);
        if (rootIndex == -1) rootIndex = 9; // Default to A

        int[] majorIntervals = { 0, 2, 4, 5, 7, 9, 11 };
        foreach (int step in majorIntervals)
        {
            _scaleNotes.Add(allNotes[(rootIndex + step) % allNotes.Length]);
        }
        _scaleNotes.Add(allNotes[rootIndex]);
        GD.Print($"Generated default scale notes: {string.Join(", ", _scaleNotes)}");
    }

    private void UpdateScaleNotesDisplay()
    {
        if (_scaleNotesLabel == null) return;

        string displayText = "";
        for (int i = 0; i < _scaleNotes.Count; i++)
        {
            if (i > 0) displayText += " ";
            displayText += _playedNotes[i] ? $"[color=green]{_scaleNotes[i]}[/color]" : _scaleNotes[i];
        }
        _scaleNotesLabel.Text = displayText;
        GD.Print($"Updated scale notes display: {displayText}");
    }

    private void CheckPlayedNote(string noteName)
    {
        if (_scaleCompleted)
        {
            ResetPractice();
            return;
        }

        if (_scaleNotes.Contains(noteName))
        {
            for (int i = 0; i < _scaleNotes.Count; i++)
            {
                if (!_playedNotes[i] && _scaleNotes[i] == noteName)
                {
                    _playedNotes[i] = true;
                    PlayNoteSound(_correctNoteSound);
                    break;
                }
            }

            UpdateScaleNotesDisplay();

            if (_playedNotes.All(played => played))
            {
                _scaleCompleted = true;
                PlayNoteSound(_completeScaleSound);
            }
        }
    }

    public void ResetPractice()
    {
        _playedNotes = new List<bool>(new bool[_scaleNotes.Count]);
        _scaleCompleted = false;
        _lastDetectedNote = "";
        UpdateScaleNotesDisplay();
        GD.Print($"Reset practice with scale notes: {string.Join(", ", _scaleNotes)}");
    }

    private void PlayNoteSound(AudioStream sound)
    {
        if (_audioPlayer == null || sound == null) return;
        _audioPlayer.Stream = sound;
        _audioPlayer.Play();
    }

    private string FrequencyToNoteName(float frequency)
    {
        if (frequency <= 0) return "-";
        float a4 = 440.0f;
        string[] allNotes = { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };
        float semitoneFromA4 = 12 * Mathf.Log(frequency / a4) / Mathf.Log(2);
        int noteIndex = (int)Mathf.Round(semitoneFromA4) + 9;
        int octave = 4 + (noteIndex / 12);
        noteIndex %= 12;
        if (noteIndex < 0) noteIndex += 12;
        return allNotes[noteIndex] + octave.ToString();
    }

    private string StripOctave(string noteWithOctave)
    {
        if (noteWithOctave == "-") return noteWithOctave;
        return new string(noteWithOctave.TakeWhile(c => !char.IsDigit(c)).ToArray());
    }

    public void SetAscendingOnly(bool ascending)
    {
        _ascendingOnly = ascending;
        ResetPractice();
    }
}