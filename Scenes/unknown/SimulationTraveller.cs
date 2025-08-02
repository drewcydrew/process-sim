using Godot;
using System;
using System.Collections.Generic;

public class SimulationTraveller : Node2D
{
    private static int _nextId = 0;
    private int _travellerId;
    private string _travellerName;

    // Static method to reset ID counter for new simulation runs
    public static void ResetIdCounter()
    {
        _nextId = 0;
    }

    // Simulation state (managed by discrete events)
    public struct TravellerState
    {
        public Vector2 Position;
        public Vector2 TargetPosition;
        public TravellerActivity Activity;
        public float ActivityStartTime;
        public int CarriedBoxId;
        public bool HasBox;
        public List<Vector2> PlannedRoute;
        public int CurrentWaypointIndex;
    }

    public TravellerState SimulationState { get; private set; }

    // Visual representation (follows simulation)
    private Vector2 _visualPosition;
    private Vector2 _visualTarget;
    private float _visualProgress;
    private bool _isVisuallyMoving;
    private float _visualMoveStartTime;
    private float _visualMoveDuration;

    // References
    private DiscreteEventSimulation _simulation;
    private Node2D _carriedBox;

    // UI Elements
    private Label _nameLabel;
    private Label _activityLabel;

    // Journey configuration
    private float _segmentDuration = 2.0f;
    private float _delayDuration = 0.5f;
    private Action _onJourneyComplete;

    // Activity tracking for timeline
    public List<TravellerSegment> Timeline { get; private set; } = new List<TravellerSegment>();

    // Names for travellers
    private static readonly string[] _travellerNames = {
        "Alex", "Blake", "Casey", "Dana", "Ellis", "Finley", "Gray", "Harper",
        "Indie", "Jordan", "Kelly", "Logan", "Morgan", "Noel", "Oakley", "Parker",
        "Quinn", "River", "Sage", "Taylor", "Unity", "Vale", "Wren", "Xander", "Yael", "Zara"
    };

    public override void _Ready()
    {
        // Generate traveller ID and name
        _travellerId = _nextId++;
        _travellerName = _travellerNames[_travellerId % _travellerNames.Length];

        // Get references
        _simulation = GetTree().Root.GetNode<DiscreteEventSimulation>("Main/DiscreteEventSimulation");

        // Get UI elements
        _nameLabel = GetNode<Label>("NameLabel");
        _activityLabel = GetNode<Label>("ActivityLabel");

        // Set initial name
        _nameLabel.Text = _travellerName;

        // Initialize state
        SimulationState = new TravellerState
        {
            Position = GlobalPosition,
            Activity = TravellerActivity.Starting,
            ActivityStartTime = _simulation.CurrentTime,
            CarriedBoxId = 0,
            HasBox = false,
            PlannedRoute = new List<Vector2>(),
            CurrentWaypointIndex = 0
        };

        _visualPosition = GlobalPosition;
        _UpdateActivity(TravellerActivity.Starting);
    }

    public override void _Process(float delta)
    {
        // Update visual representation to follow simulation state
        UpdateVisualMovement(delta);

        // Update carried box position if we have one
        if (_carriedBox != null && SimulationState.HasBox)
        {
            _carriedBox.GlobalPosition = _visualPosition;
            _carriedBox.Visible = true;
        }
    }

    private void UpdateVisualMovement(float delta)
    {
        if (!_isVisuallyMoving)
        {
            // Check if we need to start visual movement towards simulation position
            float distance = _visualPosition.DistanceTo(SimulationState.Position);
            if (distance > 1.0f) // Threshold to start visual movement
            {
                StartVisualMovement(SimulationState.Position);
            }
            return;
        }

        // Calculate visual movement progress
        float elapsed = _simulation.CurrentTime - _visualMoveStartTime;
        float progress = Mathf.Clamp(elapsed / _visualMoveDuration, 0f, 1f);

        // Interpolate visual position
        _visualPosition = _visualPosition.LinearInterpolate(_visualTarget, progress);
        GlobalPosition = _visualPosition;

        // Check if visual movement is complete
        if (progress >= 1.0f)
        {
            _isVisuallyMoving = false;
            _visualPosition = _visualTarget;
            GlobalPosition = _visualPosition;
        }
    }

    private void StartVisualMovement(Vector2 target)
    {
        _visualTarget = target;
        _visualMoveStartTime = _simulation.CurrentTime;

        // Calculate duration based on distance (for smooth visual movement)
        float distance = _visualPosition.DistanceTo(target);
        _visualMoveDuration = Mathf.Max(0.1f, distance / 100f); // Adjust speed as needed

        _isVisuallyMoving = true;
    }

    public void StartJourney(
        List<Vector2> waypoints,
        float segmentDuration,
        float delayBetween,
        Node2D boxToCarry,
        Action onJourneyComplete = null
    )
    {
        _segmentDuration = segmentDuration;
        _delayDuration = delayBetween;
        _onJourneyComplete = onJourneyComplete;
        _carriedBox = boxToCarry;

        // Update simulation state
        var state = SimulationState;
        state.PlannedRoute = waypoints;
        state.CurrentWaypointIndex = 0;
        SimulationState = state;

        GD.Print($"SimulationTraveller {_travellerId}: Starting journey at sim time {_simulation.CurrentTime}");

        // Schedule first movement with a small delay to ensure simulation is ready
        float startDelay = 0.1f; // Small delay to ensure simulation is properly initialized
        _simulation.ScheduleEvent(_simulation.CurrentTime + startDelay, EventType.TravellerStartMove, this, (_) => ScheduleNextMovement());
    }

    private void ScheduleNextMovement()
    {
        if (SimulationState.CurrentWaypointIndex >= SimulationState.PlannedRoute.Count)
        {
            // Journey complete
            HandleJourneyComplete();
            return;
        }

        Vector2 targetPosition = SimulationState.PlannedRoute[SimulationState.CurrentWaypointIndex];

        // Update activity based on current waypoint
        TravellerActivity nextActivity;
        if (SimulationState.CurrentWaypointIndex == 0)
        {
            nextActivity = TravellerActivity.MovingToPickup;
        }
        else if (SimulationState.CurrentWaypointIndex == 1)
        {
            nextActivity = SimulationState.HasBox ? TravellerActivity.MovingToDelivery : TravellerActivity.MovingToPickup;
        }
        else
        {
            nextActivity = TravellerActivity.Returning;
        }

        _UpdateActivity(nextActivity);

        // Schedule arrival at waypoint
        float arrivalTime = _simulation.CurrentTime + _segmentDuration;
        _simulation.ScheduleEvent(arrivalTime, EventType.TravellerReachWaypoint, this, OnReachWaypoint);

        GD.Print($"SimulationTraveller {_travellerId}: Scheduled waypoint arrival at {arrivalTime} (current: {_simulation.CurrentTime})");

        // Update simulation state
        var state = SimulationState;
        state.TargetPosition = targetPosition;
        SimulationState = state;
    }

    private void OnReachWaypoint(object data)
    {
        // Update simulation position
        var state = SimulationState;
        state.Position = SimulationState.TargetPosition;
        SimulationState = state;

        // Handle waypoint-specific logic
        if (SimulationState.CurrentWaypointIndex == 0) // Reached pickup point
        {
            HandlePickup();
        }
        else if (SimulationState.CurrentWaypointIndex == 1) // Reached delivery point
        {
            if (SimulationState.HasBox)
            {
                HandleDelivery();
            }
            else
            {
                // Continue to next waypoint
                AdvanceToNextWaypoint();
            }
        }
        else
        {
            // Continue to next waypoint or complete journey
            AdvanceToNextWaypoint();
        }
    }

    private void HandlePickup()
    {
        _UpdateActivity(TravellerActivity.PickingUp);

        // Schedule pickup completion
        float pickupCompleteTime = _simulation.CurrentTime + _delayDuration;
        _simulation.ScheduleEvent(pickupCompleteTime, EventType.TravellerPickupBox, this, OnPickupComplete);
    }

    private void OnPickupComplete(object data)
    {
        // Update simulation state
        var state = SimulationState;
        state.HasBox = true;
        state.CarriedBoxId = (int)(_carriedBox?.GetInstanceId() ?? 0);
        SimulationState = state;

        // Handle visual pickup
        if (_carriedBox != null)
        {
            _carriedBox.GetParent().RemoveChild(_carriedBox);
            AddChild(_carriedBox);
            _carriedBox.Position = Vector2.Zero;
            _carriedBox.Visible = true;

            // Reorganize remaining boxes
            var main = GetTree().Root.GetNode<Main>("Main");
            main.CallDeferred("_ReorganizeBoxes");
        }

        // Continue to next waypoint
        AdvanceToNextWaypoint();
    }

    private void HandleDelivery()
    {
        _UpdateActivity(TravellerActivity.Delivering);

        // Schedule delivery completion
        float deliveryCompleteTime = _simulation.CurrentTime + _delayDuration;
        _simulation.ScheduleEvent(deliveryCompleteTime, EventType.TravellerDeliverBox, this, OnDeliveryComplete);
    }

    private void OnDeliveryComplete(object data)
    {
        // Update simulation state
        var state = SimulationState;
        state.HasBox = false;
        state.CarriedBoxId = 0;
        SimulationState = state;

        // Handle visual delivery
        if (_carriedBox != null)
        {
            var environment = GetTree().Root.GetNode<Node>("Main/Environment");
            var deliveredBoxContainer = environment.GetNode<Node2D>("DeliveredBoxes");
            var endPosition = environment.GetNode<ColorRect>("EndPosition");

            RemoveChild(_carriedBox);
            deliveredBoxContainer.AddChild(_carriedBox);

            // Position delivered boxes in a grid
            int deliveredCount = deliveredBoxContainer.GetChildCount() - 1;
            int row = deliveredCount / 5;
            int col = deliveredCount % 5;

            Vector2 gridOffset = new Vector2(
                (col - 2f) * 25f,
                row * 25f
            );

            _carriedBox.Position = endPosition.RectPosition + gridOffset;
            _carriedBox = null;
        }

        // Check if there are more boxes to deliver
        var main = GetTree().Root.GetNode<Main>("Main");
        if (main._availableBoxes.Count > 0)
        {
            // Start return journey for next box
            StartReturnJourney();
        }
        else
        {
            // No more boxes, complete journey
            HandleJourneyComplete();
        }
    }

    private void StartReturnJourney()
    {
        _UpdateActivity(TravellerActivity.Returning);

        // Set up return journey to pickup point
        var env = GetTree().Root.GetNode<Node>("Main/Environment");
        var midC = env.GetNode<ColorRect>("MiddlePosition");

        var state = SimulationState;
        state.PlannedRoute = new List<Vector2> { midC.RectPosition };
        state.CurrentWaypointIndex = 0;
        SimulationState = state;

        // Schedule delay then movement
        float delayCompleteTime = _simulation.CurrentTime + _delayDuration;
        _simulation.ScheduleEvent(delayCompleteTime, EventType.TravellerStartMove, this, (_) => ScheduleNextMovement());
    }

    private void AdvanceToNextWaypoint()
    {
        var state = SimulationState;
        state.CurrentWaypointIndex++;
        SimulationState = state;

        // Schedule delay then next movement
        float delayCompleteTime = _simulation.CurrentTime + _delayDuration;
        _simulation.ScheduleEvent(delayCompleteTime, EventType.TravellerStartMove, this, (_) => ScheduleNextMovement());
    }

    private void HandleJourneyComplete()
    {
        _UpdateActivity(TravellerActivity.Finished);

        // Schedule cleanup
        float cleanupTime = _simulation.CurrentTime + 0.1f;
        _simulation.ScheduleEvent(cleanupTime, EventType.TravellerFinishJourney, this, (_) =>
        {
            _onJourneyComplete?.Invoke();
        });
    }

    private void _UpdateActivity(TravellerActivity activity)
    {
        // End the previous activity if one was active
        if (Timeline.Count > 0 && Timeline[Timeline.Count - 1].EndTime == -1)
        {
            Timeline[Timeline.Count - 1].EndTime = _simulation.CurrentTime;
        }

        // Update simulation state
        var state = SimulationState;
        state.Activity = activity;
        state.ActivityStartTime = _simulation.CurrentTime;
        SimulationState = state;

        // Start a new timeline segment for this activity
        Timeline.Add(new TravellerSegment
        {
            StartTime = _simulation.CurrentTime,
            EndTime = -1, // Will be set when activity changes
            ActivityType = activity,
            TravellerId = _travellerId,
            TravellerName = _travellerName
        });

        // Update visual activity label
        switch (activity)
        {
            case TravellerActivity.Starting:
                _activityLabel.Text = "Starting journey";
                break;
            case TravellerActivity.MovingToPickup:
                _activityLabel.Text = "Moving to pickup";
                break;
            case TravellerActivity.PickingUp:
                _activityLabel.Text = "Picking up box";
                break;
            case TravellerActivity.MovingToDelivery:
                _activityLabel.Text = "Delivering box";
                break;
            case TravellerActivity.Delivering:
                _activityLabel.Text = "Dropping off box";
                break;
            case TravellerActivity.Returning:
                _activityLabel.Text = "Returning for next";
                break;
            case TravellerActivity.Waiting:
                _activityLabel.Text = "Waiting";
                break;
            case TravellerActivity.Finished:
                _activityLabel.Text = "Journey complete";
                break;
        }
    }

    // Public method to get traveller info for Gantt chart
    public Traveller.TravellerInfo GetTravellerInfo()
    {
        // Convert our timeline to the expected format
        var convertedTimeline = new List<Traveller.TravellerSegment>();
        foreach (var segment in Timeline)
        {
            convertedTimeline.Add(new Traveller.TravellerSegment
            {
                StartTime = segment.StartTime,
                EndTime = segment.EndTime,
                From = segment.From,
                To = segment.To,
                TravellerId = segment.TravellerId,
                TravellerName = segment.TravellerName,
                ActivityType = (Traveller.TravellerActivity)segment.ActivityType
            });
        }

        return new Traveller.TravellerInfo
        {
            Id = _travellerId,
            Name = _travellerName,
            Timeline = convertedTimeline,
            CurrentActivity = (Traveller.TravellerActivity)SimulationState.Activity
        };
    }
}

// Activity states (reusing from original)
public enum TravellerActivity
{
    Starting,
    MovingToPickup,
    PickingUp,
    MovingToDelivery,
    Delivering,
    Returning,
    Waiting,
    Finished
}

// Timeline segment (reusing from original)
public class TravellerSegment
{
    public float StartTime;
    public float EndTime;
    public Vector2 From;
    public Vector2 To;
    public int TravellerId;
    public string TravellerName;
    public TravellerActivity ActivityType;
}
