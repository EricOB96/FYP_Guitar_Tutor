# NOT IN USE

extends CanvasLayer


# Called when the node enters the scene tree for the first time.
func _ready() -> void:
	mic_permission()
	$Control/ColorRect/MarginContainer/VBoxContainer/Tuner_button.pressed.connect(_on_tuner_button_pressed)
	$Control/ColorRect/MarginContainer/VBoxContainer/Scale_button.pressed.connect(_on_scale_button_pressed)

	

# Ask android for microphone persission	
func mic_permission():
	if OS.get_name() == "Android":
		OS.request_permission("RECORD_AUDIO")



func _on_tuner_button_pressed() -> void:
	get_tree().change_scene_to_file("res://Guitar_Tutor/Scenes/Tuner_Main.tscn")


func _on_scale_button_pressed() -> void:
	get_tree().change_scene_to_file("res://Guitar_Tutor/Scenes/scale_main.tscn")
