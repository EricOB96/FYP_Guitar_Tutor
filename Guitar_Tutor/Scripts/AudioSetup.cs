using Godot;
using System;

public partial class AudioSetup : Node
{
    [Export] private NodePath _yinDetectorPath;
    [Export] private bool _debugMode = true;

    // Configuration
    [Export] private string _busMixerName = "Capture";
    [Export] private int _sampleRate = 44100;
    [Export] private int _bufferSize = 2048;

    // Audio processing
    private AudioEffectCapture _captureEffect;
    private Timer _audioCheckTimer;
    private YinPitchDetector _yinDetector;

    private float[] _tempBuffer;
    private int _framesWithoutAudio = 0;

    public override void _Ready()
    {
        // Get reference to YinPitchDetector
        _yinDetector = GetNode<YinPitchDetector>(_yinDetectorPath);
        if (_yinDetector == null)
        {
            GD.PushError("YinPitchDetector not found! Please check the NodePath.");
            return;
        }

        _tempBuffer = new float[_bufferSize];

        // Set up audio capture
        SetupAudioCapture();

        // Set up monitoring timer
        _audioCheckTimer = new Timer();
        _audioCheckTimer.WaitTime = 5.0;
        _audioCheckTimer.Timeout += CheckAudioStatus;
        AddChild(_audioCheckTimer);
        _audioCheckTimer.Start();

        GD.Print("AudioSetup initialized successfully!");
    }

    private void SetupAudioCapture()
    {
        // First, check if the bus already exists
        int busIdx = AudioServer.GetBusIndex(_busMixerName);
        if (busIdx == -1)
        {
            // Create the bus if it doesn't exist
            busIdx = AudioServer.BusCount;
            AudioServer.AddBus();
            AudioServer.SetBusName(busIdx, _busMixerName);
            GD.Print($"Created new audio bus: {_busMixerName}");
        }

        // Make sure the bus is not muted
        AudioServer.SetBusMute(busIdx, false);

        // Clear existing effects and add a new capture effect
        for (int i = AudioServer.GetBusEffectCount(busIdx) - 1; i >= 0; i--)
        {
            AudioServer.RemoveBusEffect(busIdx, i);
        }

        _captureEffect = new AudioEffectCapture();
        AudioServer.AddBusEffect(busIdx, _captureEffect, 0);

        // Request permissions on Android/Quest devices
        if (OS.GetName() == "Android")
        {
            GD.Print("Requesting RECORD_AUDIO permission on Android device...");
            OS.RequestPermission("RECORD_AUDIO");
        }


        GD.Print($"Audio capture setup complete. Bus: {_busMixerName}, Index: {busIdx}");
        GD.Print($"Sample rate: {_sampleRate}, Buffer size: {_bufferSize}");
    }

    private void CheckAudioStatus()
    {
        if (_captureEffect == null)
        {
            GD.PrintErr("AudioEffectCapture is null! Recreating audio setup...");
            SetupAudioCapture();
            return;
        }

        int framesAvailable = _captureEffect.GetFramesAvailable();
        GD.Print($"Audio frames available: {framesAvailable}");

        // Check if we have any audio input
        if (framesAvailable == 0)
        {
            _framesWithoutAudio++;

            if (_framesWithoutAudio >= 3) // 3 checks without audio
            {
                GD.Print("No audio detected for multiple checks. Reinitializing audio...");
                _framesWithoutAudio = 0;

                // Try to restart the audio capture
                int busIdx = AudioServer.GetBusIndex(_busMixerName);
                if (busIdx != -1)
                {
                    AudioServer.RemoveBus(busIdx);
                }

                SetupAudioCapture();
            }
        }
        else
        {
            _framesWithoutAudio = 0;

            // Print audio level for debugging
            if (_debugMode)
            {
                var samples = _captureEffect.GetBuffer(Math.Min(framesAvailable, 128));
                float level = 0;
                foreach (var sample in samples)
                {
                    level = Math.Max(level, Math.Abs(sample.X));
                }
                GD.Print($"Audio level: {level:F3}");
            }

            // Clear buffer if too many samples accumulate
            if (framesAvailable > _bufferSize * 4)
            {
                _captureEffect.ClearBuffer();
            }
        }
    }

    public override void _Process(double delta)
    {
        // Process audio and feed it to the YinPitchDetector
        if (_captureEffect != null && _yinDetector != null)
        {
            int framesAvailable = _captureEffect.GetFramesAvailable();

            if (framesAvailable >= _bufferSize)
            {
                // Get audio data from the capture effect
                var samples = _captureEffect.GetBuffer(_bufferSize);

                // Convert to float array (only use left channel)
                for (int i = 0; i < _bufferSize; i++)
                {
                    _tempBuffer[i] = samples[i].X;
                }

                // Process the audio with YinPitchDetector
                float pitch = _yinDetector.DetectPitch(_tempBuffer);
                float confidence = _yinDetector.GetConfidence();

                if (_debugMode && confidence > 0.5f)
                {
                    GD.Print($"Detected pitch: {pitch:F1} Hz (Confidence: {confidence:F2})");
                }
            }
        }
    }
}