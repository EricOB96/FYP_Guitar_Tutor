
// *** NOT IN USE ***

using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using GuitarTutor.Audio;
using GuitarTutor.Visual;

namespace GuitarTutor.Detection
{
    public partial class NoteDetectorHighlighter : Node
    {
        // Dependencies
        [Export] private NodePath _scaleHighlighterPath;

        // Configuration
        [Export] private float _confidenceThreshold = 0.7f;
        [Export] private float _highlightDuration = 0.5f; // How long note stays highlighted in seconds
        [Export] private int _numFrets = 22;  // Number of frets on the fretboard

        // Paths to materials
        [Export] private string _playedNoteMaterialPath = "res://Guitar_Tutor/Assets/Material/played_note.tres";

        // Standard tuning notes for guitar strings (from String6 to String1)
        private readonly string[] _stringRootNotes = { "E", "A", "D", "G", "B", "E" };
        private readonly string[] _allNotes = { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };
        private readonly int[] _stringOctaves = { 2, 2, 3, 3, 3, 4 }; // Octaves for each string

        // Runtime variables
        private YinPitchDetector _yinDetector;
        private ScaleHighlighter _scaleHighlighter;
        private string _lastDetectedNote = "";
        private float _highlightTimer = 0;
        private readonly Dictionary<string, Node3D> _highlightedNodes = new Dictionary<string, Node3D>();
        private Material _playedNoteMaterial;
        private Dictionary<Node3D, Material> _originalMaterials = new Dictionary<Node3D, Material>();

        public override void _Ready()
        {
            // Load dependencies
            _yinDetector = GetNode<YinPitchDetector>("/root/YinPitchDetector");
            _scaleHighlighter = GetNode<ScaleHighlighter>(_scaleHighlighterPath);

            if (_yinDetector == null)
            {
                GD.PushError("Failed to find YinPitchDetector node. Check the path in NoteDetectorHighlighter.");
                return;
            }

            if (_scaleHighlighter == null)
            {
                GD.PushError("Failed to find ScaleHighlighter node. Check the path in NoteDetectorHighlighter.");
                return;
            }

            // Load materials
            _playedNoteMaterial = GD.Load<Material>(_playedNoteMaterialPath);
            if (_playedNoteMaterial == null)
            {
                GD.PushError($"Failed to load played note material: {_playedNoteMaterialPath}");
                // Create a default green material if the custom one fails to load
                StandardMaterial3D defaultGreenMaterial = new StandardMaterial3D();
                defaultGreenMaterial.AlbedoColor = new Color(0, 1, 0); // Green
                defaultGreenMaterial.EmissionEnabled = true;
                defaultGreenMaterial.Emission = new Color(0, 0.5f, 0); // Dark green emission
                _playedNoteMaterial = defaultGreenMaterial;
            }

            GD.Print("NoteDetectorHighlighter initialized!");
        }

        public override void _Process(double delta)
        {
            // Check if highlighted note and update its timer
            if (_highlightTimer > 0)
            {
                _highlightTimer -= (float)delta;

                // If timer expires, reset the highlighted note
                if (_highlightTimer <= 0)
                {
                    ResetHighlightedNotes();
                }
            }

            // Get the current pitch and confidence from the YinDetector
            float pitch = _yinDetector.GetLastPitch();
            float confidence = _yinDetector.GetConfidence();

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

                    // Find all positions on the fretboard for note
                    HighlightPlayedNote(noteWithoutOctave);

                    // Start the highlight timer
                    _highlightTimer = _highlightDuration;

                    GD.Print($"Detected note: {detectedNote} (Frequency: {pitch:F1} Hz, Confidence: {confidence:F2})");
                }
            }
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
        /// Find all positions on the fretboard for a specific note and highlight them
        /// </summary>
        private void HighlightPlayedNote(string noteName)
        {
            GD.Print($"Highlighting played note: {noteName}");

            // Find and highlight all positions where this note occurs
            for (int stringNum = 6; stringNum >= 1; stringNum--)
            {
                // String index in the tuning array (0 for String6, 5 for String1)
                int stringIndex = 6 - stringNum;
                string stringRoot = _stringRootNotes[stringIndex];
                int stringRootIndex = Array.IndexOf(_allNotes, stringRoot);

                // Find which fret would produce this note on this string
                for (int fret = 0; fret <= _numFrets; fret++)
                {
                    int noteIndex = (stringRootIndex + fret) % _allNotes.Length;
                    string currentNote = _allNotes[noteIndex];

                    if (currentNote == noteName)
                    {
                        // Get the node for this position
                        Node3D node;

                        // Handle differently for open string vs regular frets
                        if (fret == 0)
                        {
                            node = GetNodeOrNull<Node3D>($"../XRToolsPickable/FretboardV3/ScaleNodes/OpenString/String{stringNum}");
                        }
                        else
                        {
                            node = GetNodeOrNull<Node3D>($"../XRToolsPickable/FretboardV3/ScaleNodes/Fret{fret}/String{stringNum}");
                        }

                        if (node != null)
                        {
                            // Store the node for later reset
                            string nodeKey = $"String{stringNum}_Fret{fret}";
                            _highlightedNodes[nodeKey] = node;

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
                // Store the original material for later reset
                if (node is CsgPrimitive3D csgNode)
                {
                    if (!_originalMaterials.ContainsKey(node))
                    {
                        _originalMaterials[node] = csgNode.MaterialOverride;
                    }
                    csgNode.MaterialOverride = _playedNoteMaterial;
                }
                else if (node is MeshInstance3D meshNode)
                {
                    if (!_originalMaterials.ContainsKey(node))
                    {
                        _originalMaterials[node] = meshNode.MaterialOverride;
                    }
                    meshNode.MaterialOverride = _playedNoteMaterial;
                }
                else
                {
                    GD.Print($"Node {node.Name} is not a compatible type for materials");
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
            foreach (var nodeEntry in _highlightedNodes)
            {
                Node3D node = nodeEntry.Value;

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
        }

        /// <summary>
        /// Public method to force clearing all highlighted notes
        /// </summary>
        public void ClearHighlights()
        {
            ResetHighlightedNotes();
            _highlightTimer = 0;
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
}
