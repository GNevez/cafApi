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
    public async Task<ActionResult<List<ClienteDto>>> GetAll()
    {
        try
        {
            var clientes = await _context.Clientes
                .Where(c => c.Ativo)
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

