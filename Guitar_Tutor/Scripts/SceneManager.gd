extends CanvasLayer


# Called when the node enters the scene tree for the first time.
func _ready() -> void:
	$Control/ColorRect/MarginContainer/VBoxContainer/Tuner_button.pressed.connect(_on_tuner_button_pressed)


# Called every frame. 'delta' is the elapsed time since the previous frame.
func _process(delta: float) -> void:
	pass


func _on_tuner_button_pressed() -> void:
	get_tree().change_scene_to_file("res://Guitar_Tutor/Scenes/Tuner_Main.tscn")


func _on_scale_button_pressed() -> void:
	pass # Replace with function body.


func _on_chord_button_pressed() -> void:
	pass # Replace with function body.
