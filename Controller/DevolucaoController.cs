using cafApi.Contexts;
using cafApi.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace cafApi.Controller;

[ApiController]
[Route("api/[controller]")]
public class DevolucaoController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public DevolucaoController(ApplicationDbContext context)
    {
        _context = context;
    }

    public class CriarDevolucaoItemDto
    {
        public int ItemCarrinhoId { get; set; }
        public int Quantidade { get; set; }
    }

    public class CriarDevolucaoDto
    {
        public int PedidoId { get; set; }
        public string Cpf { get; set; } = null!;
        public string NomeCliente { get; set; } = null!;
        public string Email { get; set; } = null!;
        public List<CriarDevolucaoItemDto> Itens { get; set; } = new();
    }

    [HttpPost]
    public async Task<ActionResult<object>> Criar([FromBody] CriarDevolucaoDto dto)
    {
        if (dto == null) return BadRequest("Dados inválidos");
        if (dto.PedidoId <= 0) return BadRequest("PedidoId inválido");
        if (string.IsNullOrWhiteSpace(dto.Cpf)) return BadRequest("CPF é obrigatório");
        if (string.IsNullOrWhiteSpace(dto.NomeCliente)) return BadRequest("Nome é obrigatório");
        if (string.IsNullOrWhiteSpace(dto.Email)) return BadRequest("Email é obrigatório");
        if (dto.Itens == null || dto.Itens.Count == 0) return BadRequest("Selecione ao menos um item");

        var pedido = await _context.Pedidos
            .Include(p => p.Carrinho)
                .ThenInclude(c => c.Itens)
                    .ThenInclude(i => i.Produto)
            .Include(p => p.Carrinho)
                .ThenInclude(c => c.Itens)
                    .ThenInclude(i => i.Cor)
            .FirstOrDefaultAsync(p => p.Id == dto.PedidoId);

        if (pedido == null) return NotFound("Pedido não encontrado");

        // Validação básica do CPF com o cliente do pedido, se existir
        var cpfLimpo = dto.Cpf.Replace(".", "").Replace("-", "").Trim();
        if (pedido.ClienteId != 0)
        {
            var cliente = await _context.Clientes.FindAsync(pedido.ClienteId);
            if (cliente != null && !string.IsNullOrEmpty(cliente.Cpf))
            {
                if (!string.Equals(cliente.Cpf, cpfLimpo, StringComparison.Ordinal))
                {
                    return BadRequest(new { message = "CPF não corresponde ao titular do pedido" });
                }
            }
        }

        var devolucao = new Devolucao
        {
            PedidoId = pedido.Id,
            Cpf = cpfLimpo,
            NomeCliente = dto.NomeCliente,
            Email = dto.Email,
            Status = DevolucaoStatus.Solicitado,
            DataCriacao = DateTime.UtcNow
        };

        foreach (var itemDto in dto.Itens)
        {
            var itemPedido = pedido.Carrinho.Itens.FirstOrDefault(i => i.Id == itemDto.ItemCarrinhoId);
            if (itemPedido == null) return BadRequest($"Item {itemDto.ItemCarrinhoId} não pertence ao pedido");
            if (itemDto.Quantidade <= 0 || itemDto.Quantidade > itemPedido.Quantidade)
                return BadRequest($"Quantidade inválida para o item {itemDto.ItemCarrinhoId}");

            devolucao.Itens.Add(new DevolucaoItem
            {
                ItemCarrinhoId = itemPedido.Id,
                ProdutoId = itemPedido.ProdutoId,
                ProdutoNome = itemPedido.Produto.Nome,
                CorId = itemPedido.CorId,
                CorNome = itemPedido.Cor.Nome,
                Quantidade = itemDto.Quantidade
            });
        }

        _context.Devolucoes.Add(devolucao);
        await _context.SaveChangesAsync();

        return Ok(new { id = devolucao.Id, status = devolucao.Status.ToString() });
    }

    [HttpGet]
    public async Task<ActionResult<object>> Listar(
        [FromQuery] DevolucaoStatus? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 10;
        if (pageSize > 100) pageSize = 100;

        var query = _context.Devolucoes
            .Include(d => d.Itens)
            .AsQueryable();

        if (status.HasValue)
        {
            query = query.Where(d => d.Status == status.Value);
        }

        var totalCount = await query.CountAsync();
        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

        var items = await query
            .OrderByDescending(d => d.DataCriacao)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(d => new
            {
                d.Id,
                d.PedidoId,
                d.Cpf,
                NomeCliente = d.NomeCliente,
                d.Email,
                d.Status,
                DataCriacao = d.DataCriacao,
                DataAtualizacao = d.DataAtualizacao,
                Itens = d.Itens.Select(i => new
                {
                    i.Id,
                    DevolucaoId = i.DevolucaoId,
                    ItemCarrinhoId = i.ItemCarrinhoId,
                    ProdutoId = i.ProdutoId,
                    ProdutoNome = i.ProdutoNome,
                    CorId = i.CorId,
                    CorNome = i.CorNome,
                    Quantidade = i.Quantidade
                }).ToList()
            })
            .ToListAsync();

        return Ok(new
        {
            Items = items,
            PageNumber = page,
            PageSize = pageSize,
            TotalPages = totalPages,
            TotalCount = totalCount,
            HasPreviousPage = page > 1,
            HasNextPage = page < totalPages
        });
    }

    public class AtualizarStatusDto
    {
        public DevolucaoStatus Status { get; set; }
    }

    [HttpPut("{id}/status")]
    public async Task<ActionResult> AtualizarStatus(int id, [FromBody] AtualizarStatusDto dto)
    {
        if (dto == null) return BadRequest("Dados inválidos");

        var devolucao = await _context.Devolucoes.FindAsync(id);
        if (devolucao == null) return NotFound("Devolução não encontrada");

        devolucao.Status = dto.Status;
        devolucao.DataAtualizacao = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return Ok(new { message = "Status atualizado com sucesso", status = devolucao.Status.ToString() });
    }
}
