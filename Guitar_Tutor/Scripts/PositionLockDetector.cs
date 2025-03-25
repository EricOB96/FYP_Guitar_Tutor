using Godot;
using System;
using System.Linq;
using System.Collections.Generic;

public partial class PositionLockDetector : Node
{
    // References to other components
    [Export] private NodePath _yinDetectorPath;
    [Export] private NodePath _patternHighlighterPath;

    // Position lock configuration
    [Export] private int _minFret = 0;
    [Export] private int _maxFret = 4;
    [Export] private bool _includeOpenStrings = true;

    // Configuration
    [Export] private float _confidenceThreshold = 0.4f; 
    [Export] private float _highlightDuration = 0.5f;
    [Export] private string _playedNoteMaterialPath = "res://Guitar_Tutor/Assets/Material/played_note.tres";

    // Component references
    private YinPitchDetector _yinDetector;
    private PatternHighlighter _patternHighlighter;
    private Material _playedNoteMaterial;

    // State tracking
    private string _lastDetectedNote = "";
    private float _noteTimer = 0f;
    private Dictionary<Node3D, Material> _originalMaterials = new Dictionary<Node3D, Material>();
    private List<Node3D> _highlightedNodes = new List<Node3D>();

    // Guitar data
    private readonly string[] _stringRootNotes = { "E", "A", "D", "G", "B", "E" };
    private readonly string[] _allNotes = { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };

    public override void _Ready()
    {
        // Get references to other components
        _yinDetector = GetNode<YinPitchDetector>(_yinDetectorPath);
        _patternHighlighter = GetNode<PatternHighlighter>(_patternHighlighterPath);

        if (_yinDetector == null)
        {
            GD.PushError("YinPitchDetector not found! Check the path in PositionLockDetector."); // Debug
            return;
        }

        if (_patternHighlighter == null)
        {
            GD.PushError("PatternHighlighter not found! Check the path in PositionLockDetector."); // Debug
            return;
        }

        // Load material
        _playedNoteMaterial = GD.Load<Material>(_playedNoteMaterialPath);
        if (_playedNoteMaterial == null)
        {
            GD.Print($"Failed to load played note material: {_playedNoteMaterialPath}, creating default green material");

            // Create a default green material if the custom one fails to load
            StandardMaterial3D defaultGreenMaterial = new StandardMaterial3D();
            defaultGreenMaterial.AlbedoColor = new Color(0, 1, 0); // Green
            defaultGreenMaterial.EmissionEnabled = true;
            defaultGreenMaterial.Emission = new Color(0, 0.5f, 0); // Dark green emission
            _playedNoteMaterial = defaultGreenMaterial;
        }

        GD.Print($"PositionLockDetector initialized with fret range: {_minFret}-{_maxFret}");
        GD.Print($"Open strings {(_includeOpenStrings ? "included" : "excluded")}");
    }

    public override void _Process(double delta)
    {
        // Update note timer
        if (_noteTimer > 0)
        {
            _noteTimer -= (float)delta;

            // Reset highlighted notes if timer expired
            if (_noteTimer <= 0)
            {
                ResetHighlightedNotes();
            }
        }

        // Get the current pitch and confidence from the YinDetector
        float pitch = _yinDetector.GetLastPitch();
        float confidence = _yinDetector.GetConfidence();

        // Debug print
        if (pitch > 0)
        {
            GD.Print($"Detected pitch: {pitch:F1} Hz, Confidence: {confidence:F2}");
        }

        // Only process if we have a confident detection
        if (pitch > 0 && confidence > _confidenceThreshold)
        {
            // Convert frequency to note name with octave
            string detectedNote = FrequencyToNoteName(pitch);
            string noteWithoutOctave = StripOctave(detectedNote);

            // Only process if this is a different note than last time
            if (detectedNote != _lastDetectedNote)
            {
                _lastDetectedNote = detectedNote;

                // Clear previous highlights
                ResetHighlightedNotes();

                // Find positions within the locked range for this note
                HighlightNotesInPositionLock(noteWithoutOctave);

                // Start the highlight timer
                _noteTimer = _highlightDuration;

                GD.Print($"Detected note: {detectedNote} (Frequency: {pitch:F1} Hz, Confidence: {confidence:F2})");
            }
        }
    }

    /// <summary>
    /// Find positions on the fretboard for a specific note within the position-locked range
    /// </summary>
    private void HighlightNotesInPositionLock(string noteName)
    {
        GD.Print($"Highlighting note in position lock: {noteName} (Frets {_minFret}-{_maxFret})");

        // Find and highlight all positions where this note occurs within the locked position
        for (int stringNum = 6; stringNum >= 1; stringNum--)
        {
            // String index in the tuning array (0 for String6, 5 for String1, etc..)
            int stringIndex = 6 - stringNum;
            string stringRoot = _stringRootNotes[stringIndex];
            int stringRootIndex = Array.IndexOf(_allNotes, stringRoot);

            // Find which frets would produce this note on this string
            for (int fret = 0; fret <= 22; fret++)
            {
                // Skip frets outside the locked position (except for open strings if included)
                if (fret != 0 && (fret < _minFret || fret > _maxFret))
                {
                    continue;
                }

                // Skip open strings if not included
                if (fret == 0 && !_includeOpenStrings)
                {
                    continue;
                }

                int noteIndex = (stringRootIndex + fret) % _allNotes.Length;
                string currentNote = _allNotes[noteIndex];

                if (currentNote == noteName)
                {
                    // Get the node for this position
                    Node3D node;

                    // Debug path
                    string nodePath;

                    // Handle differently for open string vs regular frets
                    if (fret == 0)
                    {
                        nodePath = $"../XRToolsPickable/FretboardV3/ScaleNodes/OpenString/String{stringNum}";
                    }
                    else
                    {
                        nodePath = $"../XRToolsPickable/FretboardV3/ScaleNodes/Fret{fret}/String{stringNum}";
                    }

                    GD.Print($"Looking for node at: {nodePath}");
                    node = GetNodeOrNull<Node3D>(nodePath);
                    GD.Print($"Node found: {node != null}");

                    if (node != null)
                    {
                        // Add to highlighted nodes list
                        _highlightedNodes.Add(node);

                        // Apply green material to highlight the played note
                        ApplyHighlightMaterial(node);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Apply the highlight material to a node
    /// </summary>
    private void ApplyHighlightMaterial(Node3D node)
    {
        try
        {
            GD.Print($"Applying highlight material to {node.Name}");

            // Store the original material for later reset
            if (node is CsgPrimitive3D csgNode)
            {
                if (!_originalMaterials.ContainsKey(node))
                {
                    _originalMaterials[node] = csgNode.MaterialOverride;
                }
                csgNode.MaterialOverride = _playedNoteMaterial;
                GD.Print("Applied to CSGPrimitive3D node");
            }
            else if (node is MeshInstance3D meshNode)
            {
                if (!_originalMaterials.ContainsKey(node))
                {
                    _originalMaterials[node] = meshNode.MaterialOverride;
                }
                meshNode.MaterialOverride = _playedNoteMaterial;
                GD.Print("Applied to MeshInstance3D node");
            }
            else
            {
                GD.Print($"Node {node.Name} is not a compatible type for materials. Type: {node.GetType().Name}");
            }
        }
        catch (Exception ex)
        {
            GD.PrintErr($"Error setting highlight material for {node.Name}: {ex.Message}");
        }
    }

    /// <summary>
    /// Reset all highlighted nodes to their original materials
    /// </summary>
    private void ResetHighlightedNotes()
    {
        foreach (var node in _highlightedNodes)
        {
            // Reset to original material
            if (node is CsgPrimitive3D csgNode && _originalMaterials.ContainsKey(node))
            {
                csgNode.MaterialOverride = _originalMaterials[node];
            }
            else if (node is MeshInstance3D meshNode && _originalMaterials.ContainsKey(node))
            {
                meshNode.MaterialOverride = _originalMaterials[node];
            }
        }

        // Clear the highlighted nodes list
        _highlightedNodes.Clear();
        _originalMaterials.Clear();
    }

    /// <summary>
    /// Convert frequency to note name with octave
    /// </summary>
    private string FrequencyToNoteName(float frequency)
    {
        if (frequency <= 0) return "-";

        // A4 is 440Hz
        float a4 = 440.0f;

        // Calculate note number relative to A4
        float semitoneFromA4 = 12 * Mathf.Log(frequency / a4) / Mathf.Log(2);
        int noteIndex = (int)Mathf.Round(semitoneFromA4) + 9; // A is at index 9 in our array

        // Calculate octave
        int octave = 4 + (noteIndex / 12); // A4 reference point
        noteIndex %= 12;
        if (noteIndex < 0) noteIndex += 12;

        return _allNotes[noteIndex] + octave.ToString();
    }

    /// <summary>
    /// Remove the octave from a note name
    /// </summary>
    private string StripOctave(string noteWithOctave)
    {
        // Handle special case
        if (noteWithOctave == "-") return noteWithOctave;

        return new string(noteWithOctave.ToCharArray().TakeWhile(c => !char.IsDigit(c)).ToArray());
    }

    /// <summary>
    /// Set the position lock range
    /// </summary>
    public void SetPositionLock(int minFret, int maxFret)
    {
        _minFret = Mathf.Clamp(minFret, 0, 22);
        _maxFret = Mathf.Clamp(maxFret, minFret, 22);

        // Reset any current highlights
        ResetHighlightedNotes();

        GD.Print($"Position lock set to frets {_minFret}-{_maxFret}");
    }

    /// <summary>
    /// Set whether open strings should be included
    /// </summary>
    public void SetIncludeOpenStrings(bool include)
    {
        _includeOpenStrings = include;

        // Reset any current highlights
        ResetHighlightedNotes();

        GD.Print($"Open strings {(_includeOpenStrings ? "included" : "excluded")}");
    }

    /// <summary>
    /// Public method to force clearing all highlighted notes
    /// </summary>
    public void ClearHighlights()
    {
        ResetHighlightedNotes();
        _noteTimer = 0;
        _lastDetectedNote = "";
    }

    /// <summary>
    /// Get the last detected note
    /// </summary>
    public string GetLastDetectedNote()
    {
        return _lastDetectedNote;
    }
}
