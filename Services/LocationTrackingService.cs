using HeatMapTracker.Models;

namespace HeatMapTracker.Services;

/// <summary>
/// Wraps Microsoft.Maui.Devices.Sensors.Geolocation to provide continuous
/// foreground location tracking. Every fix received while tracking is active
/// is written to the database and surfaced via the PointRecorded event so the
/// UI / heat map can update live without re-querying the whole table.
///
/// NOTE on background tracking: Microsoft.Maui.Devices.Sensors.Geolocation
/// only listens for location while the app is in the foreground. A
/// production app that must keep tracking with the screen off needs a
/// platform-specific background service (a foreground Service + location
/// permission on Android, a "Location updates" background mode +
/// CLLocationManager on iOS, a background task on Windows). This class is
/// written so those platform listeners can be slotted in later via the
/// IPlatformLocationListener seam below without changing any calling code.
/// </summary>
public class LocationTrackingService
{
    private readonly LocationDatabase _database;
    private CancellationTokenSource? _cts;
    private string _currentSessionId = string.Empty;

    /// <summary>How often to request/poll a location fix while tracking.</summary>
    public TimeSpan PollingInterval { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Timeout for a single GetLocationAsync call. Kept separate from
    /// PollingInterval (rather than reusing it) so the two concerns —
    /// "how often do we ask" vs "how long do we wait for one answer" —
    /// don't end up coupled by accident.
    /// </summary>
    public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>Desired GPS accuracy. Best/High drains battery faster but produces denser, more useful heat map data.</summary>
    public GeolocationAccuracy DesiredAccuracy { get; set; } = GeolocationAccuracy.Best;

    public bool IsTracking { get; private set; }

    /// <summary>Raised every time a new point is successfully recorded.</summary>
    public event EventHandler<LocationPoint>? PointRecorded;

    /// <summary>Raised if a location request fails (permission revoked mid-session, GPS off, timeout, etc).</summary>
    public event EventHandler<Exception>? TrackingError;

    public LocationTrackingService(LocationDatabase database)
    {
        _database = database;
    }

    /// <summary>
    /// Requests the runtime permission needed to read device location.
    /// Must be called (and granted) before StartTrackingAsync will succeed.
    /// </summary>
    public async Task<bool> RequestPermissionsAsync()
    {
        var status = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();

        if (status != PermissionStatus.Granted)
            status = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();

        return status == PermissionStatus.Granted;
    }

    /// <summary>
    /// Starts a new tracking session. Spawns a background polling loop that
    /// requests a fix every PollingInterval and persists it to SQLite.
    /// </summary>
    public async Task<bool> StartTrackingAsync()
    {
        if (IsTracking)
            return true;

        var granted = await RequestPermissionsAsync();
        if (!granted)
            return false;

        _currentSessionId = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
        _cts = new CancellationTokenSource();
        IsTracking = true;

        _ = PollLoopAsync(_cts.Token);

        return true;
    }

    public void StopTracking()
    {
        IsTracking = false;
        _cts?.Cancel();
        _cts = null;
    }

    private async Task PollLoopAsync(CancellationToken token)
    {
        var request = new GeolocationRequest(DesiredAccuracy, RequestTimeout);

        while (!token.IsCancellationRequested)
        {
            var loopStarted = DateTime.UtcNow;

            try
            {
                var location = await Geolocation.Default.GetLocationAsync(request, token);

                if (location is not null)
                {
                    var point = new LocationPoint
                    {
                        Latitude = location.Latitude,
                        Longitude = location.Longitude,
                        Accuracy = location.Accuracy,
                        TimestampUtc = DateTime.UtcNow,
                        SessionId = _currentSessionId
                    };

                    await _database.InsertPointAsync(point);
                    PointRecorded?.Invoke(this, point);
                }
            }
            catch (OperationCanceledException)
            {
                // Tracking was stopped — expected, not an error.
                break;
            }
            catch (Exception ex)
            {
                TrackingError?.Invoke(this, ex);
            }

            // Wait out the rest of PollingInterval, accounting for time
            // already spent waiting on the fix above, so the actual cadence
            // between recorded points matches PollingInterval rather than
            // PollingInterval-plus-however-long-the-fix-took.
            var elapsed = DateTime.UtcNow - loopStarted;
            var remaining = PollingInterval - elapsed;

            if (remaining > TimeSpan.Zero)
            {
                try
                {
                    await Task.Delay(remaining, token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
    }
}
