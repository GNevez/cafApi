using cafApi.Attributes;
using cafApi.Models.DTOs;
using cafApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace cafApi.Controller;

/// <summary>
/// Controller para operações de NF-e (Nota Fiscal Eletrônica)
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
[RequireAdmin]
public class NfeController : ControllerBase
{
    private readonly INfeService _nfeService;
    private readonly ILogger<NfeController> _logger;

    public NfeController(INfeService nfeService, ILogger<NfeController> logger)
    {
        _nfeService = nfeService;
        _logger = logger;
    }

    /// <summary>
    /// Verifica se o serviço de NF-e está configurado
    /// </summary>
    [HttpGet("status")]
    public IActionResult GetStatus()
    {
        var configurado = _nfeService.IsConfigured();
        
        return Ok(new
        {
            configurado,
            mensagem = configurado 
                ? "Serviço de NF-e configurado e pronto para uso"
                : "Serviço de NF-e não está configurado. Verifique as configurações no appsettings.json e o certificado digital."
        });
    }

    /// <summary>
    /// Emite uma NF-e para um pedido
    /// </summary>
    [HttpPost("emitir")]
    public async Task<IActionResult> Emitir([FromBody] EmitirNfeDto dto)
    {
        _logger.LogInformation("[NfeController] Emitindo NF-e para pedido {PedidoId}", dto.PedidoId);
        
        var resultado = await _nfeService.EmitirNfeAsync(dto.PedidoId, dto.NaturezaOperacao);
        
        if (resultado.Sucesso)
        {
            return Ok(resultado);
        }
        
        return BadRequest(resultado);
    }

    /// <summary>
    /// Consulta uma NF-e pela chave de acesso
    /// </summary>
    [HttpGet("consultar/{chaveAcesso}")]
    public async Task<IActionResult> Consultar(string chaveAcesso)
    {
        if (string.IsNullOrEmpty(chaveAcesso) || chaveAcesso.Length != 44)
        {
            return BadRequest(new { mensagem = "Chave de acesso inválida. Deve ter 44 dígitos." });
        }
        
        var resultado = await _nfeService.ConsultarNfeAsync(chaveAcesso);
        
        if (resultado.Sucesso)
        {
            return Ok(resultado);
        }
        
        return NotFound(resultado);
    }

    /// <summary>
    /// Cancela uma NF-e autorizada
    /// </summary>
    [HttpPost("cancelar")]
    public async Task<IActionResult> Cancelar([FromBody] CancelarNfeDto dto)
    {
        if (string.IsNullOrEmpty(dto.Motivo) || dto.Motivo.Length < 15)
        {
            return BadRequest(new { mensagem = "Motivo do cancelamento deve ter no mínimo 15 caracteres." });
        }
        
        _logger.LogInformation("[NfeController] Cancelando NF-e {Id}", dto.NotaFiscalId);
        
        var resultado = await _nfeService.CancelarNfeAsync(dto.NotaFiscalId, dto.Motivo);
        
        if (resultado.Sucesso)
        {
            return Ok(resultado);
        }
        
        return BadRequest(resultado);
    }

    /// <summary>
    /// Inutiliza uma faixa de numeração de NF-e
    /// </summary>
    [HttpPost("inutilizar")]
    public async Task<IActionResult> Inutilizar([FromBody] InutilizarNfeDto dto)
    {
        if (string.IsNullOrEmpty(dto.Justificativa) || dto.Justificativa.Length < 15)
        {
            return BadRequest(new { mensagem = "Justificativa deve ter no mínimo 15 caracteres." });
        }
        
        if (dto.NumeroInicial > dto.NumeroFinal)
        {
            return BadRequest(new { mensagem = "Número inicial deve ser menor ou igual ao número final." });
        }
        
        _logger.LogInformation("[NfeController] Inutilizando numeração de {Inicio} a {Fim}", 
            dto.NumeroInicial, dto.NumeroFinal);
        
        var resultado = await _nfeService.InutilizarNumeracaoAsync(
            dto.Serie, dto.NumeroInicial, dto.NumeroFinal, dto.Justificativa);
        
        if (resultado.Sucesso)
        {
            return Ok(resultado);
        }
        
        return BadRequest(resultado);
    }

    /// <summary>
    /// Lista todas as NF-e de um pedido
    /// </summary>
    [HttpGet("pedido/{pedidoId}")]
    public async Task<IActionResult> ListarPorPedido(int pedidoId)
    {
        var notas = await _nfeService.ListarPorPedidoAsync(pedidoId);
        return Ok(notas);
    }

    /// <summary>
    /// Obtém o XML de uma NF-e
    /// </summary>
    [HttpGet("{notaFiscalId}/xml")]
    public async Task<IActionResult> ObterXml(int notaFiscalId)
    {
        var xml = await _nfeService.ObterXmlAsync(notaFiscalId);
        
        if (string.IsNullOrEmpty(xml))
        {
            return NotFound(new { mensagem = "XML não encontrado" });
        }
        
        return Content(xml, "application/xml");
    }

    /// <summary>
    /// Gera e retorna o DANFE em PDF (autenticado - admin)
    /// </summary>
    [HttpGet("{notaFiscalId}/danfe")]
    public async Task<IActionResult> GerarDanfe(int notaFiscalId)
    {
        var pdf = await _nfeService.GerarDanfeAsync(notaFiscalId);
        
        if (pdf == null)
        {
            return NotFound(new { mensagem = "Não foi possível gerar o DANFE" });
        }
        
        return File(pdf, "application/pdf", $"DANFE_{notaFiscalId}.pdf");
    }

    /// <summary>
    /// Gera e retorna o DANFE em PDF (público - acesso via chave de acesso)
    /// A chave de acesso tem 44 dígitos e é única para cada NF-e
    /// </summary>
    [HttpGet("danfe/{chaveAcesso}")]
    [AllowAnonymous]
    public async Task<IActionResult> GerarDanfePublico(string chaveAcesso)
    {
        // Validar formato da chave de acesso (44 dígitos numéricos)
        if (string.IsNullOrEmpty(chaveAcesso) || chaveAcesso.Length != 44 || !chaveAcesso.All(char.IsDigit))
        {
            return BadRequest(new { mensagem = "Chave de acesso inválida" });
        }
        
        var pdf = await _nfeService.GerarDanfePorChaveAcessoAsync(chaveAcesso);
        
        if (pdf == null)
        {
            return NotFound(new { mensagem = "DANFE não encontrado" });
        }
        
        return File(pdf, "application/pdf", $"DANFE_{chaveAcesso}.pdf");
    }

    /// <summary>
    /// Obtém o próximo número de NF-e disponível
    /// </summary>
    [HttpGet("proximo-numero")]
    public async Task<IActionResult> ProximoNumero([FromQuery] int serie = 1)
    {
        var numero = await _nfeService.ObterProximoNumeroAsync(serie);
        return Ok(new { proximoNumero = numero, serie });
    }

    /// <summary>
    /// Lista todos os pedidos com informações de NF-e
    /// </summary>
    [HttpGet("pedidos")]
    public async Task<IActionResult> ListarPedidos(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? busca = null)
    {
        var resultado = await _nfeService.ListarTodosPedidosNfeAsync(pageNumber, pageSize, busca);
        return Ok(resultado);
    }

    /// <summary>
    /// Lista pedidos COM nota fiscal emitida
    /// </summary>
    [HttpGet("pedidos/com-nota")]
    public async Task<IActionResult> ListarPedidosComNota(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? busca = null)
    {
        var resultado = await _nfeService.ListarPedidosComNfeAsync(pageNumber, pageSize, busca);
        return Ok(resultado);
    }

    /// <summary>
    /// Lista pedidos SEM nota fiscal emitida
    /// </summary>
    [HttpGet("pedidos/sem-nota")]
    public async Task<IActionResult> ListarPedidosSemNota(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? busca = null)
    {
        var resultado = await _nfeService.ListarPedidosSemNfeAsync(pageNumber, pageSize, busca);
        return Ok(resultado);
    }

    /// <summary>
    /// Obtém estatísticas de NF-e
    /// </summary>
    [HttpGet("estatisticas")]
    public async Task<IActionResult> ObterEstatisticas()
    {
        var resultado = await _nfeService.ObterEstatisticasAsync();
        return Ok(resultado);
    }

    /// <summary>
    /// Envia NF-e por email para o cliente
    /// </summary>
    [HttpPost("{notaFiscalId}/enviar-email")]
    public async Task<IActionResult> EnviarEmail(int notaFiscalId, [FromBody] EnviarNfeEmailDto? dto = null)
    {
        _logger.LogInformation("[NfeController] Enviando NF-e {Id} por email", notaFiscalId);
        
        var resultado = await _nfeService.EnviarNfePorEmailAsync(notaFiscalId, dto?.Email);
        
        if (resultado.Sucesso)
        {
            return Ok(resultado);
        }
        
        return BadRequest(resultado);
    }

    /// <summary>
    /// Utilitário para criptografar a senha do certificado
    /// Use este endpoint UMA VEZ para gerar os valores criptografados
    /// </summary>
    [HttpPost("util/criptografar-senha")]
    public IActionResult CriptografarSenha([FromBody] CriptografarSenhaDto dto)
    {
        if (string.IsNullOrEmpty(dto.Senha))
        {
            return BadRequest(new { mensagem = "Senha não informada" });
        }
        
        var (senhaCriptografada, chave) = NfeService.CriptografarSenha(dto.Senha);
        
        return Ok(new
        {
            instrucoes = "Guarde esses valores em local seguro!",
            senhaCriptografada,
            chave,
            configuracao = new
            {
                appsettings = "Adicione 'SenhaCriptografada' na seção NFe:Certificado",
                variavelAmbiente = "Defina NFE_ENCRYPTION_KEY com o valor da chave"
            }
        });
    }
}

public class CriptografarSenhaDto
{
    public string Senha { get; set; } = string.Empty;
}

public class EnviarNfeEmailDto
{
    public string? Email { get; set; }
}
