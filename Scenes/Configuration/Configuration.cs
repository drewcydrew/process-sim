using Godot;
using System;

public class Configuration : Control
{
    // Configuration properties
    public int StartingBoxes { get; private set; } = 10;
    public int StartingTravellers { get; private set; } = 1;
    public float TravellerSpeed { get; private set; } = 2.0f;
    public float DelayBetweenActivities { get; private set; } = 0.5f;

    // UI Elements
    private SpinBox _boxesSpinBox;
    private SpinBox _travellersSpinBox;
    private HSlider _speedSlider;
    private Label _speedLabel;
    private HSlider _delaySlider;
    private Label _delayLabel;
    private Button _applyButton;
    private Button _resetButton;
    private Button _closeButton;

    // Events to notify Main when settings change
    public event Action<int, int, float, float> ConfigurationChanged;
    public event Action ConfigurationClosed;

    // Default values
    private const int DEFAULT_BOXES = 10;
    private const int DEFAULT_TRAVELLERS = 1;
    private const float DEFAULT_SPEED = 2.0f;
    private const float DEFAULT_DELAY = 0.5f;

    public override void _Ready()
    {
        // Get UI elements
        _boxesSpinBox = GetNode<SpinBox>("VBoxContainer/GridContainer/BoxesSpinBox");
        _travellersSpinBox = GetNode<SpinBox>("VBoxContainer/GridContainer/TravellersSpinBox");
        _speedSlider = GetNode<HSlider>("VBoxContainer/GridContainer/SpeedSlider");
        _speedLabel = GetNode<Label>("VBoxContainer/GridContainer/SpeedLabel");
        _delaySlider = GetNode<HSlider>("VBoxContainer/GridContainer/DelaySlider");
        _delayLabel = GetNode<Label>("VBoxContainer/GridContainer/DelayLabel");
        _applyButton = GetNode<Button>("VBoxContainer/HBoxContainer/ApplyButton");
        _resetButton = GetNode<Button>("VBoxContainer/HBoxContainer/ResetButton");
        _closeButton = GetNode<Button>("VBoxContainer/HBoxContainer/CloseButton");

        // Set initial values
        _boxesSpinBox.Value = StartingBoxes;
        _travellersSpinBox.Value = StartingTravellers;
        _speedSlider.Value = TravellerSpeed;
        _delaySlider.Value = DelayBetweenActivities;

        // Connect signals
        _boxesSpinBox.Connect("value_changed", this, nameof(OnBoxesChanged));
        _travellersSpinBox.Connect("value_changed", this, nameof(OnTravellersChanged));
        _speedSlider.Connect("value_changed", this, nameof(OnSpeedChanged));
        _delaySlider.Connect("value_changed", this, nameof(OnDelayChanged));
        _applyButton.Connect("pressed", this, nameof(OnApplyPressed));
        _resetButton.Connect("pressed", this, nameof(OnResetPressed));
        _closeButton.Connect("pressed", this, nameof(OnClosePressed));

        // Update labels
        UpdateSpeedLabel();
        UpdateDelayLabel();

        // Always visible since it's now in a tab
        Visible = true;
    }

    private void OnBoxesChanged(float value)
    {
        StartingBoxes = (int)value;
    }

    private void OnTravellersChanged(float value)
    {
        StartingTravellers = (int)value;
    }

    private void OnSpeedChanged(float value)
    {
        TravellerSpeed = value;
        UpdateSpeedLabel();
    }

    private void OnDelayChanged(float value)
    {
        DelayBetweenActivities = value;
        UpdateDelayLabel();
    }

    private void UpdateSpeedLabel()
    {
        _speedLabel.Text = $"Speed: {TravellerSpeed:F1}s per segment";
    }

    private void UpdateDelayLabel()
    {
        _delayLabel.Text = $"Delay: {DelayBetweenActivities:F1}s between activities";
    }

    private void OnApplyPressed()
    {
        // Notify Main of configuration changes
        ConfigurationChanged?.Invoke(StartingBoxes, StartingTravellers, TravellerSpeed, DelayBetweenActivities);

        GD.Print($"Configuration applied: {StartingBoxes} boxes, {StartingTravellers} travellers, {TravellerSpeed:F1}s speed, {DelayBetweenActivities:F1}s delay");
    }

    private void OnResetPressed()
    {
        // Reset to default values
        StartingBoxes = DEFAULT_BOXES;
        StartingTravellers = DEFAULT_TRAVELLERS;
        TravellerSpeed = DEFAULT_SPEED;
        DelayBetweenActivities = DEFAULT_DELAY;

        // Update UI
        _boxesSpinBox.Value = StartingBoxes;
        _travellersSpinBox.Value = StartingTravellers;
        _speedSlider.Value = TravellerSpeed;
        _delaySlider.Value = DelayBetweenActivities;

        UpdateSpeedLabel();
        UpdateDelayLabel();

        GD.Print("Configuration reset to defaults");
    }

    private void OnClosePressed()
    {
        // Close button is no longer needed in tab view, but keep for compatibility
        ConfigurationClosed?.Invoke();
    }

    // Public method to show the configuration panel
    public void ShowConfiguration()
    {
        Visible = true;

        // Bring to front
        GetParent().MoveChild(this, GetParent().GetChildCount() - 1);
    }

    // Public method to get current configuration
    public ConfigurationData GetConfiguration()
    {
        return new ConfigurationData
        {
            StartingBoxes = StartingBoxes,
            StartingTravellers = StartingTravellers,
            TravellerSpeed = TravellerSpeed,
            DelayBetweenActivities = DelayBetweenActivities
        };
    }

    // Public method to set configuration
    public void SetConfiguration(ConfigurationData config)
    {
        StartingBoxes = config.StartingBoxes;
        StartingTravellers = config.StartingTravellers;
        TravellerSpeed = config.TravellerSpeed;
        DelayBetweenActivities = config.DelayBetweenActivities;

        // Update UI if it exists
        if (_boxesSpinBox != null)
        {
            _boxesSpinBox.Value = StartingBoxes;
            _travellersSpinBox.Value = StartingTravellers;
            _speedSlider.Value = TravellerSpeed;
            _delaySlider.Value = DelayBetweenActivities;

            UpdateSpeedLabel();
            UpdateDelayLabel();
        }
    }
}

// Data class for configuration
public class ConfigurationData
{
    public int StartingBoxes { get; set; } = 10;
    public int StartingTravellers { get; set; } = 1;
    public float TravellerSpeed { get; set; } = 2.0f;
    public float DelayBetweenActivities { get; set; } = 0.5f;
}
