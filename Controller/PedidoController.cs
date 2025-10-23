using cafApi.Models.DTOs;
using cafApi.Services;
using Microsoft.AspNetCore.Mvc;

namespace cafApi.Controller;

[ApiController]
[Route("api/[controller]")]
public class PedidoController : ControllerBase
{
    private readonly IPedidoService _pedidoService;

    public PedidoController(IPedidoService pedidoService)
    {
        _pedidoService = pedidoService;
    }

    [HttpGet]
    public async Task<ActionResult<List<PedidoDto>>> GetAll()
    {
        try
        {
            var pedidos = await _pedidoService.GetAllAsync();
            return Ok(pedidos);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Erro interno do servidor: {ex.Message}");
        }
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<PedidoDto>> GetById(int id)
    {
        try
        {
            var pedido = await _pedidoService.GetByIdAsync(id);
            if (pedido == null)
                return NotFound("Pedido não encontrado");

            return Ok(pedido);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Erro interno do servidor: {ex.Message}");
        }
    }

    [HttpGet("cliente/{clienteId}")]
    public async Task<ActionResult<List<PedidoDto>>> GetByClienteId(int clienteId)
    {
        try
        {
            var pedidos = await _pedidoService.GetByClienteIdAsync(clienteId);
            return Ok(pedidos);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Erro interno do servidor: {ex.Message}");
        }
    }

    [HttpPut("{id}/status")]
    public async Task<ActionResult<PedidoDto>> UpdateStatus(int id, [FromBody] AtualizarStatusPedidoDto updateDto)
    {
        try
        {
            var pedido = await _pedidoService.UpdateStatusAsync(id, updateDto);
            if (pedido == null)
                return NotFound("Pedido não encontrado");

            return Ok(pedido);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Erro interno do servidor: {ex.Message}");
        }
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult> Delete(int id)
    {
        try
        {
            var sucesso = await _pedidoService.DeleteAsync(id);
            if (!sucesso)
                return NotFound("Pedido não encontrado");

            return NoContent();
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Erro interno do servidor: {ex.Message}");
        }
    }
}
