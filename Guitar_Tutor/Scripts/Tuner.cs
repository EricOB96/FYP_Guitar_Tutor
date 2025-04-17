using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using GuitarTutor.Audio;

namespace GuitarTutor.UI
{
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

		// Yin references
		private YinPitchDetector _yinDetector;      // Reference to the YinPitchDetector

		// Standard tuning notes and frequencies
		private readonly Dictionary<string, float> _standardTuningNotes = new Dictionary<string, float>
		{
			{ "E2", 82.41f },  // Low E (6th string)
			{ "A2", 110.00f }, // A (5th string)
			{ "D3", 146.83f }, // D (4th string)
			{ "G3", 196.00f }, // G (3rd string)
			{ "B3", 246.94f }, // B (2nd string)
			{ "E4", 329.63f }  // High E (1st string)
		};

		// String identifiers and names
		private readonly (string note, string name, int stringNumber)[] _standardTuning = new[]
		{
			("E2", "Low E", 6),
			("A2", "A", 5),
			("D3", "D", 4),
			("G3", "G", 3),
			("B3", "B", 2),
			("E4", "High E", 1)
		};

		// Tuner settings
		[Export] private float _confidenceThreshold = 0.85f;  // Minimum confidence to consider a note valid
		[Export] private float _tuneThresholdCents = 5.0f;    // How close to target frequency to consider "in tune"
		[Export] private float _displayDuration = 3.0f;       // How long to display a note before clearing
		[Export] private float _minFrequency = 65.0f;         // Minimum frequency to consider (below low E)
		[Export] private float _maxFrequency = 420.0f;        // Maximum frequency to consider (above high E)
		[Export] private int _bufferSize = 15;                // Increased buffer size for better averaging
		[Export] private float _smoothingFactor = 0.25f;      // Reduced smoothing for faster response
		[Export] private bool _debugMode = true;              // Enable debug output
		[Export] private float _frequencyCorrectionThreshold = 1.5f; // Percent difference to apply correction
		[Export] private float _highStringCorrectionThreshold = 1.0f; // Stricter threshold for high strings

		// Runtime state
		private float _displayTimer = 0f;
		private float _lastDisplayedPitch = 0f;
		private float _lastDisplayedCents = 0f;
		private string _lastDetectedNote = "";
		private bool _isDisplayingNote = false;
		private float[] _pitchBuffer;
		private float[] _confidenceBuffer;
		private int _bufferIndex = 0;
		private int _validSamplesCount = 0;
		private float _lastRawPitch = 0f;
		private float _lastRawConfidence = 0f;
		private float _silenceTimer = 0f;
		private const float SILENCE_THRESHOLD = 0.3f;
		private const float SILENCE_DURATION = 0.5f;

		/// <summary>
		/// Called when the node enters the scene tree.
		/// </summary>
		public override void _Ready()
		{
			// Initialize buffers
			_pitchBuffer = new float[_bufferSize];
			_confidenceBuffer = new float[_bufferSize];
			ClearBuffers();

			// Get UI elements
			_pitchDisplay = GetNode<Label3D>(_pitchDisplayPath);
			_noteDisplay = GetNode<Label3D>(_noteDisplayPath);
			_tuningIndicator = GetNode<Label3D>(_tuningIndicatorPath);

			// Get the YinPitchDetector
			_yinDetector = GetNode<YinPitchDetector>("/root/YinPitchDetector");

			if (_yinDetector == null)
			{
				GD.PrintErr("Could not find autoloaded YinPitchDetector");
			}

			// Clear display initially
			ClearDisplay();
		}

		/// <summary>
		/// Called every frame to process audio.
		/// </summary>
		public override void _Process(double delta)
		{
			// Update timers
			if (_displayTimer > 0)
			{
				_displayTimer -= (float)delta;
				if (_displayTimer <= 0 && _isDisplayingNote)
				{
					ClearDisplay();
				}
			}

			// Process pitch detection if YinPitchDetector is available
			if (_yinDetector != null)
			{
				float rawPitch = _yinDetector.GetLastPitch();
				float confidence = _yinDetector.GetConfidence();

				// Store raw values for debugging
				_lastRawPitch = rawPitch;
				_lastRawConfidence = confidence;

				// Check for silence
				if (confidence < SILENCE_THRESHOLD)
				{
					_silenceTimer += (float)delta;
					if (_silenceTimer >= SILENCE_DURATION && _isDisplayingNote)
					{
						ClearDisplay();
					}
					return;
				}
				else
				{
					_silenceTimer = 0f;
				}

				// Skip processing if pitch is unrealistic for guitar
				if (rawPitch < _minFrequency || rawPitch > _maxFrequency)
				{
					if (_debugMode)
					{
						GD.Print($"Skipping unrealistic pitch: {rawPitch:F1}Hz");
					}
					return;
				}

				// Add to buffer if confidence is high enough
				if (confidence > _confidenceThreshold)
				{
					_pitchBuffer[_bufferIndex] = rawPitch;
					_confidenceBuffer[_bufferIndex] = confidence;
					_bufferIndex = (_bufferIndex + 1) % _bufferSize;
					_validSamplesCount = Math.Min(_validSamplesCount + 1, _bufferSize);

					// Only process if we have enough samples
					if (_validSamplesCount >= 3)
					{
						// Get the average pitch
						float avgPitch = GetAveragePitch();

						// Analyze frequency to get note info
						(string noteName, float noteFrequency, float cents, int stringNumber) =
							AnalyzeFrequency(avgPitch);

						// Check if we detected the same note consistently
						if (noteName == _lastDetectedNote)
						{
							// Update display with smoothed values
							float smoothedPitch = Mathf.Lerp(_lastDisplayedPitch, avgPitch, _smoothingFactor);
							float smoothedCents = Mathf.Lerp(_lastDisplayedCents, cents, _smoothingFactor);

							_lastDisplayedPitch = smoothedPitch;
							_lastDisplayedCents = smoothedCents;
							_isDisplayingNote = true;
							_displayTimer = _displayDuration;

							// Update display
							UpdateTunerDisplay(smoothedPitch, noteName, noteFrequency, smoothedCents, stringNumber);

							if (_debugMode)
							{
								GD.Print($"Note: {noteName}, Raw: {rawPitch:F1}Hz, Avg: {avgPitch:F1}Hz, " +
									$"Target: {noteFrequency:F1}Hz, Cents: {cents:F1}, Conf: {confidence:F3}");
							}
						}
						else
						{
							// New note detected
							_lastDetectedNote = noteName;
							_lastDisplayedPitch = avgPitch;
							_lastDisplayedCents = cents;
							_isDisplayingNote = true;
							_displayTimer = _displayDuration;

							// Update display
							UpdateTunerDisplay(avgPitch, noteName, noteFrequency, cents, stringNumber);

							if (_debugMode)
							{
								GD.Print($"New note: {noteName}, Raw: {rawPitch:F1}Hz, Avg: {avgPitch:F1}Hz, " +
									$"Target: {noteFrequency:F1}Hz, Cents: {cents:F1}, Conf: {confidence:F3}");
							}
						}
					}
				}
			}
		}

		/// <summary>
		/// Clears all buffers.
		/// </summary>
		private void ClearBuffers()
		{
			for (int i = 0; i < _bufferSize; i++)
			{
				_pitchBuffer[i] = 0f;
				_confidenceBuffer[i] = 0f;
			}
			_bufferIndex = 0;
			_validSamplesCount = 0;
		}

		/// <summary>
		/// Gets the average pitch value from the buffer.
		/// </summary>
		private float GetAveragePitch()
		{
			if (_validSamplesCount == 0) return 0f;

			// Use median for more stability
			List<float> validPitches = new List<float>();
			for (int i = 0; i < _bufferSize; i++)
			{
				if (_pitchBuffer[i] > 0)
				{
					validPitches.Add(_pitchBuffer[i]);
				}
			}

			if (validPitches.Count == 0) return 0f;

			validPitches.Sort();
			return validPitches[validPitches.Count / 2];
		}

		/// <summary>
		/// Analyzes a frequency to determine the closest standard tuning note, its frequency, and cents deviation.
		/// </summary>
		private (string noteName, float noteFrequency, float cents, int stringNumber) AnalyzeFrequency(float frequency)
		{
			if (_debugMode)
			{
				GD.Print($"Analyzing frequency: {frequency:F2}Hz");
			}
			
			// Find closest note and string number from our standard tuning
			var closestPair = _standardTuning
				.Select(pair => new
				{
					Note = pair.note,
					Frequency = _standardTuningNotes[pair.note],
					StringNumber = pair.stringNumber,
					Distance = Math.Abs(frequency - _standardTuningNotes[pair.note])
				})
				.OrderBy(x => x.Distance)
				.First();

			string noteName = closestPair.Note;
			float noteFrequency = closestPair.Frequency;
			int stringNumber = closestPair.StringNumber;

			// Calculate cents deviation
			float cents = 1200.0f * (float)(Math.Log(frequency / noteFrequency) / Math.Log(2.0));

			// Apply frequency correction if very close to a guitar string
			float percentDiff = Math.Abs(frequency - noteFrequency) / noteFrequency * 100;
			
			// Use stricter threshold for high strings (B3 and E4)
			float correctionThreshold = (stringNumber <= 2) ? _highStringCorrectionThreshold : _frequencyCorrectionThreshold;
			
			if (percentDiff < correctionThreshold)
			{
				if (_debugMode)
				{
					GD.Print($"Correcting frequency from {frequency:F2}Hz to {noteFrequency:F2}Hz (within {percentDiff:F1}%)");
				}
				frequency = noteFrequency;
				cents = 0.0f; // Reset cents since we're using the exact frequency
			}
			else if (_debugMode && stringNumber <= 2)
			{
				GD.Print($"High string ({noteName}) detected at {frequency:F2}Hz, target: {noteFrequency:F2}Hz, diff: {percentDiff:F1}%");
			}

			return (noteName, noteFrequency, cents, stringNumber);
		}

		/// <summary>
		/// Updates the display with current tuning information.
		/// </summary>
		private void UpdateTunerDisplay(float frequency, string noteName, float noteFrequency, float cents, int stringNumber)
		{
			if (_pitchDisplay != null)
			{
				_pitchDisplay.Text = $"{frequency:F1} Hz";
			}

			if (_noteDisplay != null)
			{
				// Get the string name
				string stringName = _standardTuning.First(t => t.note == noteName).name;
				_noteDisplay.Text = $"{stringName} String ({stringNumber})";
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

					// Color gradient from yellow to red based on how far out of tune
					float normalizedCents = Mathf.Clamp(Mathf.Abs(cents) / 50f, 0, 1);
					_tuningIndicator.Modulate = Colors.Yellow.Lerp(Colors.Red, normalizedCents);
				}
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
				_noteDisplay.Text = "Standard Tuning";

			if (_tuningIndicator != null)
			{
				_tuningIndicator.Text = "Play a string...";
				_tuningIndicator.Modulate = Colors.Gray;
			}

			_isDisplayingNote = false;
			_lastDetectedNote = "";
			_lastDisplayedPitch = 0f;
			_lastDisplayedCents = 0f;
			ClearBuffers();
		}
	}
}