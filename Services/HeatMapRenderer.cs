using HeatMapTracker.Models;
using SkiaSharp;

namespace HeatMapTracker.Services;

/// <summary>
/// Renders a set of GPS points as a true density heat map (soft color-gradient
/// blobs, like Google's heatmaps layer) onto an SkiaSharp canvas.
///
/// Algorithm:
///  1. Project every lat/lng point to a pixel coordinate on the visible map
///     region (the caller supplies this projection — see MapPixelProjector).
///  2. Accumulate a scalar "heat" value into an off-screen intensity buffer
///     using a Gaussian-like falloff around each point (so points add up
///     where they overlap — this is what makes dense areas glow brighter).
///  3. Normalize the buffer to 0..1 and map each value through a color ramp
///     (transparent -> blue -> cyan -> green -> yellow -> red), matching the
///     classic heat map look.
///  4. Blit the resulting bitmap onto the canvas at the map's zoom/pan state.
///
/// This is intentionally done in plain SkiaSharp (no platform-specific
/// rendering) so the exact same code produces the heat map on Android, iOS,
/// and Windows.
/// </summary>
public class HeatMapRenderer
{
    /// <summary>Pixel radius of influence each point spreads heat over. Larger = smoother/blobbier.</summary>
    public float Radius { get; set; } = 40f;

    /// <summary>Overall opacity multiplier for the whole heat layer (0..1).</summary>
    public float Opacity { get; set; } = 0.75f;

    /// <summary>
    /// Builds the heat map as an SKBitmap the same pixel size as the map viewport.
    /// projectedPoints are already-converted screen-space (x,y) coordinates —
    /// points outside the visible viewport should be filtered out by the caller
    /// before calling this for performance.
    /// </summary>
    public SKBitmap Render(IReadOnlyList<SKPoint> projectedPoints, int width, int height)
    {
        var info = new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);
        var bitmap = new SKBitmap(info);

        if (projectedPoints.Count == 0 || width <= 0 || height <= 0)
            return bitmap;

        // Step 1: accumulate intensity into a float buffer.
        var intensity = new float[width * height];
        AccumulateIntensity(projectedPoints, intensity, width, height);

        // Step 2: find max for normalization.
        float max = 0f;
        for (int i = 0; i < intensity.Length; i++)
            if (intensity[i] > max) max = intensity[i];

        if (max <= 0f)
            return bitmap;

        // Step 3: paint normalized intensity through the color ramp directly
        // into the bitmap's own pixel buffer (bitmap.GetPixels() gives a
        // pointer to memory SKBitmap already owns and will free when
        // disposed — far safer than pinning a separate managed array and
        // calling InstallPixels, which hands ownership semantics around in
        // a way that's easy to get wrong).
        IntPtr pixelsPtr = bitmap.GetPixels();

        unsafe
        {
            uint* dst = (uint*)pixelsPtr;

            for (int i = 0; i < intensity.Length; i++)
            {
                float normalized = Math.Clamp(intensity[i] / max, 0f, 1f);
                SKColor color = normalized <= 0.02f
                    ? SKColors.Transparent
                    : ColorRamp(normalized, Opacity);

                // Rgba8888 + Premul: pack as premultiplied RGBA in memory order.
                byte a = color.Alpha;
                byte r = (byte)(color.Red * a / 255);
                byte g = (byte)(color.Green * a / 255);
                byte b = (byte)(color.Blue * a / 255);

                dst[i] = (uint)(a << 24 | b << 16 | g << 8 | r);
            }
        }

        return bitmap;
    }

    /// <summary>
    /// Splats a soft circular falloff around every point into the shared
    /// intensity buffer. Using a simple inverse-square-ish falloff inside
    /// Radius keeps this fast enough to run on every map redraw even with
    /// a few thousand points, without needing a full Gaussian convolution.
    /// </summary>
    private void AccumulateIntensity(IReadOnlyList<SKPoint> points, float[] buffer, int width, int height)
    {
        float radiusSq = Radius * Radius;

        foreach (var p in points)
        {
            int minX = Math.Max(0, (int)(p.X - Radius));
            int maxX = Math.Min(width - 1, (int)(p.X + Radius));
            int minY = Math.Max(0, (int)(p.Y - Radius));
            int maxY = Math.Min(height - 1, (int)(p.Y + Radius));

            for (int y = minY; y <= maxY; y++)
            {
                float dy = y - p.Y;
                int rowOffset = y * width;

                for (int x = minX; x <= maxX; x++)
                {
                    float dx = x - p.X;
                    float distSq = dx * dx + dy * dy;

                    if (distSq > radiusSq)
                        continue;

                    // Smooth falloff: 1 at the point center, 0 at the radius edge.
                    float t = 1f - (distSq / radiusSq);
                    buffer[rowOffset + x] += t * t; // squared for a softer, more "glowy" center
                }
            }
        }
    }

    /// <summary>
    /// Classic heat map color ramp: transparent -> blue -> cyan -> green -> yellow -> red.
    /// </summary>
    private static SKColor ColorRamp(float t, float opacity)
    {
        // Stops, evenly spaced 0..1
        (float pos, byte r, byte g, byte b)[] stops =
        {
            (0.00f,   0,   0, 255), // blue
            (0.25f,   0, 255, 255), // cyan
            (0.50f,   0, 255,   0), // green
            (0.75f, 255, 255,   0), // yellow
            (1.00f, 255,   0,   0), // red
        };

        for (int i = 0; i < stops.Length - 1; i++)
        {
            var (p0, r0, g0, b0) = stops[i];
            var (p1, r1, g1, b1) = stops[i + 1];

            if (t >= p0 && t <= p1)
            {
                float localT = (p1 - p0) <= 0 ? 0 : (t - p0) / (p1 - p0);
                byte r = (byte)(r0 + (r1 - r0) * localT);
                byte g = (byte)(g0 + (g1 - g0) * localT);
                byte b = (byte)(b0 + (b1 - b0) * localT);

                // Alpha ramps up with intensity too, so low-density edges fade
                // out softly instead of having a hard color boundary.
                byte a = (byte)(Math.Clamp(t * 1.4f, 0f, 1f) * 255 * opacity);
                return new SKColor(r, g, b, a);
            }
        }

        return new SKColor(255, 0, 0, (byte)(255 * opacity));
    }
}
