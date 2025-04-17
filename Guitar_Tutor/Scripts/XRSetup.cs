using Godot;
using System;

namespace GuitarTutor.XR
{
    public partial class XRSetup : Node
    {
        // Reference to the XRServer
        private XRInterface xrInterface;

        public override void _Ready()
        {
            // Initialize XR
            InitializeXR();
        }

        private void InitializeXR()
        {
            // Get the XRServer singleton
            xrInterface = XRServer.FindInterface("OpenXR");

            if (xrInterface != null && xrInterface.IsInitialized())
            {
                GD.Print("OpenXR interface found and initialized.");

                // Enable AR passthrough
                EnableARPassthrough();

                // Set the primary interface
                XRServer.PrimaryInterface = xrInterface;

                // Enable XR mode
                GetViewport().UseXR = true;

                GD.Print("XR mode enabled successfully.");
            }
            else
            {
                GD.PrintErr("Failed to initialize OpenXR interface.");
            }
        }

        private void EnableARPassthrough()
        {
            // Check if AR passthrough is supported
            if (xrInterface.IsPassthroughSupported())
            {
                // Enable AR passthrough
                xrInterface.StartPassthrough();

                GD.Print("AR passthrough enabled.");
            }
            else
            {
                GD.PrintErr("AR passthrough is not supported on this device.");
            }
        }
    }
}