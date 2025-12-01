using cafApi.Contexts;
using cafApi.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace cafApi.Controller;

[ApiController]
[Route("api/[controller]")]
public class ClienteController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public ClienteController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<List<ClienteDto>>> GetAll([FromQuery] bool? ativo)
    {
        try
        {
            var query = _context.Clientes.AsQueryable();
            if (ativo.HasValue)
            {
                query = query.Where(c => c.Ativo == ativo.Value);
            }
            else
            {
                query = query.Where(c => c.Ativo);
            }

            var clientes = await query
                .Select(c => new ClienteDto
                {
                    Id = c.Id,
                    Nome = c.Nome,
                    Email = c.Email,
                    Cpf = c.Cpf,
                    Telefone = c.Telefone,
                    DataCriacao = c.DataCriacao,
                    DataAtualizacao = c.DataAtualizacao,
                    TotalPedidos = c.Pedidos.Count
                })
                .OrderByDescending(c => c.DataCriacao)
                .ToListAsync();

            return Ok(clientes);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Erro interno do servidor: {ex.Message}");
        }
    }

    [HttpPut("{id}/ativo")]
    public async Task<ActionResult> SetAtivo(int id, [FromBody] SetAtivoDto dto)
    {
        try
        {
            var cliente = await _context.Clientes.FindAsync(id);
            if (cliente == null) return NotFound("Cliente não encontrado");

            if (dto == null)
            {
                return BadRequest("Payload inválido");
            }

            cliente.Ativo = dto.Ativo;
            cliente.DataAtualizacao = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return Ok(new { ativo = cliente.Ativo });
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Erro interno do servidor: {ex.Message}");
        }
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ClienteDto>> GetById(int id)
    {
        try
        {
            var cliente = await _context.Clientes
                .Where(c => c.Id == id && c.Ativo)
                .Select(c => new ClienteDto
                {
                    Id = c.Id,
                    Nome = c.Nome,
                    Email = c.Email,
                    Cpf = c.Cpf,
                    Telefone = c.Telefone,
                    DataCriacao = c.DataCriacao,
                    DataAtualizacao = c.DataAtualizacao,
                    TotalPedidos = c.Pedidos.Count
                })
                .FirstOrDefaultAsync();

            if (cliente == null)
            {
                return NotFound("Cliente não encontrado");
            }

            return Ok(cliente);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Erro interno do servidor: {ex.Message}");
        }
    }

    [HttpGet("verificar-cpf/{cpf}")]
    public async Task<ActionResult<ClienteExistenteDto>> VerificarCpf(string cpf)
    {
        // Limpar CPF (remover caracteres especiais)
        var cpfLimpo = cpf.Replace(".", "").Replace("-", "").Trim();
        
        if (string.IsNullOrEmpty(cpfLimpo))
        {
            return NotFound();
        }

        var cliente = await _context.Clientes
            .FirstOrDefaultAsync(c => c.Cpf == cpfLimpo && c.Ativo);

        if (cliente == null)
        {
            return NotFound();
        }

        return Ok(new ClienteExistenteDto
        {
            Id = cliente.Id,
            Nome = cliente.Nome,
            Email = cliente.Email,
            Cpf = cliente.Cpf,
            Telefone = cliente.Telefone
        });
    }
}

public class ClienteExistenteDto
{
    public int Id { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Cpf { get; set; }
    public string? Telefone { get; set; }
}

public class ClienteDto
{
    public int Id { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Cpf { get; set; }
    public string? Telefone { get; set; }
    public DateTime DataCriacao { get; set; }
    public DateTime? DataAtualizacao { get; set; }
    public int TotalPedidos { get; set; }
}

public class SetAtivoDto
{
    // Uso PascalCase para compatibilidade do System.Text.Json; JSON "ativo" será mapeado para Ativo
    public bool Ativo { get; set; }
}

