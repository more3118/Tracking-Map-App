using SQLite;
using Tracking_Map_App.Models;

namespace Tracking_Map_App.Services
{
    public class LocationDatabaseService
    {
        private readonly string _dbPath;
        private SQLiteAsyncConnection? _connection;

        public LocationDatabaseService()
        {
            _dbPath = Path.Combine(FileSystem.AppDataDirectory, "locations.db3");
        }

        public async Task InitializeTableAsync()
        {
            _connection ??= new SQLiteAsyncConnection(_dbPath);
            await _connection.CreateTableAsync<LocationData>();
        }

        public async Task<int> SaveLocationAsync(LocationData location)
        {
            if (location == null)
                throw new ArgumentNullException(nameof(location));

            await InitializeTableAsync();
            return location.Id == 0
                ? await _connection!.InsertAsync(location)
                : await _connection!.UpdateAsync(location);
        }

        public async Task<List<LocationData>> GetAllLocationsAsync()
        {
            await InitializeTableAsync();
            return await _connection!.Table<LocationData>()
                .OrderBy(l => l.Timestamp)
                .ToListAsync();
        }

        public async Task<List<LocationData>> GetLocationsInRangeAsync(
            double minLatitude, double maxLatitude, double minLongitude, double maxLongitude)
        {
            await InitializeTableAsync();
            return await _connection!.Table<LocationData>()
                .Where(l =>
                    l.Latitude >= minLatitude && l.Latitude <= maxLatitude &&
                    l.Longitude >= minLongitude && l.Longitude <= maxLongitude)
                .OrderBy(l => l.Timestamp)
                .ToListAsync();
        }

        public async Task<List<LocationData>> GetLocationsFromLastAsync(TimeSpan timeSpan)
        {
            var cutoffTime = DateTime.UtcNow.Subtract(timeSpan);
            await InitializeTableAsync();
            return await _connection!.Table<LocationData>()
                .Where(l => l.Timestamp >= cutoffTime)
                .OrderBy(l => l.Timestamp)
                .ToListAsync();
        }

        public async Task<int> DeleteLocationAsync(int id)
        {
            await InitializeTableAsync();
            return await _connection!.DeleteAsync<LocationData>(id);
        }

        public async Task<int> ClearAllLocationsAsync()
        {
            await InitializeTableAsync();
            return await _connection!.DeleteAllAsync<LocationData>();
        }

        public async Task<LocationData?> GetLastLocationAsync()
        {
            await InitializeTableAsync();
            return await _connection!.Table<LocationData>()
                .OrderByDescending(l => l.Timestamp)
                .FirstOrDefaultAsync();
        }

        public async Task<int> GetLocationCountAsync()
        {
            await InitializeTableAsync();
            return await _connection!.Table<LocationData>().CountAsync();
        }
    }
}
