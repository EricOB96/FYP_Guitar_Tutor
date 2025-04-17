using Godot;
using System.Linq;

namespace GuitarTutor.Audio
{
    /// <summary>
    /// Handles microphone input capture for audio analysis.
    /// Works as an autoload singleton to provide consistent audio capture across scenes.
    /// </summary>
    public partial class AudioCapture : AudioStreamPlayer
    {
        // Timer to ensure audio capture stays active
        private Timer _keepAliveTimer;
        // Flag to track if Android microphone permissions have been granted
        private bool _androidPermissionsChecked = false;

        public override void _Ready()
        {
            // Ensure there is 
            // a "Capture" audio bus with an AudioEffectCapture
            int busIndex = AudioServer.GetBusIndex("Capture");
            if (busIndex == -1)
            {
                // Create the bus if it doesn't exist
                GD.Print("Creating Capture bus...");
                busIndex = AudioServer.BusCount;
                AudioServer.AddBus();
                AudioServer.SetBusName(busIndex, "Capture");
                var captureEffect = new AudioEffectCapture();
                AudioServer.AddBusEffect(busIndex, captureEffect);
            }

            // Set AudioStreamPlayer to use the Capture bus
            Bus = "Capture"; 

            // Handling for Android permissions
            if (OS.HasFeature("android"))
            {
                var granted = OS.GetGrantedPermissions();
                if (!granted.Contains("android.permission.RECORD_AUDIO"))
                {
                    // Request microphone permission if not already granted
                    GD.Print("Requesting microphone permission...");
                    OS.RequestPermission("android.permission.RECORD_AUDIO");
                }
                else
                {
                    // Permission already granted, initialize the microphone
                    InitializeMicrophone();
                    _androidPermissionsChecked = true;
                }
            }
            else
            {
                // Non-Android platforms don't need permission checks
                InitializeMicrophone();
            }
        }

        public override void _Process(double delta)
        {
            // Check for Android permission results in each frame until granted
            if (OS.HasFeature("android") && !_androidPermissionsChecked)
            {
                var granted = OS.GetGrantedPermissions();
                if (granted.Contains("android.permission.RECORD_AUDIO"))
                {
                    GD.Print("Microphone permission granted");
                    InitializeMicrophone();
                    _androidPermissionsChecked = true;
                }
            }
        }

        /// <summary>
        /// Sets up the microphone input stream and creates a timer to keep it alive
        /// </summary>
        private void InitializeMicrophone()
        {
            // Set up the microphone input stream
            var micStream = new AudioStreamMicrophone();
            Stream = micStream;
            Autoplay = true; // Start automatically
            Play();

            // Create a timer to periodically check that audio capture is working
            _keepAliveTimer = new Timer();
            _keepAliveTimer.WaitTime = 1.0f; // Check every 1 second
            _keepAliveTimer.Timeout += OnKeepAliveTimeout;
            AddChild(_keepAliveTimer);
            _keepAliveTimer.Start();

            GD.Print("Microphone initialized");
        }

        /// <summary>
        /// Timer callback that ensures audio capture remains active
        /// </summary>
        private void OnKeepAliveTimeout()
        {
            // Restart audio playback if it has stopped
            if (!Playing)
            {
                GD.Print("Restarting audio playback");
                Play();
            }

            // Verify the capture bus is still properly set up
            int busIndex = AudioServer.GetBusIndex("Capture");
            if (busIndex != -1)
            {
                // Re-add the capture effect if it's missing
                if (AudioServer.GetBusEffectCount(busIndex) == 0)
                {
                    GD.Print("Adding missing AudioEffectCapture");
                    AudioServer.AddBusEffect(busIndex, new AudioEffectCapture());
                }

                // Get the capture effect to verify it exists
                var effect = AudioServer.GetBusEffect(busIndex, 0);
                if (effect is AudioEffectCapture capture)
                {
                    // Debug 
                    // GD.Print($"Frames available: {capture.GetFramesAvailable()}");
                }
            }
        }

        /// <summary>
        /// Checks if audio capture is properly initialized and working
        /// </summary>
        public bool IsCapturingAudio()
        {
            // Audio capture is not working if playback is stopped
            if (!Playing)
                return false;

            // On non-Android platforms, No need for permission checks
            if (!OS.HasFeature("android"))
                return true;

            // On Android, also verify permissions were granted
            return _androidPermissionsChecked;
        }

        /// <summary>
        /// Clean up resources when the node is removed from the scene
        /// </summary>
        public override void _ExitTree()
        {
            // Stop and remove the timer if it still exists
            if (GodotObject.IsInstanceValid(_keepAliveTimer))
            {
                _keepAliveTimer.Stop();
                _keepAliveTimer.QueueFree();
            }
        }
    }
}