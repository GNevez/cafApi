using System.Globalization;
using cafApi.Contexts;
using cafApi.Models;
using cafApi.Models.DTOs;
using Microsoft.EntityFrameworkCore;

namespace cafApi.Services
{
    public class DashboardService : IDashboardService
    {
        private readonly ApplicationDbContext _context;

        public DashboardService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<(decimal totalEntradas, decimal totalSaidas, decimal saldo)> GetTransacaoTotalsAsync()
        {
            var totalEntradas = await _context.Transacoes
                .Where(t => t.Tipo == TipoTransacao.Entrada)
                .SumAsync(t => (decimal?)t.Valor) ?? 0m;

            var totalSaidas = await _context.Transacoes
                .Where(t => t.Tipo == TipoTransacao.Saida)
                .SumAsync(t => (decimal?)t.Valor) ?? 0m;

            return (totalEntradas, totalSaidas, totalEntradas - totalSaidas);
        }

        public async Task<decimal> GetTotalSalesAsync()
        {
            var total = await _context.Pedidos.SumAsync(p => (decimal?)p.TotalPedido) ?? 0m;
            return total;
        }

        public async Task<int> GetPendingOrdersCountAsync()
        {
            var count = await _context.Pedidos.CountAsync(p => p.Status == StatusPedido.AguardandoConfirmacao);
            return count;
        }

        public async Task<int> GetTotalOrdersCountAsync()
        {
            var count = await _context.Pedidos.CountAsync();
            return count;
        }

        public async Task<decimal> GetAverageTicketAsync()
        {
            var totalOrders = await _context.Pedidos.CountAsync();
            if (totalOrders == 0) return 0m;

            var totalSales = await _context.Pedidos.SumAsync(p => (decimal?)p.TotalPedido) ?? 0m;
            return Math.Round(totalSales / totalOrders, 2);
        }

        public async Task<List<PedidoDto>> GetRecentOrdersAsync(int limit = 7)
        {
            var pedidos = await _context.Pedidos
                .AsNoTracking()
                .OrderByDescending(p => p.DataPedido)
                .ThenByDescending(p => p.Id)
                .Take(limit)
                .Include(p => p.Cliente)
                .Include(p => p.Carrinho)
                .ToListAsync();

            return pedidos.Select(p => new PedidoDto
            {
                Id = p.Id,
                ClienteId = p.ClienteId,
                ClienteNome = p.NomeCliente,
                ClienteEmail = p.EmailCliente,
                ClienteTelefone = p.TelefoneCliente,
                Status = p.Status,
                TotalPedido = p.TotalPedido,
                DataPedido = p.DataPedido,
            }).ToList();
        }

        public async Task<(List<string> labels, List<decimal> data)> GetSeriesAsync(string metric, string range)
        {
            // Determine time window and granularity
            // Usar horário local do Brasil (UTC-3)
            DateTime now = DateTime.Now;
            DateTime start;
            string granularity; // day or month
            switch (range)
            {
                case "today":
                    start = now.Date;
                    granularity = "day";
                    break;
                case "7d":
                    start = now.Date.AddDays(-6);
                    granularity = "day";
                    break;
                case "30d":
                    start = now.Date.AddDays(-29);
                    granularity = "day";
                    break;
                case "1y":
                    start = new DateTime(now.Year, 1, 1).AddYears(-1).AddMonths(1); // last 12 months
                    granularity = "month";
                    break;
                case "5y":
                    start = new DateTime(now.Year - 4, 1, 1);
                    granularity = "year"; // Usar granularidade anual para 5 anos
                    break;
                default:
                    start = now.Date.AddDays(-29);
                    granularity = "day";
                    break;
            }

            // Fetch transactions in window (considerar timezone)
            var tx = await _context.Transacoes
                .AsNoTracking()
                .Where(t => t.DataTransacao >= start && t.DataTransacao <= now)
                .ToListAsync();

            // Prepare buckets
            var labels = new List<string>();
            var data = new List<decimal>();

            if (granularity == "day")
            {
                for (DateTime d = start.Date; d <= now.Date; d = d.AddDays(1))
                {
                    labels.Add(d.ToString("dd/MM", CultureInfo.GetCultureInfo("pt-BR")));
                    decimal value = 0m;
                    var dayItems = tx.Where(t => t.DataTransacao.Date == d.Date);
                    if (metric == "entrada") value = dayItems.Where(t => t.Tipo == TipoTransacao.Entrada).Sum(t => t.Valor);
                    else if (metric == "saida") value = dayItems.Where(t => t.Tipo == TipoTransacao.Saida).Sum(t => t.Valor);
                    else // lucro
                        value = dayItems.Where(t => t.Tipo == TipoTransacao.Entrada).Sum(t => t.Valor) -
                                dayItems.Where(t => t.Tipo == TipoTransacao.Saida).Sum(t => t.Valor);
                    data.Add(value);
                }
            }
            else if (granularity == "year")
            {
                // Para 5 anos, mostrar apenas 1 ponto por ano
                for (int yearOffset = 0; yearOffset <= 4; yearOffset++)
                {
                    int year = start.Year + yearOffset;
                    labels.Add(year.ToString());
                    
                    var yearItems = tx.Where(t => t.DataTransacao.Year == year);
                    decimal value = 0m;
                    if (metric == "entrada") value = yearItems.Where(t => t.Tipo == TipoTransacao.Entrada).Sum(t => t.Valor);
                    else if (metric == "saida") value = yearItems.Where(t => t.Tipo == TipoTransacao.Saida).Sum(t => t.Valor);
                    else value = yearItems.Where(t => t.Tipo == TipoTransacao.Entrada).Sum(t => t.Valor) -
                                  yearItems.Where(t => t.Tipo == TipoTransacao.Saida).Sum(t => t.Valor);
                    data.Add(value);
                }
            }
            else // month
            {
                DateTime cursor = new DateTime(start.Year, start.Month, 1);
                DateTime endMonth = new DateTime(now.Year, now.Month, 1);
                
                while (cursor <= endMonth)
                {
                    labels.Add(cursor.ToString("MM/yy", CultureInfo.GetCultureInfo("pt-BR")));
                    var monthItems = tx.Where(t => t.DataTransacao.Year == cursor.Year && t.DataTransacao.Month == cursor.Month);
                    decimal value = 0m;
                    if (metric == "entrada") value = monthItems.Where(t => t.Tipo == TipoTransacao.Entrada).Sum(t => t.Valor);
                    else if (metric == "saida") value = monthItems.Where(t => t.Tipo == TipoTransacao.Saida).Sum(t => t.Valor);
                    else value = monthItems.Where(t => t.Tipo == TipoTransacao.Entrada).Sum(t => t.Valor) -
                                  monthItems.Where(t => t.Tipo == TipoTransacao.Saida).Sum(t => t.Valor);
                    data.Add(value);
                    cursor = cursor.AddMonths(1);
                }
            }

            return (labels, data);
        }

        public async Task<decimal> GetSeriesPointValueAsync(string metric, string date, string granularity)
        {
            DateTime targetDate;
            DateTime startDate;
            DateTime endDate;

            if (granularity == "day")
            {
                // Aceita yyyy-MM-dd (ISO) ou dd/MM
                if (DateTime.TryParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out targetDate))
                {
                    startDate = targetDate.Date;
                    endDate = targetDate.Date.AddDays(1).AddSeconds(-1);
                }
                else
                {
                    var parts = date.Split('/');
                    if (parts.Length != 2) return 0m;
                    int day = int.Parse(parts[0]);
                    int month = int.Parse(parts[1]);
                    int year = DateTime.Now.Year;
                    if (month > DateTime.Now.Month)
                        year--;
                    targetDate = new DateTime(year, month, day);
                    startDate = targetDate.Date;
                    endDate = targetDate.Date.AddDays(1).AddSeconds(-1);
                }
            }
            else if (granularity == "year")
            {
                // Parse date in format yyyy
                if (!int.TryParse(date, out int year)) return 0m;
                startDate = new DateTime(year, 1, 1);
                endDate = new DateTime(year, 12, 31, 23, 59, 59);
            }
            else // month
            {
                // Aceita yyyy-MM (ISO) ou MM/yy
                if (DateTime.TryParseExact(date, "yyyy-MM", CultureInfo.InvariantCulture, DateTimeStyles.None, out targetDate))
                {
                    startDate = new DateTime(targetDate.Year, targetDate.Month, 1);
                    endDate = startDate.AddMonths(1).AddSeconds(-1);
                }
                else
                {
                    var parts = date.Split('/');
                    if (parts.Length != 2) return 0m;
                    int month = int.Parse(parts[0]);
                    int year = 2000 + int.Parse(parts[1]);
                    targetDate = new DateTime(year, month, 1);
                    startDate = targetDate;
                    endDate = targetDate.AddMonths(1).AddSeconds(-1);
                }
            }

            var tx = await _context.Transacoes
                .AsNoTracking()
                .Where(t => t.DataTransacao >= startDate && t.DataTransacao <= endDate)
                .ToListAsync();

            decimal value = 0m;
            if (metric == "entrada")
                value = tx.Where(t => t.Tipo == TipoTransacao.Entrada).Sum(t => t.Valor);
            else if (metric == "saida")
                value = tx.Where(t => t.Tipo == TipoTransacao.Saida).Sum(t => t.Valor);
            else // lucro
                value = tx.Where(t => t.Tipo == TipoTransacao.Entrada).Sum(t => t.Valor) -
                        tx.Where(t => t.Tipo == TipoTransacao.Saida).Sum(t => t.Valor);

            return value;
        }

        public async Task<decimal> GetCartAbandonmentRateAsync()
        {
            var totalCartsWithItems = await _context.Carrinhos
                .Where(c => c.Itens.Any() && c.DataCriacao >= DateTime.UtcNow.AddDays(-30))
                .CountAsync();

            if (totalCartsWithItems == 0) return 0m;

            var abandonedCarts = await _context.Carrinhos
                .Where(c => c.Itens.Any() 
                    && c.Status == StatusCarrinho.Ativo 
                    && c.DataCriacao >= DateTime.UtcNow.AddDays(-30)
                    && c.DataAtualizacao < DateTime.UtcNow.AddHours(-24))
                .CountAsync();

            return Math.Round((decimal)abandonedCarts / totalCartsWithItems * 100, 2);
        }

        public async Task<decimal> GetCustomerLTVAsync()
        {
            var customersWithOrders = await _context.Pedidos
                .GroupBy(p => p.ClienteId)
                .Select(g => new { ClienteId = g.Key, TotalSpent = g.Sum(p => p.TotalPedido) })
                .ToListAsync();

            if (customersWithOrders.Count == 0) return 0m;

            var avgLTV = customersWithOrders.Average(c => c.TotalSpent);
            return Math.Round(avgLTV, 2);
        }

        public async Task<List<(string productName, int quantity, decimal revenue)>> GetTopProductsAsync(int limit = 5)
        {
            var topProducts = await _context.Pedidos
                .Where(p => p.Status != StatusPedido.Cancelado)
                .SelectMany(p => p.Carrinho.Itens)
                .GroupBy(item => new { item.ProdutoId, item.Produto.Nome, item.Produto.Preco })
                .Select(g => new
                {
                    ProductName = g.Key.Nome,
                    Quantity = g.Sum(i => i.Quantidade),
                    Revenue = g.Sum(i => i.Quantidade * g.Key.Preco)
                })
                .OrderByDescending(x => x.Revenue)
                .Take(limit)
                .ToListAsync();

            return topProducts.Select(p => (p.ProductName, p.Quantity, p.Revenue)).ToList();
        }

        public async Task<List<(string productName, int quantity)>> GetLowPerformingProductsAsync(int limit = 5)
        {
            var last30Days = DateTime.UtcNow.AddDays(-30);
            
            // Products with sales in the last 30 days, ordered by quantity sold (ascending)
            var lowPerformers = await _context.Pedidos
                .Where(p => p.Status != StatusPedido.Cancelado && p.DataPedido >= last30Days)
                .SelectMany(p => p.Carrinho.Itens)
                .GroupBy(item => new { item.ProdutoId, item.Produto.Nome })
                .Select(g => new
                {
                    ProductName = g.Key.Nome,
                    Quantity = g.Sum(i => i.Quantidade)
                })
                .OrderBy(x => x.Quantity)
                .Take(limit)
                .ToListAsync();

            return lowPerformers.Select(p => (p.ProductName, p.Quantity)).ToList();
        }

        public async Task<List<(string categoryName, decimal revenue)>> GetSalesByCategoryAsync()
        {
            var salesByCategory = await _context.Pedidos
                .Where(p => p.Status != StatusPedido.Cancelado)
                .SelectMany(p => p.Carrinho.Itens)
                .GroupBy(item => item.Produto.Categoria.Nome)
                .Select(g => new
                {
                    CategoryName = g.Key,
                    Revenue = g.Sum(i => i.Quantidade * i.Produto.Preco)
                })
                .OrderByDescending(x => x.Revenue)
                .ToListAsync();

            return salesByCategory.Select(c => (c.CategoryName, c.Revenue)).ToList();
        }

        public async Task<(int totalUsed, decimal totalDiscount)> GetCouponUsageAsync()
        {
            var couponUsage = await _context.Set<CupomUso>()
                .Where(cu => cu.DataUso >= DateTime.UtcNow.AddDays(-30))
                .GroupBy(cu => 1)
                .Select(g => new
                {
                    TotalUsed = g.Count(),
                    TotalDiscount = g.Sum(cu => cu.ValorDescontoAplicado)
                })
                .FirstOrDefaultAsync();

            return couponUsage != null 
                ? (couponUsage.TotalUsed, couponUsage.TotalDiscount) 
                : (0, 0m);
        }
    }
}
