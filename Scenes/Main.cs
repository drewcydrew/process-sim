using Godot;
using System;
using System.Collections.Generic;

public class Main : Node
{
	[Export]
	public PackedScene TravellerScene;
	[Export] public PackedScene BoxScene;
	[Export] public PackedScene SimulationTravellerScene; // New scene for discrete event travellers

	// Discrete event simulation
	private DiscreteEventSimulation _simulation;

	// Configuration
	private Configuration _configuration;
	private ConfigurationData _currentConfig;

	// Simulation time and time scale - now managed by discrete event simulation
	public float simTime => _simulation?.CurrentTime ?? 0f;
	private float timeScale = 1.0f;
	private float previousTimeScale = 1.0f;
	private bool simulationRunning = true;
	private bool hasEverStarted = false;
	private int totalBoxes = 10; // Will be set by configuration

	// queue of all unclaimed boxes
	public Queue<Node2D> _availableBoxes = new Queue<Node2D>();

	// List of active travellers for Gantt chart tracking
	public List<Traveller> _activeTravellers = new List<Traveller>();
	public List<SimulationTraveller> _activeSimulationTravellers = new List<SimulationTraveller>();

	// Grid configuration for box positioning
	private const int BOXES_PER_ROW = 5;
	private const float BOX_SPACING = 25.0f;

	public override void _Ready()
	{
		// Initialize discrete event simulation
		_simulation = new DiscreteEventSimulation();
		AddChild(_simulation);
		_simulation.Name = "DiscreteEventSimulation";

		// Initialize configuration
		_configuration = GetNode<Configuration>("UI/TabContainer/Configuration");
		_currentConfig = _configuration.GetConfiguration();
		totalBoxes = _currentConfig.StartingBoxes;

		// Connect configuration events
		_configuration.ConfigurationChanged += OnConfigurationChanged;

		_InitializeBoxes();

		// Spawn one traveller at the start of the simulation (like before)
		if (!hasEverStarted)
		{
			_SpawnTraveller();
			hasEverStarted = true;
		}

		// Initialize UI elements
		_InitializeUI();
	}

	private void _InitializeUI()
	{
		// Get UI elements
		var timeLabel = GetNode<Label>("UI/ControlBar/TimeLabel");
		var speedLabel = GetNode<Label>("UI/ControlBar/SpeedLabel");
		var speedSlider = GetNode<HSlider>("UI/ControlBar/SpeedSlider");
		var stopStartButton = GetNode<Button>("UI/ControlBar/StopStartButton");

		// Initialize speed slider
		speedSlider.MinValue = 0;
		speedSlider.MaxValue = 100;
		speedSlider.Value = _SpeedToSliderValue(timeScale);

		// Update initial labels
		UpdateTimeLabel(0.0f);
		UpdateSpeedLabel(timeScale);
		UpdateSimulationButton(simulationRunning);
	}

	private void _InitializeBoxes()
	{
		// grab environment nodes
		var env = GetNode<Node>("UI/TabContainer/Environment");
		var mid = env.GetNode<ColorRect>("MiddlePosition");
		var boxContainer = env.GetNode<Node2D>("Boxes");

		// clear existing boxes
		foreach (Node child in boxContainer.GetChildren())
		{
			child.QueueFree();
		}
		_availableBoxes.Clear();

		// create DeliveredBoxes container if it doesn't exist
		var deliveredBoxContainer = env.GetNodeOrNull<Node2D>("DeliveredBoxes");
		if (deliveredBoxContainer == null)
		{
			deliveredBoxContainer = new Node2D();
			deliveredBoxContainer.Name = "DeliveredBoxes";
			env.AddChild(deliveredBoxContainer);
		}
		else
		{
			// clear delivered boxes
			foreach (Node child in deliveredBoxContainer.GetChildren())
			{
				child.QueueFree();
			}
		}

		// create boxes based on configuration
		for (int i = 0; i < totalBoxes; i++)
		{
			var box = BoxScene.Instance() as Node2D;

			// Calculate grid position
			int row = i / BOXES_PER_ROW;
			int col = i % BOXES_PER_ROW;

			// Position boxes in a grid relative to middle position
			Vector2 gridOffset = new Vector2(
				(col - (BOXES_PER_ROW - 1) / 2.0f) * BOX_SPACING,
				row * BOX_SPACING
			);

			box.Position = mid.RectPosition + gridOffset;
			boxContainer.AddChild(box);
			_availableBoxes.Enqueue(box);
		}
	}

	private void OnResetSimulation()
	{
		// Reset discrete event simulation
		_simulation.Reset();

		timeScale = 1.0f; // Reset to normal speed
		previousTimeScale = 1.0f; // Reset previous speed
		simulationRunning = true; // Restart simulation
		hasEverStarted = false; // Reset first start tracking

		// Ensure discrete event simulation is running
		_simulation.TimeScale = timeScale;
		_simulation.IsRunning = true;

		// Reset traveller ID counters for consistent Gantt chart display
		Traveller.ResetIdCounter();

		// Clear all travellers from the Environment tab
		var env = GetNode<Node>("UI/TabContainer/Environment");
		var travellers = new List<Node>();
		foreach (Node child in env.GetChildren())
		{
			if (child is Traveller)
			{
				travellers.Add(child);
			}
		}

		foreach (Node traveller in travellers)
		{
			traveller.QueueFree();
		}

		// Clear active travellers lists
		_activeTravellers.Clear();

		// Clear persistent Gantt chart data
		_persistentTimelines.Clear();

		// Reset boxes based on current configuration
		totalBoxes = _currentConfig.StartingBoxes;
		_InitializeBoxes();

		// Use CallDeferred to ensure everything is cleaned up before spawning new travellers
		CallDeferred(nameof(_SpawnInitialTravellers));
		hasEverStarted = true;

		// Update UI button state
		UpdateSimulationButton(simulationRunning);

		GD.Print($"Simulation reset! Discrete event sim running: {_simulation.IsRunning}, time scale: {_simulation.TimeScale}");
	}
	private void _SpawnTravellerDeferred()
	{
		_SpawnTraveller();
	}

	private void _SpawnInitialTravellers()
	{
		// Schedule travellers to spawn 1 second apart in simulation time
		for (int i = 0; i < _currentConfig.StartingTravellers; i++)
		{
			float spawnTime = simTime + (i * 1.0f); // 1 second apart
			_simulation.ScheduleEvent(spawnTime, EventType.TravellerSpawn, null, (data) => _SpawnTraveller());
			GD.Print($"Scheduled traveller {i + 1} to spawn at simulation time {spawnTime:F1}s");
		}
	}
	private void OnToggleSimulation()
	{
		simulationRunning = !simulationRunning;

		if (simulationRunning)
		{
			// Starting - restore previous time scale
			timeScale = previousTimeScale;
			_simulation.TimeScale = timeScale;
			_simulation.IsRunning = true;

			// Update speed slider and labels
			var speedSlider = GetNodeOrNull<HSlider>("UI/ControlBar/SpeedSlider");
			if (speedSlider != null)
			{
				speedSlider.Value = _SpeedToSliderValue(timeScale);
			}
			UpdateSpeedLabel(timeScale);
		}
		else
		{
			// Stopping - save current time scale and set to 0
			previousTimeScale = timeScale;
			timeScale = 0.0f;
			_simulation.TimeScale = 0.0f;
			_simulation.IsRunning = false;

			// Update speed label to show stopped
			UpdateSpeedLabel(0.0f);
		}

		// Update UI button state
		UpdateSimulationButton(simulationRunning);

		GD.Print($"Simulation {(simulationRunning ? "started" : "stopped")}");
	}

	public void OnTimeScaleChanged(float newScale)
	{
		// Only update if simulation is running (not stopped)
		if (simulationRunning)
		{
			timeScale = newScale;
			previousTimeScale = newScale; // Store as previous for when we stop/start
			_simulation.TimeScale = newScale; // Update discrete event simulation
		}
		else
		{
			// If stopped, just update the previous scale for when we resume
			previousTimeScale = newScale;
		}
	}

	private void OnMaxSpeed()
	{
		// Set time scale to high value to complete simulation quickly
		// This preserves data fidelity while running much faster than normal
		timeScale = 1000.0f; // High speed for fast completion
		previousTimeScale = 1000.0f; // Store as previous
		_simulation.TimeScale = timeScale;

		// Ensure simulation is running
		if (!simulationRunning)
		{
			simulationRunning = true;
			_simulation.IsRunning = true;
			UpdateSimulationButton(simulationRunning);
		}

		// Update the speed slider to show max value using exponential mapping
		var speedSlider = GetNodeOrNull<HSlider>("UI/ControlBar/SpeedSlider");
		if (speedSlider != null)
		{
			speedSlider.Value = _SpeedToSliderValue(1000.0f); // This will set slider to its maximum position (100)
		}

		GD.Print("Max speed activated - simulation running at maximum speed");
	}

	private void OnConfigurationChanged(int startingBoxes, int startingTravellers, float travellerSpeed, float delayBetweenActivities)
	{
		// Update current configuration
		_currentConfig.StartingBoxes = startingBoxes;
		_currentConfig.StartingTravellers = startingTravellers;
		_currentConfig.TravellerSpeed = travellerSpeed;
		_currentConfig.DelayBetweenActivities = delayBetweenActivities;

		totalBoxes = startingBoxes;

		GD.Print($"Configuration changed: {startingBoxes} boxes, {startingTravellers} travellers, {travellerSpeed:F1}s speed, {delayBetweenActivities:F1}s delay");
		GD.Print("Reset the simulation to apply the new configuration.");
	}

	private void _SpawnTraveller()
	{
		// Don't spawn if simulation is complete
		if (!simulationRunning)
		{
			GD.Print("Simulation is complete. Reset to spawn more travellers.");
			return;
		}

		// only spawn if there's still a box to carry
		if (_availableBoxes.Count == 0)
		{
			GD.Print("No more boxes to deliver!");
			return;
		}

		// dequeue one box but DON'T remove it from the scene yet
		var boxToCarry = _availableBoxes.Dequeue();

		// Use regular traveller (revert to original logic)
		var tNode = TravellerScene.Instance() as Node2D;

		// Add traveller to the Environment tab instead of Main
		var env = GetNode<Node>("UI/TabContainer/Environment");
		env.AddChild(tNode);
		var traveller = tNode as Traveller;

		// Add to active travellers list
		_activeTravellers.Add(traveller);

		// get the waypoints
		var startC = env.GetNode<ColorRect>("StartPosition");
		var midC = env.GetNode<ColorRect>("MiddlePosition");
		var endC = env.GetNode<ColorRect>("EndPosition");

		// Start traveller at start position
		traveller.Position = startC.RectPosition;

		// Journey goes from start → middle → end
		var points = new Godot.Collections.Array<Vector2> {
			midC.RectPosition,
			endC.RectPosition
		};

		// start the journey, with callback to clean up traveller only when no more boxes
		traveller.StartJourney(
			points,
			_currentConfig.TravellerSpeed,      // duration from config
			_currentConfig.DelayBetweenActivities,      // delay from config
			() => timeScale,
			() => simTime,
			boxToCarry, // ← box to carry
			() =>
			{
				// callback when journey is complete (no more boxes)
				_activeTravellers.Remove(traveller);
				traveller.QueueFree();
				GD.Print("All boxes delivered!");
			}
		);
	}

	public void _ReorganizeBoxes()
	{
		var env = GetNode<Node>("UI/TabContainer/Environment");
		var mid = env.GetNode<ColorRect>("MiddlePosition");
		var boxContainer = env.GetNode<Node2D>("Boxes");

		// Get all remaining boxes
		var remainingBoxes = new List<Node2D>();
		foreach (Node child in boxContainer.GetChildren())
		{
			if (child is Node2D box)
				remainingBoxes.Add(box);
		}

		// Reposition them in grid formation
		for (int i = 0; i < remainingBoxes.Count; i++)
		{
			int row = i / BOXES_PER_ROW;
			int col = i % BOXES_PER_ROW;

			Vector2 gridOffset = new Vector2(
				(col - (BOXES_PER_ROW - 1) / 2.0f) * BOX_SPACING,
				row * BOX_SPACING
			);

			remainingBoxes[i].Position = mid.RectPosition + gridOffset;
		}
	}

	// Method for HUD to get all traveller timeline data
	public List<Traveller.TravellerInfo> GetAllTravellerTimelines()
	{
		var timelines = new List<Traveller.TravellerInfo>();

		// Get data from regular travellers
		foreach (var traveller in _activeTravellers)
		{
			if (traveller != null && IsInstanceValid(traveller))
			{
				timelines.Add(traveller.GetTravellerInfo());
			}
		}

		return timelines;
	}

	// Method to check if simulation is running
	public bool IsSimulationRunning()
	{
		return simulationRunning;
	}

	// Optional: you can update simTime in _Process
	public override void _Process(float delta)
	{
		// The discrete event simulation handles time advancement
		// We just need to update the UI and check for completion

		// Check if all boxes have been delivered
		CheckSimulationComplete();

		// Update time display
		var dateTime = DateTimeOffset.FromUnixTimeSeconds((long)simTime).ToLocalTime();
		string timeString = dateTime.ToString("HH:mm:ss");
		string statusText = simulationRunning ? timeString : $"{timeString} [COMPLETE]";
		UpdateTimeLabel(simTime);

		// Update Gantt chart
		UpdateGanttChart();
	}

	private void CheckSimulationComplete()
	{
		// Get delivered boxes count
		var env = GetNode<Node>("UI/TabContainer/Environment");
		var deliveredBoxContainer = env.GetNodeOrNull<Node2D>("DeliveredBoxes");

		if (deliveredBoxContainer != null)
		{
			int deliveredCount = deliveredBoxContainer.GetChildCount();

			// Stop simulation when all boxes are delivered and no active travellers
			if (deliveredCount >= totalBoxes && _activeTravellers.Count == 0)
			{
				if (simulationRunning)
				{
					simulationRunning = false;
					_simulation.IsRunning = false;
					GD.Print($"Simulation Complete! All {totalBoxes} boxes delivered in {simTime:F1} seconds.");
				}
			}
		}
	}

	// Event handlers for the new control bar
	private void OnSpawnButtonPressed()
	{
		_SpawnTraveller();
	}

	private void OnSpeedSliderChanged(float value)
	{
		// Convert slider value (0-100) to speed using exponential mapping
		float newScale = _SliderValueToSpeed(value);
		OnTimeScaleChanged(newScale);
		UpdateSpeedLabel(newScale);
	}

	private void OnResetPressed()
	{
		OnResetSimulation();
	}

	private void OnToggleSimulationPressed()
	{
		OnToggleSimulation();
	}

	private void OnMaxSpeedPressed()
	{
		OnMaxSpeed();
	}

	private void OnDownloadCsvPressed()
	{
		// Get gantt data and save to CSV
		var ganttData = GetAllTravellerTimelines();
		SaveGanttDataToCsv(ganttData);
	}

	// UI helper methods
	private void UpdateTimeLabel(float time)
	{
		var timeLabel = GetNodeOrNull<Label>("UI/ControlBar/TimeLabel");
		if (timeLabel != null)
		{
			timeLabel.Text = $"Time: {time:F1}s";
		}
	}

	private void UpdateSpeedLabel(float speed)
	{
		var speedLabel = GetNodeOrNull<Label>("UI/ControlBar/SpeedLabel");
		if (speedLabel != null)
		{
			speedLabel.Text = $"Speed: {speed:F1}x";
		}
	}

	private void UpdateSimulationButton(bool isRunning)
	{
		var button = GetNodeOrNull<Button>("UI/ControlBar/StopStartButton");
		if (button != null)
		{
			button.Text = isRunning ? "Stop" : "Start";
		}
	}

	private float _SpeedToSliderValue(float speed)
	{
		// Exponential mapping: slider 0-100 maps to speed 0.1-10
		if (speed <= 0.1f) return 0f;
		if (speed >= 10f) return 100f;

		// Use logarithmic scale: speed = 0.1 * 10^(slider/50)
		return 50f * (float)Math.Log10(speed / 0.1f);
	}

	private float _SliderValueToSpeed(float sliderValue)
	{
		// Convert slider value (0-100) to speed (0.1-10) using exponential mapping
		if (sliderValue <= 0f) return 0.1f;
		if (sliderValue >= 100f) return 10f;

		// Exponential scale: speed = 0.1 * 10^(slider/50)
		return 0.1f * Mathf.Pow(10f, sliderValue / 50f);
	}

	private void SaveGanttDataToCsv(List<Traveller.TravellerInfo> ganttData)
	{
		// Create CSV content
		var csvContent = "Traveller,Segment,Start Time,End Time,Duration,From,To\n";

		foreach (var traveller in ganttData)
		{
			foreach (var segment in traveller.Timeline)
			{
				float duration = segment.EndTime - segment.StartTime;
				csvContent += $"{traveller.Name},Segment,{segment.StartTime:F2},{segment.EndTime:F2},{duration:F2},{segment.From},{segment.To}\n";
			}
		}

		// Save to file
		var file = new File();
		var filePath = "user://gantt_data.csv";

		if (file.Open(filePath, File.ModeFlags.Write) == Error.Ok)
		{
			file.StoreString(csvContent);
			file.Close();
			GD.Print($"Gantt data saved to: {OS.GetUserDataDir()}/gantt_data.csv");
		}
		else
		{
			GD.PrintErr("Failed to save CSV file");
		}
	}

	// Gantt chart properties and methods
	private Control _ganttContent;
	private Panel _ganttChart;
	private const float GANTT_ROW_HEIGHT = 25.0f;
	private const float GANTT_TIME_SCALE = 60.0f;
	private const float GANTT_LEFT_MARGIN = 100.0f;

	// Persistent timeline storage (survives traveller deletion)
	private Dictionary<int, Traveller.TravellerInfo> _persistentTimelines = new Dictionary<int, Traveller.TravellerInfo>();

	// Colors for different activities
	private readonly Dictionary<Traveller.TravellerActivity, Color> _activityColors = new Dictionary<Traveller.TravellerActivity, Color>
	{
		{ Traveller.TravellerActivity.Starting, new Color(0.5f, 0.5f, 0.5f) },
		{ Traveller.TravellerActivity.MovingToPickup, new Color(0.3f, 0.7f, 1.0f) },
		{ Traveller.TravellerActivity.PickingUp, new Color(1.0f, 0.8f, 0.2f) },
		{ Traveller.TravellerActivity.MovingToDelivery, new Color(0.2f, 0.8f, 0.2f) },
		{ Traveller.TravellerActivity.Delivering, new Color(1.0f, 0.6f, 0.2f) },
		{ Traveller.TravellerActivity.Returning, new Color(0.8f, 0.4f, 0.8f) },
		{ Traveller.TravellerActivity.Waiting, new Color(0.6f, 0.6f, 0.6f) },
		{ Traveller.TravellerActivity.Finished, new Color(0.2f, 0.5f, 0.2f) }
	};

	private void UpdateGanttChart()
	{
		// Initialize Gantt chart if not done yet
		if (_ganttChart == null)
		{
			_ganttChart = GetNodeOrNull<Panel>("UI/TabContainer/Data/GanttChart");
			if (_ganttChart == null) return;

			_ganttContent = _ganttChart.GetNodeOrNull<Control>("ScrollContainer/GanttContent");
			if (_ganttContent == null) return;
		}

		// Get all traveller timeline data
		var currentTravellerData = GetAllTravellerTimelines();
		float currentTime = simTime;

		// Debug: Log traveller data
		if (currentTravellerData.Count == 0 && _persistentTimelines.Count == 0)
		{
			// No data yet - this is normal immediately after reset
			return;
		}

		// Update persistent timelines with current data
		foreach (var traveller in currentTravellerData)
		{
			if (_persistentTimelines.ContainsKey(traveller.Id))
			{
				// Update existing traveller data
				_persistentTimelines[traveller.Id] = traveller;
			}
			else
			{
				// Add new traveller
				_persistentTimelines[traveller.Id] = traveller;
				GD.Print($"Gantt: Added new traveller {traveller.Id} ({traveller.Name}) to timeline");
			}
		}

		// Clear existing gantt content
		foreach (Node child in _ganttContent.GetChildren())
		{
			child.QueueFree();
		}

		// Draw gantt chart with persistent data
		DrawGanttChart(new List<Traveller.TravellerInfo>(_persistentTimelines.Values), currentTime);
	}

	private void DrawGanttChart(List<Traveller.TravellerInfo> travellerData, float currentTime)
	{
		if (_ganttContent == null) return;

		int rowIndex = 0;
		float maxTime = 0f;

		// Calculate the maximum time across all travellers
		foreach (var traveller in travellerData)
		{
			foreach (var segment in traveller.Timeline)
			{
				float endTime = segment.EndTime > 0 ? segment.EndTime : currentTime;
				maxTime = Mathf.Max(maxTime, endTime);
			}
		}

		// Ensure minimum width for the timeline
		maxTime = Mathf.Max(maxTime, 30.0f);

		foreach (var traveller in travellerData)
		{
			// Create traveller label
			var nameLabel = new Label();
			nameLabel.Text = traveller.Name;
			nameLabel.RectPosition = new Vector2(5, rowIndex * GANTT_ROW_HEIGHT + 5);
			nameLabel.RectSize = new Vector2(GANTT_LEFT_MARGIN - 10, GANTT_ROW_HEIGHT - 5);
			_ganttContent.AddChild(nameLabel);

			// Draw timeline segments
			foreach (var segment in traveller.Timeline)
			{
				float startX = GANTT_LEFT_MARGIN + segment.StartTime * GANTT_TIME_SCALE;
				float endTime = segment.EndTime > 0 ? segment.EndTime : currentTime;
				float width = (endTime - segment.StartTime) * GANTT_TIME_SCALE;

				if (width > 0)
				{
					var segmentRect = new ColorRect();
					Color activityColor = _activityColors.ContainsKey(segment.ActivityType) ? _activityColors[segment.ActivityType] : Colors.Gray;
					segmentRect.Color = activityColor;
					segmentRect.RectPosition = new Vector2(startX, rowIndex * GANTT_ROW_HEIGHT + 2);
					segmentRect.RectSize = new Vector2(width, GANTT_ROW_HEIGHT - 4);
					_ganttContent.AddChild(segmentRect);

					// Add activity label on the segment if it's wide enough
					if (width > 50)
					{
						var activityLabel = new Label();
						activityLabel.Text = segment.ActivityType.ToString();
						activityLabel.RectPosition = segmentRect.RectPosition + new Vector2(5, 2);
						activityLabel.RectSize = new Vector2(width - 10, GANTT_ROW_HEIGHT - 8);
						activityLabel.AddColorOverride("font_color", Colors.White);
						_ganttContent.AddChild(activityLabel);
					}
				}
			}

			rowIndex++;
		}

		// Update content size
		float contentWidth = GANTT_LEFT_MARGIN + maxTime * GANTT_TIME_SCALE + 50;
		float contentHeight = rowIndex * GANTT_ROW_HEIGHT + 20;
		_ganttContent.RectMinSize = new Vector2(contentWidth, contentHeight);
	}
}