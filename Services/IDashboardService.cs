using cafApi.Models;
using cafApi.Models.DTOs;

namespace cafApi.Services
{
    public interface IDashboardService
    {
        Task<(decimal totalEntradas, decimal totalSaidas, decimal saldo)> GetTransacaoTotalsAsync();
        Task<decimal> GetTotalSalesAsync();
        Task<int> GetPendingOrdersCountAsync();
        Task<int> GetTotalOrdersCountAsync();
        Task<decimal> GetAverageTicketAsync();
        Task<List<PedidoDto>> GetRecentOrdersAsync(int limit = 7);
        Task<(List<string> labels, List<decimal> data)> GetSeriesAsync(string metric, string range);
        Task<decimal> GetSeriesPointValueAsync(string metric, string date, string granularity);
        
        // Behavior analytics
        Task<decimal> GetCartAbandonmentRateAsync();
        Task<decimal> GetCustomerLTVAsync();
        
        // Product performance
        Task<List<(string productName, int quantity, decimal revenue)>> GetTopProductsAsync(int limit = 5);
        Task<List<(string productName, int quantity)>> GetLowPerformingProductsAsync(int limit = 5);
        Task<List<(string categoryName, decimal revenue)>> GetSalesByCategoryAsync();
        Task<(int totalUsed, decimal totalDiscount)> GetCouponUsageAsync();
    }
}
