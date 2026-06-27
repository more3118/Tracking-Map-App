using HeatMapTracker.Models;
using HeatMapTracker.Services;
using HeatMapTracker.ViewModels;
using SkiaSharp;
using SkiaSharp.Views.Maui;
using SkiaSharp.Views.Maui.Controls;
using Location = Microsoft.Maui.Devices.Sensors.Location;

#if ANDROID || IOS || MACCATALYST
using Microsoft.Maui.Maps;
using Map = Microsoft.Maui.Controls.Maps.Map;
#endif

namespace HeatMapTracker.Views;

public partial class MainPage : ContentPage
{
    private readonly MainViewModel _viewModel;
    private readonly HeatMapRenderer _heatMapRenderer = new();

    // Default starting view if the device has no prior location yet.
    private static readonly Location DefaultLocation = new(37.4220, -122.0840); // Mountain View, CA

#if ANDROID || IOS || MACCATALYST
    private Map _mapView = null!;
    private SKCanvasView _heatMapCanvas = null!;
    private IDispatcherTimer? _viewportPollTimer;
    private MapSpan? _lastKnownRegion;
#else
    // Windows fallback: Microsoft.Maui.Controls.Maps has no implementation
    // for net8.0-windows (UseMauiMaps() throws on that platform — see
    // MauiProgram.cs and README.md). Tracking, SQLite persistence, and the
    // heat map *data* all still work identically on Windows; only the live
    // map rendering is swapped out for a sortable list of recorded points
    // plus a simple SkiaSharp density plot in lat/lng space (no street map
    // tiles underneath, since there's no supported native map control to
    // draw them).
    private CollectionView _pointsList = null!;
    private SKCanvasView _heatMapCanvas = null!;
#endif

    public MainPage(MainViewModel viewModel)
    {
        InitializeComponent();

        _viewModel = viewModel;
        BindingContext = _viewModel;

        BuildMapArea();

        _viewModel.PointsChanged += (_, _) =>
        {
            _heatMapCanvas.InvalidateSurface();
#if !ANDROID && !IOS && !MACCATALYST
            _pointsList.ItemsSource = _viewModel.Points.AsEnumerable().Reverse().ToList();
#endif
        };
        _viewModel.NewPointTracked += OnNewPointTracked;
    }

    /// <summary>
    /// Builds the map area entirely in code so a single page works on every
    /// target without the XAML compiler ever needing to resolve the
    /// Microsoft.Maui.Controls.Maps namespace on Windows, where that
    /// control isn't implemented.
    /// </summary>
    private void BuildMapArea()
    {
#if ANDROID || IOS || MACCATALYST
        _mapView = new Map
        {
            IsShowingUser = true,
            MapType = MapType.Street
        };

        // Transparent SkiaSharp canvas drawn directly on top of the map.
        // InputTransparent lets all touch/gesture input (pan, pinch-zoom)
        // pass straight through to the Map control underneath, so the heat
        // map is purely visual and never blocks map interaction.
        _heatMapCanvas = new SKCanvasView { InputTransparent = true };
        _heatMapCanvas.PaintSurface += OnHeatMapPaintSurface;

        MapAreaHost.Children.Add(_mapView);
        MapAreaHost.Children.Add(_heatMapCanvas);

        // IMPORTANT: Map.VisibleRegion is explicitly NOT backed by a
        // BindableProperty, and there is no reliably-firing change
        // notification for it across platforms as of .NET 8/9 (see
        // dotnet/maui#16556 and #32520) — subscribing to PropertyChanged
        // for it does not work consistently. Instead, poll VisibleRegion on
        // a short timer and only repaint when it has actually changed
        // (pan/zoom).
        _viewportPollTimer = Dispatcher.CreateTimer();
        _viewportPollTimer.Interval = TimeSpan.FromMilliseconds(200);
        _viewportPollTimer.IsRepeating = true;
        _viewportPollTimer.Tick += (_, _) => CheckForViewportChange();
        _viewportPollTimer.Start();
#else
        // Windows: stack a simple SkiaSharp scatter/heat plot (lat/lng
        // mapped to the canvas's own bounds, no street map tiles) above a
        // scrollable list of recorded points, so the user can still see and
        // verify everything SQLite is storing.
        _heatMapCanvas = new SKCanvasView { HeightRequest = 300 };
        _heatMapCanvas.PaintSurface += OnWindowsHeatMapPaintSurface;

        _pointsList = new CollectionView
        {
            ItemTemplate = new DataTemplate(() =>
            {
                var lat = new Label { FontAttributes = FontAttributes.Bold };
                var time = new Label { FontSize = 12, TextColor = Colors.Gray };

                var stack = new VerticalStackLayout
                {
                    Padding = new Thickness(12, 6),
                    Children = { lat, time }
                };

                // Using BindingContextChanged here instead of SetBinding
                // with a lambda getter: the lambda-Func overload of
                // SetBinding requires .NET MAUI 9's compiled-binding
                // generator (this project targets net8.0), and even there
                // it only supports simple property access — not the
                // formatted/interpolated text needed here. This approach
                // works identically on every MAUI version.
                stack.BindingContextChanged += (s, e) =>
                {
                    if (stack.BindingContext is LocationPoint point)
                    {
                        lat.Text = $"{point.Latitude:F5}, {point.Longitude:F5}";
                        time.Text = point.TimestampUtc.ToLocalTime().ToString("g");
                    }
                };

                return stack;
            })
        };

        var layout = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition { Height = new GridLength(300, GridUnitType.Absolute) },
                new RowDefinition { Height = GridLength.Star }
            }
        };
        layout.Add(_heatMapCanvas, 0, 0);
        layout.Add(_pointsList, 0, 1);

        MapAreaHost.Children.Add(layout);
#endif
    }

#if ANDROID || IOS || MACCATALYST
    private void CheckForViewportChange()
    {
        var current = _mapView.VisibleRegion;
        if (current is null)
            return;

        bool changed = _lastKnownRegion is null
            || Math.Abs(current.Center.Latitude - _lastKnownRegion.Center.Latitude) > 0.00001
            || Math.Abs(current.Center.Longitude - _lastKnownRegion.Center.Longitude) > 0.00001
            || Math.Abs(current.LatitudeDegrees - _lastKnownRegion.LatitudeDegrees) > 0.00001
            || Math.Abs(current.LongitudeDegrees - _lastKnownRegion.LongitudeDegrees) > 0.00001;

        if (changed)
        {
            _lastKnownRegion = current;
            _heatMapCanvas.InvalidateSurface();
        }
    }
#endif

    protected override async void OnAppearing()
    {
        base.OnAppearing();

#if ANDROID || IOS || MACCATALYST
        _viewportPollTimer?.Start();
        _mapView.MoveToRegion(MapSpan.FromCenterAndRadius(DefaultLocation, Distance.FromKilometers(2)));
#endif

        await _viewModel.LoadPointsCommand.ExecuteAsync(null);
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
#if ANDROID || IOS || MACCATALYST
        _viewportPollTimer?.Stop();
#endif
    }

    /// <summary>
    /// When tracking, re-center the map on each new fix so the user can watch
    /// the heat map build up live as they move, similar to the screenshots
    /// where the live position dot leads the recorded trail.
    /// </summary>
    private void OnNewPointTracked(object? sender, Location location)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
#if ANDROID || IOS || MACCATALYST
            _mapView.MoveToRegion(MapSpan.FromCenterAndRadius(location, Distance.FromMeters(300)));
#endif
        });
    }

#if ANDROID || IOS || MACCATALYST
    /// <summary>
    /// Repaints the heat map overlay: projects every recorded point into the
    /// map's current visible region, then hands the resulting screen-space
    /// points to HeatMapRenderer to build the gradient bitmap.
    /// </summary>
    private void OnHeatMapPaintSurface(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear(SKColors.Transparent);

        var visibleRegion = _mapView.VisibleRegion;
        if (visibleRegion is null || _viewModel.Points.Count == 0)
            return;

        int width = e.Info.Width;
        int height = e.Info.Height;

        var locations = _viewModel.Points.Select(p => new Location(p.Latitude, p.Longitude));
        var projected = MapPixelProjector.ProjectAll(locations, visibleRegion, width, height);

        if (projected.Count == 0)
            return;

        using var heatBitmap = _heatMapRenderer.Render(projected, width, height);
        canvas.DrawBitmap(heatBitmap, 0, 0);
    }
#else
    /// <summary>
    /// Windows fallback heat map: since there's no native Map control to
    /// project lat/lng against, this normalizes every recorded point's
    /// lat/lng against the min/max bounds of the recorded data itself, so
    /// the whole point cloud always fills the canvas. No street map tiles —
    /// just the density gradient — which still fully demonstrates the
    /// SQLite-backed heat map data.
    /// </summary>
    private void OnWindowsHeatMapPaintSurface(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear(SKColors.Transparent);

        var points = _viewModel.Points;
        if (points.Count == 0)
            return;

        int width = e.Info.Width;
        int height = e.Info.Height;

        double minLat = points.Min(p => p.Latitude);
        double maxLat = points.Max(p => p.Latitude);
        double minLng = points.Min(p => p.Longitude);
        double maxLng = points.Max(p => p.Longitude);

        // Guard against a degenerate single-point bounding box.
        double latSpan = Math.Max(maxLat - minLat, 0.0001);
        double lngSpan = Math.Max(maxLng - minLng, 0.0001);

        var projected = points.Select(p =>
        {
            float x = (float)((p.Longitude - minLng) / lngSpan * width);
            float y = (float)((maxLat - p.Latitude) / latSpan * height); // invert: north is y=0
            return new SKPoint(x, y);
        }).ToList();

        using var heatBitmap = _heatMapRenderer.Render(projected, width, height);
        canvas.DrawBitmap(heatBitmap, 0, 0);
    }
#endif
}
