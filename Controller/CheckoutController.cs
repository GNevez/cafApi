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

            return Ok(pedido);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = $"Erro interno do servidor: {ex.Message}" });
        }
    }
}
