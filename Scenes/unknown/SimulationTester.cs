using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public class SimulationTester : Node
{
    private DiscreteEventSimulation _simulation;
    private List<float> _eventTimes = new List<float>();
    private List<string> _eventLog = new List<string>();

    public override void _Ready()
    {
        // Find the discrete event simulation
        _simulation = GetNode<DiscreteEventSimulation>("/root/Main/DiscreteEventSimulation");

        if (_simulation != null)
        {
            _simulation.EventProcessed += OnEventProcessed;
            GD.Print("SimulationTester: Connected to discrete event simulation");

            // Schedule some test events
            _simulation.ScheduleEvent(1.0f, EventType.TravellerSpawn, "Test Event 1", (data) =>
            {
                GD.Print($"Test Event 1 executed at time {_simulation.CurrentTime}: {data}");
            });

            _simulation.ScheduleEvent(2.5f, EventType.TravellerStartMove, "Test Event 2", (data) =>
            {
                GD.Print($"Test Event 2 executed at time {_simulation.CurrentTime}: {data}");
            });

            _simulation.ScheduleEvent(5.0f, EventType.TravellerReachWaypoint, "Test Event 3", (data) =>
            {
                GD.Print($"Test Event 3 executed at time {_simulation.CurrentTime}: {data}");
            });
        }
        else
        {
            GD.Print("SimulationTester: Could not find discrete event simulation!");
        }
    }

    private void OnEventProcessed(SimulationEvent evt)
    {
        _eventTimes.Add(evt.ScheduledTime);
        _eventLog.Add($"[{evt.ScheduledTime:F2}] {evt.Type}");

        // Log every 10 events
        if (_eventLog.Count % 10 == 0)
        {
            GD.Print($"SimulationTester: Processed {_eventLog.Count} events so far");
        }
    }

    public override void _Process(float delta)
    {
        // Every 10 seconds, print a summary
        if ((_simulation?.CurrentTime ?? 0f) % 10f < 0.1f && _eventTimes.Count > 0)
        {
            PrintEventSummary();
        }
    }

    private void PrintEventSummary()
    {
        if (_eventTimes.Count == 0) return;

        var sortedTimes = _eventTimes.OrderBy(t => t).ToList();
        float minTime = sortedTimes.First();
        float maxTime = sortedTimes.Last();
        float avgTime = sortedTimes.Average();

        GD.Print($"Event Summary: {_eventTimes.Count} events, Time range: {minTime:F2}-{maxTime:F2}, Avg: {avgTime:F2}");

        // Check if events are processed in order
        bool inOrder = true;
        for (int i = 1; i < sortedTimes.Count; i++)
        {
            if (sortedTimes[i] < sortedTimes[i - 1])
            {
                inOrder = false;
                break;
            }
        }

        GD.Print($"Events processed in chronological order: {inOrder}");
    }

    public override void _ExitTree()
    {
        PrintFinalSummary();
    }

    private void PrintFinalSummary()
    {
        GD.Print("=== Final Simulation Test Summary ===");
        GD.Print($"Total events processed: {_eventLog.Count}");
        GD.Print($"Final simulation time: {_simulation?.CurrentTime:F2}");

        if (_eventLog.Count > 0)
        {
            GD.Print("Last 5 events:");
            for (int i = Math.Max(0, _eventLog.Count - 5); i < _eventLog.Count; i++)
            {
                GD.Print($"  {_eventLog[i]}");
            }
        }

        PrintEventSummary();
    }
}
