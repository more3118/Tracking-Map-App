using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Tracking_Map_App.Models;
using Tracking_Map_App.Services;

namespace Tracking_Map_App.PageModels
{
    public partial class LocationTrackingPageModel : ObservableObject
    {
        private readonly LocationTrackingService _trackingService;
        private readonly LocationDatabaseService _databaseService;

        public event EventHandler? LocationsChanged;
        public event EventHandler<LocationData>? LocationRecorded;

        [ObservableProperty]
        private bool isTracking;

        [ObservableProperty]
        private string statusMessage = "Ready to track";

        [ObservableProperty]
        private string trackButtonText = "Start Tracking";

        [ObservableProperty]
        private int locationCount;

        [ObservableProperty]
        private LocationData? lastLocation;

        [ObservableProperty]
        private List<LocationData> locations = new();

        public LocationTrackingPageModel(LocationTrackingService trackingService, LocationDatabaseService databaseService)
        {
            _databaseService = databaseService;
            _trackingService = trackingService;

            _trackingService.TrackingStatusChanged += OnTrackingStatusChanged;
            _trackingService.LocationUpdated += OnLocationUpdated;
        }

        public async Task InitializeAsync()
        {
            await _databaseService.InitializeTableAsync();
            await RefreshDataAsync();
        }

        [RelayCommand]
        private async Task ToggleTracking()
        {
            if (IsTracking)
            {
                await _trackingService.StopTrackingAsync();
                IsTracking = false;
                TrackButtonText = "Start Tracking";
                StatusMessage = "Tracking stopped";
            }
            else
            {
                await _trackingService.StartTrackingAsync();
                IsTracking = _trackingService.IsTracking;
                TrackButtonText = IsTracking ? "Stop Tracking" : "Start Tracking";
                if (!IsTracking)
                    StatusMessage = "Unable to start tracking";
            }
        }

        [RelayCommand]
        private async Task ClearData()
        {
            var mainPage = Application.Current?.Windows[0].Page;
            if (mainPage != null)
            {
                var result = await mainPage.DisplayAlert(
                    "Confirm", "Clear all location data?", "Yes", "No");

                if (result)
                {
                    await _databaseService.ClearAllLocationsAsync();
                    await RefreshDataAsync();
                    StatusMessage = "Data cleared";
                }
            }
        }

        [RelayCommand]
        private async Task RefreshDataAsync()
        {
            var loadedLocations = await _databaseService.GetAllLocationsAsync();
            MainThread.BeginInvokeOnMainThread(() =>
            {
                Locations = loadedLocations.ToList();
                LocationCount = loadedLocations.Count;
                LastLocation = loadedLocations.LastOrDefault();
                StatusMessage = $"Loaded {LocationCount} locations";
                LocationsChanged?.Invoke(this, EventArgs.Empty);
            });
        }

        private void OnTrackingStatusChanged(object? sender, string status)
        {
            MainThread.BeginInvokeOnMainThread(() => StatusMessage = status);
        }

        private void OnLocationUpdated(object? sender, LocationData location)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                Locations.Add(location);
                LocationCount = Locations.Count;
                LastLocation = location;
                StatusMessage = $"Updated: {location.Latitude:F4}, {location.Longitude:F4}";
                LocationsChanged?.Invoke(this, EventArgs.Empty);
                LocationRecorded?.Invoke(this, location);
            });
        }
    }
}
