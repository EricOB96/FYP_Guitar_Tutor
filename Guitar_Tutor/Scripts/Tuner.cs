using Godot;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

/// <summary>
/// A guitar tuner class that detects pitch from audio input and displays tuning information in 3D space.
/// </summary>
public partial class Tuner : Node3D
{
	// Constants for audio processing
	private const int SAMPLE_RATE = 44100;  // Standard audio sample rate in Hz
	private const int BUFFER_SIZE = 2048;   // Size of audio buffer for processing
	private const float DEFAULT_THRESHOLD = 0.15f;  // Threshold for pitch detection

	
	private class PitchDetector
	{
		private int bufferSize;
		private float[] yinBuffer;  // Buffer for YIN algorithm calculations

		/// <summary>
		/// Initializes a new pitch detector with specified buffer size.
		/// </summary>
		public PitchDetector(int size)
		{
			bufferSize = size;
			yinBuffer = new float[size / 2];
		}

		/// <summary>
		/// Main pitch detection method implementing the YIN algorithm.
		/// </summary>
		/// <returns>Detected frequency in Hz, or 0 if no pitch detected.</returns>
		public float DetectPitch(float[] audioBuffer)
		{
			int tauEstimate = -1;
			float pitchInHz = 0.0f;

			DifferenceFunction(audioBuffer);
			CumulativeMeanNormalizedDifference();
			tauEstimate = AbsoluteThreshold();

			if (tauEstimate != -1)
			{
				float betterTau = ParabolicInterpolation(tauEstimate);
				pitchInHz = SAMPLE_RATE / betterTau;
			}

			return pitchInHz;
		}

		/// <summary>
		/// Step 1 of YIN: Calculates the squared difference function.
		/// </summary>
		private void DifferenceFunction(float[] audioBuffer)
		{
			for (int tau = 0; tau < yinBuffer.Length; tau++)
			{
				yinBuffer[tau] = 0.0f;
			}

			for (int tau = 1; tau < yinBuffer.Length; tau++)
			{
				for (int i = 0; i < yinBuffer.Length; i++)
				{
					float delta = audioBuffer[i] - audioBuffer[i + tau];
					yinBuffer[tau] += delta * delta;
				}
			}
		}

		/// <summary>
		/// Step 2 of YIN: Normalizes the difference function.
		/// </summary>
		private void CumulativeMeanNormalizedDifference()
		{
			float runningSum = 0.0f;
			yinBuffer[0] = 1.0f;

			for (int tau = 1; tau < yinBuffer.Length; tau++)
			{
				runningSum += yinBuffer[tau];
				yinBuffer[tau] *= tau / runningSum;
			}
		}

		/// <summary>
		/// Step 3 of YIN: Searches for the first dip in the normalized difference below the threshold.
		/// </summary>
		private int AbsoluteThreshold()
		{
			for (int tau = 2; tau < yinBuffer.Length; tau++)
			{
				if (yinBuffer[tau] < DEFAULT_THRESHOLD)
				{
					while (tau + 1 < yinBuffer.Length && yinBuffer[tau + 1] < yinBuffer[tau])
					{
						tau++;
					}
					return tau;
				}
			}
			return -1;
		}

		/// <summary>
		/// Step 4 of YIN: Refines the pitch estimate using parabolic interpolation.
		/// </summary>
		private float ParabolicInterpolation(int tauEstimate)
		{
			int x0 = tauEstimate > 0 ? tauEstimate - 1 : tauEstimate;
			int x2 = tauEstimate + 1 < yinBuffer.Length ? tauEstimate + 1 : tauEstimate;

			if (x0 == tauEstimate)
			{
				return yinBuffer[tauEstimate] <= yinBuffer[x2] ? tauEstimate : x2;
			}
			else if (x2 == tauEstimate)
			{
				return yinBuffer[tauEstimate] <= yinBuffer[x0] ? tauEstimate : x0;
			}

			float s0 = yinBuffer[x0];
			float s1 = yinBuffer[tauEstimate];
			float s2 = yinBuffer[x2];
			return tauEstimate + (s2 - s0) / (2.0f * (2.0f * s1 - s2 - s0));
		}
	}

	// Private member variables
	private PitchDetector pitchDetector;
	private AudioEffectCapture audioEffect;
	private float[] audioBuffer;
	private Timer audioCheckTimer;

	// UI Elements
	[Export]
	private Label3D pitchDisplay;      // Displays frequency in Hz
	[Export]
	private Label3D noteDisplay;       // Displays note name
	[Export]
	private Label3D tuningIndicator;   // Displays tuning status and cents

	private Node3D xrCamera;           // Reference to XR camera for positioning

	/// <summary>
	/// Called when the node enters the scene tree.
	/// </summary>
	public override void _Ready()
	{
		Initialize();
	}

	/// <summary>
	/// Initializes the tuner components and audio system.
	/// </summary>
	private void Initialize()
	{
		try
		{
			pitchDetector = new PitchDetector(BUFFER_SIZE);
			audioBuffer = new float[BUFFER_SIZE];
			SetupAudio();
			ConfigureDisplay();

			// Get XR camera reference for positioning
			xrCamera = GetNode<Node3D>("../Player/XRCamera3D");
		}
		catch (Exception e)
		{
			GD.PrintErr($"Error during initialization: {e.Message}");
		}
	}

	/// <summary>
	/// Sets up the audio capture system and monitoring timer.
	/// </summary>
	private void SetupAudio()
	{
		int busIdx = AudioServer.GetBusIndex("Capture");
		if (busIdx == -1)
		{
			busIdx = AudioServer.BusCount;
			AudioServer.AddBus();
			AudioServer.SetBusName(busIdx, "Capture");
		}

		audioEffect = new AudioEffectCapture();
		AudioServer.AddBusEffect(busIdx, audioEffect, 0);

		// Request audio permission on Android
		if (OS.GetName() == "Android")
		{
			OS.RequestPermission("RECORD_AUDIO");
		}

		// Setup timer to monitor audio system
		if (audioCheckTimer == null)
		{
			audioCheckTimer = new Timer();
			AddChild(audioCheckTimer);
		}
		audioCheckTimer.WaitTime = 5.0;
		audioCheckTimer.Timeout += CheckAudio;
		audioCheckTimer.Start();
	}

	/// <summary>
	/// Checks audio system health and reinitializes if necessary.
	/// </summary>
	private void CheckAudio()
	{
		if (audioEffect == null || audioEffect.GetFramesAvailable() == 0)
		{
			GD.Print("Audio stopped - reinitializing...");
			int busIdx = AudioServer.GetBusIndex("Capture");
			if (busIdx != -1)
			{
				AudioServer.RemoveBus(busIdx);
			}
			SetupAudio();
		}
	}

	/// <summary>
	/// Configures the initial state of the display elements.
	/// </summary>
	private void ConfigureDisplay()
	{
		pitchDisplay.Text = "--";
		noteDisplay.Text = "--";
		tuningIndicator.Text = "Waiting...";
		tuningIndicator.Modulate = Colors.Gray;

		pitchDisplay.FontSize = 24;
		noteDisplay.FontSize = 32;
		tuningIndicator.FontSize = 20;
	}

	/// <summary>
	/// Retrieves audio samples from the capture buffer.
	/// </summary>
	/// <returns>Array of audio samples, or empty array if not enough samples available.</returns>
	private float[] GetAudioSamples()
	{
		int available = audioEffect.GetFramesAvailable();
		if (available >= BUFFER_SIZE)
		{
			var samples = audioEffect.GetBuffer(BUFFER_SIZE);
			for (int i = 0; i < BUFFER_SIZE; i++)
			{
				audioBuffer[i] = samples[i].X;
			}
			return audioBuffer;
		}
		return Array.Empty<float>();
	}

	/// <summary>
	/// Updates the display with current tuning information.
	/// </summary>
	private void UpdateTunerDisplay(float frequency)
	{
		if (frequency == 0)
			return;

		float minDistance = float.PositiveInfinity;
		float closestFreq = 82.41f;

		// Standard guitar string frequencies
		float[] noteFreqs = { 82.41f, 110.00f, 146.83f, 196.00f, 246.94f, 329.63f };
		foreach (float noteFreq in noteFreqs)
		{
			float distance = Math.Abs(frequency - noteFreq);
			if (distance < minDistance)
			{
				minDistance = distance;
				closestFreq = noteFreq;
			}
		}

		// Calculate cents deviation from closest note
		float cents = 1200.0f * (float)(Math.Log(frequency / closestFreq) / Math.Log(2.0));

		// Update display elements
		pitchDisplay.Text = $"{frequency:F1} Hz";
		noteDisplay.Text = GetNoteName(closestFreq);

		if (Math.Abs(cents) < 5.0f)
		{
			tuningIndicator.Text = "IN TUNE";
			tuningIndicator.Modulate = Colors.Green;
		}
		else
		{
			tuningIndicator.Text = $"{Math.Abs(cents):F0} cents {(cents > 0 ? "♯" : "♭")}";
			tuningIndicator.Modulate = Colors.Red;
		}
	}

	/// <summary>
	/// Converts a frequency to its corresponding note name.
	/// </summary>
	private string GetNoteName(float freq)
	{
		var notes = new Dictionary<float, string>
		{
			{ 82.41f, "E2" },  // Low E
            { 110.00f, "A2" }, // A
            { 146.83f, "D3" }, // D
            { 196.00f, "G3" }, // G
            { 246.94f, "B3" }, // B
            { 329.63f, "E4" }  // High E
        };

		return notes.TryGetValue(freq, out string note) ? note : "--";
	}

	/// <summary>
	/// Called every frame to update tuner position and process audio.
	/// </summary>
	public override void _Process(double delta)
	{
		// Update tuner position relative to XR camera
		if (xrCamera != null)
		{
			Vector3 forward = -xrCamera.GlobalTransform.Basis.Z;
			Vector3 targetPos = xrCamera.GlobalPosition + (forward * 1.5f);
			targetPos.Y += -0.2f;  // Position slightly below eye level

			GlobalPosition = targetPos;
			LookAt(xrCamera.GlobalPosition);
			RotateObjectLocal(Vector3.Up, Mathf.Pi);
		}

		// Process audio and update display
		float[] samples = GetAudioSamples();
		if (samples.Length > 0)
		{
			float frequency = pitchDetector.DetectPitch(samples);
			UpdateTunerDisplay(frequency);
		}

		// Clear buffer if too many samples accumulate
		if (audioEffect.GetFramesAvailable() > BUFFER_SIZE * 2)
		{
			audioEffect.ClearBuffer();
		}
	}
}