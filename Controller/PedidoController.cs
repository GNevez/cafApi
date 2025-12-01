using cafApi.Models;
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
    public async Task<ActionResult<object>> GetAll([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 7)
    {
        try
        {
            if (pageNumber < 1) pageNumber = 1;
            if (pageSize < 1 || pageSize > 100) pageSize = 7;

            var (pedidos, totalCount) = await _pedidoService.GetAllAsync(pageNumber, pageSize);

            var response = new
            {
                items = pedidos,
                totalCount = totalCount,
                pageNumber = pageNumber,
                pageSize = pageSize,
                totalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Erro interno do servidor: {ex.Message}");
        }
    }

    [HttpGet("by-cpf")]
    public async Task<ActionResult<List<PedidoDto>>> GetByCpf([FromQuery] string cpf)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(cpf))
                return BadRequest("CPF é obrigatório");

            var pedidos = await _pedidoService.GetByCpfAsync(cpf);
            return Ok(pedidos);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Erro interno do servidor: {ex.Message}");
        }
    }

    [HttpGet("{id:int}")]
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

    [HttpGet("codigo/{codigoPedido}")]
    public async Task<ActionResult<PedidoDto>> GetByCodigoPedido(string codigoPedido)
    {
        try
        {
            var pedido = await _pedidoService.GetByCodigoPedidoAsync(codigoPedido);
            if (pedido == null)
                return NotFound("Pedido não encontrado");

            return Ok(pedido);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Erro interno do servidor: {ex.Message}");
        }
    }

    [HttpGet("cliente/{clienteId:int}")]
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

    [HttpGet("status/{status:int}")]
    public async Task<ActionResult<object>> GetByStatus(int status, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 7)
    {
        try
        {
            if (!Enum.IsDefined(typeof(StatusPedido), status))
            {
                return BadRequest("Status inválido");
            }

            if (pageNumber < 1) pageNumber = 1;
            if (pageSize < 1 || pageSize > 100) pageSize = 7;

            var (pedidos, totalCount) = await _pedidoService.GetByStatusAsync((StatusPedido)status, pageNumber, pageSize);

            var response = new
            {
                items = pedidos,
                totalCount = totalCount,
                pageNumber = pageNumber,
                pageSize = pageSize,
                totalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Erro interno do servidor: {ex.Message}");
        }
    }

    [HttpPut("{id:int}/status")]
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

    [HttpPut("codigo/{codigoPedido}/status")]
    public async Task<ActionResult<PedidoDto>> UpdateStatusByCodigoPedido(string codigoPedido, [FromBody] AtualizarStatusPedidoDto updateDto)
    {
        try
        {
            var pedido = await _pedidoService.UpdateStatusByCodigoPedidoAsync(codigoPedido, updateDto);
            if (pedido == null)
                return NotFound("Pedido não encontrado");

            return Ok(pedido);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Erro interno do servidor: {ex.Message}");
        }
    }

    [HttpDelete("{id:int}")]
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
