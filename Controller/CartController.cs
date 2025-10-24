using cafApi.Models.DTOs;
using cafApi.Services;
using Microsoft.AspNetCore.Mvc;

namespace cafApi.Controller;

[ApiController]
[Route("api/[controller]")]
public class CartController : ControllerBase
{
    private readonly ICartService _cartService;

    public CartController(ICartService cartService)
    {
        _cartService = cartService;
    }

    [HttpGet]
    public async Task<ActionResult<CarrinhoDto>> GetCart()
    {
        var cartToken = Request.Cookies["cart_token"];
        var cart = await _cartService.GetOrCreateCartAsync(cartToken);

        if (string.IsNullOrEmpty(cartToken) || cart.Token != cartToken)
        {
            SetCartCookie(cart.Token);
        }

        return Ok(cart);
    }

    [HttpPost("add")]
    public async Task<ActionResult<CarrinhoDto>> AddItem([FromBody] AdicionarItemCarrinhoDto item)
    {
        try
        {
            var cartToken = Request.Cookies["cart_token"];
            var cart = await _cartService.AddItemAsync(cartToken, item);

            // Definir/atualizar cookie se for um novo carrinho ou se o token mudou
            if (string.IsNullOrEmpty(cartToken) || cart.Token != cartToken)
            {
                SetCartCookie(cart.Token);
            }

            return Ok(cart);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erro ao adicionar item ao carrinho: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
            return StatusCode(500, new { message = ex.Message, details = ex.InnerException?.Message });
        }
    }

    [HttpPut("update")]
    public async Task<ActionResult<CarrinhoDto>> UpdateItem([FromBody] AtualizarItemCarrinhoDto item)
    {
        var cartToken = Request.Cookies["cart_token"];
        
        if (string.IsNullOrEmpty(cartToken))
        {
            return BadRequest("Token do carrinho não encontrado");
        }

        var cart = await _cartService.UpdateItemQuantityAsync(cartToken, item);
        if (cart.Token != cartToken)
        {
            SetCartCookie(cart.Token);
        }
        return Ok(cart);
    }

    [HttpDelete("remove/{itemId}")]
    public async Task<ActionResult<CarrinhoDto>> RemoveItem(int itemId)
    {
        var cartToken = Request.Cookies["cart_token"];
        
        if (string.IsNullOrEmpty(cartToken))
        {
            return BadRequest("Token do carrinho não encontrado");
        }

        var cart = await _cartService.RemoveItemAsync(cartToken, itemId);
        if (cart.Token != cartToken)
        {
            SetCartCookie(cart.Token);
        }
        return Ok(cart);
    }

    [HttpDelete("clear")]
    public async Task<ActionResult<CarrinhoDto>> ClearCart()
    {
        var cartToken = Request.Cookies["cart_token"];
        
        if (string.IsNullOrEmpty(cartToken))
        {
            return BadRequest("Token do carrinho não encontrado");
        }

        var cart = await _cartService.ClearCartAsync(cartToken);
        if (cart.Token != cartToken)
        {
            SetCartCookie(cart.Token);
        }
        return Ok(cart);
    }

    [HttpGet("validate")]
    public async Task<ActionResult<bool>> ValidateCart()
    {
        var cartToken = Request.Cookies["cart_token"];
        var isValid = await _cartService.ValidateCartTokenAsync(cartToken);
        return Ok(isValid);
    }

    [HttpPost("apply-coupon")]
    public async Task<ActionResult<CarrinhoDto>> ApplyCoupon([FromBody] ApplyCouponRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Codigo))
        {
            return BadRequest(new { message = "Código do cupom é obrigatório" });
        }

        var cartToken = Request.Cookies["cart_token"];
        try
        {
            var cart = await _cartService.ApplyCouponAsync(cartToken, request.Codigo.Trim());
            if (string.IsNullOrEmpty(cartToken) || cart.Token != cartToken)
            {
                SetCartCookie(cart.Token);
            }
            return Ok(cart);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erro ao aplicar cupom: {ex.Message}");
            return StatusCode(500, new { message = "Erro interno ao aplicar cupom" });
        }
    }

    [HttpDelete("remove-coupon")]
    public async Task<ActionResult<CarrinhoDto>> RemoveCoupon()
    {
        var cartToken = Request.Cookies["cart_token"];
        var cart = await _cartService.RemoveCouponAsync(cartToken);
        if (string.IsNullOrEmpty(cartToken) || cart.Token != cartToken)
        {
            SetCartCookie(cart.Token);
        }
        return Ok(cart);
    }
    

    private void SetCartCookie(string cartToken)
    {
        var isHttps = Request.IsHttps;
        var cookieOptions = new CookieOptions
        {
            HttpOnly = true,
            Secure = isHttps,
            SameSite = SameSiteMode.Lax,
            Path = "/",
            Expires = DateTime.UtcNow.AddHours(1) 
        };

        Response.Cookies.Append("cart_token", cartToken, cookieOptions);
    }
}

public class ApplyCouponRequest
{
    public string Codigo { get; set; } = string.Empty;
}
