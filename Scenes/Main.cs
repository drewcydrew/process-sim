using Godot;
using System;
using System.Collections.Generic;

public class Main : Node
{
	[Export]
	public PackedScene TravellerScene;
	[Export] public PackedScene BoxScene;

	// Simulation time and time scale
	private float simTime = 0f;
	private float timeScale = 1.0f;

	// queue of all unclaimed boxes
	public Queue<Node2D> _availableBoxes = new Queue<Node2D>();

	// Grid configuration for box positioning
	private const int BOXES_PER_ROW = 5;
	private const float BOX_SPACING = 25.0f;

	public override void _Ready()
	{
		var hud = GetNode<Hud>("Hud");
		hud.SpawnTraveller += () =>
		{
			_SpawnTraveller();
		};

		hud.TimeScaleChanged += OnTimeScaleChanged;
		hud.ResetSimTime += OnResetSimulation;

		_InitializeBoxes();
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
		simTime = 0f;

		// Clear all travellers
		var travellers = new List<Node>();
		foreach (Node child in GetChildren())
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

		// Reset boxes
		_InitializeBoxes();

		GD.Print("Simulation reset!");
	}

	private void OnResetSimTime()
	{
		simTime = 0f;
	}


	public void OnTimeScaleChanged(float newScale)
	{
		timeScale = newScale;
	}

	private void _SpawnTraveller()
	{
		// only spawn if there's still a box to carry
		if (_availableBoxes.Count == 0)
		{
			GD.Print("No more boxes to deliver!");
			return;
		}

		// dequeue one box but DON'T remove it from the scene yet
		var boxToCarry = _availableBoxes.Dequeue();

		// instance traveller
		var tNode = TravellerScene.Instance() as Node2D;
		AddChild(tNode);
		var traveller = tNode as Traveller;

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
				traveller.QueueFree();
				GD.Print("All boxes delivered!");
			}
		);
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




	// Optional: you can update simTime in _Process
	public override void _Process(float delta)
	{
		simTime += delta * timeScale;

		var hud = GetNode<Hud>("Hud");
		var dateTime = DateTimeOffset.FromUnixTimeSeconds((long)simTime).ToLocalTime();
		string timeString = dateTime.ToString("HH:mm:ss");
		hud.UpdateSimTime(timeString);


	}
}