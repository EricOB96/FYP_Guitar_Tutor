using Godot;
using System;

namespace GuitarTutor.Audio
{
    public partial class YinPitchDetector : Node
    {
        // Configuration parameters
        [Export] private float _threshold = 0.1f; // Lower threshold for better detection
        [Export] private int _sampleRate = 44100; // Audio sample rate
        [Export] private float _minFrequency = 60.0f; // Low E string is 82.4 Hz
        [Export] private float _maxFrequency = 1350.0f; // Upper limit for detection
        [Export] private int _bufferSize = 4096; // Buffer size for analysis
        [Export] private float _lowFrequencyEnhancementFactor = 1.2f; // Reduced enhancement factor
        [Export] private float _highFrequencyEnhancementFactor = 1.3f; // Enhancement factor for high frequencies
        [Export] private bool _debugMode = false; // Enable debug logging

        // Frequency thresholds for different frequency ranges
        private const float VERY_LOW_FREQ_THRESHOLD = 100.0f; // Range for lowest strings (E2, A2)
        private const float LOW_FREQ_THRESHOLD = 250.0f;      // Range for middle-low strings (D3, G3)
        private const float HIGH_FREQ_THRESHOLD = 300.0f;     // Range for high strings (B3, E4)

        // Audio capture effect
        private AudioEffectCapture _captureEffect;
        
        // Smoothing factor for pitch detection
        private float _smoothingFactor = 0.7f;
        
        // Guitar string frequencies
        private readonly float[] _guitarFrequencies = new float[]
        {
            82.41f,  // E2
            110.00f, // A2
            146.83f, // D3
            196.00f, // G3
            246.94f, // B3
            329.63f  // E4
        };

        // Runtime variables
        private float[] _buffer;
        private float[] _yinBuffer;
        private float _confidence;
        private float _detectedPitch;
        private float _lastValidPitch = 0.0f;
        private int _maxPeriod; // Maximum period (for low frequency detection)
        private int _minPeriod; // Minimum period (for high frequency rejection)
        private float[] _pitchHistory = new float[3]; // Small history for basic smoothing
        private int _pitchHistoryIndex = 0;

        private bool _captureEffectInitialized = false;

        public override void _Ready()
        {
            // Request recording permission from headset
            if (OS.HasFeature("android"))
            {
                OS.RequestPermission("RECORD_AUDIO");
                if (_debugMode) GD.Print("Requested RECORD_AUDIO permission for Android/Quest");
            }

            // Initialize buffers for analysis
            _buffer = new float[_bufferSize];

            // Calculate min and max periods based on frequency range
            _maxPeriod = (int)(_sampleRate / _minFrequency);
            _minPeriod = (int)(_sampleRate / _maxFrequency);

            // Ensure we don't exceed buffer size
            _maxPeriod = Math.Min(_maxPeriod, _bufferSize / 2);

            // Create Yin buffer for analysis
            _yinBuffer = new float[_bufferSize / 2];

            // Debug
            if (_debugMode)
            {
                GD.Print($"YinPitchDetector initialized with buffer size: {_bufferSize}");
                GD.Print($"Frequency range: {_minFrequency} - {_maxFrequency} Hz");
                GD.Print($"Period range: {_minPeriod} - {_maxPeriod} samples");
            }

            // Initialization of capture effect will be done in _Process
            GetTree().CreateTimer(1.0f).Timeout += InitializeCaptureEffect;
        }

        public override void _Process(double delta)
        {
            // Retry getting capture effect if not initialized
            if (!_captureEffectInitialized)
            {
                InitializeCaptureEffect();
            }
        }

        private void InitializeCaptureEffect()
        {
            // First find the autoloaded AudioCapture
            var audioCapture = GetNode<AudioStreamPlayer>("/root/AudioCapture");

            if (audioCapture == null)
            {
                if (_debugMode) GD.PrintErr("YinPitchDetector: Couldn't find autoloaded AudioCapture");
                return;
            }

            // Now get the bus from AudioCapture
            string busName = audioCapture.Bus;
            int busIndex = AudioServer.GetBusIndex(busName);

            if (busIndex == -1)
            {
                if (_debugMode) GD.PrintErr($"YinPitchDetector: Bus '{busName}' not found");
                return;
            }

            if (_debugMode)
            {
                GD.Print($"YinPitchDetector: Found '{busName}' bus at index {busIndex}");
                GD.Print($"Bus effect count: {AudioServer.GetBusEffectCount(busIndex)}");
            }

            // Check for the capture effect
            if (AudioServer.GetBusEffectCount(busIndex) > 0)
            {
                var effect = AudioServer.GetBusEffect(busIndex, 0);
                if (effect is AudioEffectCapture captureEffect)
                {
                    _captureEffect = captureEffect;
                    _captureEffectInitialized = true;
                    if (_debugMode) GD.Print("YinPitchDetector: Successfully got AudioEffectCapture from autoloaded AudioCapture");
                }
                else
                {
                    if (_debugMode) GD.PrintErr($"YinPitchDetector: First effect on '{busName}' bus is not an AudioEffectCapture");
                }
            }
            else
            {
                if (_debugMode) GD.PrintErr($"YinPitchDetector: No effects on '{busName}' bus");
            }
        }

        /// <summary>
        /// Process audio buffer and detect pitch
        /// </summary>
        /// 
        /// <returns>Detected pitch in Hz, or -1 if no pitch detected</returns>
        public float DetectPitch(float[] audioBuffer)
        {
            if (audioBuffer == null)
            {
                if (_debugMode) GD.PrintErr("Audio buffer is null");
                return -1;
            }

            // Check if the buffer is too small and handle it
            if (audioBuffer.Length < _bufferSize)
            {
                if (_debugMode) GD.Print($"Audio buffer size ({audioBuffer.Length}) is smaller than expected ({_bufferSize}). Resizing buffer.");
                
                // Create a new buffer of the correct size
                _buffer = new float[_bufferSize];
                
                // Copy the available data
                Array.Copy(audioBuffer, _buffer, Math.Min(audioBuffer.Length, _bufferSize));

                // Fill the rest with zeros if needed
                if (audioBuffer.Length < _bufferSize)
                {
                    for (int i = audioBuffer.Length; i < _bufferSize; i++)
                    {
                        _buffer[i] = 0.0f;
                    }
                }
            }
            else
            {
                // Copy input to our buffer
                Array.Copy(audioBuffer, _buffer, _bufferSize);
            }

            // Step 1: Calculate the squared difference function
            CalculateDifference();

            // Step 2: Cumulative mean normalized difference
            CumulativeMeanNormalizedDifference();

            // Step 3: Find the absolute threshold and apply low frequency enhancement
            int tauEstimate = FindTau();

            // No valid pitch found
            if (tauEstimate == -1)
            {
                _confidence = 0;
                return _lastValidPitch;
            }

            // Step 4: Parabolic interpolation for higher accuracy
            float betterTau = ParabolicInterpolation(tauEstimate);

            // Calculate the pitch
            _detectedPitch = _sampleRate / betterTau;

            // Apply frequency range filtering
            if (_detectedPitch < _minFrequency || _detectedPitch > _maxFrequency)
            {
                _confidence = 0;
                return _lastValidPitch;
            }

            // Apply basic smoothing
            _detectedPitch = SmoothPitch(_detectedPitch);

            // Store last valid pitch
            _lastValidPitch = _detectedPitch;

            if (_debugMode)
            {
                GD.Print($"Raw detected pitch: {_detectedPitch:F2} Hz, Confidence: {_confidence:F3}");
            }

            // Clear the audio buffer after processing to prevent it from getting full
            if (_captureEffectInitialized)
            {
                var effect = AudioServer.GetBusEffect(AudioServer.GetBusIndex("Capture"), 0);
                if (effect is AudioEffectCapture capture)
                {
                    capture.ClearBuffer();
                }
            }

            return _detectedPitch;
        }

        /// <summary>
        /// Step 1: Calculate squared difference function
        /// </summary>
        private void CalculateDifference()
        {
            int yinBufferLength = _yinBuffer.Length;

            for (int tau = 0; tau < yinBufferLength; tau++)
            {
                _yinBuffer[tau] = 0;
            }

            // Calculate squared difference for each tau
            for (int tau = 0; tau < yinBufferLength; tau++)
            {
                
                for (int i = 0; i < yinBufferLength - tau; i++)
                {
                    float delta = _buffer[i] - _buffer[i + tau];
                    _yinBuffer[tau] += delta * delta;
                }
            }
        }

        /// <summary>
        /// Step 2: Cumulative mean normalized difference function
        /// </summary>
        private void CumulativeMeanNormalizedDifference()
        {
            int yinBufferLength = _yinBuffer.Length;

            // First value is special case - ( avoid division by zero )
            _yinBuffer[0] = 1;

            // Calculate running cumulative mean
            float runningSum = 0;
            for (int tau = 1; tau < yinBufferLength; tau++)
            {
                runningSum += _yinBuffer[tau];

                if (runningSum > 0)
                {
                    // Normalized by cumulative mean
                    _yinBuffer[tau] *= tau / runningSum;
                }
                else
                {
                    _yinBuffer[tau] = 1;
                }
            }
        }

        /// <summary>
        /// Step 3: Find the first tau that is below the threshold and apply low frequency enhancement
        /// </summary>
        private int FindTau()
        {
            // Enhanced Yin algorithm for low frequencies
            int yinBufferLength = _yinBuffer.Length;

            // Find the first dip in the Yin function
            int minTau = _minPeriod;
            float minTauValue = float.MaxValue;

            for (int tau = _minPeriod; tau < _maxPeriod; tau++)
            {
                // Enhancement for different frequency ranges by adjusting the threshold
                float dynamicThreshold = _threshold;

                // Calculate current frequency
                float currentFreq = _sampleRate / tau;

                // Apply different thresholds based on frequency range
                if (currentFreq < VERY_LOW_FREQ_THRESHOLD) // Very low frequencies (E2, A2 strings)
                {
                    dynamicThreshold *= 0.75f * _lowFrequencyEnhancementFactor;
                }
                else if (currentFreq < LOW_FREQ_THRESHOLD) // Low frequencies (D3, G3 strings)
                {
                    dynamicThreshold *= 0.85f * _lowFrequencyEnhancementFactor;
                }
                else if (currentFreq > HIGH_FREQ_THRESHOLD) // High frequencies (B3, E4 strings)
                {
                    dynamicThreshold *= 0.9f * _highFrequencyEnhancementFactor;
                }

                // Check for dip below threshold
                if (_yinBuffer[tau] < dynamicThreshold)
                {
                    // Store confidence level (1.0 - _yinBuffer[tau])
                    _confidence = 1.0f - _yinBuffer[tau];
                    return tau;
                }
                else if (_yinBuffer[tau] < minTauValue)
                {
                    // Keep track of the minimum value for absolute minimum detection
                    minTau = tau;
                    minTauValue = _yinBuffer[tau];
                }
            }

            // If no value found below threshold, use the absolute minimum if it's reasonable
            if (minTauValue < 0.5f)
            {
                _confidence = 1.0f - minTauValue;
                return minTau;
            }

            // No reasonable pitch found
            return -1;
        }

        /// <summary>
        /// Step 4: Parabolic interpolation for better accuracy
        /// </summary>
        private float ParabolicInterpolation(int tauEstimate)
        {
            // Avoid boundaries
            if (tauEstimate < 1 || tauEstimate >= _yinBuffer.Length - 1)
            {
                return tauEstimate;
            }

            float s0 = _yinBuffer[tauEstimate - 1];
            float s1 = _yinBuffer[tauEstimate];
            float s2 = _yinBuffer[tauEstimate + 1];

            // Implements quadratic interpolation based on three points
            float adjustment = (s2 - s0) / (2 * (2 * s1 - s2 - s0));

            // Limit the adjustment to reasonable values
            adjustment = Math.Max(-1.0f, Math.Min(1.0f, adjustment));

            return tauEstimate + adjustment;
        }

        /// <summary>
        /// Corrects harmonics in the detected frequency
        /// </summary>
        /// <param name="frequency">The detected frequency</param>
        /// <returns>The corrected frequency</returns>
        private float CorrectHarmonics(float frequency)
        {
            // Check if the frequency is close to a harmonic of a guitar string
            foreach (var stringFreq in _guitarFrequencies)
            {
                // Check for harmonics up to the 4th harmonic
                for (int harmonic = 1; harmonic <= 4; harmonic++)
                {
                    float harmonicFreq = stringFreq * harmonic;
                    float tolerance = harmonicFreq * 0.05f; // 5% tolerance

                    if (Math.Abs(frequency - harmonicFreq) < tolerance)
                    {
                        // If it's a harmonic, return the fundamental frequency
                        return stringFreq;
                    }
                }
            }

            return frequency;
        }

        /// <summary>
        /// Applies smoothing to the detected frequency
        /// </summary>
        /// <param name="frequency">The detected frequency</param>
        /// <returns>The smoothed frequency</returns>
        private float ApplySmoothing(float frequency)
        {
            // Simple exponential smoothing
            if (_lastValidPitch > 0.0f)
            {
                return _smoothingFactor * frequency + (1.0f - _smoothingFactor) * _lastValidPitch;
            }

            return frequency;
        }

        /// <summary>
        /// Apply basic smoothing to reduce jitter
        /// </summary>
        private float SmoothPitch(float newPitch)
        {
            // Add the new pitch to the history
            _pitchHistory[_pitchHistoryIndex] = newPitch;
            _pitchHistoryIndex = (_pitchHistoryIndex + 1) % 3;

            // Simple moving average
            float sum = 0;
            for (int i = 0; i < 3; i++)
            {
                sum += _pitchHistory[i];
            }
            return sum / 3;
        }

        /// <summary>
        /// Get the last detected confidence level (0.0 to 1.0)
        /// </summary>
        public float GetConfidence()
        {
            return _confidence;
        }

        /// <summary>
        /// Get the last detected pitch
        /// </summary>
        public float GetLastPitch()
        {
            return _detectedPitch;
        }

        /// <summary>
        ///  The detection threshold (0.1 to 0.2 is typical range)
        /// </summary>
        public void SetThreshold(float threshold)
        {
            _threshold = Mathf.Clamp(threshold, 0.05f, 0.5f);
        }

        /// <summary>
        /// Low frequency enhancement factor
        /// </summary>
        public void SetLowFrequencyEnhancement(float factor)
        {
            _lowFrequencyEnhancementFactor = Mathf.Clamp(factor, 1.0f, 3.0f);
        }

        /// <summary>
        /// High frequency enhancement factor
        /// </summary>
        public void SetHighFrequencyEnhancement(float factor)
        {
            _highFrequencyEnhancementFactor = Mathf.Clamp(factor, 1.0f, 3.0f);
        }

        /// <summary>
        /// Update frequency range for detection
        /// </summary>
        public void SetFrequencyRange(float minFreq, float maxFreq)
        {
            _minFrequency = Mathf.Clamp(minFreq, 20.0f, 500.0f);
            _maxFrequency = Mathf.Clamp(maxFreq, _minFrequency + 100.0f, 2000.0f);

            // Recalculate periods
            _maxPeriod = (int)(_sampleRate / _minFrequency);
            _minPeriod = (int)(_sampleRate / _maxFrequency);

            // Ensure we don't exceed buffer size
            _maxPeriod = Math.Min(_maxPeriod, _bufferSize / 2);
        }

        /// <summary>
        /// Set debug mode
        /// </summary>
        public void SetDebugMode(bool enabled)
        {
            _debugMode = enabled;
        }

        /// <summary>
        /// Debug method to get the current Yin buffer values for visualization
        /// </summary>
        public float[] GetYinBuffer()
        {
            float[] bufferCopy = new float[_yinBuffer.Length];
            Array.Copy(_yinBuffer, bufferCopy, _yinBuffer.Length);
            return bufferCopy;
        }

        /// <summary>
        /// Check if capture effect is properly initialized
        /// </summary>
        public bool IsCaptureEffectInitialized()
        {
            return _captureEffectInitialized;
        }

        /// <summary>
        /// Force a retry of capture effect initialization
        /// </summary>
        public void RetryInitializeCaptureEffect()
        {
            _captureEffectInitialized = false;
            InitializeCaptureEffect();
        }
    }
}
