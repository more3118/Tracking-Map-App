using System.Globalization;

namespace HeatMapTracker.Views;

/// <summary>Flips the tracking button's label between "Start Tracking" and "Stop Tracking".</summary>
public class TrackingButtonTextConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        bool isTracking = value is bool b && b;
        return isTracking ? "Stop Tracking" : "Start Tracking";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
