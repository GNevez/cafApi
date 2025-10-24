using cafApi.Models;
using cafApi.Models.DTOs;

namespace cafApi.Services
{
    public interface IPedidoService
    {
        Task<PedidoDto?> GetByIdAsync(int id);
        Task<(List<PedidoDto> pedidos, int totalCount)> GetAllAsync(int pageNumber, int pageSize);
        Task<List<PedidoDto>> GetByClienteIdAsync(int clienteId);
        Task<(List<PedidoDto> pedidos, int totalCount)> GetByStatusAsync(StatusPedido status, int pageNumber, int pageSize);
        Task<PedidoDto> CreateAsync(CriarPedidoDto criarPedidoDto, string cartToken);
        Task<PedidoDto?> UpdateStatusAsync(int id, AtualizarStatusPedidoDto updateDto);
        Task<bool> DeleteAsync(int id);
    }
}
