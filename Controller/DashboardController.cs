using cafApi.Services;
using Microsoft.AspNetCore.Mvc;

namespace cafApi.Controller
{
    [ApiController]
    [Route("api/[controller]")]
    public class DashboardController : ControllerBase
    {
        private readonly IDashboardService _dashboardService;
        private readonly IActiveClientsTracker _activeClients;
        private readonly IGoogleAnalyticsService _googleAnalyticsService;

        public DashboardController(
            IDashboardService dashboardService,
            IActiveClientsTracker activeClients,
            IGoogleAnalyticsService googleAnalyticsService)
        {
            _dashboardService = dashboardService;
            _activeClients = activeClients;
            _googleAnalyticsService = googleAnalyticsService;
        }

        [HttpGet("summary")]
        public async Task<IActionResult> GetSummary()
        {
            var (entradas, saidas, saldo) = await _dashboardService.GetTransacaoTotalsAsync();
            var vendasTotais = await _dashboardService.GetTotalSalesAsync();
            var pedidosPendentes = await _dashboardService.GetPendingOrdersCountAsync();
            var totalPedidos = await _dashboardService.GetTotalOrdersCountAsync();
            var ticketMedio = await _dashboardService.GetAverageTicketAsync();
            var clientesAtivos = _activeClients.GetActiveCount();

            long? visitantes = null;
            long? sessoes = null;
            if (_googleAnalyticsService.IsConfigured())
            {
                try
                {
                    var (users, sessions) = await _googleAnalyticsService.GetUsersAndSessionsAsync();
                    visitantes = users;
                    sessoes = sessions;
                }
                catch
                {
                }
            }

            return Ok(new
            {
                totalEntradas = entradas,
                totalSaidas = saidas,
                saldo,
                vendasTotais,
                pedidosPendentes,
                totalPedidos,
                ticketMedio,
                clientesAtivos,
                visitantes,
                sessoes
            });
        }

        [HttpGet("series")]
        public async Task<IActionResult> GetSeries([FromQuery] string metric = "lucro", [FromQuery] string range = "30d")
        {
            metric = metric.ToLowerInvariant();
            range = range.ToLowerInvariant();
            if (metric != "lucro" && metric != "entrada" && metric != "saida")
            {
                return BadRequest(new { message = "metric must be one of: lucro, entrada, saida" });
            }
            var allowedRanges = new HashSet<string> { "today", "7d", "30d", "1y", "5y" };
            if (!allowedRanges.Contains(range)) range = "30d";

            var (labels, data) = await _dashboardService.GetSeriesAsync(metric, range);
            return Ok(new { labels, data });
        }

        [HttpGet("series/point")]
        public async Task<IActionResult> GetSeriesPoint([FromQuery] string metric = "lucro", [FromQuery] string date = "", [FromQuery] string granularity = "day")
        {
            metric = metric.ToLowerInvariant();
            granularity = granularity.ToLowerInvariant();
            
            if (metric != "lucro" && metric != "entrada" && metric != "saida")
            {
                return BadRequest(new { message = "metric must be one of: lucro, entrada, saida" });
            }
            
            if (string.IsNullOrWhiteSpace(date))
            {
                return BadRequest(new { message = "date is required" });
            }
            
            if (granularity != "day" && granularity != "month" && granularity != "year")
            {
                return BadRequest(new { message = "granularity must be one of: day, month, year" });
            }

            var value = await _dashboardService.GetSeriesPointValueAsync(metric, date, granularity);
            return Ok(new { date, value });
        }

        [HttpGet("recent-orders")]
        public async Task<IActionResult> GetRecentOrders([FromQuery] int limit = 7)
        {
            limit = Math.Clamp(limit, 1, 50);
            var pedidos = await _dashboardService.GetRecentOrdersAsync(limit);
            return Ok(pedidos);
        }

        public class HeartbeatRequest { public string? ClientId { get; set; } }

        [HttpPost("heartbeat")]
        public IActionResult Heartbeat([FromBody] HeartbeatRequest request)
        {
            var clientId = request.ClientId;
            if (string.IsNullOrWhiteSpace(clientId))
            {
                // fallback to header if not provided in body
                clientId = Request.Headers["X-Client-Id"].FirstOrDefault();
            }
            if (string.IsNullOrWhiteSpace(clientId))
            {
                return BadRequest(new { message = "clientId is required" });
            }
            _activeClients.Heartbeat(clientId);
            return Ok(new { ok = true });
        }

        [HttpPost("disconnect")]
        public IActionResult Disconnect([FromBody] HeartbeatRequest request)
        {
            var clientId = request.ClientId;
            if (string.IsNullOrWhiteSpace(clientId))
            {
                clientId = Request.Headers["X-Client-Id"].FirstOrDefault();
            }
            if (string.IsNullOrWhiteSpace(clientId))
            {
                return BadRequest(new { message = "clientId is required" });
            }
            _activeClients.Disconnect(clientId);
            return Ok(new { ok = true });
        }

        [HttpGet("active-clients")]
        public IActionResult GetActiveClients()
        {
            var count = _activeClients.GetActiveCount();
            return Ok(new { count });
        }

        [HttpGet("behavior-analytics")]
        public async Task<IActionResult> GetBehaviorAnalytics()
        {
            var cartAbandonmentRate = await _dashboardService.GetCartAbandonmentRateAsync();
            var customerLTV = await _dashboardService.GetCustomerLTVAsync();

            decimal bounceRate = 0m;
            object mostViewedProductsHistorical = "N/A";
            object mostAccessedPagesHistorical = "N/A";
            object mostViewedProductsRealtime = "N/A";
            object mostAccessedPagesRealtime = "N/A";

            if (_googleAnalyticsService.IsConfigured())
            {
                try
                {
                    bounceRate = await _googleAnalyticsService.GetBounceRateAsync();

                    // Historical data (last 7 days)
                    var topProductsHistorical = await _googleAnalyticsService.GetTopViewedProductsHistoricalAsync(15);
                    var topPagesHistorical = await _googleAnalyticsService.GetTopPagesHistoricalAsync(15);

                    // Realtime data (last 30 minutes)
                    var topProductsRealtime = await _googleAnalyticsService.GetTopViewedProductsRealtimeAsync(15);
                    var topPagesRealtime = await _googleAnalyticsService.GetTopPagesRealtimeAsync(15);

                    mostViewedProductsHistorical = topProductsHistorical.Select(p => new { productName = p.productName, views = p.views });

                    // Separar path e title para páginas históricas
                    mostAccessedPagesHistorical = topPagesHistorical.Select(p =>
                    {
                        var parts = p.pagePath.Split('|');
                        return new
                        {
                            pagePath = parts[0],
                            pageTitle = parts.Length > 1 ? parts[1] : parts[0],
                            views = p.views
                        };
                    });

                    mostViewedProductsRealtime = topProductsRealtime.Select(p => new { productName = p.productName, views = p.views });

                    // Separar path e title para páginas realtime
                    mostAccessedPagesRealtime = topPagesRealtime.Select(p =>
                    {
                        var parts = p.pagePath.Split('|');
                        return new
                        {
                            pagePath = parts[0],
                            pageTitle = parts.Length > 1 ? parts[1] : parts[0],
                            views = p.views
                        };
                    });
                }
                catch
                {
                }
            }

            return Ok(new
            {
                cartAbandonmentRate,
                customerLTV,
                bounceRate,
                mostViewedProductsHistorical,
                mostAccessedPagesHistorical,
                mostViewedProductsRealtime,
                mostAccessedPagesRealtime
            });
        }

        [HttpGet("product-performance")]
        public async Task<IActionResult> GetProductPerformance()
        {
            var topProducts = await _dashboardService.GetTopProductsAsync(5);
            var lowPerformers = await _dashboardService.GetLowPerformingProductsAsync(5);
            var salesByCategory = await _dashboardService.GetSalesByCategoryAsync();
            var (totalCouponsUsed, totalDiscount) = await _dashboardService.GetCouponUsageAsync();
            var returnRate = await _dashboardService.GetReturnRateAsync();

            return Ok(new
            {
                topProducts = topProducts.Select(p => new { name = p.productName, quantity = p.quantity, revenue = p.revenue }),
                lowPerformingProducts = lowPerformers.Select(p => new { name = p.productName, quantity = p.quantity }),
                salesByCategory = salesByCategory.Select(c => new { category = c.categoryName, revenue = c.revenue }),
                returnRate,
                couponUsage = new { totalUsed = totalCouponsUsed, totalDiscount }
            });
        }
    }
}
