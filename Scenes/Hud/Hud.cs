using Godot;
using System;

public class Hud : CanvasLayer
{
	// C# event (no args)
	public event Action SpawnTraveller;
	public event Action ResetSimTime;
	public event Action ToggleSimulation;
	public event Action MaxSpeed;
	public event Action ShowConfiguration;

	// C# event (with float arg)
	public event Action<float> TimeScaleChanged;

	public override void _Ready()
	{
		// Initialize slider to default speed (1x = slider value 0)
		var speedSlider = GetNode<HSlider>("ControlBar/SpeedSlider");
		speedSlider.Value = ExponentialSpeedToSlider(1.0f); // Set slider to position for 1x speed
		UpdateSpeedLabel(1.0f);
	}

	public void SetSliderFromSpeed(float speed)
	{
		var speedSlider = GetNode<HSlider>("ControlBar/SpeedSlider");
		speedSlider.Value = ExponentialSpeedToSlider(speed);
	}

	public void UpdateSimulationButton(bool isRunning)
	{
		var stopStartButton = GetNodeOrNull<Button>("ControlBar/StopStartButton");
		if (stopStartButton != null)
		{
			stopStartButton.Text = isRunning ? "Stop" : "Start";
		}
	}

	private void OnSpawnButtonPressed()
	{
		SpawnTraveller?.Invoke();
	}

	private void OnToggleSimulationPressed()
	{
		ToggleSimulation?.Invoke();
	}

	private void OnMaxSpeedPressed()
	{
		MaxSpeed?.Invoke();
		// Update speed label to show MAX (using 1000x speed)
		UpdateSpeedLabel(1000.0f);
	}

	private void OnConfigButtonPressed()
	{
		ShowConfiguration?.Invoke();
	}

	private void OnResetPressed()
	{
		ResetSimTime?.Invoke();

		// Reset speed slider and label to normal (1x speed)
		var speedSlider = GetNode<HSlider>("ControlBar/SpeedSlider");
		speedSlider.Value = ExponentialSpeedToSlider(1.0f);
		UpdateSpeedLabel(1.0f);

		GD.Print("HUD reset - speed controls reset");
	}

	private void OnSpeedSliderChanged(float value)
	{
		// Convert linear slider value (0-100) to exponential speed (1x-1000x)
		float exponentialSpeed = SliderToExponentialSpeed(value);
		TimeScaleChanged?.Invoke(exponentialSpeed);
		UpdateSpeedLabel(exponentialSpeed);
	}

	private float SliderToExponentialSpeed(float sliderValue)
	{
		// Map slider value (0-100) to exponential range (1x-1000x)
		// Using exponential formula: speed = 1 * (1000^(sliderValue/100))
		// This gives smooth exponential curve from 1x to 1000x
		float normalizedValue = sliderValue / 100.0f; // Convert to 0-1 range
		return Mathf.Pow(1000.0f, normalizedValue);
	}

	private float ExponentialSpeedToSlider(float speed)
	{
		// Convert exponential speed back to slider value
		// Inverse of exponential formula: sliderValue = 100 * log(speed) / log(1000)
		float normalizedValue = Mathf.Log(speed) / Mathf.Log(1000.0f);
		return normalizedValue * 100.0f;
	}

	public void UpdateSpeedLabel(float speed)
	{
		var speedLabel = GetNode<Label>("ControlBar/SpeedLabel");
		if (speed >= 1000.0f)
		{
			speedLabel.Text = "Speed: MAX";
			speedLabel.Modulate = Colors.Red; // Highlight max speed
		}
		else if (speed == 0.0f)
		{
			speedLabel.Text = "Speed: STOPPED";
			speedLabel.Modulate = Colors.Yellow; // Highlight stopped state
		}
		else
		{
			speedLabel.Text = $"Speed: {speed:F0}x"; // Remove decimal places for cleaner display
			speedLabel.Modulate = Colors.White; // Normal color
		}
	}

	public void UpdateSimTime(string text)
	{
		GetNode<Label>("ControlBar/TimeLabel").Text = $"Time: {text}";
	}
}