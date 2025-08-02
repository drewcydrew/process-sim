using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public class Hud : CanvasLayer
{
	// C# event (no args)
	public event Action SpawnTraveller;
	public event Action ResetSimTime;

	// C# event (with float arg)
	public event Action<float> TimeScaleChanged;

	// Gantt chart components
	private Control _ganttContent;
	private Panel _ganttChart;
	private const float GANTT_ROW_HEIGHT = 25.0f; // Slightly smaller for more compact view
	private const float GANTT_TIME_SCALE = 60.0f; // Increased from 50 to use more horizontal space
	private const float GANTT_LEFT_MARGIN = 100.0f; // Slightly larger for traveller names

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

	public override void _Ready()
	{
		// Initialize slider to default speed
		var speedSlider = GetNode<HSlider>("SpeedSlider");
		speedSlider.Value = 1.0;
		UpdateSpeedLabel(1.0f);

		// Get Gantt chart components
		_ganttChart = GetNode<Panel>("GanttChart");
		_ganttContent = GetNode<Control>("GanttChart/ScrollContainer/GanttContent");
	}

	private void OnSpawnButtonPressed()
	{
		SpawnTraveller?.Invoke();
	}

	private void OnResetPressed()
	{
		ResetSimTime?.Invoke();

		// Clear persistent timeline data
		_persistentTimelines.Clear();

		// Clear Gantt chart
		foreach (Node child in _ganttContent.GetChildren())
		{
			child.QueueFree();
		}
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

		// Update Gantt chart
		UpdateGanttChart();
	}

	private void UpdateGanttChart()
	{
		// Get all traveller timeline data from Main
		var main = GetTree().Root.GetNode<Main>("Main");
		if (main == null) return;

		var currentTravellerData = main.GetAllTravellerTimelines();
		float currentTime = main.simTime;

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
			}
		}

		// Ensure any ongoing activities in persistent data get their end times updated
		foreach (var persistentTraveller in _persistentTimelines.Values)
		{
			if (persistentTraveller.Timeline.Count > 0)
			{
				var lastSegment = persistentTraveller.Timeline[persistentTraveller.Timeline.Count - 1];
				if (lastSegment.EndTime == -1)
				{
					// Find if this traveller is still active
					var activeTraveller = currentTravellerData.FirstOrDefault(t => t.Id == persistentTraveller.Id);
					if (activeTraveller != null)
					{
						// Still active, keep updating
						lastSegment.EndTime = -1;
					}
					else
					{
						// No longer active, close the segment at current time
						lastSegment.EndTime = currentTime;
					}
				}
			}
		}

		// Clear existing Gantt chart content
		foreach (Node child in _ganttContent.GetChildren())
		{
			child.QueueFree();
		}

		if (_persistentTimelines.Count == 0) return;

		// Calculate time range from all persistent data
		float minTime = 0;
		float maxTime = currentTime;

		foreach (var traveller in _persistentTimelines.Values)
		{
			foreach (var segment in traveller.Timeline)
			{
				if (segment.StartTime < minTime) minTime = segment.StartTime;
				float endTime = segment.EndTime == -1 ? currentTime : segment.EndTime;
				if (endTime > maxTime) maxTime = endTime;
			}
		}

		// Add more padding and ensure we use the full available width
		maxTime = Math.Max(maxTime, currentTime) + 10;

		// Calculate available width from scroll container (985px - margins)
		float availableWidth = 985.0f - GANTT_LEFT_MARGIN - 50; // Account for margins and padding
		float timeRange = maxTime - minTime;

		// Use the full available width, but ensure reasonable scaling
		float dynamicTimeScale = Math.Max(30.0f, Math.Min(availableWidth / timeRange, 100.0f));

		// Set content size using dynamic scaling
		float contentWidth = (maxTime - minTime) * dynamicTimeScale + GANTT_LEFT_MARGIN + 50;
		float contentHeight = _persistentTimelines.Count * GANTT_ROW_HEIGHT + 40; // Extra space for time scale
		_ganttContent.RectMinSize = new Vector2(contentWidth, contentHeight);

		// Draw each traveller's timeline (sort by ID for consistent ordering)
		var sortedTravellers = _persistentTimelines.Values.OrderBy(t => t.Id).ToList();
		for (int i = 0; i < sortedTravellers.Count; i++)
		{
			var traveller = sortedTravellers[i];
			float yPos = i * GANTT_ROW_HEIGHT + 10;

			// Add traveller name label
			var nameLabel = new Label();
			nameLabel.Text = traveller.Name;
			nameLabel.RectPosition = new Vector2(5, yPos);
			nameLabel.RectSize = new Vector2(GANTT_LEFT_MARGIN - 10, GANTT_ROW_HEIGHT - 5);
			nameLabel.Valign = Label.VAlign.Center;
			_ganttContent.AddChild(nameLabel);

			// Draw activity segments
			foreach (var segment in traveller.Timeline)
			{
				float startX = GANTT_LEFT_MARGIN + (segment.StartTime - minTime) * dynamicTimeScale;
				float endTime = segment.EndTime == -1 ? currentTime : segment.EndTime;
				float width = (endTime - segment.StartTime) * dynamicTimeScale;

				// Skip segments that are too small to see
				if (width < 1) continue;

				var segmentRect = new ColorRect();
				Color segmentColor = _activityColors.ContainsKey(segment.ActivityType)
					? _activityColors[segment.ActivityType]
					: Colors.Gray;
				segmentRect.Color = segmentColor;
				segmentRect.RectPosition = new Vector2(startX, yPos + 2);
				segmentRect.RectSize = new Vector2(width, GANTT_ROW_HEIGHT - 8);
				_ganttContent.AddChild(segmentRect);

				// Add activity label if segment is wide enough
				if (width > 40) // Adjusted threshold for dynamic scaling
				{
					var activityLabel = new Label();
					activityLabel.Text = GetActivityShortName(segment.ActivityType);
					activityLabel.RectPosition = new Vector2(startX + 5, yPos + 2);
					activityLabel.RectSize = new Vector2(width - 10, GANTT_ROW_HEIGHT - 8);
					activityLabel.Valign = Label.VAlign.Center;
					activityLabel.Modulate = Colors.White;
					activityLabel.AddStyleboxOverride("normal", new StyleBoxEmpty());
					_ganttContent.AddChild(activityLabel);
				}
			}
		}

		// Add current time indicator
		float currentTimeX = GANTT_LEFT_MARGIN + (currentTime - minTime) * dynamicTimeScale;
		var timeLine = new ColorRect();
		timeLine.Color = Colors.Red;
		timeLine.RectPosition = new Vector2(currentTimeX, 0);
		timeLine.RectSize = new Vector2(2, contentHeight);
		_ganttContent.AddChild(timeLine);

		// Add time scale at bottom
		AddTimeScale(minTime, maxTime, contentHeight, dynamicTimeScale);
	}

	private void AddTimeScale(float minTime, float maxTime, float contentHeight, float timeScale)
	{
		// Calculate appropriate time interval based on available space
		float timeRange = maxTime - minTime;
		float targetMarkers = 8; // Aim for about 8 time markers
		float baseInterval = timeRange / targetMarkers;

		// Round to nice intervals (1, 2, 5, 10, 20, 50, etc.)
		float timeInterval;
		if (baseInterval <= 1) timeInterval = 1;
		else if (baseInterval <= 2) timeInterval = 2;
		else if (baseInterval <= 5) timeInterval = 5;
		else if (baseInterval <= 10) timeInterval = 10;
		else if (baseInterval <= 20) timeInterval = 20;
		else if (baseInterval <= 50) timeInterval = 50;
		else timeInterval = Mathf.Ceil(baseInterval / 10) * 10;

		for (float t = Mathf.Ceil(minTime / timeInterval) * timeInterval; t <= maxTime; t += timeInterval)
		{
			float x = GANTT_LEFT_MARGIN + (t - minTime) * timeScale;

			// Time marker line
			var markerLine = new ColorRect();
			markerLine.Color = new Color(0.7f, 0.7f, 0.7f);
			markerLine.RectPosition = new Vector2(x, contentHeight - 20);
			markerLine.RectSize = new Vector2(1, 15);
			_ganttContent.AddChild(markerLine);

			// Time label
			var timeLabel = new Label();
			timeLabel.Text = $"{t:F0}s";
			timeLabel.RectPosition = new Vector2(x - 15, contentHeight - 20);
			timeLabel.RectSize = new Vector2(30, 15);
			timeLabel.Align = Label.AlignEnum.Center;
			timeLabel.Valign = Label.VAlign.Center;
			_ganttContent.AddChild(timeLabel);
		}
	}
	private string GetActivityShortName(Traveller.TravellerActivity activity)
	{
		switch (activity)
		{
			case Traveller.TravellerActivity.Starting: return "Start";
			case Traveller.TravellerActivity.MovingToPickup: return "→ Pickup";
			case Traveller.TravellerActivity.PickingUp: return "Pick";
			case Traveller.TravellerActivity.MovingToDelivery: return "→ Deliver";
			case Traveller.TravellerActivity.Delivering: return "Drop";
			case Traveller.TravellerActivity.Returning: return "Return";
			case Traveller.TravellerActivity.Waiting: return "Wait";
			case Traveller.TravellerActivity.Finished: return "Done";
			default: return "?";
		}
	}
}