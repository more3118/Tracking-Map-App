using Microsoft.Maui.Controls.Maps;
using Microsoft.Maui.Maps;
using SkiaSharp;

namespace HeatMapTracker.Services;

/// <summary>
/// Converts lat/lng coordinates into pixel coordinates relative to the
/// MAUI Map control's current viewport, using simple linear interpolation
/// across the Map's VisibleRegion.
///
/// This is an approximation (a true Mercator-correct projection would
/// account for the slight non-linearity of longitude/latitude lines as you
/// move away from the equator), but over the span of a single visible map
/// viewport — at most a few tens of kilometers — the error is a few pixels
/// at most, which is invisible once points are smoothed by the heat map's
/// Gaussian falloff radius anyway.
/// </summary>
public class MapPixelProjector
{
    /// <summary>
    /// Projects a single coordinate to a pixel position within a viewport of the given size,
    /// based on the map's currently visible region (center + span).
    /// Returns null if the point falls outside the visible region (caller should skip it).
    /// </summary>
    public static SKPoint? Project(Location point, MapSpan visibleRegion, int viewportWidthPx, int viewportHeightPx)
    {
        double north = visibleRegion.Center.Latitude + visibleRegion.LatitudeDegrees / 2;
        double south = visibleRegion.Center.Latitude - visibleRegion.LatitudeDegrees / 2;
        double east = visibleRegion.Center.Longitude + visibleRegion.LongitudeDegrees / 2;
        double west = visibleRegion.Center.Longitude - visibleRegion.LongitudeDegrees / 2;

        // NOTE: this simple bounds check does not handle the antimeridian
        // (a viewport spanning longitude 180/-180), an edge case unlikely
        // to come up for typical local tracking use.
        bool withinLat = point.Latitude <= north && point.Latitude >= south;
        bool withinLng = point.Longitude >= west && point.Longitude <= east;

        if (!withinLat || !withinLng)
            return null;

        double xRatio = (point.Longitude - west) / (east - west);
        double yRatio = (north - point.Latitude) / (north - south); // inverted: north is y=0

        float x = (float)(xRatio * viewportWidthPx);
        float y = (float)(yRatio * viewportHeightPx);

        return new SKPoint(x, y);
    }

    /// <summary>
    /// Projects a whole batch of points at once, silently dropping any that
    /// fall outside the current viewport (the heat map only needs to render
    /// what's actually visible).
    /// </summary>
    public static List<SKPoint> ProjectAll(IEnumerable<Location> points, MapSpan visibleRegion, int viewportWidthPx, int viewportHeightPx)
    {
        var result = new List<SKPoint>();

        foreach (var point in points)
        {
            var projected = Project(point, visibleRegion, viewportWidthPx, viewportHeightPx);
            if (projected.HasValue)
                result.Add(projected.Value);
        }

        return result;
    }
}
