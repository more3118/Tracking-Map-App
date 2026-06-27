using SQLite;

namespace HeatMapTracker.Models;

/// <summary>
/// A single recorded GPS fix. Every point the device reports while tracking
/// is active gets written here. The heat map is built entirely from rows
/// in this table — no point is ever "summarized away", since the density
/// of overlapping points is exactly what produces the heat gradient.
/// </summary>
public class LocationPoint
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    /// <summary>Latitude in degrees, WGS84.</summary>
    public double Latitude { get; set; }

    /// <summary>Longitude in degrees, WGS84.</summary>
    public double Longitude { get; set; }

    /// <summary>Reported horizontal accuracy in meters, if available. Used to optionally
    /// weight/filter low-quality fixes out of the heat map.</summary>
    public double? Accuracy { get; set; }

    /// <summary>UTC timestamp the fix was captured.</summary>
    public DateTime TimestampUtc { get; set; }

    /// <summary>Groups points captured in one continuous tracking session, so a user
    /// can review/compare individual walks/runs/drives later if desired.</summary>
    public string SessionId { get; set; } = string.Empty;
}
