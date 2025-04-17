// NOT IN USE

using Godot;
using System;
using System.Linq;

namespace GuitarTutor.Audio
{
    public partial class AudioManager : Node
    {
        // Singleton pattern
        private static AudioManager _instance;
        public static AudioManager Instance => _instance;

        // Components
        private AudioStreamPlayer _audioPlayer;
        private AudioEffectCapture _captureEffect;
        private YinPitchDetector _pitchDetector;

        // Audio data buffer
        private float[] _audioBuffer;
        private int _bufferSize = 2048;

        // Analysis timing
        [Export] private float _analysisInterval = 0.1f;
        private float _lastAnalysisTime;
        private bool _androidPermissionsChecked;

        // Results
        private float _currentPitch;
        private float _currentVolume;
        private float _confidence;

        [Export] private float _volumeThreshold = 0.0f;
        [Export] public bool DebugMode = false;

        public override void _EnterTree()
        {
            // Singleton enforcement
            if (_instance != null && _instance != this)
            {
                QueueFree();
                GD.PrintErr("Duplicate AudioManager detected and removed");
                return;
            }
            _instance = this;
        }

        public override void _Ready()
        {
            InitializeAudioSystem();
            InitializePitchDetector();
            InitializeAndroidPermissions();
        }

        private void InitializeAudioSystem()
        {
            _audioBuffer = new float[_bufferSize];

            // Get reference to AudioCapture autoload
            _audioPlayer = GetNode<AudioStreamPlayer>("/root/AudioCapture");

            if (_audioPlayer != null)
            {
                SetupCaptureBus();
                InitializeCaptureEffect();
            }
            else if (DebugMode)
            {
                GD.PrintErr("AudioCapture autoload not found");
            }
        }

        private void SetupCaptureBus()
        {
            var busIndex = AudioServer.GetBusIndex("Capture");
            if (busIndex == -1)
            {
                busIndex = AudioServer.BusCount;
                AudioServer.AddBus(busIndex);
                AudioServer.SetBusName(busIndex, "Capture");
                AudioServer.AddBusEffect(busIndex, new AudioEffectCapture());
            }
            _audioPlayer.Bus = "Capture";
        }

        private void InitializeCaptureEffect()
        {
            var busIndex = AudioServer.GetBusIndex("Capture");
            if (busIndex != -1 && AudioServer.GetBusEffectCount(busIndex) > 0)
            {
                _captureEffect = AudioServer.GetBusEffect(busIndex, 0) as AudioEffectCapture;
                if (DebugMode) GD.Print(_captureEffect != null ?
                    "Capture effect initialized" : "Invalid capture effect");
            }
        }

        private void InitializePitchDetector()
        {
            // Get the YinPitchDetector
            _pitchDetector = GetNode<YinPitchDetector>("/root/YinPitchDetector");

            // Debug
            if (_pitchDetector != null)
            {
                GD.Print("AudioManager: Using autoloaded YinPitchDetector");
            }
            else
            {
                GD.PrintErr("AudioManager: Could not find autoloaded YinPitchDetector");
            }
        }

        private void InitializeAndroidPermissions()
        {
            if (!OS.HasFeature("android")) return;

            var granted = OS.GetGrantedPermissions();
            if (granted.Contains("android.permission.RECORD_AUDIO"))
            {
                StartCapturing();
                _androidPermissionsChecked = true;
            }
            else
            {
                OS.RequestPermission("android.permission.RECORD_AUDIO");
            }
        }

        public override void _Process(double delta)
        {
            HandleAndroidPermissions();
            HandleAudioAnalysis();
        }

        private void HandleAndroidPermissions()
        {
            if (!OS.HasFeature("android") || _androidPermissionsChecked) return;

            var granted = OS.GetGrantedPermissions();
            if (granted.Contains("android.permission.RECORD_AUDIO"))
            {
                StartCapturing();
                _androidPermissionsChecked = true;
            }
        }

        private void HandleAudioAnalysis()
        {
            if (_captureEffect == null) return;

            var currentTime = (float)Time.GetTicksMsec() / 1000f;
            if (currentTime - _lastAnalysisTime >= _analysisInterval)
            {
                AnalyzeAudio();
                _lastAnalysisTime = currentTime;
            }
        }

        private void AnalyzeAudio()
        {
            try
            {
                var framesAvailable = _captureEffect.GetFramesAvailable();
                if (framesAvailable < _bufferSize) return;

                var buffer = _captureEffect.GetBuffer(framesAvailable);
                ProcessAudioBuffer(buffer);
                CalculateVolume();

                if (_currentVolume > _volumeThreshold)
                {
                    DetectPitch();
                    LogDebugInfo();
                }
            }
            catch (Exception e)
            {
                if (DebugMode) GD.PrintErr($"Analysis error: {e.Message}");
            }
        }

        private void ProcessAudioBuffer(Vector2[] buffer)
        {
            for (var i = 0; i < Math.Min(_bufferSize, buffer.Length); i++)
            {
                _audioBuffer[i] = (buffer[i].X + buffer[i].Y) * 0.5f;
            }
        }

        private void CalculateVolume()
        {
            var sum = 0f;
            foreach (var sample in _audioBuffer)
            {
                sum += sample * sample;
            }
            _currentVolume = Mathf.Sqrt(sum / _bufferSize);
        }

        private void DetectPitch()
        {
            _currentPitch = _pitchDetector.DetectPitch(_audioBuffer);
            _confidence = _pitchDetector.GetConfidence();
        }

        private void LogDebugInfo()
        {
            if (DebugMode)
            {
                GD.Print($"Pitch: {_currentPitch:F1}Hz | " +
                        $"Volume: {_currentVolume:F4} | " +
                        $"Confidence: {_confidence:P0}");
            }
        }

        public void StartCapturing()
        {
            if (_captureEffect == null) return;

            _captureEffect.ClearBuffer();
            if (!_audioPlayer.Playing) _audioPlayer.Play();
            if (DebugMode) GD.Print("Capture started");
        }

        public void StopCapturing()
        {
            if (_captureEffect != null) _captureEffect.ClearBuffer();
            if (DebugMode) GD.Print("Capture stopped");
        }

        // Public accessors
        public float GetCurrentPitch() => _currentPitch;
        public float GetCurrentVolume() => _currentVolume;
        public float GetConfidence() => _confidence;

        public override void _ExitTree()
        {
            StopCapturing();
            _instance = null;
        }
    }
}