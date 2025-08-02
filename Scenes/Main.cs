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

	// Simulation time and time scale - now managed by discrete event simulation
	public float simTime => _simulation?.CurrentTime ?? 0f;
	private float timeScale = 1.0f;
	private float previousTimeScale = 1.0f;
	private bool simulationRunning = true;
	private bool hasEverStarted = false;
	private const int TOTAL_BOXES = 10;

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

		var hud = GetNode<Hud>("Hud");
		hud.SpawnTraveller += () =>
		{
			_SpawnTraveller();
		};

		hud.TimeScaleChanged += OnTimeScaleChanged;
		hud.ResetSimTime += OnResetSimulation;
		hud.ToggleSimulation += OnToggleSimulation;
		hud.MaxSpeed += OnMaxSpeed;

		_InitializeBoxes();

		// Spawn one traveller at the start of the simulation
		if (!hasEverStarted)
		{
			_SpawnTraveller();
			hasEverStarted = true;
		}

		// Update HUD button state
		hud.UpdateSimulationButton(simulationRunning);
	}

	private void _InitializeBoxes()
	{
		// grab environment nodes
		var env = GetNode<Node>("Environment");
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

		// create 10 boxes at the middle location with grid positioning
		for (int i = 0; i < 10; i++)
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
		SimulationTraveller.ResetIdCounter();

		// Clear all travellers
		var travellers = new List<Node>();
		foreach (Node child in GetChildren())
		{
			if (child is Traveller || child is SimulationTraveller)
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
		_activeSimulationTravellers.Clear();

		// Reset boxes
		_InitializeBoxes();

		// Use CallDeferred to ensure everything is cleaned up before spawning new traveller
		CallDeferred(nameof(_SpawnTravellerDeferred));
		hasEverStarted = true;

		// Update HUD button state
		var hud = GetNode<Hud>("Hud");
		hud.UpdateSimulationButton(simulationRunning);

		GD.Print($"Simulation reset! Discrete event sim running: {_simulation.IsRunning}, time scale: {_simulation.TimeScale}");
	}
	private void _SpawnTravellerDeferred()
	{
		_SpawnTraveller();
	}
	private void OnToggleSimulation()
	{
		simulationRunning = !simulationRunning;

		var hud = GetNode<Hud>("Hud");

		if (simulationRunning)
		{
			// Starting - restore previous time scale
			timeScale = previousTimeScale;
			_simulation.TimeScale = timeScale;
			_simulation.IsRunning = true;

			// Update speed slider to show current speed using exponential mapping
			hud.SetSliderFromSpeed(timeScale);
			hud.UpdateSpeedLabel(timeScale);
		}
		else
		{
			// Stopping - save current time scale and set to 0
			previousTimeScale = timeScale;
			timeScale = 0.0f;
			_simulation.TimeScale = 0.0f;
			_simulation.IsRunning = false;

			// Update speed label to show stopped
			hud.UpdateSpeedLabel(0.0f);
		}

		// Update HUD button state
		hud.UpdateSimulationButton(simulationRunning);

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
			var hud = GetNode<Hud>("Hud");
			hud.UpdateSimulationButton(simulationRunning);
		}

		// Update the speed slider to show max value using exponential mapping
		var hud2 = GetNode<Hud>("Hud");
		hud2.SetSliderFromSpeed(1000.0f); // This will set slider to its maximum position (100)

		GD.Print("Max speed activated - simulation running at maximum speed");
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

		// Use simulation traveller instead of regular traveller
		var tNode = SimulationTravellerScene?.Instance() as Node2D ?? TravellerScene.Instance() as Node2D;
		AddChild(tNode);

		if (tNode is SimulationTraveller simTraveller)
		{
			// Add to active simulation travellers list
			_activeSimulationTravellers.Add(simTraveller);

			// get the waypoints
			var env = GetNode<Node>("Environment");
			var startC = env.GetNode<ColorRect>("StartPosition");
			var midC = env.GetNode<ColorRect>("MiddlePosition");
			var endC = env.GetNode<ColorRect>("EndPosition");

			// Start traveller at start position
			simTraveller.Position = startC.RectPosition;

			// Journey goes from start → middle → end
			var points = new List<Vector2> {
				midC.RectPosition,
				endC.RectPosition
			};

			// start the journey, with callback to clean up traveller only when no more boxes
			simTraveller.StartJourney(
				points,
				2.0f,      // duration
				0.5f,      // delay
				boxToCarry, // ← box to carry
				() =>
				{
					// callback when journey is complete (no more boxes)
					_activeSimulationTravellers.Remove(simTraveller);
					simTraveller.QueueFree();
					GD.Print("All boxes delivered!");
				}
			);
		}
		else if (tNode is Traveller traveller)
		{
			// Fallback to old traveller system
			_activeTravellers.Add(traveller);

			// get the waypoints
			var env = GetNode<Node>("Environment");
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
				2.0f,      // duration
				0.5f,      // delay
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
	}

	public void _ReorganizeBoxes()
	{
		var env = GetNode<Node>("Environment");
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

		// Get data from simulation travellers
		foreach (var simTraveller in _activeSimulationTravellers)
		{
			if (simTraveller != null && IsInstanceValid(simTraveller))
			{
				timelines.Add(simTraveller.GetTravellerInfo());
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
		// We just need to update the HUD and check for completion

		// Check if all boxes have been delivered
		CheckSimulationComplete();

		var hud = GetNode<Hud>("Hud");
		var dateTime = DateTimeOffset.FromUnixTimeSeconds((long)simTime).ToLocalTime();
		string timeString = dateTime.ToString("HH:mm:ss");
		string statusText = simulationRunning ? timeString : $"{timeString} [COMPLETE]";
		hud.UpdateSimTime(statusText);
	}

	private void CheckSimulationComplete()
	{
		// Get delivered boxes count
		var env = GetNode<Node>("Environment");
		var deliveredBoxContainer = env.GetNodeOrNull<Node2D>("DeliveredBoxes");

		if (deliveredBoxContainer != null)
		{
			int deliveredCount = deliveredBoxContainer.GetChildCount();

			// Stop simulation when all boxes are delivered and no active travellers
			if (deliveredCount >= TOTAL_BOXES && _activeTravellers.Count == 0 && _activeSimulationTravellers.Count == 0)
			{
				if (simulationRunning)
				{
					simulationRunning = false;
					_simulation.IsRunning = false;
					GD.Print($"Simulation Complete! All {TOTAL_BOXES} boxes delivered in {simTime:F1} seconds.");
				}
			}
		}
	}
}