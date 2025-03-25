using Godot;
using System;

public partial class YinPitchDetector : Node
{
    // Configuration parameters
    [Export] private float _threshold = 0.1f; // Default threshold for the algorithm
    [Export] private int _sampleRate = 44100; // Audio sample rate
    [Export] private float _minFrequency = 75.0f; // Low E string is about 82.4 Hz
    [Export] private float _maxFrequency = 1000.0f; // Upper limit for detection
    [Export] private int _bufferSize = 2048; // Buffer size for analysis, larger for better low frequency detection
    [Export] private float _lowFrequencyEnhancementFactor = 1.5f; // Enhancement factor for low frequencies

    // Frequency thresholds for different low frequency ranges
    private const float VERY_LOW_FREQ_THRESHOLD = 100.0f; // Range for lowest strings (E2, A2)
    private const float LOW_FREQ_THRESHOLD = 200.0f;      // Range for middle-low strings (D3, G3)

    // Runtime variables
    private float[] _buffer;
    private float[] _yinBuffer;
    private float _confidence;
    private float _detectedPitch;
    private float _lastValidPitch = 0.0f;
    private int _maxPeriod; // Maximum period (for low frequency detection)
    private int _minPeriod; // Minimum period (for high frequency rejection)

    private AudioEffectCapture _captureEffect; // Capture effect fom AudioStreamPlayer

    public override void _Ready()
    {
        // Request recording permission from headset
        if (OS.GetName() == "Android")
        {
            OS.RequestPermission("RECORD_AUDIO");
        }

        // Get the capture effect from bus
        _captureEffect = AudioServer.GetBusEffect(AudioServer.GetBusIndex("Capture"), 0) as AudioEffectCapture;


        _buffer = new float[_bufferSize];

        // Calculate min and max periods based on frequency range
        _maxPeriod = (int)(_sampleRate / _minFrequency);
        _minPeriod = (int)(_sampleRate / _maxFrequency);

        // Ensure we don't exceed buffer size
        _maxPeriod = Math.Min(_maxPeriod, _bufferSize / 2);

        // Create Yin buffer for analysis
        _yinBuffer = new float[_bufferSize / 2];

        // Debug
        GD.Print($"YinPitchDetector initialized with buffer size: {_bufferSize}");
        GD.Print($"Frequency range: {_minFrequency} - {_maxFrequency} Hz");
        GD.Print($"Period range: {_minPeriod} - {_maxPeriod} samples");
    }

    /// <summary>
    /// Process audio buffer and detect pitch
    /// </summary>
    /// 
    /// <returns>Detected pitch in Hz, or -1 if no pitch detected</returns>
    public float DetectPitch(float[] audioBuffer)
    {
        if (audioBuffer == null || audioBuffer.Length < _bufferSize)
        {
            GD.PrintErr("Audio buffer is null or too small for analysis");
            return -1;
        }

        // Copy input to our buffer
        Array.Copy(audioBuffer, _buffer, _bufferSize);

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
            return _lastValidPitch; // Return last valid pitch instead of -1
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

        // Store last valid pitch
        _lastValidPitch = _detectedPitch;

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
            for (int i = 0; i < yinBufferLength; i++)
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
            // Apply enhancement for lower frequencies by adjusting the threshold
            float dynamicThreshold = _threshold;

            // More aggressive threshold for low frequencies
            float currentFreq = _sampleRate / tau;
            if (currentFreq < VERY_LOW_FREQ_THRESHOLD) // Very low frequencies (E2, A2 strings)
            {
                dynamicThreshold *= 0.7f * _lowFrequencyEnhancementFactor;
            }
            else if (currentFreq < LOW_FREQ_THRESHOLD) // Low frequencies (D3, G3 strings)
            {
                dynamicThreshold *= 0.85f * _lowFrequencyEnhancementFactor;
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
    /// Debug method to get the current Yin buffer values for visualization
    /// </summary>
    public float[] GetYinBuffer()
    {
        float[] bufferCopy = new float[_yinBuffer.Length];
        Array.Copy(_yinBuffer, bufferCopy, _yinBuffer.Length);
        return bufferCopy;
    }
}