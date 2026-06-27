using HeatMapTracker.Models;
using SQLite;

namespace HeatMapTracker.Services;

/// <summary>
/// Thin wrapper around sqlite-net-pcl. Owns the single SQLiteAsyncConnection
/// for the app's lifetime and exposes simple CRUD methods used by the
/// tracking service and the map/heat-map view model.
/// </summary>
public class LocationDatabase
{
    private SQLiteAsyncConnection? _connection;
    private readonly string _dbPath;

    public LocationDatabase()
    {
        _dbPath = Path.Combine(FileSystem.AppDataDirectory, "locations.db3");
    }

    /// <summary>
    /// Lazily creates the connection and table. Safe to call repeatedly;
    /// CreateTableAsync is a no-op if the table already exists.
    /// </summary>
    private async Task<SQLiteAsyncConnection> GetConnectionAsync()
    {
        if (_connection is not null)
            return _connection;

        _connection = new SQLiteAsyncConnection(_dbPath);
        await _connection.CreateTableAsync<LocationPoint>();
        return _connection;
    }

    public async Task<int> InsertPointAsync(LocationPoint point)
    {
        var conn = await GetConnectionAsync();
        return await conn.InsertAsync(point);
    }

    /// <summary>Returns every recorded point, oldest first. Used to render the full heat map.</summary>
    public async Task<List<LocationPoint>> GetAllPointsAsync()
    {
        var conn = await GetConnectionAsync();
        return await conn.Table<LocationPoint>()
                          .OrderBy(p => p.TimestampUtc)
                          .ToListAsync();
    }

    /// <summary>Returns only the points from one tracking session.</summary>
    public async Task<List<LocationPoint>> GetPointsForSessionAsync(string sessionId)
    {
        var conn = await GetConnectionAsync();
        return await conn.Table<LocationPoint>()
                          .Where(p => p.SessionId == sessionId)
                          .OrderBy(p => p.TimestampUtc)
                          .ToListAsync();
    }

    public async Task<List<string>> GetSessionIdsAsync()
    {
        var conn = await GetConnectionAsync();
        var all = await conn.Table<LocationPoint>().ToListAsync();
        return all.Select(p => p.SessionId)
                   .Distinct()
                   .OrderDescending()
                   .ToList();
    }

    public async Task<int> GetPointCountAsync()
    {
        var conn = await GetConnectionAsync();
        return await conn.Table<LocationPoint>().CountAsync();
    }

    /// <summary>Wipes every recorded point. Exposed for a "Clear data" button during testing/grading.</summary>
    public async Task DeleteAllAsync()
    {
        var conn = await GetConnectionAsync();
        await conn.DeleteAllAsync<LocationPoint>();
    }
}
