using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// A guitar tuner that uses YinPitchDetector to detect notes and provide visual feedback.
/// </summary>
public partial class Tuner : Node
{
	// UI Elements
	[Export] private NodePath _pitchDisplayPath = "Tuner/Display/PitchLabel";
	[Export] private NodePath _noteDisplayPath = "Tuner/Display/NoteLabel";
	[Export] private NodePath _tuningIndicatorPath = "Tuner/Display/TuningIndicator";

	private Label3D _pitchDisplay;      // Displays frequency in Hz
	private Label3D _noteDisplay;       // Displays note name
	private Label3D _tuningIndicator;   // Displays tuning status and cents

	// Component references
	[Export] private NodePath _yinDetectorPath = "../PitchDetector"; // Path to YinPitchDetector node
	private YinPitchDetector _yinDetector;      // Reference to the YinPitchDetector

	// Settings
	[Export] private float _detectionThreshold = 0.7f;
	[Export] private float _displayDuration = 1.5f;
	[Export] private float _tuneThresholdCents = 10.0f;

	// Runtime state
	private string _lastDetectedNote = "";
	private float _detectionCooldown = 0f;
	private float _displayTimer = 0f;

	// Guitar note data - includes all notes in standard guitar range
	private readonly Dictionary<string, float> _guitarNotes = new Dictionary<string, float>
	{
        // Low E string (E2) and surrounding notes
        { "D2", 73.42f },
		{ "D#2", 77.78f },
		{ "E2", 82.41f },  // Low E (6th string)
        { "F2", 87.31f },
		{ "F#2", 92.50f },
        
        // A string (A2) and surrounding notes
        { "G2", 98.00f },
		{ "G#2", 103.83f },
		{ "A2", 110.00f }, // A (5th string)
        { "A#2", 116.54f },
		{ "B2", 123.47f },
        
        // D string (D3) and surrounding notes
        { "C3", 130.81f },
		{ "C#3", 138.59f },
		{ "D3", 146.83f }, // D (4th string)
        { "D#3", 155.56f },
		{ "E3", 164.81f },
        
        // G string (G3) and surrounding notes
        { "F3", 174.61f },
		{ "F#3", 185.00f },
		{ "G3", 196.00f }, // G (3rd string)
        { "G#3", 207.65f },
		{ "A3", 220.00f },
        
        // B string (B3) and surrounding notes
        { "A#3", 233.08f },
		{ "B3", 246.94f }, // B (2nd string)
        { "C4", 261.63f },
		{ "C#4", 277.18f },
		{ "D4", 293.66f },
        
        // High E string (E4) and surrounding notes
        { "D#4", 311.13f },
		{ "E4", 329.63f }, // High E (1st string)
        { "F4", 349.23f },
		{ "F#4", 369.99f },
		{ "G4", 392.00f }
	};

	// String identifiers for standard tuning
	private readonly string[] _standardTuning = { "E2", "A2", "D3", "G3", "B3", "E4" };

	/// <summary>
	/// Called when the node enters the scene tree.
	/// </summary>
	public override void _Ready()
	{
		GD.Print("Tuner: _Ready() called");

		// Get UI elements
		_pitchDisplay = GetNode<Label3D>(_pitchDisplayPath);
		_noteDisplay = GetNode<Label3D>(_noteDisplayPath);
		_tuningIndicator = GetNode<Label3D>(_tuningIndicatorPath);

		// Print debug information for UI elements
		GD.Print($"PitchDisplay found: {_pitchDisplay != null}, Path: {_pitchDisplayPath}");
		GD.Print($"NoteDisplay found: {_noteDisplay != null}, Path: {_noteDisplayPath}");
		GD.Print($"TuningIndicator found: {_tuningIndicator != null}, Path: {_tuningIndicatorPath}");

		// Get the YinPitchDetector
		_yinDetector = GetNode<YinPitchDetector>(_yinDetectorPath);
		if (_yinDetector == null)
		{
			GD.PrintErr("YinPitchDetector not found at path: " + _yinDetectorPath);
		}
		else
		{
			GD.Print("Tuner: YinPitchDetector connected successfully");
		}

		// Add a timer to periodically check if things are working
		var timer = new Timer();
		timer.WaitTime = 2.0f;
		timer.Autostart = true;
		AddChild(timer);
		timer.Timeout += () =>
		{
			if (_yinDetector != null)
			{
				float pitch = _yinDetector.GetLastPitch();
				float confidence = _yinDetector.GetConfidence();
				//GD.Print($"Debug - Pitch: {pitch:F1} Hz, Confidence: {confidence:F2}, Threshold: {_detectionThreshold}");
			}
		};
	}

	/// <summary>
	/// Called every frame to process audio.
	/// </summary>
	public override void _Process(double delta)
	{
		// Update timers
		if (_displayTimer > 0) _displayTimer -= (float)delta;
		if (_detectionCooldown > 0) _detectionCooldown -= (float)delta;

		// Process pitch detection if YinPitchDetector is available
		if (_yinDetector != null && _detectionCooldown <= 0)
		{
			float pitch = _yinDetector.GetLastPitch();
			float confidence = _yinDetector.GetConfidence();

			if (pitch > 0 && confidence > _detectionThreshold)
			{
				// Get note information
				(string noteName, float noteFrequency, float cents) = AnalyzeFrequency(pitch);

				// Update display
				UpdateTunerDisplay(pitch, noteName, noteFrequency, cents);

				// Reset cooldown to avoid flickering
				_detectionCooldown = 0.1f;
				_displayTimer = _displayDuration;
			}
			else if (_displayTimer <= 0)
			{
				// Clear display when not detecting
				ClearDisplay();
			}
		}
	}

	/// <summary>
	/// Analyzes a frequency to determine the closest note, its frequency, and cents deviation.
	/// </summary>
	private (string noteName, float noteFrequency, float cents) AnalyzeFrequency(float frequency)
	{
		// Find closest note in our dictionary
		var closestNote = _guitarNotes.OrderBy(n => Math.Abs(frequency - n.Value)).First();
		string noteName = closestNote.Key;
		float noteFrequency = closestNote.Value;

		// Calculate cents deviation
		float cents = 1200.0f * (float)(Math.Log(frequency / noteFrequency) / Math.Log(2.0));

		//GD.Print($"Analysis - Freq: {frequency:F1} Hz, Note: {noteName}, Ref Freq: {noteFrequency:F1} Hz, Cents: {cents:F1}");

		return (noteName, noteFrequency, cents);
	}

	/// <summary>
	/// Updates the display with current tuning information.
	/// </summary>
	private void UpdateTunerDisplay(float frequency, string noteName, float noteFrequency, float cents)
	{
		GD.Print($"Updating display - Freq: {frequency:F1} Hz, Note: {noteName}, Cents: {cents:F1}");

		if (_pitchDisplay != null)
		{
			_pitchDisplay.Text = $"{frequency:F1} Hz";
			GD.Print($"Set pitch display: {_pitchDisplay.Text}");
		}
		else
		{
			GD.PrintErr("PitchDisplay is null when trying to update!");
		}

		if (_noteDisplay != null)
		{
			// Get the note name and indicate if it's a guitar string in standard tuning
			bool isStandardString = _standardTuning.Contains(noteName);
			string displayNote = noteName.Replace("#", "♯");

			if (isStandardString)
			{
				// Identify which string this is in standard tuning
				int stringNumber = Array.IndexOf(_standardTuning, noteName) + 1;
				_noteDisplay.Text = $"{displayNote} ({stringNumber})";
			}
			else
			{
				_noteDisplay.Text = displayNote;
			}

			GD.Print($"Set note display: {_noteDisplay.Text}");
		}
		else
		{
			GD.PrintErr("NoteDisplay is null when trying to update!");
		}

		if (_tuningIndicator != null)
		{
			if (Math.Abs(cents) < _tuneThresholdCents)
			{
				_tuningIndicator.Text = "IN TUNE";
				_tuningIndicator.Modulate = Colors.Green;
			}
			else
			{
				_tuningIndicator.Text = $"{Math.Abs(cents):F0} cents {(cents > 0 ? "♯" : "♭")}";
				_tuningIndicator.Modulate = Colors.Red;
			}

			GD.Print($"Set tuning indicator: {_tuningIndicator.Text}");
		}
		else
		{
			GD.PrintErr("TuningIndicator is null when trying to update!");
		}
	}

	/// <summary>
	/// Clears the tuner display when no note is detected.
	/// </summary>
	private void ClearDisplay()
	{
		if (_pitchDisplay != null)
			_pitchDisplay.Text = "--";

		if (_noteDisplay != null)
			_noteDisplay.Text = "--";

		if (_tuningIndicator != null)
		{
			_tuningIndicator.Text = "Play a note...";
			_tuningIndicator.Modulate = Colors.Gray;
		}
	}
}