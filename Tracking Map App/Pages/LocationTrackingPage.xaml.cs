using Tracking_Map_App.Controls;
using System.Text.Json;
using Tracking_Map_App.Models;
using Tracking_Map_App.PageModels;

namespace Tracking_Map_App.Pages
{
    public partial class LocationTrackingPage : ContentPage
    {
        private static readonly string DefaultLocationsJson = "[]";

        private readonly LocationTrackingPageModel _viewModel;
        private string? _mapTemplate;
        private bool _mapReady;
        private string? _pendingLocationsJson;

        public LocationTrackingPage(LocationTrackingPageModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            BindingContext = _viewModel;

            _viewModel.LocationsChanged += OnLocationsChanged;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await _viewModel.InitializeAsync();
            await EnsureMapLoadedAsync();
            await RenderMapAsync();
        }

        private void OnLocationsChanged(object? sender, EventArgs e)
        {
            MainThread.BeginInvokeOnMainThread(() => _ = RenderMapAsync());
        }

        private async void MapWebView_Navigated(object? sender, WebNavigatedEventArgs e)
        {
            if (e.Result != WebNavigationResult.Success)
            {
                return;
            }

            _mapReady = true;

            if (!string.IsNullOrWhiteSpace(_pendingLocationsJson))
            {
                var pending = _pendingLocationsJson;
                _pendingLocationsJson = null;
                await MapWebView.EvaluateJavaScriptAsync($"window.renderLocations({pending});");
                return;
            }

            await RenderMapAsync();
        }

        private async Task EnsureMapLoadedAsync()
        {
            if (_mapTemplate != null)
            {
                return;
            }

            await using var stream = await FileSystem.OpenAppPackageFileAsync("TrackingMap.html");
            using var reader = new StreamReader(stream);

            _mapTemplate = await reader.ReadToEndAsync();
            _mapTemplate = _mapTemplate.Replace("__MAPS_API_KEY__", Constants.GoogleMapsApiKey);

            MapWebView.Source = new HtmlWebViewSource
            {
                Html = _mapTemplate
            };
        }

        private async Task RenderMapAsync()
        {
            if (_mapTemplate == null)
            {
                await EnsureMapLoadedAsync();
            }

            var locations = _viewModel.Locations
                .TakeLast(150)
                .Select(location => new
                {
                    latitude = location.Latitude,
                    longitude = location.Longitude,
                    title = location.Timestamp.ToLocalTime().ToString("HH:mm:ss")
                });

            var locationsJson = JsonSerializer.Serialize(locations);

            if (!_mapReady)
            {
                _pendingLocationsJson = locationsJson;
                return;
            }

            _pendingLocationsJson = null;
            await MapWebView.EvaluateJavaScriptAsync($"window.renderLocations({locationsJson});");
        }
    }
}
