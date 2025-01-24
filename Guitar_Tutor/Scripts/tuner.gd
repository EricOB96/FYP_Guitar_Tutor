extends Node3D

<<<<<<< Updated upstream
# Constants for the YIN algorithm and audio processing
const SAMPLE_RATE: int = 44100     # Standard audio rate
const BUFFER_SIZE: int = 2048      # Buffer size for processing
const TAU_MAX: int = 1024          # Maximum period to detect (determines lowest frequency)
const YIN_THRESHOLD: float = 0.15   # Lower values = more sensitive detection
const MIN_FREQUENCY: float = 50.0   # Just below low E string (82.41 Hz)
const MAX_FREQUENCY: float = 450.0  # Above high E string (329.63 Hz)




# Class to store note information with proper frequency ranges
class NoteData:
	var freq: float
	var name: String
	var min_freq: float
	var max_freq: float
	
	func _init(f: float, n: String, tol: float = 0.03):
		freq = f
		name = n
		# Calculate frequency range with 3% tolerance
		min_freq = f * (1.0 - tol)
		max_freq = f * (1.0 + tol)

# Guitar strings dictionary with standard tuning frequencies
var GUITAR_NOTES: Dictionary = {}

# Audio processing buffers
var audio_buffer: PackedFloat32Array
var yin_buffer: PackedFloat32Array
var audio_effect: AudioEffectCapture
var last_frequency: float = 0.0
var is_active: bool = true

# Performance and quality settings
@export var update_rate: float = 0.05  # 20 updates per second
@export var smoothing: float = 0.7     # Smoothing factor for frequency display

# UI References
@onready var pitch_display: Label3D = $Display/PitchLabel
@onready var note_display: Label3D = $Display/NoteLabel
@onready var tuning_indicator: Label3D = $Display/TuningIndicator

@onready var xr_camera = get_node("/root/Main/Player/XRCamera3D")
@export var distance_from_camera: float = 1.5  # How far in front
@export var height_offset: float = -0.2 
=======

const SAMPLE_RATE: int = 44100     # Audio sampling rate
const BUFFER_SIZE: int = 2048      # Buffer size
const TAU_MAX: int = 1024          # Maximum period for pitch detection
const YIN_THRESHOLD: float = 0.15   # Threshold for YIN algorithm

# PitchDetector class implements the YIN pitch detection algorithm
class PitchDetector:
	var buffer_size: int
	var yin_buffer: PackedFloat32Array
	
	# Initialize buffer for YIN algorithm
	func _init(size: int) -> void:
		buffer_size = size
		yin_buffer = PackedFloat32Array()
		yin_buffer.resize(size / 2)
	
	# Main pitch detection function
	func detect_pitch(audio_buffer: PackedFloat32Array) -> float:
		var tau_estimate: int = difference_function(audio_buffer)
		if tau_estimate != -1:
			var better_tau: float = parabolic_interpolation(tau_estimate)
			return SAMPLE_RATE / better_tau
		return 0.0
	
	# Step 1 & 2 of YIN algorithm: Difference function and cumulative mean
	func difference_function(audio_buffer: PackedFloat32Array) -> int:
		# Clear buffer
		for tau in range(yin_buffer.size()):
			yin_buffer[tau] = 0.0
		
		# Calculate difference function
		for tau in range(1, yin_buffer.size()):
			for i in range(yin_buffer.size()):
				var delta: float = audio_buffer[i] - audio_buffer[i + tau]
				yin_buffer[tau] += delta * delta
		
		# Cumulative mean normalized difference
		var running_sum: float = 0.0
		yin_buffer[0] = 1.0
		
		for tau in range(1, yin_buffer.size()):
			running_sum += yin_buffer[tau]
			yin_buffer[tau] *= tau / running_sum
		
		# Find first dip below threshold
		for tau in range(2, yin_buffer.size()):
			if yin_buffer[tau] < YIN_THRESHOLD:
				while tau + 1 < yin_buffer.size() and yin_buffer[tau + 1] < yin_buffer[tau]:
					tau += 1
				return tau
		return -1
	
	# Step 3: Parabolic interpolation
	func parabolic_interpolation(tau_estimate: int) -> float:
		var x0: int = tau_estimate - 1 if tau_estimate > 0 else tau_estimate
		var x2: int = tau_estimate + 1 if tau_estimate + 1 < yin_buffer.size() else tau_estimate
		
		if x0 == tau_estimate:
			return float(tau_estimate if yin_buffer[tau_estimate] <= yin_buffer[x2] else x2)
		elif x2 == tau_estimate:
			return float(tau_estimate if yin_buffer[tau_estimate] <= yin_buffer[x0] else x0)
		
		# Parabolic interpolation formula
		var s0: float = yin_buffer[x0]
		var s1: float = yin_buffer[tau_estimate]
		var s2: float = yin_buffer[x2]
		return tau_estimate + (s2 - s0) / (2.0 * (2.0 * s1 - s2 - s0))

# Main class variables
var pitch_detector: PitchDetector
var audio_effect: AudioEffectCapture
var last_frequency: float = 0.0
var smoothing_factor: float = 0.7  # Smoothing (Higher for more)
var update_timer: float = 0.0
const UPDATE_INTERVAL: float = 0.05  # 20 updates per second

# UI elements
@onready var pitch_display: Label3D = $Display/PitchLabel
@onready var note_display: Label3D = $Display/NoteLabel
@onready var tuning_indicator: Label3D = $Display/TuningIndicator
@onready var xr_camera: Node3D = get_node("/root/Main/Player/XRCamera3D") # Camera for tuner following
>>>>>>> Stashed changes

# Initialize everything
func _ready() -> void:
	initialize_notes()
	initialize_buffers()
	setup_audio()
	configure_display()
	
	

func initialize_notes() -> void:
	# Standard guitar tuning frequencies
	var notes: Array[Dictionary] = [
		{"freq": 82.41, "name": "E2"},   # Low E
		{"freq": 110.00, "name": "A2"},  # A
		{"freq": 146.83, "name": "D3"},  # D
		{"freq": 196.00, "name": "G3"},  # G
		{"freq": 246.94, "name": "B3"},  # B
		{"freq": 329.63, "name": "E4"}   # High E
	]
	
	for note in notes:
		var note_data := NoteData.new(note["freq"], note["name"])
		GUITAR_NOTES[note["freq"]] = note_data

func initialize_buffers() -> void:
	# Initialize audio processing buffers
	audio_buffer = PackedFloat32Array()
	audio_buffer.resize(BUFFER_SIZE)
	
	yin_buffer = PackedFloat32Array()
	yin_buffer.resize(TAU_MAX)

# Set up audio capture system
func setup_audio() -> void:
	# Set up audio capture system
	var bus_idx := AudioServer.get_bus_index("Capture")
	if bus_idx == -1:
		bus_idx = AudioServer.bus_count
		AudioServer.add_bus()
		AudioServer.set_bus_name(bus_idx, "Capture")
	
	audio_effect = AudioEffectCapture.new()
	AudioServer.add_bus_effect(bus_idx, audio_effect)
	
<<<<<<< Updated upstream
	
=======
	# Request microphone permission on Android
>>>>>>> Stashed changes
	if OS.get_name() == "Android":
		OS.request_permission("RECORD_AUDIO")

# Initialize display elements
func configure_display() -> void:
	# Set up display elements
	pitch_display.text = "--"
	note_display.text = "--"
	tuning_indicator.text = "Waiting..."
	tuning_indicator.modulate = Color.GRAY
	
<<<<<<< Updated upstream
	# Configure text sizes for VR visibility
=======
	# Set font sizes for VR visibility
>>>>>>> Stashed changes
	pitch_display.font_size = 24
	note_display.font_size = 32
	tuning_indicator.font_size = 20

<<<<<<< Updated upstream
func _process(_delta: float) -> void:
	if not is_active or not audio_effect:
		return
		
	if xr_camera:
		# Get the camera's forward direction
		var forward = -xr_camera.global_transform.basis.z
		
		# Calculate position in front of camera
		var target_pos = xr_camera.global_position + (forward * distance_from_camera)
		target_pos.y += height_offset  # Adjust height
		
		# Update tuner position
		global_position = target_pos
		
		# Make tuner face the player
		look_at(xr_camera.global_position)
		# Rotate 180 degrees so text faces player
		rotate_object_local(Vector3.UP, PI)

	process_audio()

func process_audio() -> void:
	# Get new audio samples
	var samples := get_audio_samples()
	if samples.size() < BUFFER_SIZE:
		await get_tree().create_timer(0.01).timeout
		return
	
	# Apply YIN algorithm for pitch detection
	var frequency := detect_pitch_yin(samples)
	if frequency > 0:
		# Apply smoothing to reduce jitter
		frequency = lerp(last_frequency, frequency, 1.0 - smoothing)
		last_frequency = frequency
		update_tuner_display(frequency)

=======
# Get audio samples with error handling
>>>>>>> Stashed changes
func get_audio_samples() -> PackedFloat32Array:
	var raw_samples := audio_effect.get_buffer(BUFFER_SIZE)
	
<<<<<<< Updated upstream
	# Convert to mono and normalize
=======
	# Check if audio capture is working
	if not audio_effect or not audio_effect.is_active():
		setup_audio()
		return samples
		
	var raw_samples: Array = audio_effect.get_buffer(BUFFER_SIZE)
	if raw_samples.size() == 0:
		return samples
		
	# Convert stereo to mono ( No need for stereo 3)
>>>>>>> Stashed changes
	for i in range(min(BUFFER_SIZE, raw_samples.size())):
		audio_buffer[i] = raw_samples[i].x
	
	return audio_buffer

# Implementation of the YIN pitch detection algorithm
func detect_pitch_yin(samples: PackedFloat32Array) -> float:
	# Step 1: Calculate difference function
	for tau in range(TAU_MAX):
		var tmp: float = 0.0
		for i in range(TAU_MAX):
			var delta: float = samples[i] - samples[i + tau]
			tmp += delta * delta
		yin_buffer[tau] = tmp
	
	# Step 2: Cumulative mean normalized difference
	var running_sum: float = 0.0
	yin_buffer[0] = 1.0  # Avoid divide by zero later
	
	for tau in range(1, TAU_MAX):
		running_sum += yin_buffer[tau]
		yin_buffer[tau] = yin_buffer[tau] * tau / running_sum
	
	# Step 3: Absolute threshold method
	var tau_estimate: float = -1.0
	
	# Find first dip below threshold
	for tau in range(2, TAU_MAX):
		if yin_buffer[tau] < YIN_THRESHOLD:
			while tau + 1 < TAU_MAX and yin_buffer[tau + 1] < yin_buffer[tau]:
				tau += 1
			# Interpolate the minimum for better accuracy
			var alpha: float = yin_buffer[tau - 1]
			var beta: float = yin_buffer[tau]
			var gamma: float = yin_buffer[tau + 1]
			var parabolic_shift: float = 0.5 * (alpha - gamma) / (alpha - 2.0 * beta + gamma)
			tau_estimate = float(tau) + parabolic_shift
			break
	
	if tau_estimate != -1.0:
		var frequency: float = SAMPLE_RATE / tau_estimate
		if frequency >= MIN_FREQUENCY and frequency <= MAX_FREQUENCY:
			return frequency
	
	return 0.0

# Update display with detected pitch
func update_tuner_display(frequency: float) -> void:
<<<<<<< Updated upstream
	var closest_note: NoteData = null
	var min_distance: float = INF
	
	# Find the closest matching note
	for base_freq in GUITAR_NOTES:
		var base_freq_float: float = base_freq as float
		var current_note: NoteData = GUITAR_NOTES[base_freq_float]
		if frequency >= current_note.min_freq and frequency <= current_note.max_freq:
			var distance: float = abs(frequency - current_note.freq)
			if distance < min_distance:
				min_distance = distance
				closest_note = current_note
	
	if closest_note != null:
		# Calculate cents deviation
		var cents: float = 1200.0 * log(frequency / closest_note.freq) / log(2.0)
		display_tuning(frequency, closest_note, cents)
	else:
		display_no_note()

func display_tuning(freq: float, note: NoteData, cents: float) -> void:
	pitch_display.text = "%.1f Hz" % freq
	note_display.text = note.name
=======
	# Find closest matching note
	var min_distance: float = INF
	var closest_freq: float = 82.41  # Default to low E
	
	# Standard guitar tuning frequencies
	for note_freq in [82.41, 110.00, 146.83, 196.00, 246.94, 329.63]:
		var distance: float = abs(frequency - note_freq)
		if distance < min_distance:
			min_distance = distance
			closest_freq = note_freq
	
	# Calculate cents deviation from nearest note
	var cents: float = 1200.0 * log(frequency / closest_freq) / log(2.0)
	
	# Update display elements
	pitch_display.text = "%.1f Hz" % frequency
	note_display.text = get_note_name(closest_freq)
>>>>>>> Stashed changes
	
	if abs(cents) < 5.0:
		tuning_indicator.text = "IN TUNE"
		tuning_indicator.modulate = Color.GREEN
	else:
		tuning_indicator.text = ("%.0f cents %s" % [abs(cents), "♯" if cents > 0 else "♭"])
		tuning_indicator.modulate = Color.RED

<<<<<<< Updated upstream
func display_no_note() -> void:
	pitch_display.text = "--"
	note_display.text = "--"
	tuning_indicator.text = "No note detected"
	tuning_indicator.modulate = Color.GRAY
=======
# Convert frequency to note name
func get_note_name(freq: float) -> String:
	var notes: Dictionary = {
		82.41: "E2",  # Low E
		110.00: "A2", # A
		146.83: "D3", # D
		196.00: "G3", # G
		246.94: "B3", # B
		329.63: "E4"  # High E
	}
	return notes.get(freq, "--")

# Main process function
func _process(delta: float) -> void:
	# Update tuner position to follow camera
	if xr_camera:
		var forward: Vector3 = -xr_camera.global_transform.basis.z
		var target_pos: Vector3 = xr_camera.global_position + (forward * 1.5)
		target_pos.y += -0.2  # Height offset
		
		global_position = target_pos
		look_at(xr_camera.global_position)
		rotate_object_local(Vector3.UP, PI)  # Face the player
	
	# Control update frequency
	update_timer += delta
	if update_timer < UPDATE_INTERVAL:
		return
	update_timer = 0.0
	
	# Process audio and detect pitch
	var samples: PackedFloat32Array = get_audio_samples()
	if samples.size() < BUFFER_SIZE:
		return
		
	var detected_freq: float = pitch_detector.detect_pitch(samples)
	if detected_freq > 0:
		# Apply smoothing to reduce jitter
		detected_freq = lerp(last_frequency, detected_freq, 1.0 - smoothing_factor)
		last_frequency = detected_freq
		update_tuner_display(detected_freq)
>>>>>>> Stashed changes
