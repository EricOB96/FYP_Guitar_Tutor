using Godot;
using System;
using System.Collections.Generic;

namespace GuitarTutor.Data
{
    public partial class ScaleLibrary : Node
    {
        // Signals for scale and root note changes
        [Signal]
        public delegate void RootNoteChangedEventHandler(string newRootNote);

        [Signal]
        public delegate void ScaleTypeChangedEventHandler(string newScaleType);

        // Properties with change notification
        private string _currentRootNote = "A";
        public string CurrentRootNote
        {
            get => _currentRootNote;
            set
            {
                if (_currentRootNote != value)
                {
                    _currentRootNote = value;
                    GD.Print($"ScaleLibrary root note changed to: {value}");
                    EmitSignal(SignalName.RootNoteChanged, value);
                }
            }
        }

        private string _currentScaleType = "major";
        public string CurrentScaleType
        {
            get => _currentScaleType;
            set
            {
                if (_currentScaleType != value)
                {
                    _currentScaleType = value;
                    GD.Print($"ScaleLibrary scale type changed to: {value}");
                    EmitSignal(SignalName.ScaleTypeChanged, value);
                }
            }
        }

        // Dictionary to store scale definitions
        private Dictionary<string, ScaleDefinition> _scales = new Dictionary<string, ScaleDefinition>();

        // Called when the node enters the scene tree for the first time.
        public override void _Ready()
        {
            // Initialize with default scales
            InitializeScales();
            GD.Print("Scale Library initialized with standard scales");
        }

        // Initialize the scale library with default scales
        private void InitializeScales()
        {
            // Major scale
            _scales["major"] = new ScaleDefinition(
                "Major",
                new[] { 0, 2, 4, 5, 7, 9, 11 },
                "The standard major scale"
            );

            // Minor scale
            _scales["minor"] = new ScaleDefinition(
                "Minor",
                new[] { 0, 2, 3, 5, 7, 8, 10 },
                "The natural minor scale"
            );

            // Major Pentatonic scale
            _scales["pentatonic_major"] = new ScaleDefinition(
                "Major Pentatonic",
                new[] { 0, 2, 4, 7, 9 },
                "Five-note major scale"
            );

            // Minor Pentatonic scale
            _scales["pentatonic_minor"] = new ScaleDefinition(
                "Minor Pentatonic",
                new[] { 0, 3, 5, 7, 10 },
                "Five-note minor scale"
            );
        }

        // Get a scale definition by its key name
        public ScaleDefinition GetScale(string scaleKey)
        {
            if (_scales.TryGetValue(scaleKey.ToLower(), out var scale))
            {
                return scale;
            }

            GD.PushError($"Scale not found: {scaleKey}");
            return null;
        }

        // Get all scale keys
        public string[] GetScaleKeys()
        {
            string[] keys = new string[_scales.Count];
            _scales.Keys.CopyTo(keys, 0);
            return keys;
        }

        // Get all scale display names
        public string[] GetScaleDisplayNames()
        {
            string[] names = new string[_scales.Count];
            int index = 0;

            foreach (var scale in _scales.Values)
            {
                names[index++] = scale.DisplayName;
            }

            return names;
        }

        // Convert display name to scale key
        public string GetScaleKeyFromDisplayName(string displayName)
        {
            foreach (var kvp in _scales)
            {
                if (kvp.Value.DisplayName.Equals(displayName, StringComparison.OrdinalIgnoreCase))
                {
                    return kvp.Key;
                }
            }

            // Fallback if not found
            return displayName.ToLower().Replace(" ", "_");
        }
    }

    // Class to represent a scale definition
    public class ScaleDefinition
    {
        // User-friendly display name
        public string DisplayName { get; private set; }

        // Array of intervals (half-steps from root)
        public int[] Intervals { get; private set; }

        // Description of the scale
        public string Description { get; private set; }

        public ScaleDefinition(string displayName, int[] intervals, string description)
        {
            DisplayName = displayName;
            Intervals = intervals;
            Description = description;
        }
    }
}