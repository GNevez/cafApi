using cafApi.Models;
using cafApi.Models.DTOs;

namespace cafApi.Services;

public interface ICartService
{
    Task<CarrinhoDto> GetOrCreateCartAsync(string? cartToken);
    Task<CarrinhoDto> AddItemAsync(string? cartToken, AdicionarItemCarrinhoDto item);
    Task<CarrinhoDto> UpdateItemQuantityAsync(string? cartToken, AtualizarItemCarrinhoDto item);
    Task<CarrinhoDto> RemoveItemAsync(string? cartToken, int itemId);
    Task<CarrinhoDto> ClearCartAsync(string? cartToken);
    Task<bool> ValidateCartTokenAsync(string? cartToken);
    Task<CarrinhoDto> ApplyCouponAsync(string? cartToken, string codigo);
    Task<CarrinhoDto> RemoveCouponAsync(string? cartToken);

    /// <summary>
    /// Associa um cliente ao carrinho. Cria o cliente se não existir.
    /// </summary>
    Task<CarrinhoDto> AssociateClientAsync(string? cartToken, string email, string? nome = null);

    /// <summary>
    /// Marca carrinho como abandonado
    /// </summary>
    Task MarkCartAsAbandonedAsync(int carrinhoId);

    /// <summary>
    /// Obtém carrinhos abandonados que ainda não receberam email de recuperação
    /// </summary>
    Task<List<Carrinho>> GetAbandonedCartsForEmailAsync();

    /// <summary>
    /// Marca que o email de recuperação foi enviado
    /// </summary>
    Task MarkAbandonmentEmailSentAsync(int carrinhoId);

    /// <summary>
    /// Recupera um carrinho expirado, reativando-o para o usuário
    /// </summary>
    Task<CarrinhoDto?> RecoverCartAsync(string cartToken);
}
