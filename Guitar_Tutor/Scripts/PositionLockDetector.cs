using Godot;
using System;
using System.Linq;
using System.Collections.Generic;
using GuitarTutor.Audio;
using GuitarTutor.Visual;

namespace GuitarTutor.Detection
{
    public partial class PositionLockDetector : Node
    {
        // References to other components
        [Export] private NodePath _patternHighlighterPath;

        // Position lock configuration
        [Export] private int _minFret = 0;
        [Export] private int _maxFret = 4;
        [Export] private bool _includeOpenStrings = true;

        // New option to enable/disable single note mode
        [Export] private bool _singleNoteMode = true;

        // Configuration
        [Export] private float _confidenceThreshold = 0.9f;
        [Export] private float _highlightDuration = 1.5f;
        [Export] private string _playedNoteMaterialPath = "res://Guitar_Tutor/Assets/Material/played_note.tres";
        
        // Buffer management
        [Export] private int _bufferClearInterval = 10; // Clear buffer every N frames
        private int _frameCounter = 0;
        private AudioEffectCapture _captureEffect;
        
        // Pattern validation
        [Export] private bool _validateAgainstPattern = true;
        [Export] private float _patternValidationThreshold = 0.8f;
        
        // Pattern data
        private HashSet<(int stringNum, int fret)> _patternPositions = new HashSet<(int stringNum, int fret)>();

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
        
        // Pre-calculated note positions for performance
        private Dictionary<string, List<(int stringNum, int fret)>> _notePositionsCache;

        public override void _Ready()
        {
            // Get references to other components
            _yinDetector = GetNode<YinPitchDetector>("/root/YinPitchDetector");
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
            
            // Initialize the capture effect reference
            int busIndex = AudioServer.GetBusIndex("Capture");
            if (busIndex != -1)
            {
                var effect = AudioServer.GetBusEffect(busIndex, 0);
                if (effect is AudioEffectCapture capture)
                {
                    _captureEffect = capture;
                    GD.Print("Audio capture effect initialized successfully");
                }
            }
            
            // Pre-calculate note positions for all notes in the position lock range
            PrecalculateNotePositions();
            
            // Initialize pattern positions
            UpdatePatternPositions();

            GD.Print($"PositionLockDetector initialized with fret range: {_minFret}-{_maxFret}");
            GD.Print($"Open strings {(_includeOpenStrings ? "included" : "excluded")}");
            GD.Print($"Single note mode is {(_singleNoteMode ? "enabled" : "disabled")}");
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
            
            // Manage audio buffer to prevent it from getting full
            _frameCounter++;
            if (_frameCounter >= _bufferClearInterval)
            {
                _frameCounter = 0;
                ClearAudioBuffer();
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

                    // Find positions to highlight
                    if (_singleNoteMode)
                    {
                        // In single note mode, find the most likely position based on frequency
                        HighlightSingleNoteByFrequency(noteWithoutOctave, pitch);
                    }
                    else
                    {
                        // In normal mode, highlight all occurrences within position lock
                        HighlightNotesInPositionLock(noteWithoutOctave);
                    }

                    // Start the highlight timer
                    _noteTimer = _highlightDuration;

                    GD.Print($"Detected note: {detectedNote} (Frequency: {pitch:F1} Hz, Confidence: {confidence:F2})");
                }
            }
        }
        
        /// <summary>
        /// Clear the audio buffer to prevent it from getting full
        /// </summary>
        private void ClearAudioBuffer()
        {
            if (_captureEffect != null)
            {
                int framesAvailable = _captureEffect.GetFramesAvailable();
                if (framesAvailable > 0)
                {
                    _captureEffect.ClearBuffer();
                    GD.Print($"Cleared audio buffer with {framesAvailable} frames available");
                }
            }
        }
        
        /// <summary>
        /// Pre-calculate all note positions within the position lock range for better performance
        /// </summary>
        private void PrecalculateNotePositions()
        {
            _notePositionsCache = new Dictionary<string, List<(int stringNum, int fret)>>();
            
            // For each note in our scale
            foreach (string note in _allNotes)
            {
                _notePositionsCache[note] = new List<(int stringNum, int fret)>();
                
                // For each string
                for (int stringNum = 6; stringNum >= 1; stringNum--)
                {
                    // String index in the tuning array (0 for String6, 5 for String1)
                    int stringIndex = 6 - stringNum;
                    string stringRoot = _stringRootNotes[stringIndex];
                    int stringRootIndex = Array.IndexOf(_allNotes, stringRoot);
                    
                    // Only check frets within the position lock range (plus open strings if included)
                    int startFret = _includeOpenStrings ? 0 : 1;
                    int endFret = _maxFret;
                    
                    // For each fret in the position lock range
                    for (int fret = startFret; fret <= endFret; fret++)
                    {
                        // Skip frets outside the locked position (except for open strings if included)
                        if (fret != 0 && (fret < _minFret || fret > _maxFret))
                        {
                            continue;
                        }
                        
                        // Calculate the note at this position
                        int noteIndex = (stringRootIndex + fret) % _allNotes.Length;
                        string currentNote = _allNotes[noteIndex];
                        
                        // If this position produces our target note, add it to the cache
                        if (currentNote == note)
                        {
                            _notePositionsCache[note].Add((stringNum, fret));
                        }
                    }
                }
            }
            
            GD.Print("Note positions pre-calculated for all notes in position lock range");
        }

        /// <summary>
        /// Update the pattern positions based on the current pattern
        /// </summary>
        private void UpdatePatternPositions()
        {
            _patternPositions.Clear();
            
            if (_patternHighlighter == null)
            {
                GD.Print("PatternHighlighter not available, cannot update pattern positions");
                return;
            }
            
            // Get the current pattern from the pattern highlighter
            // This assumes the pattern highlighter has a method to get the current pattern
            // If not, we'll need to implement a different approach
            
            // For now, we'll use a simple approach: check if the pattern highlighter has any highlighted nodes
            // and extract their positions
            
            // This is a placeholder - you'll need to implement the actual logic based on your PatternHighlighter class
            // For example, if your PatternHighlighter has a method to get the current pattern positions:
            // _patternPositions = _patternHighlighter.GetCurrentPatternPositions();
            
            // For now, we'll just log that we need to implement this
            GD.Print("Pattern position extraction needs to be implemented based on your PatternHighlighter class");
        }
        
        /// <summary>
        /// Check if a position is part of the current pattern
        /// </summary>
        private bool IsPositionInPattern(int stringNum, int fret)
        {
            // If pattern validation is disabled, all positions are considered valid
            if (!_validateAgainstPattern)
            {
                return true;
            }
            
            // Check if the position is in our pattern positions set
            return _patternPositions.Contains((stringNum, fret));
        }

        /// <summary>
        /// Find and highlight a single note position based on frequency matching
        /// </summary>
        private void HighlightSingleNoteByFrequency(string noteName, float frequency)
        {
            GD.Print($"Finding best position for note: {noteName} (Frequency: {frequency:F1} Hz)");

            // Track the best position and its score
            Node3D bestNode = null;
            float bestScore = float.MinValue;

            // For each string
            for (int stringNum = 6; stringNum >= 1; stringNum--)
            {
                // String index in the tuning array (0 for String6, 5 for String1)
                int stringIndex = 6 - stringNum;
                string stringRoot = _stringRootNotes[stringIndex];
                int stringRootIndex = Array.IndexOf(_allNotes, stringRoot);

                // Check each fret
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

                    // Calculate the note at this position
                    int noteIndex = (stringRootIndex + fret) % _allNotes.Length;
                    string currentNote = _allNotes[noteIndex];

                    // If this position matches our note
                    if (currentNote == noteName)
                    {
                        // Calculate the expected frequency for this position
                        float expectedFrequency = CalculateExpectedFrequency(stringNum, fret);

                        // Calculate a score based on how close the frequency matches
                        // (higher is better, using negative difference to make higher = better)
                        float score = -Math.Abs(frequency - expectedFrequency);
                        
                        // Apply pattern validation if enabled
                        if (_validateAgainstPattern)
                        {
                            // Check if this position is part of the current pattern
                            bool isInPattern = IsPositionInPattern(stringNum, fret);
                            
                            // If not in pattern, reduce the score significantly
                            if (!isInPattern)
                            {
                                score -= 1000.0f; // Large penalty for positions not in the pattern
                                GD.Print($"Position (String {stringNum}, Fret {fret}) not in pattern, applying penalty");
                            }
                        }

                        // Debug
                        GD.Print($"Potential position: String {stringNum}, Fret {fret}, " +
                                 $"Expected: {expectedFrequency:F1} Hz, Detected: {frequency:F1} Hz, Score: {score:F1}");

                        // If this is the best match so far
                        if (score > bestScore)
                        {
                            // Get the node for this position
                            Node3D node;

                            // Find the node path based on whether this is an open string or fretted note
                            if (fret == 0)
                            {
                                node = GetNodeOrNull<Node3D>($"../XRToolsPickable/FretboardV3/ScaleNodes/OpenString/String{stringNum}");
                            }
                            else
                            {
                                node = GetNodeOrNull<Node3D>($"../XRToolsPickable/FretboardV3/ScaleNodes/Fret{fret}/String{stringNum}");
                            }

                            // If we found the node, update our best match
                            if (node != null)
                            {
                                bestNode = node;
                                bestScore = score;
                            }
                        }
                    }
                }
            }

            // If we found a best match
            if (bestNode != null)
            {
                // Highlight only this one node
                _highlightedNodes.Add(bestNode);
                ApplyHighlightMaterial(bestNode);
                GD.Print($"Highlighted node: {bestNode.Name} (best frequency match)");
            }
            else
            {
                GD.Print($"No suitable position found for note {noteName} within position lock range");
            }
        }

        /// <summary>
        /// Calculate the expected frequency for a string and fret position
        /// </summary>
        private float CalculateExpectedFrequency(int stringNum, int fret)
        {
            // Base frequencies for open strings (standard tuning)
            float[] openStringFrequencies = {
                82.41f,  // String 6 (low E)
                110.00f, // String 5 (A)
                146.83f, // String 4 (D)
                196.00f, // String 3 (G)
                246.94f, // String 2 (B)
                329.63f  // String 1 (high E)
            };

            // Convert string number to index (0-based)
            int stringIndex = 6 - stringNum;

            // Each fret is a semitone (multiply by 2^(1/12))
            return openStringFrequencies[stringIndex] * Mathf.Pow(2, fret / 12.0f);
        }

        /// <summary>
        /// Find positions on the fretboard for a specific note within the position-locked range
        /// </summary>
        private void HighlightNotesInPositionLock(string noteName)
        {
            GD.Print($"Highlighting note in position lock: {noteName} (Frets {_minFret}-{_maxFret})");

            // Use the pre-calculated positions from the cache
            if (_notePositionsCache.ContainsKey(noteName))
            {
                // If pattern validation is enabled, filter positions to only those in the pattern
                List<(int stringNum, int fret)> validPositions = _notePositionsCache[noteName];
                
                if (_validateAgainstPattern)
                {
                    // Filter positions to only those in the current pattern
                    validPositions = validPositions.Where(pos => 
                        IsPositionInPattern(pos.stringNum, pos.fret)).ToList();
                    
                    GD.Print($"Filtered to {validPositions.Count} positions in the current pattern");
                }
                
                foreach (var position in validPositions)
                {
                    int stringNum = position.stringNum;
                    int fret = position.fret;
                    
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
            else
            {
                GD.Print($"No positions found for note {noteName} in the cache");
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
            
            // Recalculate note positions for the new range
            PrecalculateNotePositions();
            
            // Update pattern positions
            UpdatePatternPositions();

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
            
            // Recalculate note positions for the new setting
            PrecalculateNotePositions();
            
            // Update pattern positions
            UpdatePatternPositions();

            GD.Print($"Open strings {(_includeOpenStrings ? "included" : "excluded")}");
        }

        /// <summary>
        /// Toggle between single note mode and all notes mode
        /// </summary>
        public void SetSingleNoteMode(bool enabled)
        {
            _singleNoteMode = enabled;

            // Reset any current highlights
            ResetHighlightedNotes();

            GD.Print($"Single note mode {(_singleNoteMode ? "enabled" : "disabled")}");
        }

        /// <summary>
        /// Toggle pattern validation on/off
        /// </summary>
        public void SetValidateAgainstPattern(bool validate)
        {
            _validateAgainstPattern = validate;
            GD.Print($"Pattern validation {(_validateAgainstPattern ? "enabled" : "disabled")}");
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
}