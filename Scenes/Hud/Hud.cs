using Godot;
using System;

public class Hud : CanvasLayer
{
	// C# event (no args)
	public event Action SpawnTraveller;
	public event Action ResetSimTime;

	// C# event (with float arg)
	public event Action<float> TimeScaleChanged;

	public override void _Ready()
	{
		// Initialize slider to default speed
		var speedSlider = GetNode<HSlider>("SpeedSlider");
		speedSlider.Value = 1.0;
		UpdateSpeedLabel(1.0f);
	}

	private void OnSpawnButtonPressed()
	{
		SpawnTraveller?.Invoke();
	}

	private void OnResetPressed()
	{
		ResetSimTime?.Invoke();
	}

	private void OnSpeedSliderChanged(float value)
	{
		TimeScaleChanged?.Invoke(value);
		UpdateSpeedLabel(value);
	}

	private void UpdateSpeedLabel(float speed)
	{
		var speedLabel = GetNode<Label>("SpeedLabel");
		speedLabel.Text = $"Speed: {speed:F1}x";
	}

	public void UpdateSimTime(string text)
	{
		GetNode<Label>("TimeLabel").Text = $"Time: {text}";
	}
}