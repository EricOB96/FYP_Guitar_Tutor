extends Node3D

const SAMPLE_RATE: int = 44100
const BUFFER_SIZE: int = 2048
const TAU_MAX: int = 1024
const YIN_THRESHOLD: float = 0.15

class PitchDetector:
	var buffer_size: int
	var yin_buffer: PackedFloat32Array
	
	func _init(size: int) -> void:
		buffer_size = size
		yin_buffer = PackedFloat32Array()
		yin_buffer.resize(size / 2)
	
	func detect_pitch(audio_buffer: PackedFloat32Array) -> float:
		var tau_estimate: int = difference_function(audio_buffer)
		if tau_estimate != -1:
			var better_tau: float = parabolic_interpolation(tau_estimate)
			return SAMPLE_RATE / better_tau
		return 0.0
	
	func difference_function(audio_buffer: PackedFloat32Array) -> int:
		for tau in range(yin_buffer.size()):
			yin_buffer[tau] = 0.0
		
		for tau in range(1, yin_buffer.size()):
			for i in range(yin_buffer.size()):
				var delta: float = audio_buffer[i] - audio_buffer[i + tau]
				yin_buffer[tau] += delta * delta
		
		var running_sum: float = 0.0
		yin_buffer[0] = 1.0
		
		for tau in range(1, yin_buffer.size()):
			running_sum += yin_buffer[tau]
			yin_buffer[tau] *= tau / running_sum
		
		for tau in range(2, yin_buffer.size()):
			if yin_buffer[tau] < YIN_THRESHOLD:
				while tau + 1 < yin_buffer.size() and yin_buffer[tau + 1] < yin_buffer[tau]:
					tau += 1
				return tau
		return -1
	
	func parabolic_interpolation(tau_estimate: int) -> float:
		var x0: int = tau_estimate - 1 if tau_estimate > 0 else tau_estimate
		var x2: int = tau_estimate + 1 if tau_estimate + 1 < yin_buffer.size() else tau_estimate
		
		if x0 == tau_estimate:
			return float(tau_estimate if yin_buffer[tau_estimate] <= yin_buffer[x2] else x2)
		elif x2 == tau_estimate:
			return float(tau_estimate if yin_buffer[tau_estimate] <= yin_buffer[x0] else x0)
		
		var s0: float = yin_buffer[x0]
		var s1: float = yin_buffer[tau_estimate]
		var s2: float = yin_buffer[x2]
		return tau_estimate + (s2 - s0) / (2.0 * (2.0 * s1 - s2 - s0))

var pitch_detector: PitchDetector
var audio_effect: AudioEffectCapture
var last_frequency: float = 0.0
var smoothing_factor: float = 0.7  # Higher = more smoothing (0-1)
var update_timer: float = 0.0
const UPDATE_INTERVAL: float = 0.05  # 20 updates per second

@onready var pitch_display: Label3D = $Display/PitchLabel
@onready var note_display: Label3D = $Display/NoteLabel
@onready var tuning_indicator: Label3D = $Display/TuningIndicator
@onready var xr_camera: Node3D = get_node("/root/Main/Player/XRCamera3D")

func _ready() -> void:
	pitch_detector = PitchDetector.new(BUFFER_SIZE)
	setup_audio()
	configure_display()

func setup_audio() -> void:
	var bus_idx: int = AudioServer.get_bus_index("Capture")
	if bus_idx == -1:
		bus_idx = AudioServer.bus_count
		AudioServer.add_bus()
		AudioServer.set_bus_name(bus_idx, "Capture")
	
	audio_effect = AudioEffectCapture.new()
	AudioServer.add_bus_effect(bus_idx, audio_effect)
	
	if OS.get_name() == "Android":
		OS.request_permission("RECORD_AUDIO")

func configure_display() -> void:
	pitch_display.text = "--"
	note_display.text = "--"
	tuning_indicator.text = "Waiting..."
	tuning_indicator.modulate = Color.GRAY
	
	pitch_display.font_size = 24
	note_display.font_size = 32
	tuning_indicator.font_size = 20

func get_audio_samples() -> PackedFloat32Array:
	var samples: PackedFloat32Array = PackedFloat32Array()
	samples.resize(BUFFER_SIZE)
	
	var raw_samples: Array = audio_effect.get_buffer(BUFFER_SIZE)
	for i in range(min(BUFFER_SIZE, raw_samples.size())):
		samples[i] = raw_samples[i].x
	
	return samples

func update_tuner_display(frequency: float) -> void:
	var min_distance: float = INF
	var closest_freq: float = 82.41
	
	for note_freq in [82.41, 110.00, 146.83, 196.00, 246.94, 329.63]:
		var distance: float = abs(frequency - note_freq)
		if distance < min_distance:
			min_distance = distance
			closest_freq = note_freq
	
	var cents: float = 1200.0 * log(frequency / closest_freq) / log(2.0)
	
	pitch_display.text = "%.1f Hz" % frequency
	note_display.text = get_note_name(closest_freq)
	
	if abs(cents) < 5.0:
		tuning_indicator.text = "IN TUNE"
		tuning_indicator.modulate = Color.GREEN
	else:
		tuning_indicator.text = ("%.0f cents %s" % [abs(cents), "♯" if cents > 0 else "♭"])
		tuning_indicator.modulate = Color.RED

func get_note_name(freq: float) -> String:
	var notes: Dictionary = {
		82.41: "E2",
		110.00: "A2",
		146.83: "D3",
		196.00: "G3",
		246.94: "B3",
		329.63: "E4"
	}
	return notes.get(freq, "--")

func _process(_delta: float) -> void:
	if xr_camera:
		var forward: Vector3 = -xr_camera.global_transform.basis.z
		var target_pos: Vector3 = xr_camera.global_position + (forward * 1.5)
		target_pos.y += -0.2  # Height offset
		
		global_position = target_pos
		look_at(xr_camera.global_position)
		rotate_object_local(Vector3.UP, PI)
	
	var samples: PackedFloat32Array = get_audio_samples()
	if samples.size() < BUFFER_SIZE:
		return
		
	var detected_freq: float = pitch_detector.detect_pitch(samples)
	if detected_freq > 0:
		last_frequency = detected_freq
		update_tuner_display(detected_freq)
