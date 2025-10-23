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
}
