extends Node3D

const SAMPLE_RATE: int = 44100
const BUFFER_SIZE: int = 2048
const DEFAULT_THRESHOLD: float = 0.15

class PitchDetector:
	var buffer_size: int
	var yin_buffer: PackedFloat32Array
	
	func _init(size: int) -> void:
		buffer_size = size
		yin_buffer = PackedFloat32Array()
		yin_buffer.resize(size / 2)
	
	func detect_pitch(audio_buffer: PackedFloat32Array) -> float:
		var tau_estimate = -1
		var pitch_in_hz = 0.0
		
		difference_function(audio_buffer)
		cumulative_mean_normalized_difference()
		tau_estimate = absolute_threshold()
		
		if tau_estimate != -1:
			var better_tau = parabolic_interpolation(tau_estimate)
			pitch_in_hz = SAMPLE_RATE / better_tau
			
		return pitch_in_hz
	
	func difference_function(audio_buffer: PackedFloat32Array) -> void:
		for tau in range(yin_buffer.size()):
			yin_buffer[tau] = 0.0
			
		for tau in range(1, yin_buffer.size()):
			for i in range(yin_buffer.size()):
				var delta = audio_buffer[i] - audio_buffer[i + tau]
				yin_buffer[tau] += delta * delta
	
	func cumulative_mean_normalized_difference() -> void:
		var running_sum = 0.0
		yin_buffer[0] = 1.0
		
		for tau in range(1, yin_buffer.size()):
			running_sum += yin_buffer[tau]
			yin_buffer[tau] *= tau / running_sum
	
	func absolute_threshold() -> int:
		for tau in range(2, yin_buffer.size()):
			if yin_buffer[tau] < DEFAULT_THRESHOLD:
				while tau + 1 < yin_buffer.size() and yin_buffer[tau + 1] < yin_buffer[tau]:
					tau += 1
				return tau
		return -1
	
	func parabolic_interpolation(tau_estimate: int) -> float:
		var x0 = tau_estimate - 1 if tau_estimate > 0 else tau_estimate
		var x2 = tau_estimate + 1 if tau_estimate + 1 < yin_buffer.size() else tau_estimate
		
		if x0 == tau_estimate:
			return float(tau_estimate if yin_buffer[tau_estimate] <= yin_buffer[x2] else x2)
		elif x2 == tau_estimate:
			return float(tau_estimate if yin_buffer[tau_estimate] <= yin_buffer[x0] else x0)
		
		var s0 = yin_buffer[x0]
		var s1 = yin_buffer[tau_estimate]
		var s2 = yin_buffer[x2]
		return tau_estimate + (s2 - s0) / (2.0 * (2.0 * s1 - s2 - s0))

var pitch_detector: PitchDetector
var audio_effect: AudioEffectCapture
var audio_buffer: PackedFloat32Array
var audio_check_timer: Timer

@onready var pitch_display: Label3D = $Display/PitchLabel
@onready var note_display: Label3D = $Display/NoteLabel
@onready var tuning_indicator: Label3D = $Display/TuningIndicator
@onready var xr_camera: Node3D = get_node("/root/Tuner_main/Player/Player/XRCamera3D")

func _ready() -> void:
	if not XRServer:
		await get_tree().create_timer(0.5).timeout
	pitch_detector = PitchDetector.new(BUFFER_SIZE)
	audio_buffer.resize(BUFFER_SIZE)
	setup_audio()
	configure_display()

func setup_audio() -> void:
	var bus_idx = AudioServer.get_bus_index("Capture")
	if bus_idx == -1:
		bus_idx = AudioServer.bus_count
		AudioServer.add_bus()
		AudioServer.set_bus_name(bus_idx, "Capture")
	
	audio_effect = AudioEffectCapture.new()
	AudioServer.add_bus_effect(bus_idx, audio_effect)
	
	if OS.get_name() == "Android":
		OS.request_permission("RECORD_AUDIO")
	
	# Setup audio check timer
	if not audio_check_timer:
		audio_check_timer = Timer.new()
		add_child(audio_check_timer)
	audio_check_timer.wait_time = 5.0
	audio_check_timer.timeout.connect(_check_audio)
	audio_check_timer.start()

func _check_audio() -> void:
	if not audio_effect or audio_effect.get_frames_available() == 0:
		print("Audio stopped - reinitializing...")
		var bus_idx = AudioServer.get_bus_index("Capture")
		if bus_idx != -1:
			AudioServer.remove_bus(bus_idx)
		setup_audio()

func configure_display() -> void:
	pitch_display.text = "--"
	note_display.text = "--"
	tuning_indicator.text = "Waiting..."
	tuning_indicator.modulate = Color.GRAY
	
	pitch_display.font_size = 24
	note_display.font_size = 32
	tuning_indicator.font_size = 20

func get_audio_samples() -> PackedFloat32Array:
	var available = audio_effect.get_frames_available()
	if available >= BUFFER_SIZE:
		var samples = audio_effect.get_buffer(BUFFER_SIZE)
		for i in range(BUFFER_SIZE):
			audio_buffer[i] = samples[i].x
		return audio_buffer
	return PackedFloat32Array()

func update_tuner_display(frequency: float) -> void:
	if frequency == 0:
		return
		
	var min_distance = INF
	var closest_freq = 82.41
	
	for note_freq in [82.41, 110.00, 146.83, 196.00, 246.94, 329.63]:
		var distance = abs(frequency - note_freq)
		if distance < min_distance:
			min_distance = distance
			closest_freq = note_freq
	
	var cents = 1200.0 * log(frequency / closest_freq) / log(2.0)
	
	pitch_display.text = "%.1f Hz" % frequency
	note_display.text = get_note_name(closest_freq)
	
	if abs(cents) < 5.0:
		tuning_indicator.text = "IN TUNE"
		tuning_indicator.modulate = Color.GREEN
	else:
		tuning_indicator.text = ("%.0f cents %s" % [abs(cents), "♯" if cents > 0 else "♭"])
		tuning_indicator.modulate = Color.RED

func get_note_name(freq: float) -> String:
	var notes = {
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
		var forward = -xr_camera.global_transform.basis.z
		var target_pos = xr_camera.global_position + (forward * 1.5)
		target_pos.y += -0.2
		
		global_position = target_pos
		look_at(xr_camera.global_position)
		rotate_object_local(Vector3.UP, PI)
	
	var samples = get_audio_samples()
	if samples.size() > 0:
		var frequency = pitch_detector.detect_pitch(samples)
		update_tuner_display(frequency)
	
	if audio_effect.get_frames_available() > BUFFER_SIZE * 2:
		audio_effect.clear_buffer()
