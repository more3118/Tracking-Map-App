using Tracking_Map_App.Models;

namespace Tracking_Map_App.Services
{
    public class LocationTrackingService
    {
        private readonly LocationDatabaseService _databaseService;
        private CancellationTokenSource? _cancellationTokenSource;
        private bool _isTracking = false;

        public event EventHandler<LocationData>? LocationUpdated;
        public event EventHandler<string>? TrackingStatusChanged;

        public bool IsTracking => _isTracking;

        public LocationTrackingService(LocationDatabaseService databaseService)
        {
            _databaseService = databaseService ?? throw new ArgumentNullException(nameof(databaseService));
        }

        public async Task<bool> RequestLocationPermissionsAsync()
        {
            try
            {
                var status = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();
                if (status != PermissionStatus.Granted)
                {
                    status = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
                }

                return status == PermissionStatus.Granted;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Permission error: {ex.Message}");
                return false;
            }
        }

        public async Task StartTrackingAsync(int updateIntervalMs = 5000)
        {
            if (_isTracking)
                return;

            var hasPermission = await RequestLocationPermissionsAsync();
            if (!hasPermission)
            {
                TrackingStatusChanged?.Invoke(this, "Location permission denied");
                return;
            }

            _isTracking = true;
            _cancellationTokenSource = new CancellationTokenSource();
            TrackingStatusChanged?.Invoke(this, "Tracking started");

            _ = TrackingLoop(updateIntervalMs, _cancellationTokenSource.Token);
        }

        public async Task StopTrackingAsync()
        {
            _isTracking = false;
            _cancellationTokenSource?.Cancel();
            TrackingStatusChanged?.Invoke(this, "Tracking stopped");
            await Task.CompletedTask;
        }

        private async Task TrackingLoop(int updateIntervalMs, CancellationToken cancellationToken)
        {
            while (_isTracking && !cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var location = await GetCurrentLocationAsync();
                    if (location != null)
                    {
                        await _databaseService.SaveLocationAsync(location);
                        LocationUpdated?.Invoke(this, location);
                    }

                    await Task.Delay(updateIntervalMs, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Tracking error: {ex.Message}");
                }
            }
        }

        private async Task<LocationData?> GetCurrentLocationAsync()
        {
            try
            {
                // Check if geolocation is supported
                var isSupported = DeviceInfo.Current.Platform == DevicePlatform.iOS ||
                                  DeviceInfo.Current.Platform == DevicePlatform.Android ||
                                  DeviceInfo.Current.Platform == DevicePlatform.WinUI;

                if (!isSupported)
                {
                    TrackingStatusChanged?.Invoke(this, "Location not supported on this platform");
                    return null;
                }

                var request = new GeolocationRequest(GeolocationAccuracy.Best, TimeSpan.FromSeconds(10));
                var location = await Geolocation.Default.GetLocationAsync(request);

                if (location != null)
                {
                    return new LocationData
                    {
                        Latitude = location.Latitude,
                        Longitude = location.Longitude,
                        Timestamp = DateTime.UtcNow,
                        Accuracy = location.Accuracy ?? 0,
                        Altitude = location.Altitude ?? 0,
                        Speed = location.Speed ?? 0
                    };
                }
            }
            catch (FeatureNotSupportedException)
            {
                TrackingStatusChanged?.Invoke(this, "Location not supported");
            }
            catch (FeatureNotEnabledException)
            {
                TrackingStatusChanged?.Invoke(this, "Location service disabled");
            }
            catch (PermissionException)
            {
                TrackingStatusChanged?.Invoke(this, "Location permission denied");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Location error: {ex.Message}");
            }

            return null;
        }
    }
}
