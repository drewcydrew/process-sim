using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

public class Hud : CanvasLayer
{
	// C# event (no args)
	public event Action SpawnTraveller;
	public event Action ResetSimTime;
	public event Action ToggleSimulation;
	public event Action MaxSpeed;

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

		// Update download button text
		UpdateDownloadButtonText();
	}

	public void UpdateSimulationButton(bool isRunning)
	{
		var stopStartButton = GetNodeOrNull<Button>("StopStartButton");
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

		// Reset speed slider and label to normal
		var speedSlider = GetNode<HSlider>("SpeedSlider");
		speedSlider.Value = 1.0f;
		UpdateSpeedLabel(1.0f);
	}

	private void OnSpeedSliderChanged(float value)
	{
		TimeScaleChanged?.Invoke(value);
		UpdateSpeedLabel(value);
	}

	public void UpdateSpeedLabel(float speed)
	{
		var speedLabel = GetNode<Label>("SpeedLabel");
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

		// Update download button text
		UpdateDownloadButtonText();
	}

	private void UpdateDownloadButtonText()
	{
		var downloadButton = GetNodeOrNull<Button>("GanttChart/DownloadButton");
		if (downloadButton != null)
		{
			int travellerCount = _persistentTimelines.Count;
			downloadButton.Text = travellerCount > 0 ? $"Download CSV ({travellerCount} travellers)" : "Download CSV";
		}
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

	private void OnDownloadCsvPressed()
	{
		if (_persistentTimelines.Count == 0)
		{
			GD.Print("No traveller data to export.");
			return;
		}

		try
		{
			string csvContent = GenerateCsvData();
			string fileName = $"traveller_timeline_{DateTime.Now:yyyyMMdd_HHmmss}.csv";

			// Check if running on web platform
			if (OS.GetName() == "HTML5")
			{
				// Web platform - use JavaScript to trigger download
				DownloadCsvOnWeb(csvContent, fileName);
			}
			else
			{
				// Desktop platform - use file system
				DownloadCsvOnDesktop(csvContent, fileName);
			}
		}
		catch (Exception e)
		{
			GD.PrintErr($"Error exporting CSV: {e.Message}");
		}
	}

	private void DownloadCsvOnWeb(string csvContent, string fileName)
	{
		// Escape content for JavaScript
		string escapedContent = csvContent.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r");

		// JavaScript code to create and download the file
		string jsCode = $@"
			var content = ""{escapedContent}"";
			var blob = new Blob([content], {{ type: 'text/csv;charset=utf-8;' }});
			var link = document.createElement('a');
			var url = URL.createObjectURL(blob);
			link.setAttribute('href', url);
			link.setAttribute('download', '{fileName}');
			link.style.visibility = 'hidden';
			document.body.appendChild(link);
			link.click();
			document.body.removeChild(link);
			URL.revokeObjectURL(url);
		";

		// Execute JavaScript to trigger download
		JavaScript.Eval(jsCode);
		GD.Print($"CSV download triggered for web: {fileName}");
	}

	private void DownloadCsvOnDesktop(string csvContent, string fileName)
	{
		string filePath = OS.GetUserDataDir() + "/" + fileName;

		var file = new File();
		file.Open(filePath, File.ModeFlags.Write);
		file.StoreString(csvContent);
		file.Close();

		GD.Print($"CSV exported successfully to: {filePath}");

		// Also try to save to Downloads folder if possible
		string downloadsPath = OS.GetSystemDir(OS.SystemDir.Downloads) + "/" + fileName;
		var downloadsFile = new File();
		if (downloadsFile.Open(downloadsPath, File.ModeFlags.Write) == Error.Ok)
		{
			downloadsFile.StoreString(csvContent);
			downloadsFile.Close();
			GD.Print($"CSV also saved to Downloads: {downloadsPath}");
		}
	}

	private string GenerateCsvData()
	{
		var csv = new StringBuilder();

		// Add metadata header
		var main = GetTree().Root.GetNode<Main>("Main");
		csv.AppendLine($"# Process Simulation Timeline Export");
		csv.AppendLine($"# Export Time: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
		csv.AppendLine($"# Simulation Time: {main.simTime:F2} seconds");
		csv.AppendLine($"# Simulation Status: {(main.IsSimulationRunning() ? "Running" : "Complete")}");
		csv.AppendLine($"# Total Travellers: {_persistentTimelines.Count}");
		csv.AppendLine($"#");

		// CSV Header with more detailed information
		csv.AppendLine("TravellerID,TravellerName,ActivityType,StartTime,EndTime,Duration,SequenceNumber");

		// Sort travellers by ID for consistent output
		var sortedTravellers = _persistentTimelines.Values.OrderBy(t => t.Id).ToList();

		foreach (var traveller in sortedTravellers)
		{
			for (int i = 0; i < traveller.Timeline.Count; i++)
			{
				var segment = traveller.Timeline[i];
				float endTime = segment.EndTime == -1 ?
					GetTree().Root.GetNode<Main>("Main").simTime : segment.EndTime;
				float duration = endTime - segment.StartTime;

				csv.AppendLine($"{traveller.Id}," +
							  $"\"{traveller.Name}\"," +
							  $"\"{segment.ActivityType}\"," +
							  $"{segment.StartTime:F3}," +
							  $"{endTime:F3}," +
							  $"{duration:F3}," +
							  $"{i + 1}");
			}
		}

		return csv.ToString();
	}
}