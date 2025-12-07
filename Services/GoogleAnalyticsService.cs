using Google.Analytics.Data.V1Beta;
using Google.Api.Gax;
using Microsoft.Extensions.Configuration;

namespace cafApi.Services;

public class GoogleAnalyticsService : IGoogleAnalyticsService
{
    private readonly IConfiguration _configuration;
    private readonly string _propertyId;
    private BetaAnalyticsDataClient? _client;

    public GoogleAnalyticsService(IConfiguration configuration)
    {
        _configuration = configuration;
        var rawPropertyId = _configuration["GoogleAnalytics:PropertyId"] ?? "";
        
        // Parse PropertyId - accept both "properties/123456" and "123456"
        if (!string.IsNullOrEmpty(rawPropertyId))
        {
            _propertyId = rawPropertyId.StartsWith("properties/") ? rawPropertyId : $"properties/{rawPropertyId}";
        }
        else
        {
            _propertyId = "";
        }
        
        var credentialsPath = _configuration["GoogleAnalytics:CredentialsPath"];
        Console.WriteLine($"[GoogleAnalytics] PropertyId: {_propertyId}");
        Console.WriteLine($"[GoogleAnalytics] CredentialsPath (raw): {credentialsPath}");
        
        if (!string.IsNullOrEmpty(credentialsPath))
        {
            if (!Path.IsPathRooted(credentialsPath))
            {
                credentialsPath = Path.Combine(AppContext.BaseDirectory, credentialsPath);
            }
            
            Console.WriteLine($"[GoogleAnalytics] CredentialsPath (resolved): {credentialsPath}");
            Console.WriteLine($"[GoogleAnalytics] File exists: {File.Exists(credentialsPath)}");
            
            if (File.Exists(credentialsPath))
            {
                Environment.SetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS", credentialsPath);
                try
                {
                    _client = BetaAnalyticsDataClient.Create();
                    Console.WriteLine("[GoogleAnalytics] Client created successfully");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[GoogleAnalytics] Error creating client: {ex.Message}");
                }
            }
            else
            {
                Console.WriteLine($"[GoogleAnalytics] Credentials file not found at: {credentialsPath}");
            }
        }
        else
        {
            Console.WriteLine("[GoogleAnalytics] CredentialsPath is empty");
        }
        
        Console.WriteLine($"[GoogleAnalytics] IsConfigured: {IsConfigured()}");
    }

    public bool IsConfigured() => _client != null && !string.IsNullOrEmpty(_propertyId);

    public async Task<decimal> GetBounceRateAsync()
    {
        Console.WriteLine("[GoogleAnalytics] GetBounceRateAsync called");
        if (!IsConfigured())
        {
            Console.WriteLine("[GoogleAnalytics] Not configured, returning 0");
            return 0m;
        }

        try
        {
            var request = new RunReportRequest
            {
                Property = _propertyId,
                DateRanges = { new DateRange { StartDate = "30daysAgo", EndDate = "today" } },
                Metrics = { new Metric { Name = "bounceRate" } }
            };

            Console.WriteLine($"[GoogleAnalytics] Sending request to property: {_propertyId}");
            var response = await _client!.RunReportAsync(request);
            Console.WriteLine($"[GoogleAnalytics] Response received. Rows: {response.Rows.Count}");
            
            if (response.Rows.Count > 0)
            {
                var bounceRate = double.Parse(response.Rows[0].MetricValues[0].Value);
                Console.WriteLine($"[GoogleAnalytics] Bounce rate: {bounceRate}");
                return (decimal)(bounceRate * 100); 
            }
            else
            {
                Console.WriteLine("[GoogleAnalytics] No rows in response");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[GoogleAnalytics] Error fetching bounce rate: {ex.Message}");
            Console.WriteLine($"[GoogleAnalytics] Stack trace: {ex.StackTrace}");
        }

        return 0m;
    }

    public async Task<List<(string pagePath, long views)>> GetTopPagesAsync(int limit = 10)
    {
        Console.WriteLine("[GoogleAnalytics] GetTopPagesAsync called");
        if (!IsConfigured())
        {
            Console.WriteLine("[GoogleAnalytics] Not configured, returning empty list");
            return new List<(string, long)>();
        }

        try
        {
            // Try historical data first (last 7 days for faster processing)
            var request = new RunReportRequest
            {
                Property = _propertyId,
                DateRanges = { new DateRange { StartDate = "7daysAgo", EndDate = "today" } },
                Dimensions = { new Dimension { Name = "pagePath" } },
                Metrics = { new Metric { Name = "screenPageViews" } },
                OrderBys = { new OrderBy { Metric = new OrderBy.Types.MetricOrderBy { MetricName = "screenPageViews" }, Desc = true } },
                Limit = limit
            };

            Console.WriteLine($"[GoogleAnalytics] Requesting top pages (last 7 days) from: {_propertyId}");
            var response = await _client!.RunReportAsync(request);
            Console.WriteLine($"[GoogleAnalytics] Historical response received. Rows: {response.Rows.Count}");
            
            var result = new List<(string, long)>();
            
            // Add historical data
            foreach (var row in response.Rows)
            {
                var pagePath = row.DimensionValues[0].Value;
                var views = long.Parse(row.MetricValues[0].Value);
                Console.WriteLine($"[GoogleAnalytics] Historical Page: {pagePath}, Views: {views}");
                result.Add((pagePath, views));
            }
            
            // If we have some data, return it
            if (result.Count > 0)
            {
                return result;
            }
            
            // If no historical data, try realtime
            Console.WriteLine("[GoogleAnalytics] No historical data, trying realtime...");
            var realtimeData = await GetTopPagesRealtimeAsync(limit);
            return realtimeData;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[GoogleAnalytics] Error fetching top pages: {ex.Message}");
            Console.WriteLine($"[GoogleAnalytics] Stack trace: {ex.StackTrace}");
            return new List<(string, long)>();
        }
    }

    private async Task<List<(string pagePath, long views)>> GetTopPagesRealtimeInternalAsync(int limit = 10)
    {
        try
        {
            // Realtime API only supports unifiedScreenName dimension
            var request = new RunRealtimeReportRequest
            {
                Property = _propertyId,
                Dimensions = { new Dimension { Name = "unifiedScreenName" } },
                Metrics = { new Metric { Name = "screenPageViews" } },
                OrderBys = { new OrderBy { Metric = new OrderBy.Types.MetricOrderBy { MetricName = "screenPageViews" }, Desc = true } },
                Limit = limit
            };

            Console.WriteLine($"[GoogleAnalytics] Requesting realtime top pages from: {_propertyId}");
            var response = await _client!.RunRealtimeReportAsync(request);
            Console.WriteLine($"[GoogleAnalytics] Realtime response received. Rows: {response.Rows.Count}");
            
            var result = new List<(string, long)>();
            foreach (var row in response.Rows)
            {
                var pageName = row.DimensionValues[0].Value;
                var views = long.Parse(row.MetricValues[0].Value);
                Console.WriteLine($"[GoogleAnalytics] Realtime Page: {pageName}, Views: {views}");
                result.Add((pageName, views));
            }
            return result;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[GoogleAnalytics] Error fetching realtime pages: {ex.Message}");
            return new List<(string, long)>();

        }
    }

    public async Task<List<(string productName, long views)>> GetTopViewedProductsAsync(int limit = 10)
    {
        Console.WriteLine("[GoogleAnalytics] GetTopViewedProductsAsync called");
        if (!IsConfigured())
        {
            Console.WriteLine("[GoogleAnalytics] Not configured, returning empty list");
            return new List<(string, long)>();
        }

        try
        {
            // Try historical data first (last 7 days)
            var request = new RunReportRequest
            {
                Property = _propertyId,
                DateRanges = { new DateRange { StartDate = "7daysAgo", EndDate = "today" } },
                Dimensions = { new Dimension { Name = "pagePath" } },
                Metrics = { new Metric { Name = "screenPageViews" } },
                DimensionFilter = new FilterExpression
                {
                    Filter = new Filter
                    {
                        FieldName = "pagePath",
                        StringFilter = new Filter.Types.StringFilter
                        {
                            MatchType = Filter.Types.StringFilter.Types.MatchType.Contains,
                            Value = "/product/"
                        }
                    }
                },
                OrderBys = { new OrderBy { Metric = new OrderBy.Types.MetricOrderBy { MetricName = "screenPageViews" }, Desc = true } },
                Limit = limit
            };

            Console.WriteLine($"[GoogleAnalytics] Requesting top products (last 7 days) from: {_propertyId}");
            var response = await _client!.RunReportAsync(request);
            Console.WriteLine($"[GoogleAnalytics] Historical response received. Rows: {response.Rows.Count}");
            
            var result = new List<(string, long)>();
            
            foreach (var row in response.Rows)
            {
                var pagePath = row.DimensionValues[0].Value;
                var productName = ExtractProductNameFromPath(pagePath);
                var views = long.Parse(row.MetricValues[0].Value);
                Console.WriteLine($"[GoogleAnalytics] Historical Product: {productName} (Path: {pagePath}), Views: {views}");
                result.Add((productName, views));
            }
            
            if (result.Count > 0)
            {
                return result;
            }
            
            // Fallback to realtime data if no historical data
            Console.WriteLine("[GoogleAnalytics] No historical product data, trying realtime...");
            return await GetTopProductsRealtimeInternalAsync(limit);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[GoogleAnalytics] Error fetching top viewed products: {ex.Message}");
            Console.WriteLine($"[GoogleAnalytics] Stack trace: {ex.StackTrace}");
            return new List<(string, long)>();
        }
    }

    private string ExtractProductNameFromPath(string pagePath)
    {
        // Extract product name from path like /product/oculos-de-sol-bamboo
        var productSlug = pagePath.Replace("/product/", "").Replace("/", "");
        
        // Decode URL encoding (like %C3%B3 -> ó)
        productSlug = Uri.UnescapeDataString(productSlug);
        
        // Replace hyphens with spaces and capitalize
        var productName = productSlug.Replace("-", " ");
        
        return productName;
    }

    private async Task<List<(string productName, long views)>> GetTopProductsRealtimeInternalAsync(int limit = 10)
    {
        try
        {
            // Realtime API only supports unifiedScreenName - filter by page title
            var request = new RunRealtimeReportRequest
            {
                Property = _propertyId,
                Dimensions = { new Dimension { Name = "unifiedScreenName" } },
                Metrics = { new Metric { Name = "screenPageViews" } },
                OrderBys = { new OrderBy { Metric = new OrderBy.Types.MetricOrderBy { MetricName = "screenPageViews" }, Desc = true } },
                Limit = 50
            };

            Console.WriteLine($"[GoogleAnalytics] Requesting realtime data for products from: {_propertyId}");
            var response = await _client!.RunRealtimeReportAsync(request);
            Console.WriteLine($"[GoogleAnalytics] Realtime response received. Rows: {response.Rows.Count}");
            
            var result = new List<(string, long)>();
            
            foreach (var row in response.Rows)
            {
                var pageTitle = row.DimensionValues[0].Value;
                var views = long.Parse(row.MetricValues[0].Value);
                
                // Filter pages that look like products (contain product-related keywords or patterns)
                // Since we don't have path in realtime, we rely on page title
                if (!string.IsNullOrEmpty(pageTitle) && 
                    (pageTitle.ToLower().Contains("óculos") || 
                     pageTitle.ToLower().Contains("oculos") ||
                     pageTitle.ToLower().Contains("lente") ||
                     pageTitle.ToLower().Contains("armação") ||
                     pageTitle.ToLower().Contains("armacao") ||
                     pageTitle.ToLower().Contains("sol") ||
                     pageTitle.ToLower().Contains("grau")))
                {
                    Console.WriteLine($"[GoogleAnalytics] Realtime Product: {pageTitle}, Views: {views}");
                    result.Add((pageTitle, views));
                    
                    if (result.Count >= limit) break;
                }
            }
            
            return result;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[GoogleAnalytics] Error fetching realtime products: {ex.Message}");
            return new List<(string, long)>();
        }
    }

    public async Task<(long users, long sessions)> GetUsersAndSessionsAsync()
    {
        Console.WriteLine("[GoogleAnalytics] GetUsersAndSessionsAsync called");
        if (!IsConfigured())
        {
            Console.WriteLine("[GoogleAnalytics] Not configured, returning (0, 0)");
            return (0, 0);
        }

        try
        {
            var request = new RunReportRequest
            {
                Property = _propertyId,
                DateRanges = { new DateRange { StartDate = "30daysAgo", EndDate = "today" } },
                Metrics = 
                { 
                    new Metric { Name = "activeUsers" },
                    new Metric { Name = "sessions" }
                }
            };

            Console.WriteLine($"[GoogleAnalytics] Requesting users/sessions from: {_propertyId}");
            var response = await _client!.RunReportAsync(request);
            Console.WriteLine($"[GoogleAnalytics] Response received. Rows: {response.Rows.Count}");
            
            if (response.Rows.Count > 0)
            {
                var users = long.Parse(response.Rows[0].MetricValues[0].Value);
                var sessions = long.Parse(response.Rows[0].MetricValues[1].Value);
                Console.WriteLine($"[GoogleAnalytics] Users: {users}, Sessions: {sessions}");
                return (users, sessions);
            }
            else
            {
                Console.WriteLine("[GoogleAnalytics] No rows in response");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[GoogleAnalytics] Error fetching users and sessions: {ex.Message}");
            Console.WriteLine($"[GoogleAnalytics] Stack trace: {ex.StackTrace}");
        }

        return (0, 0);
    }

    // Historical data methods (last 30 days)
    public async Task<List<(string pagePath, long views)>> GetTopPagesHistoricalAsync(int limit = 10)
    {
        Console.WriteLine("[GoogleAnalytics] GetTopPagesHistoricalAsync called");
        if (!IsConfigured())
        {
            Console.WriteLine("[GoogleAnalytics] Not configured, returning empty list");
            return new List<(string, long)>();
        }

        try
        {
            var request = new RunReportRequest
            {
                Property = _propertyId,
                DateRanges = { new DateRange { StartDate = "30daysAgo", EndDate = "today" } },
                Dimensions = { new Dimension { Name = "pagePath" } },
                Metrics = { new Metric { Name = "screenPageViews" } },
                OrderBys = { new OrderBy { Metric = new OrderBy.Types.MetricOrderBy { MetricName = "screenPageViews" }, Desc = true } },
                Limit = limit
            };

            Console.WriteLine($"[GoogleAnalytics] Requesting historical top pages from: {_propertyId}");
            var response = await _client!.RunReportAsync(request);
            Console.WriteLine($"[GoogleAnalytics] Historical response received. Rows: {response.Rows.Count}");
            
            var result = new List<(string, long)>();
            foreach (var row in response.Rows)
            {
                var pagePath = row.DimensionValues[0].Value;
                var views = long.Parse(row.MetricValues[0].Value);
                Console.WriteLine($"[GoogleAnalytics] Historical Page: {pagePath}, Views: {views}");
                result.Add((pagePath, views));
            }
            
            return result;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[GoogleAnalytics] Error fetching historical top pages: {ex.Message}");
            return new List<(string, long)>();
        }
    }

    public async Task<List<(string productName, long views)>> GetTopViewedProductsHistoricalAsync(int limit = 10)
    {
        Console.WriteLine("[GoogleAnalytics] GetTopViewedProductsHistoricalAsync called");
        if (!IsConfigured())
        {
            Console.WriteLine("[GoogleAnalytics] Not configured, returning empty list");
            return new List<(string, long)>();
        }

        try
        {
            var request = new RunReportRequest
            {
                Property = _propertyId,
                DateRanges = { new DateRange { StartDate = "30daysAgo", EndDate = "today" } },
                Dimensions = { new Dimension { Name = "pagePath" } },
                Metrics = { new Metric { Name = "screenPageViews" } },
                DimensionFilter = new FilterExpression
                {
                    Filter = new Filter
                    {
                        FieldName = "pagePath",
                        StringFilter = new Filter.Types.StringFilter
                        {
                            MatchType = Filter.Types.StringFilter.Types.MatchType.Contains,
                            Value = "/product/"
                        }
                    }
                },
                OrderBys = { new OrderBy { Metric = new OrderBy.Types.MetricOrderBy { MetricName = "screenPageViews" }, Desc = true } },
                Limit = limit
            };

            Console.WriteLine($"[GoogleAnalytics] Requesting historical top products from: {_propertyId}");
            var response = await _client!.RunReportAsync(request);
            Console.WriteLine($"[GoogleAnalytics] Historical response received. Rows: {response.Rows.Count}");
            
            var result = new List<(string, long)>();
            foreach (var row in response.Rows)
            {
                var pagePath = row.DimensionValues[0].Value;
                var productName = ExtractProductNameFromPath(pagePath);
                var views = long.Parse(row.MetricValues[0].Value);
                Console.WriteLine($"[GoogleAnalytics] Historical Product: {productName}, Views: {views}");
                result.Add((productName, views));
            }
            
            return result;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[GoogleAnalytics] Error fetching historical top products: {ex.Message}");
            return new List<(string, long)>();
        }
    }

    // Realtime data methods (last 30 minutes) - expose existing private methods
    public async Task<List<(string pagePath, long views)>> GetTopPagesRealtimeAsync(int limit = 10)
    {
        return await GetTopPagesRealtimeInternalAsync(limit);
    }

    public async Task<List<(string productName, long views)>> GetTopViewedProductsRealtimeAsync(int limit = 10)
    {
        return await GetTopProductsRealtimeInternalAsync(limit);
    }
}

