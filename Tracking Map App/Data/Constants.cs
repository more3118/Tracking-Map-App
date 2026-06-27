namespace Tracking_Map_App.Data
{
    public static class Constants
    {
        public const string DatabaseFilename = "AppSQLite.db3";
        public const string GoogleMapsApiKey = "AIzaSyB5I8sC3zy_seEpQptigbPpsBQm3e1Xu78";

        public static string DatabasePath =>
            $"Data Source={Path.Combine(FileSystem.AppDataDirectory, DatabaseFilename)}";
    }
}