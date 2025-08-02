using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public class DiscreteEventSimulation : Node
{
    // Priority queue for events (sorted by time)
    private SortedDictionary<float, Queue<SimulationEvent>> _eventQueue = new SortedDictionary<float, Queue<SimulationEvent>>();

    // Current simulation time
    public float CurrentTime { get; private set; } = 0f;

    // Time scale for the simulation
    public float TimeScale { get; set; } = 1.0f;

    // Whether the simulation is running
    public bool IsRunning { get; set; } = true;

    // Maximum time step to prevent infinite loops
    private const float MAX_TIME_STEP = 1.0f;

    // Events for external systems to subscribe to
    public event Action<SimulationEvent> EventProcessed;
    public event Action<float> TimeAdvanced;

    public override void _Ready()
    {
        // Set process mode to always so it continues even when paused (if supported)
        // SetProcessMode(Node.ProcessModeEnum.Always); // This might not be available in Godot 3.x
    }

    public override void _Process(float delta)
    {
        if (!IsRunning || TimeScale <= 0)
            return;

        // Calculate how much simulation time to advance
        float timeToAdvance = delta * TimeScale;

        // Limit time advancement to prevent runaway simulation
        timeToAdvance = Mathf.Min(timeToAdvance, MAX_TIME_STEP);

        AdvanceSimulation(timeToAdvance);
    }

    public void AdvanceSimulation(float deltaTime)
    {
        float targetTime = CurrentTime + deltaTime;

        // Process all events up to the target time
        while (_eventQueue.Count > 0 && _eventQueue.Keys.First() <= targetTime)
        {
            float eventTime = _eventQueue.Keys.First();
            var eventsAtTime = _eventQueue[eventTime];

            // Advance time to this event
            CurrentTime = eventTime;
            TimeAdvanced?.Invoke(CurrentTime);

            // Process all events at this time
            while (eventsAtTime.Count > 0)
            {
                var evt = eventsAtTime.Dequeue();
                ProcessEvent(evt);
            }

            // Remove this time slot if empty
            _eventQueue.Remove(eventTime);
        }

        // Advance to target time if no more events
        if (CurrentTime < targetTime)
        {
            CurrentTime = targetTime;
            TimeAdvanced?.Invoke(CurrentTime);
        }
    }

    public void ScheduleEvent(SimulationEvent evt)
    {
        if (!_eventQueue.ContainsKey(evt.ScheduledTime))
        {
            _eventQueue[evt.ScheduledTime] = new Queue<SimulationEvent>();
        }

        _eventQueue[evt.ScheduledTime].Enqueue(evt);
    }

    public void ScheduleEvent(float time, EventType type, object data = null, Action<object> callback = null)
    {
        var evt = new SimulationEvent
        {
            ScheduledTime = time,
            Type = type,
            Data = data,
            Callback = callback
        };

        ScheduleEvent(evt);
        GD.Print($"Scheduled event {type} for time {time} (current: {CurrentTime})");
    }

    private void ProcessEvent(SimulationEvent evt)
    {
        // Execute the event callback if it exists
        evt.Callback?.Invoke(evt.Data);

        // Notify external systems
        EventProcessed?.Invoke(evt);

        GD.Print($"[{CurrentTime:F2}] Processed event: {evt.Type}");
    }

    public void Reset()
    {
        CurrentTime = 0f;
        _eventQueue.Clear();
        IsRunning = true; // Ensure simulation is running after reset
        GD.Print($"Discrete event simulation reset - Running: {IsRunning}, Time: {CurrentTime}");
    }

    // Get all scheduled events (for debugging)
    public List<SimulationEvent> GetScheduledEvents()
    {
        var events = new List<SimulationEvent>();
        foreach (var timeSlot in _eventQueue)
        {
            events.AddRange(timeSlot.Value);
        }
        return events;
    }

    // Fast-forward simulation to completion
    public void FastForward()
    {
        while (_eventQueue.Count > 0)
        {
            AdvanceSimulation(1000f); // Large time step
        }
    }
}

public class SimulationEvent
{
    public float ScheduledTime { get; set; }
    public EventType Type { get; set; }
    public object Data { get; set; }
    public Action<object> Callback { get; set; }
}

public enum EventType
{
    TravellerSpawn,
    TravellerStartMove,
    TravellerReachWaypoint,
    TravellerPickupBox,
    TravellerDeliverBox,
    TravellerFinishJourney,
    SimulationComplete
}
