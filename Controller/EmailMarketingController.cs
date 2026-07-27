using cafApi.Models.DTOs;
using cafApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace cafApi.Controller;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class EmailMarketingController : ControllerBase
{
    private readonly IEmailService _emailService;
    private readonly ILogger<EmailMarketingController> _logger;

    public EmailMarketingController(
        IEmailService emailService,
        ILogger<EmailMarketingController> logger)
    {
        _emailService = emailService;
        _logger = logger;
    }

    /// <summary>
    /// Conta destinatários por segmento
    /// </summary>
    [HttpGet("destinatarios/{segmento}")]
    public async Task<ActionResult<int>> ContarDestinatarios(SegmentoCliente segmento)
    {
        try
        {
            var total = await _emailService.ContarDestinatariosPorSegmentoAsync(segmento);
            return Ok(total);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Erro ao contar destinatários: {ex.Message}");
            return StatusCode(500, "Erro ao contar destinatários");
        }
    }

    /// <summary>
    /// Conta todos os segmentos
    /// </summary>
    [HttpGet("destinatarios")]
    public async Task<ActionResult<object>> ContarTodosSegmentos()
    {
        try
        {
            var todos = await _emailService.ContarDestinatariosPorSegmentoAsync(SegmentoCliente.Todos);
            var compradores = await _emailService.ContarDestinatariosPorSegmentoAsync(SegmentoCliente.Compradores);
            var naoCompradores = await _emailService.ContarDestinatariosPorSegmentoAsync(SegmentoCliente.NaoCompradores);
            
            return Ok(new
            {
                todos,
                compradores,
                naoCompradores
            });
        }
        catch (Exception ex)
        {
            _logger.LogError($"Erro ao contar destinatários: {ex.Message}");
            return StatusCode(500, "Erro ao contar destinatários");
        }
    }

    /// <summary>
    /// Gera preview do email marketing
    /// </summary>
    [HttpPost("preview")]
    public async Task<ActionResult<PreviewEmailDto>> GerarPreview([FromBody] EmailMarketingDto dto)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(dto.Titulo))
                return BadRequest("Título é obrigatório");
            
            if (string.IsNullOrWhiteSpace(dto.Corpo))
                return BadRequest("Corpo do email é obrigatório");

            var preview = await _emailService.GerarPreviewEmailMarketingAsync(dto);
            return Ok(preview);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Erro ao gerar preview: {ex.Message}");
            return StatusCode(500, "Erro ao gerar preview");
        }
    }

    /// <summary>
    /// Envia email marketing para o segmento selecionado
    /// </summary>
    [HttpPost("enviar")]
    public async Task<ActionResult<EmailMarketingResultDto>> EnviarEmailMarketing([FromBody] EmailMarketingDto dto)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(dto.Titulo))
                return BadRequest("Título é obrigatório");
            
            if (string.IsNullOrWhiteSpace(dto.Corpo))
                return BadRequest("Corpo do email é obrigatório");

            _logger.LogInformation($"[EmailMarketing] Iniciando campanha: {dto.Titulo}");
            
            var result = await _emailService.EnviarEmailMarketingAsync(dto);
            
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Erro ao enviar email marketing: {ex.Message}");
            return StatusCode(500, $"Erro ao enviar email marketing: {ex.Message}");
        }
    }
}
