using cafApi.Models;
using cafApi.Models.DTOs;

namespace cafApi.Services
{
    public interface ITransacaoService
    {
        Task<(List<TransacaoDto> transacoes, int totalCount)> GetAllAsync(TipoTransacao? tipo = null, int pageNumber = 1, int pageSize = 10);
        Task<TransacaoDto?> GetByIdAsync(int id);
        Task<TransacaoDto> CreateAsync(CriarTransacaoDto dto);
        Task<TransacaoDto?> UpdateAsync(int id, AtualizarTransacaoDto dto);
        Task<bool> DeleteAsync(int id);
        Task<decimal> GetSaldoAsync(); // Retorna entrada - saída
        Task<decimal> GetTotalByTipoAsync(TipoTransacao tipo);
    }
}
