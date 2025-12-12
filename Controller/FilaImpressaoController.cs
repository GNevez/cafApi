using cafApi.Models;
using cafApi.Models.DTOs;
using cafApi.Contexts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace cafApi.Controller;

[ApiController]
[Route("api/[controller]")]
public class FilaImpressaoController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<FilaImpressaoController> _logger;

    public FilaImpressaoController(ApplicationDbContext context, ILogger<FilaImpressaoController> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Busca próximos itens pendentes na fila de impressão
    /// Usado pelo cliente de impressão local
    /// </summary>
    [HttpGet("pendentes")]
    public async Task<ActionResult<List<FilaImpressaoDto>>> GetPendentes(
        [FromQuery] string? clienteId = null,
        [FromQuery] int limite = 10)
    {
        // Buscar itens pendentes ou com erro (para retry)
        var itens = await _context.FilaImpressao
            .Where(f => f.Status == StatusImpressao.Pendente || 
                       (f.Status == StatusImpressao.Erro && f.Tentativas < f.MaxTentativas))
            .OrderBy(f => f.DataCriacao)
            .Take(limite)
            .Select(f => new FilaImpressaoDto
            {
                Id = f.Id,
                RotuloId = f.RotuloId,
                PedidoId = f.PedidoId,
                CodigoPedido = f.CodigoPedido,
                NomeArquivo = f.NomeArquivo,
                CaminhoArquivo = f.CaminhoArquivo,
                Status = f.Status.ToString(),
                Tentativas = f.Tentativas,
                ImpressoraDestino = f.ImpressoraDestino,
                Copias = f.Copias,
                DataCriacao = f.DataCriacao
            })
            .ToListAsync();

        return Ok(itens);
    }

    /// <summary>
    /// Reserva um item da fila para impressão
    /// O cliente de impressão chama isso antes de imprimir
    /// </summary>
    [HttpPost("{id}/reservar")]
    public async Task<ActionResult<FilaImpressaoDto>> Reservar(int id, [FromQuery] string clienteId)
    {
        var item = await _context.FilaImpressao.FindAsync(id);
        if (item == null)
        {
            return NotFound(new { error = "Item não encontrado na fila" });
        }

        if (item.Status != StatusImpressao.Pendente && 
            !(item.Status == StatusImpressao.Erro && item.Tentativas < item.MaxTentativas))
        {
            return BadRequest(new { error = "Item não está disponível para impressão" });
        }

        item.Status = StatusImpressao.EmProcessamento;
        item.ClienteId = clienteId;
        item.DataProcessamento = DateTime.UtcNow;
        item.Tentativas++;

        await _context.SaveChangesAsync();

        _logger.LogInformation("[FilaImpressao] Item {Id} reservado pelo cliente {ClienteId}", id, clienteId);

        return Ok(new FilaImpressaoDto
        {
            Id = item.Id,
            RotuloId = item.RotuloId,
            PedidoId = item.PedidoId,
            CodigoPedido = item.CodigoPedido,
            NomeArquivo = item.NomeArquivo,
            CaminhoArquivo = item.CaminhoArquivo,
            Status = item.Status.ToString(),
            Tentativas = item.Tentativas,
            ImpressoraDestino = item.ImpressoraDestino,
            Copias = item.Copias,
            DataCriacao = item.DataCriacao
        });
    }

    /// <summary>
    /// Baixa o arquivo PDF do rótulo para impressão
    /// </summary>
    [HttpGet("{id}/download")]
    public async Task<ActionResult> Download(int id)
    {
        var item = await _context.FilaImpressao.FindAsync(id);
        if (item == null)
        {
            return NotFound(new { error = "Item não encontrado" });
        }

        var caminhoCompleto = Path.Combine("wwwroot", item.CaminhoArquivo.TrimStart('/'));
        if (!System.IO.File.Exists(caminhoCompleto))
        {
            return NotFound(new { error = "Arquivo não encontrado no servidor" });
        }

        var bytes = await System.IO.File.ReadAllBytesAsync(caminhoCompleto);
        return File(bytes, "application/pdf", item.NomeArquivo);
    }

    /// <summary>
    /// Confirma que a impressão foi realizada com sucesso
    /// </summary>
    [HttpPost("{id}/confirmar")]
    public async Task<ActionResult> Confirmar(int id, [FromQuery] string clienteId)
    {
        var item = await _context.FilaImpressao.FindAsync(id);
        if (item == null)
        {
            return NotFound(new { error = "Item não encontrado" });
        }

        if (item.ClienteId != clienteId)
        {
            return BadRequest(new { error = "Este item foi reservado por outro cliente" });
        }

        item.Status = StatusImpressao.Impresso;
        item.DataImpressao = DateTime.UtcNow;
        item.MensagemErro = null;

        await _context.SaveChangesAsync();

        _logger.LogInformation("[FilaImpressao] ✓ Item {Id} impresso com sucesso pelo cliente {ClienteId}", id, clienteId);

        return Ok(new { message = "Impressão confirmada com sucesso" });
    }

    /// <summary>
    /// Reporta erro na impressão
    /// </summary>
    [HttpPost("{id}/erro")]
    public async Task<ActionResult> ReportarErro(int id, [FromBody] ReportarErroDto dto)
    {
        var item = await _context.FilaImpressao.FindAsync(id);
        if (item == null)
        {
            return NotFound(new { error = "Item não encontrado" });
        }

        item.Status = item.Tentativas >= item.MaxTentativas ? StatusImpressao.Erro : StatusImpressao.Pendente;
        item.MensagemErro = dto.MensagemErro;
        item.ClienteId = null; // Liberar para outro cliente tentar

        await _context.SaveChangesAsync();

        _logger.LogWarning("[FilaImpressao] ✗ Erro na impressão do item {Id}: {Erro}", id, dto.MensagemErro);

        return Ok(new { 
            message = "Erro reportado",
            podeRetentar = item.Tentativas < item.MaxTentativas
        });
    }

    /// <summary>
    /// Cancelar item da fila
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<ActionResult> Cancelar(int id)
    {
        var item = await _context.FilaImpressao.FindAsync(id);
        if (item == null)
        {
            return NotFound(new { error = "Item não encontrado" });
        }

        item.Status = StatusImpressao.Cancelado;
        await _context.SaveChangesAsync();

        _logger.LogInformation("[FilaImpressao] Item {Id} cancelado", id);

        return Ok(new { message = "Item cancelado" });
    }

    /// <summary>
    /// Lista todos os itens da fila com filtros
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<FilaImpressaoPaginadoDto>> GetAll(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? status = null,
        [FromQuery] DateTime? dataInicio = null,
        [FromQuery] DateTime? dataFim = null)
    {
        var query = _context.FilaImpressao.AsQueryable();

        if (!string.IsNullOrEmpty(status) && Enum.TryParse<StatusImpressao>(status, out var statusEnum))
        {
            query = query.Where(f => f.Status == statusEnum);
        }

        if (dataInicio.HasValue)
        {
            query = query.Where(f => f.DataCriacao >= dataInicio.Value);
        }

        if (dataFim.HasValue)
        {
            query = query.Where(f => f.DataCriacao <= dataFim.Value.AddDays(1));
        }

        var totalCount = await query.CountAsync();
        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

        var itens = await query
            .OrderByDescending(f => f.DataCriacao)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(f => new FilaImpressaoDto
            {
                Id = f.Id,
                RotuloId = f.RotuloId,
                PedidoId = f.PedidoId,
                CodigoPedido = f.CodigoPedido,
                NomeArquivo = f.NomeArquivo,
                CaminhoArquivo = f.CaminhoArquivo,
                Status = f.Status.ToString(),
                Tentativas = f.Tentativas,
                MensagemErro = f.MensagemErro,
                ImpressoraDestino = f.ImpressoraDestino,
                Copias = f.Copias,
                DataCriacao = f.DataCriacao,
                DataProcessamento = f.DataProcessamento,
                DataImpressao = f.DataImpressao,
                ClienteId = f.ClienteId
            })
            .ToListAsync();

        return Ok(new FilaImpressaoPaginadoDto
        {
            Items = itens,
            TotalCount = totalCount,
            PageNumber = pageNumber,
            PageSize = pageSize,
            TotalPages = totalPages
        });
    }

    /// <summary>
    /// Estatísticas da fila de impressão
    /// </summary>
    [HttpGet("estatisticas")]
    public async Task<ActionResult> GetEstatisticas()
    {
        var stats = await _context.FilaImpressao
            .GroupBy(f => f.Status)
            .Select(g => new { Status = g.Key.ToString(), Count = g.Count() })
            .ToListAsync();

        var hoje = DateTime.UtcNow.Date;
        var impressosHoje = await _context.FilaImpressao
            .CountAsync(f => f.Status == StatusImpressao.Impresso && f.DataImpressao >= hoje);

        return Ok(new
        {
            porStatus = stats,
            impressosHoje = impressosHoje,
            totalPendentes = stats.FirstOrDefault(s => s.Status == "Pendente")?.Count ?? 0,
            totalErros = stats.FirstOrDefault(s => s.Status == "Erro")?.Count ?? 0
        });
    }

    /// <summary>
    /// Adicionar item manualmente à fila (para reimprimir rótulos existentes)
    /// </summary>
    [HttpPost("adicionar")]
    public async Task<ActionResult<FilaImpressaoDto>> AdicionarManual([FromBody] AdicionarFilaImpressaoDto dto)
    {
        // Verificar se o rótulo existe
        var rotulo = await _context.Rotulos.FindAsync(dto.RotuloId);
        if (rotulo == null)
        {
            return NotFound(new { error = "Rótulo não encontrado" });
        }

        var item = new FilaImpressao
        {
            RotuloId = rotulo.Id,
            PedidoId = rotulo.IdPedido,
            NomeArquivo = rotulo.NomeArquivo,
            CaminhoArquivo = rotulo.CaminhoArquivo,
            ImpressoraDestino = dto.ImpressoraDestino,
            Copias = dto.Copias ?? 1,
            Status = StatusImpressao.Pendente
        };

        _context.FilaImpressao.Add(item);
        await _context.SaveChangesAsync();

        _logger.LogInformation("[FilaImpressao] Item adicionado manualmente - Rótulo {RotuloId}", dto.RotuloId);

        return Ok(new FilaImpressaoDto
        {
            Id = item.Id,
            RotuloId = item.RotuloId,
            PedidoId = item.PedidoId,
            NomeArquivo = item.NomeArquivo,
            CaminhoArquivo = item.CaminhoArquivo,
            Status = item.Status.ToString(),
            Copias = item.Copias,
            DataCriacao = item.DataCriacao
        });
    }
}

// DTOs
public class FilaImpressaoDto
{
    public int Id { get; set; }
    public int? RotuloId { get; set; }
    public int? PedidoId { get; set; }
    public string? CodigoPedido { get; set; }
    public string NomeArquivo { get; set; } = string.Empty;
    public string CaminhoArquivo { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int Tentativas { get; set; }
    public string? MensagemErro { get; set; }
    public string? ImpressoraDestino { get; set; }
    public int Copias { get; set; }
    public DateTime DataCriacao { get; set; }
    public DateTime? DataProcessamento { get; set; }
    public DateTime? DataImpressao { get; set; }
    public string? ClienteId { get; set; }
}

public class FilaImpressaoPaginadoDto
{
    public List<FilaImpressaoDto> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int PageNumber { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
}

public class ReportarErroDto
{
    public string? MensagemErro { get; set; }
    public string? ClienteId { get; set; }
}

public class AdicionarFilaImpressaoDto
{
    public int RotuloId { get; set; }
    public string? ImpressoraDestino { get; set; }
    public int? Copias { get; set; }
}
