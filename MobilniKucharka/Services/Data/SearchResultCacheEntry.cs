using SQLite;

namespace MobilniKucharka.Services.Data
{
    public class SearchResultCacheEntry
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        public string QueryKey { get; set; } = string.Empty;
        public string ResultsJson { get; set; } = string.Empty;
        public DateTime CachedAtUtc { get; set; }
    }
}