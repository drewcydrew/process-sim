using Godot;
using System;
using System.Collections.Generic;

public class GanttChartDebugger : Node
{
    private Main _main;
    private Hud _hud;
    private int _updateCount = 0;

    public override void _Ready()
    {
        _main = GetNode<Main>("/root/Main");
        _hud = GetNode<Hud>("/root/Main/Hud");

        if (_main == null || _hud == null)
        {
            GD.Print("GanttChartDebugger: Could not find Main or Hud nodes");
            return;
        }

        GD.Print("GanttChartDebugger: Initialized successfully");
    }

    public override void _Process(float delta)
    {
        _updateCount++;

        // Every 60 frames (about 1 second), log debug info
        if (_updateCount % 60 == 0)
        {
            LogGanttStatus();
        }
    }

    private void LogGanttStatus()
    {
        if (_main == null || _hud == null) return;

        var travellers = _main.GetAllTravellerTimelines();
        var simTime = _main.simTime;

        GD.Print($"=== Gantt Debug [{simTime:F1}s] ===");
        GD.Print($"Active travellers: {travellers.Count}");
        GD.Print($"Simulation travellers: {_main._activeSimulationTravellers.Count}");
        GD.Print($"Regular travellers: {_main._activeTravellers.Count}");

        foreach (var traveller in travellers)
        {
            GD.Print($"  Traveller {traveller.Id} ({traveller.Name}): {traveller.Timeline.Count} timeline entries");
            if (traveller.Timeline.Count > 0)
            {
                var latest = traveller.Timeline[traveller.Timeline.Count - 1];
                GD.Print($"    Latest activity: {latest.ActivityType} ({latest.StartTime:F1}s - {(latest.EndTime == -1 ? "ongoing" : latest.EndTime.ToString("F1") + "s")})");
            }
        }

        GD.Print("=========================");
    }

    public void OnSimulationReset()
    {
        GD.Print("GanttChartDebugger: Simulation reset detected");
        _updateCount = 0;
        LogGanttStatus();
    }
}
