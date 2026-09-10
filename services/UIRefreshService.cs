using System;
using Avalonia.Threading;

namespace DadPlanner2.Services;

/// <summary>
/// Lightweight native UI timer to refresh time-relative data.
/// Fires every 1 second natively on the Avalonia UI thread.
/// </summary>
public class UIRefreshService : IDisposable
{
    private readonly DispatcherTimer _refreshTimer;
    public event Action? OnRefreshTick;

    public UIRefreshService()
    {
        _refreshTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1) // Tick every second
        };
        _refreshTimer.Tick += (_, _) => OnRefreshTick?.Invoke();
    }

    public void Start() => _refreshTimer.Start();
    public void Stop() => _refreshTimer.Stop();
    public void Dispose() => _refreshTimer.Stop();
}
