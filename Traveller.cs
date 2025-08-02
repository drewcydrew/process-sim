using Godot;
using System;
using System.Collections.Generic;

public class Traveller : Node2D
{
	private static int _nextId = 0;
	private int _travellerId;

	private Godot.Collections.Array<Vector2> _waypoints;
	private int _currentIndex = 0;
	private float _duration = 2.0f;
	private float _delayTimer;
	private float _delay = 0.5f;
	private float _progress = 0.0f;
	private bool _isMoving = false;
	private bool _isDelaying = false;
	private float _segmentStartTime = 0f;
	private Vector2 _start;

	private Func<float> _getTimeScale = () => 1.0f;
	private Func<float> _getSimTime = () => 0.0f;

	private Node2D _carriedBox;

	private bool _hasDeliveredBox = false;
	private bool _hasPickedUpBox = false;
	private Action _onJourneyComplete;
	private bool _returningToPickup = false;

	private const int DELIVERED_BOXES_PER_ROW = 5;
	private const float DELIVERED_BOX_SPACING = 25.0f;

	public List<TravellerSegment> Timeline { get; private set; } = new List<TravellerSegment>();

	public override void _Process(float delta)
	{
		float scale = _getTimeScale();

		if (_isDelaying)
		{
			_delayTimer -= delta * scale;
			if (_delayTimer <= 0.0f)
				_isDelaying = false;
			return;
		}

		if (!_isMoving || _currentIndex >= _waypoints.Count)
			return;

		_progress += delta * scale;
		float t = Mathf.Clamp(_progress / _duration, 0f, 1f);
		Vector2 from = _start;
		Vector2 to = _waypoints[_currentIndex];
		Position = from.LinearInterpolate(to, t);

		// keep the box glued to the traveller (only if we have one and picked it up)
		if (_carriedBox != null && _hasPickedUpBox)
		{
			_carriedBox.GlobalPosition = GlobalPosition;
			_carriedBox.Visible = true;
		}

		if (t >= 1.0f)
		{
			_currentIndex++;

			// Check if we've reached the middle position (pickup point)
			if (_currentIndex == 1 && _carriedBox != null && !_hasPickedUpBox)
			{
				// Remove the box from the scene and attach it to the traveller
				_carriedBox.GetParent().RemoveChild(_carriedBox);
				AddChild(_carriedBox);
				_carriedBox.Position = Vector2.Zero;
				_hasPickedUpBox = true;
				_carriedBox.Visible = true;

				// Reorganize remaining boxes
				var main = GetTree().Root.GetNode<Main>("Main");
				main.CallDeferred("_ReorganizeBoxes");
			}

			if (_currentIndex >= _waypoints.Count)
			{
				if (_carriedBox != null && !_hasDeliveredBox && _hasPickedUpBox)
				{
					// Drop off the box at end position
					var environment = GetTree().Root.GetNode<Node>("Main/Environment");
					var deliveredBoxContainer = environment.GetNode<Node2D>("DeliveredBoxes");
					var endPosition = environment.GetNode<ColorRect>("EndPosition");

					RemoveChild(_carriedBox);
					deliveredBoxContainer.AddChild(_carriedBox);

					// Position delivered boxes in a grid at the end position
					int deliveredCount = deliveredBoxContainer.GetChildCount() - 1;
					int row = deliveredCount / DELIVERED_BOXES_PER_ROW;
					int col = deliveredCount % DELIVERED_BOXES_PER_ROW;

					Vector2 gridOffset = new Vector2(
						(col - (DELIVERED_BOXES_PER_ROW - 1) / 2.0f) * DELIVERED_BOX_SPACING,
						row * DELIVERED_BOX_SPACING
					);

					_carriedBox.Position = endPosition.RectPosition + gridOffset;
					_carriedBox = null;
					_hasDeliveredBox = true;

					// Start return journey to pick up another box
					_StartReturnJourney();
				}
				else if (_returningToPickup)
				{
					// We've returned to pickup point, check if there are more boxes
					var main = GetTree().Root.GetNode<Main>("Main");
					if (main._availableBoxes.Count > 0)
					{
						// Get next box and continue the cycle
						_StartNextDelivery();
					}
					else
					{
						// No more boxes, complete the journey
						_isMoving = false;
						_onJourneyComplete?.Invoke();
					}
				}
				else
				{
					// No box to deliver, just finish
					_isMoving = false;
					_onJourneyComplete?.Invoke();
				}
			}
			else
			{
				// prepare for next leg…
				_isDelaying = true;
				_delayTimer = _delay;
				_start = Position;
				_progress = 0f;
				_segmentStartTime = _getSimTime();
			}
		}
	}

	private void _StartReturnJourney()
	{
		// Set up return journey from end back to middle
		var env = GetTree().Root.GetNode<Node>("Main/Environment");
		var midC = env.GetNode<ColorRect>("MiddlePosition");

		_waypoints = new Godot.Collections.Array<Vector2> { midC.RectPosition };
		_currentIndex = 0;
		_progress = 0f;
		_start = Position;
		_returningToPickup = true;
		_isDelaying = true;
		_delayTimer = _delay;
	}

	private void _StartNextDelivery()
	{
		// Get the next box from Main
		var main = GetTree().Root.GetNode<Main>("Main");
		if (main._availableBoxes.Count == 0)
		{
			// No more boxes, complete journey
			_isMoving = false;
			_onJourneyComplete?.Invoke();
			return;
		}

		// Get next box
		var nextBox = main._availableBoxes.Dequeue();
		_carriedBox = nextBox;

		// Reset states for next delivery
		_hasPickedUpBox = false;
		_hasDeliveredBox = false;
		_returningToPickup = false;

		// Set up journey from middle to end (we're already at middle)
		var env = GetTree().Root.GetNode<Node>("Main/Environment");
		var endC = env.GetNode<ColorRect>("EndPosition");

		_waypoints = new Godot.Collections.Array<Vector2> { endC.RectPosition };
		_currentIndex = 0;
		_progress = 0f;
		_start = Position;
		_isDelaying = true;
		_delayTimer = _delay;

		// Immediately pickup the box since we're at the pickup location
		_carriedBox.GetParent().RemoveChild(_carriedBox);
		AddChild(_carriedBox);
		_carriedBox.Position = Vector2.Zero;
		_hasPickedUpBox = true;
		_carriedBox.Visible = true;

		// Reorganize remaining boxes
		main.CallDeferred("_ReorganizeBoxes");
	}

	public void StartJourney(
		Godot.Collections.Array<Vector2> waypoints,
		float segmentDuration,
		float delayBetween,
		Func<float> getTimeScale,
		Func<float> getSimTime,
		Node2D boxToCarry,
		Action onJourneyComplete = null
	)
	{
		_waypoints = waypoints;
		_duration = segmentDuration;
		_delay = delayBetween;
		_getTimeScale = getTimeScale;
		_getSimTime = getSimTime;
		_onJourneyComplete = onJourneyComplete;

		// store reference to the box but don't move it yet (pickup happens at middle position)
		_carriedBox = boxToCarry;

		// existing initialization…
		_currentIndex = 0;
		_progress = 0f;
		_isMoving = true;
		_isDelaying = false;
		_hasDeliveredBox = false;
		_hasPickedUpBox = false;
		_returningToPickup = false;
		_start = Position;
		_segmentStartTime = _getSimTime();
	}


	public class TravellerSegment
	{
		public float StartTime;
		public float EndTime;
		public Vector2 From;
		public Vector2 To;
		public int TravellerId;
	}
}
