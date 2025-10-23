using cafApi.Models.DTOs;

namespace cafApi.Services
{
    public interface IPedidoService
    {
        Task<PedidoDto?> GetByIdAsync(int id);
        Task<List<PedidoDto>> GetAllAsync();
        Task<List<PedidoDto>> GetByClienteIdAsync(int clienteId);
        Task<PedidoDto> CreateAsync(CriarPedidoDto criarPedidoDto, string cartToken);
        Task<PedidoDto?> UpdateStatusAsync(int id, AtualizarStatusPedidoDto updateDto);
        Task<bool> DeleteAsync(int id);
    }
}
