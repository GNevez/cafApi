namespace cafApi.Services
{
    public interface IGoogleAnalyticsService
    {
        bool IsConfigured();
        Task<decimal> GetBounceRateAsync();
        Task<List<(string pagePath, long views)>> GetTopPagesAsync(int limit = 10);
        Task<List<(string productName, long views)>> GetTopViewedProductsAsync(int limit = 10);
        Task<(long users, long sessions)> GetUsersAndSessionsAsync();
        
        // Historical data (last 7 days)
        Task<List<(string pagePath, long views)>> GetTopPagesHistoricalAsync(int limit = 10);
        Task<List<(string productName, long views)>> GetTopViewedProductsHistoricalAsync(int limit = 10);
        
        // Realtime data (last 30 minutes)
        Task<List<(string pagePath, long views)>> GetTopPagesRealtimeAsync(int limit = 10);
        Task<List<(string productName, long views)>> GetTopViewedProductsRealtimeAsync(int limit = 10);
    }
}
