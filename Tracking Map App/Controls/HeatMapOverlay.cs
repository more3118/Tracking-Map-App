using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using Tracking_Map_App.Models;

namespace Tracking_Map_App.Controls
{
    /// <summary>
    /// Enhanced heat map overlay with map-like features
    /// </summary>
    public class HeatMapOverlay : GraphicsView
    {
        private List<LocationData> _locations = new();
        private const int GridSize = 15;
        private double _minLat = double.MaxValue;
        private double _maxLat = double.MinValue;
        private double _minLon = double.MaxValue;
        private double _maxLon = double.MinValue;

        public static readonly BindableProperty LocationsProperty =
            BindableProperty.Create(
                nameof(Locations),
                typeof(IEnumerable<LocationData>),
                typeof(HeatMapOverlay),
                null,
                BindingMode.OneWay,
                propertyChanged: OnLocationsChanged);

        public IEnumerable<LocationData>? Locations
        {
            get => (IEnumerable<LocationData>?)GetValue(LocationsProperty);
            set => SetValue(LocationsProperty, value);
        }

        public HeatMapOverlay()
        {
            Drawable = new HeatMapDrawable(this);
            BackgroundColor = Colors.White;
        }

        private static void OnLocationsChanged(BindableObject bindable, object? oldValue, object? newValue)
        {
            var overlay = (HeatMapOverlay)bindable;
            if (newValue != null && newValue is IEnumerable<LocationData> locations)
            {
                overlay._locations = locations.ToList();
            }
            overlay.InvalidateSurface();
        }

        private void InvalidateSurface()
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                ((HeatMapDrawable?)Drawable)?.InvalidateSurface();
            });
        }

        private class HeatMapDrawable : IDrawable
        {
            private readonly HeatMapOverlay _parent;

            public HeatMapDrawable(HeatMapOverlay parent)
            {
                _parent = parent;
            }

            public void InvalidateSurface() => _parent.Invalidate();

            public void Draw(ICanvas canvas, RectF dirtyRect)
            {
                // Draw map background with grid pattern
                DrawMapBackground(canvas, dirtyRect);

                if (_parent._locations == null || _parent._locations.Count == 0)
                {
                    DrawPlaceholder(canvas, dirtyRect);
                    return;
                }

                CalculateBounds();
                DrawHeatMap(canvas, dirtyRect);
                DrawLegend(canvas, dirtyRect);
            }

            private void DrawMapBackground(ICanvas canvas, RectF dirtyRect)
            {
                // Draw light background
                canvas.FillColor = new Color(240, 245, 250);
                canvas.FillRectangle(dirtyRect);

                // Draw subtle grid pattern to simulate map tiles
                canvas.StrokeColor = new Color(200, 210, 220);
                canvas.StrokeSize = 0.5f;

                for (float x = 0; x < dirtyRect.Width; x += 50)
                {
                    canvas.DrawLine(x, 0, x, dirtyRect.Height);
                }

                for (float y = 0; y < dirtyRect.Height; y += 50)
                {
                    canvas.DrawLine(0, y, dirtyRect.Width, y);
                }
            }

            private void DrawPlaceholder(ICanvas canvas, RectF dirtyRect)
            {
                canvas.FontColor = Colors.Gray;
                canvas.FontSize = 16;
                canvas.DrawString("No location data available",
                    dirtyRect.Center.X, dirtyRect.Center.Y, HorizontalAlignment.Center);
            }

            private void CalculateBounds()
            {
                if (_parent._locations.Count == 0)
                    return;

                _parent._minLat = _parent._locations.Min(l => l.Latitude);
                _parent._maxLat = _parent._locations.Max(l => l.Latitude);
                _parent._minLon = _parent._locations.Min(l => l.Longitude);
                _parent._maxLon = _parent._locations.Max(l => l.Longitude);

                var latPadding = (_parent._maxLat - _parent._minLat) * 0.1;
                var lonPadding = (_parent._maxLon - _parent._minLon) * 0.1;

                if (latPadding == 0) latPadding = 0.01;
                if (lonPadding == 0) lonPadding = 0.01;

                _parent._minLat -= latPadding;
                _parent._maxLat += latPadding;
                _parent._minLon -= lonPadding;
                _parent._maxLon += lonPadding;
            }

            private void DrawHeatMap(ICanvas canvas, RectF dirtyRect)
            {
                var cellSize = GridSize;
                var horizontalCells = (int)(dirtyRect.Width / cellSize) + 1;
                var verticalCells = (int)(dirtyRect.Height / cellSize) + 1;

                if (horizontalCells <= 0 || verticalCells <= 0)
                    return;

                var heatGrid = new float[horizontalCells, verticalCells];

                foreach (var location in _parent._locations)
                {
                    var lonRange = _parent._maxLon - _parent._minLon;
                    var latRange = _parent._maxLat - _parent._minLat;

                    if (lonRange == 0 || latRange == 0)
                        continue;

                    var x = (int)((location.Longitude - _parent._minLon) / lonRange * dirtyRect.Width / cellSize);
                    var y = (int)((location.Latitude - _parent._minLat) / latRange * dirtyRect.Height / cellSize);

                    if (x >= 0 && x < horizontalCells && y >= 0 && y < verticalCells)
                    {
                        heatGrid[x, y] += 1.0f;
                    }

                    // Spread intensity to neighboring cells
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        for (int dy = -1; dy <= 1; dy++)
                        {
                            var nx = x + dx;
                            var ny = y + dy;
                            if (nx >= 0 && nx < horizontalCells && ny >= 0 && ny < verticalCells)
                            {
                                heatGrid[nx, ny] += 0.25f;
                            }
                        }
                    }
                }

                float maxIntensity = 0;
                for (int x = 0; x < horizontalCells; x++)
                {
                    for (int y = 0; y < verticalCells; y++)
                    {
                        if (heatGrid[x, y] > maxIntensity)
                            maxIntensity = heatGrid[x, y];
                    }
                }

                if (maxIntensity == 0)
                    maxIntensity = 1;

                // Draw heat cells
                for (int x = 0; x < horizontalCells; x++)
                {
                    for (int y = 0; y < verticalCells; y++)
                    {
                        var intensity = heatGrid[x, y] / maxIntensity;
                        if (intensity > 0.01f)
                        {
                            var color = IntensityToColor(intensity);
                            canvas.FillColor = color;
                            canvas.FillRectangle(
                                x * cellSize, y * cellSize, cellSize, cellSize);
                        }
                    }
                }

                // Draw location points with enhanced styling
                DrawLocationPoints(canvas, dirtyRect);
            }

            private void DrawLocationPoints(ICanvas canvas, RectF dirtyRect)
            {
                var lonRange = _parent._maxLon - _parent._minLon;
                var latRange = _parent._maxLat - _parent._minLat;

                if (lonRange == 0 || latRange == 0)
                    return;

                // Draw trails connecting points
                canvas.StrokeColor = new Color(0, 0, 255, 0.3f);
                canvas.StrokeSize = 1;

                for (int i = 1; i < _parent._locations.Count; i++)
                {
                    var prev = _parent._locations[i - 1];
                    var curr = _parent._locations[i];

                    var px1 = (prev.Longitude - _parent._minLon) / lonRange * dirtyRect.Width;
                    var py1 = (prev.Latitude - _parent._minLat) / latRange * dirtyRect.Height;
                    var px2 = (curr.Longitude - _parent._minLon) / lonRange * dirtyRect.Width;
                    var py2 = (curr.Latitude - _parent._minLat) / latRange * dirtyRect.Height;

                    canvas.DrawLine((float)px1, (float)py1, (float)px2, (float)py2);
                }

                // Draw individual points
                canvas.FillColor = Colors.DarkBlue;
                canvas.StrokeColor = Colors.Blue;
                canvas.StrokeSize = 2;

                foreach (var location in _parent._locations)
                {
                    var px = (location.Longitude - _parent._minLon) / lonRange * dirtyRect.Width;
                    var py = (location.Latitude - _parent._minLat) / latRange * dirtyRect.Height;

                    canvas.FillCircle((float)px, (float)py, 4);
                }
            }

            private void DrawLegend(ICanvas canvas, RectF dirtyRect)
            {
                var legendX = dirtyRect.Width - 200;
                var legendY = 20;
                var legendWidth = 180;
                var legendHeight = 140;

                // Background
                canvas.FillColor = new Color(255, 255, 255, 0.9f);
                canvas.FillRectangle(legendX, legendY, legendWidth, legendHeight);

                // Border
                canvas.StrokeColor = Colors.Gray;
                canvas.StrokeSize = 1;
                canvas.DrawRectangle(legendX, legendY, legendWidth, legendHeight);

                // Title
                canvas.FontColor = Colors.Black;
                canvas.FontSize = 12;
                canvas.DrawString("Heat Map Legend", legendX + 10, legendY + 5, HorizontalAlignment.Left);

                // Legend items
                var items = new[]
                {
                    ("Low (0-25%)", 0.1f),
                    ("Medium (25-50%)", 0.375f),
                    ("High (50-75%)", 0.625f),
                    ("Very High (75%+)", 0.9f)
                };

                var itemY = legendY + 25;
                foreach (var (label, intensity) in items)
                {
                    var color = IntensityToColor(intensity);
                    canvas.FillColor = color;
                    canvas.FillRectangle(legendX + 10, itemY, 15, 15);

                    canvas.FontColor = Colors.Black;
                    canvas.FontSize = 10;
                    canvas.DrawString(label, legendX + 30, itemY + 2, HorizontalAlignment.Left);

                    itemY += 20;
                }
            }

            private Color IntensityToColor(float intensity)
            {
                // Color gradient: Blue -> Cyan -> Green -> Yellow -> Red
                if (intensity < 0.25f)
                {
                    var t = intensity / 0.25f;
                    return new Color(0, (int)(255 * t), 255);
                }
                else if (intensity < 0.5f)
                {
                    var t = (intensity - 0.25f) / 0.25f;
                    return new Color(0, 255, (int)(255 * (1 - t)));
                }
                else if (intensity < 0.75f)
                {
                    var t = (intensity - 0.5f) / 0.25f;
                    return new Color((int)(255 * t), 255, 0);
                }
                else
                {
                    var t = (intensity - 0.75f) / 0.25f;
                    return new Color(255, (int)(255 * (1 - t)), 0);
                }
            }
        }
    }
}
