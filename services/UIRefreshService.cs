using System;
using System.Timers;

namespace DadPlanner2.Services;

/// <summary>
/// Lightweight timer-based service to refresh UI elements showing time-relative data.
/// Fires every minute to update displays like "X hours ago" or "Y hours until appointment".
/// </summary>
public class UIRefreshService : IDisposable
{
    private readonly Timer _refreshTimer;
    public event Action? OnRefreshTick;

    public UIRefreshService()
    {
        _refreshTimer = new Timer(60_000) // 60 seconds
        {
            AutoReset = true,
            Enabled = false
        };
        _refreshTimer.Elapsed += (_, _) => OnRefreshTick?.Invoke();
    }

    public void Start() => _refreshTimer.Start();
    public void Stop() => _refreshTimer.Stop();
    public void Dispose() => _refreshTimer?.Dispose();
}
