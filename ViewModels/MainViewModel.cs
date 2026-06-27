using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HeatMapTracker.Models;
using HeatMapTracker.Services;
using Microsoft.Maui.Maps;
using Location = Microsoft.Maui.Devices.Sensors.Location;

namespace HeatMapTracker.ViewModels;

/// <summary>
/// Backs MainPage. Owns the tracking start/stop commands and exposes the
/// current set of recorded points for the view to project into a heat map.
/// Deliberately framework-light (no platform map APIs here) so it stays
/// unit-testable; all map-specific pixel work happens in the code-behind /
/// HeatMapRenderer.
/// </summary>
public partial class MainViewModel : ObservableObject
{
    private readonly LocationDatabase _database;
    private readonly LocationTrackingService _trackingService;

    [ObservableProperty]
    private bool isTracking;

    [ObservableProperty]
    private string statusText = "Not tracking";

    [ObservableProperty]
    private int recordedPointCount;

    /// <summary>All points currently loaded from the database, used as the heat map source.</summary>
    public List<LocationPoint> Points { get; private set; } = new();

    /// <summary>Raised whenever Points changes and the map/heat-map overlay should redraw.</summary>
    public event EventHandler? PointsChanged;

    /// <summary>Raised when a brand-new point comes in while tracking, so the view can re-center the map if desired.</summary>
    public event EventHandler<Location>? NewPointTracked;

    public MainViewModel(LocationDatabase database, LocationTrackingService trackingService)
    {
        _database = database;
        _trackingService = trackingService;

        _trackingService.PointRecorded += OnPointRecorded;
        _trackingService.TrackingError += OnTrackingError;
    }

    [RelayCommand]
    private async Task LoadPointsAsync()
    {
        Points = await _database.GetAllPointsAsync();
        RecordedPointCount = Points.Count;
        PointsChanged?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private async Task ToggleTrackingAsync()
    {
        if (IsTracking)
        {
            _trackingService.StopTracking();
            IsTracking = false;
            StatusText = $"Stopped — {RecordedPointCount} points recorded";
            return;
        }

        StatusText = "Requesting location permission…";
        var started = await _trackingService.StartTrackingAsync();

        if (!started)
        {
            StatusText = "Location permission denied";
            IsTracking = false;
            return;
        }

        IsTracking = true;
        StatusText = "Tracking…";
    }

    [RelayCommand]
    private async Task ClearDataAsync()
    {
        await _database.DeleteAllAsync();
        Points = new();
        RecordedPointCount = 0;
        StatusText = "Data cleared";
        PointsChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnPointRecorded(object? sender, LocationPoint point)
    {
        // GetLocationAsync's continuation isn't guaranteed to run on the UI
        // thread, and this handler ends up touching UI-bound state (these
        // ObservableProperty setters raise PropertyChanged that the page is
        // bound to, and PointsChanged ultimately triggers
        // HeatMapCanvas.InvalidateSurface()) — so hop to the main thread
        // before doing any of that.
        MainThread.BeginInvokeOnMainThread(() =>
        {
            Points.Add(point);
            RecordedPointCount = Points.Count;
            StatusText = $"Tracking… ({RecordedPointCount} points)";

            PointsChanged?.Invoke(this, EventArgs.Empty);
            NewPointTracked?.Invoke(this, new Location(point.Latitude, point.Longitude));
        });
    }

    private void OnTrackingError(object? sender, Exception ex)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            StatusText = $"Tracking error: {ex.Message}";
        });
    }
}
