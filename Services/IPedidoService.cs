using cafApi.Models;
using cafApi.Models.DTOs;

namespace cafApi.Services
{
    public interface IPedidoService
    {
        Task<PedidoDto?> GetByIdAsync(int id);
        Task<PedidoDto?> GetByCodigoPedidoAsync(string codigoPedido);
        Task<(List<PedidoDto> pedidos, int totalCount)> GetAllAsync(int pageNumber, int pageSize);
        Task<List<PedidoDto>> GetByClienteIdAsync(int clienteId);
        Task<(List<PedidoDto> pedidos, int totalCount)> GetByStatusAsync(StatusPedido status, int pageNumber, int pageSize);
        Task<List<PedidoDto>> GetByCpfAsync(string cpf);
        Task<PedidoDto> CreateAsync(CriarPedidoDto criarPedidoDto, string cartToken);
        Task<PedidoDto?> UpdateStatusAsync(int id, AtualizarStatusPedidoDto updateDto);
        Task<PedidoDto?> UpdateStatusByCodigoPedidoAsync(string codigoPedido, AtualizarStatusPedidoDto updateDto);
        Task<bool> DeleteAsync(int id);
    }
}
