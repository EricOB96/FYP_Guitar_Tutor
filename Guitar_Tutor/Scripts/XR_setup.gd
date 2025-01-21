extends Node3D

var xr_interface: XRInterface
@onready var viewport : Viewport = get_viewport()
@onready var environment : Environment = $WorldEnvironment.environment

func _ready():
	xr_interface = XRServer.find_interface("OpenXR")
	if xr_interface and xr_interface.initialize():  # Changed from is_initialized()
		print("OpenXR initialized successfully")
		
		DisplayServer.window_set_vsync_mode(DisplayServer.VSYNC_DISABLED)
		get_viewport().use_xr = true
		
		# Enable both passthrough and AR settings
		enable_passthrough()
		#switch_to_ar()
	else:
		print("OpenXR not initialized, please check if your headset is connected")

func switch_to_ar():
	var xr_interface: XRInterface = XRServer.primary_interface
	if xr_interface:
		# Force alpha blend mode for Quest
		xr_interface.environment_blend_mode = XRInterface.XR_ENV_BLEND_MODE_ALPHA_BLEND
		viewport.transparent_bg = true
		
		environment.background_mode = Environment.BG_CLEAR_COLOR  # Changed from BG_COLOR
		environment.background_color = Color.TRANSPARENT
		environment.ambient_light_source = Environment.AMBIENT_SOURCE_DISABLED  # Changed from COLOR
		return true
	return false

func enable_passthrough():
	if xr_interface and xr_interface.is_passthrough_supported():
		xr_interface.start_passthrough()
		get_viewport().transparent_bg = true
		print("Passthrough enabled")
