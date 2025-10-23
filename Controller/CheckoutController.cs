using cafApi.Models.DTOs;
using cafApi.Services;
using Microsoft.AspNetCore.Mvc;

namespace cafApi.Controller;

[ApiController]
[Route("api/[controller]")]
public class CheckoutController : ControllerBase
{
    private readonly IPedidoService _pedidoService;

    public CheckoutController(IPedidoService pedidoService)
    {
        _pedidoService = pedidoService;
    }

    [HttpPost]
    public async Task<ActionResult<PedidoDto>> ProcessarCheckout([FromBody] CriarPedidoDto checkoutData)
    {
        try
        {
            var cartToken = Request.Cookies["cart_token"];
            
            if (string.IsNullOrEmpty(cartToken))
            {
                return BadRequest("Token do carrinho não encontrado");
            }

            var pedido = await _pedidoService.CreateAsync(checkoutData, cartToken);
            
            // Deletar cookie do carrinho após checkout
            Response.Cookies.Delete("cart_token", new CookieOptions { Path = "/" });
            
            return Ok(pedido);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Erro interno do servidor: {ex.Message}");
        }
    }
}
