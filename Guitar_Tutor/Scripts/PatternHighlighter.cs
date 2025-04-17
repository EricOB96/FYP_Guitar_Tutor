using Godot;
using System;
using System.Collections.Generic;
using GuitarTutor.Data;

namespace GuitarTutor.Visual
{
    public partial class PatternHighlighter : Node
    {
        // Standard tuning notes for guitar strings (from String6 to String1)
        // String6 = low E, String5 = A, String4 = D, String3 = G, String2 = B, String1 = high E
        private readonly string[] _stringRootNotes = { "E", "A", "D", "G", "B", "E" };

        // All notes in order for calculation
        private readonly string[] _allNotes = { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };

        // Reference to the scale library
        private ScaleLibrary _scaleLibrary;

        // Material paths for different types of notes (Root, Scale note, Default node)
        [Export]
        private string _rootMaterialPath = "res://Guitar_Tutor/Assets/Material/root_note_material.tres";

        [Export]
        private string _scaleMaterialPath = "res://Guitar_Tutor/Assets/Material/scale_note_material.tres";

        [Export]
        private string _defaultMaterialPath = "res://Guitar_Tutor/Assets/Material/default_note_material.tres";

        // Pattern configuration
        [Export]
        private int _minFret = 0;

        [Export]
        private int _maxFret = 4;

        [Export]
        private bool _includeOpenStrings = true;

        [Export]
        private int _numFrets = 22;  // Number of frets on the fretboard

        [Export]
        private NodePath _scaleLibraryPath = "/root/Scale_main/ScaleLibrary";  // Path to ScaleLibrary node

        // Called when the node enters the scene tree for the first time.
        public override void _Ready()
        {
            // Get reference to ScaleLibrary
            _scaleLibrary = GetNode<ScaleLibrary>(_scaleLibraryPath);

            if (_scaleLibrary == null)
            {
                GD.PushError("Failed to find ScaleLibrary node. Check the path in PatternHighlighter.");
            }
            else
            {
                GD.Print("PatternHighlighter initialized!");
            }
        }

        /// <summary>
        /// Highlights only the notes of the specified scale that fall within the current pattern
        /// </summary>
        public void HighlightPatternScale(string rootNote, string scaleType, int? numFrets = null)
        {
            int fretsToUse = numFrets ?? _numFrets; // if null fretsTouse assigned _numFrets otherwise, assigned numFrets.

            GD.Print($"Highlighting scale pattern: {rootNote} {scaleType} (Frets {_minFret}-{_maxFret})");

            // Reset all nodes to default color first
            ResetFretboard(fretsToUse);

            // Check if ScaleLibrary exists
            if (_scaleLibrary == null)
            {
                GD.PushError("ScaleLibrary reference is null. Cannot highlight scale.");
                return;
            }

            // Get the scale definition from the library
            ScaleDefinition scale = _scaleLibrary.GetScale(scaleType);
            if (scale == null)
            {
                GD.PushError($"Invalid scale type: {scaleType}");
                return;
            }

            // Generate the scale notes based on the root note
            var scaleNotes = new List<string>();
            int rootIndex = Array.IndexOf(_allNotes, rootNote);
            if (rootIndex == -1)
            {
                GD.PushError($"Invalid root note: {rootNote}");
                return;
            }

            foreach (int step in scale.Intervals)
            {
                int noteIndex = (rootIndex + step) % _allNotes.Length;
                scaleNotes.Add(_allNotes[noteIndex]);
            }

            GD.Print($"Scale: {rootNote} {scale.DisplayName}");
            GD.Print($"Notes: {string.Join(", ", scaleNotes)}");

            // Loop through all strings and frets to highlight the scale notes
            for (int stringNum = 6; stringNum >= 1; stringNum--)  // Starting from String6 (Low E) down to String1 (High E)
            {
                // String index in the tuning array (0 for String6, 5 for String1, etc)
                int stringIndex = 6 - stringNum;
                string stringRoot = _stringRootNotes[stringIndex];
                int stringRootIndex = Array.IndexOf(_allNotes, stringRoot);

                for (int fret = 0; fret <= fretsToUse; fret++)  // Include the open string
                {
                    // Skip frets outside the pattern range
                    if (fret != 0 && (fret < _minFret || fret > _maxFret))
                    {
                        continue;
                    }

                    // Skip open strings if not included in the pattern
                    if (fret == 0 && !_includeOpenStrings)
                    {
                        continue;
                    }

                    int noteIndex = (stringRootIndex + fret) % _allNotes.Length;
                    string currentNote = _allNotes[noteIndex];

                    // Get the node for this position based on your hierarchy
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
                        if (currentNote == rootNote)
                        {
                            // This is a root note
                            ApplyMaterial(node, "root"); // Red
                        }
                        else if (scaleNotes.Contains(currentNote))
                        {
                            // This is a scale note
                            ApplyMaterial(node, "scale"); // Blue
                        }
                        else
                        {
                            // This is not in the scale, make it default
                            ApplyMaterial(node, "default"); // Grey
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Helper function to apply material to a node
        /// </summary>
        public void ApplyMaterial(Node node, string materialType)
        {
            try
            {
                // Determine which material to use
                string materialResourcePath = materialType switch
                {
                    "root" => _rootMaterialPath,
                    "scale" => _scaleMaterialPath,
                    _ => _defaultMaterialPath
                };

                // Load the material resource
                Material material = GD.Load<Material>(materialResourcePath);

                if (material == null)
                {
                    GD.PrintErr($"Failed to load material: {materialResourcePath}");
                    return;
                }

                // Assign the material to the node
                if (node is CsgPrimitive3D csgNode)
                {
                    csgNode.MaterialOverride = material;
                }
                else if (node is MeshInstance3D meshNode)
                {
                    meshNode.MaterialOverride = material;
                }
                else
                {
                    GD.Print($"Node {node.Name} is not a compatible type for materials");
                }
            }
            catch (Exception ex)
            {
                GD.PrintErr($"Error setting material for {node.Name}: {ex.Message}");
            }
        }

        /// <summary>
        /// Reset all nodes to default color
        /// </summary>
        private void ResetFretboard(int numFrets)
        {
            // Reset all fretboard nodes
            // First handle open string positions
            for (int stringNum = 6; stringNum >= 1; stringNum--)
            {
                Node3D node = GetNodeOrNull<Node3D>($"../XRToolsPickable/FretboardV3/ScaleNodes/OpenString/String{stringNum}");
                if (node != null)
                {
                    ApplyMaterial(node, "default");
                }
            }

            // Then handle all other fret positions
            for (int fret = 1; fret <= numFrets; fret++)
            {
                for (int stringNum = 6; stringNum >= 1; stringNum--)
                {
                    Node3D node = GetNodeOrNull<Node3D>($"../XRToolsPickable/FretboardV3/ScaleNodes/Fret{fret}/String{stringNum}");
                    if (node != null)
                    {
                        ApplyMaterial(node, "default");
                    }
                }
            }
        }

        /// <summary>
        /// Set the pattern range
        /// </summary>
        public void SetPatternRange(int minFret, int maxFret, bool includeOpenStrings = true)
        {
            _minFret = Mathf.Clamp(minFret, 0, 22);
            _maxFret = Mathf.Clamp(maxFret, minFret, 22);
            _includeOpenStrings = includeOpenStrings;

            GD.Print($"Pattern range set to frets {_minFret}-{_maxFret}, open strings {(_includeOpenStrings ? "included" : "excluded")}");
        }
    }
}
