extends Node3D

var xr_interface: XRInterface

func _ready():
	if not XRServer:
		await get_tree().create_timer(0.5).timeout
	
	xr_interface = XRServer.find_interface("OpenXR")
	if xr_interface and xr_interface.is_initialized():
		print("OpenXR initialized successfully")
		DisplayServer.window_set_vsync_mode(DisplayServer.VSYNC_DISABLED)
		get_viewport().use_xr = true
		enable_passthrough()
	else:
		print("OpenXR not initialized, please check if headset is connected")

func enable_passthrough() -> bool:
	if not xr_interface:
		return false
		
	if xr_interface.is_passthrough_supported():
		return xr_interface.start_passthrough()
	
	var modes = xr_interface.get_supported_environment_blend_modes()
	if xr_interface.XR_ENV_BLEND_MODE_ALPHA_BLEND in modes:
		xr_interface.set_environment_blend_mode(xr_interface.XR_ENV_BLEND_MODE_ALPHA_BLEND)
		get_viewport().transparent_bg = true
		return true
	
	return false
