using cafApi.Models.DTOs;
using cafApi.Services;
using Microsoft.AspNetCore.Mvc;

namespace cafApi.Controller;

/// <summary>
/// Controller para receber eventos do webhook dos Correios.
/// Este endpoint é chamado automaticamente pelos Correios quando há
/// atualizações no rastreamento de objetos.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class CorreiosWebhookController : ControllerBase
{
    private readonly ICorreiosWebhookService _webhookService;
    private readonly ILogger<CorreiosWebhookController> _logger;
    private readonly IConfiguration _configuration;

    public CorreiosWebhookController(
        ICorreiosWebhookService webhookService,
        ILogger<CorreiosWebhookController> logger,
        IConfiguration configuration)
    {
        _webhookService = webhookService;
        _logger = logger;
        _configuration = configuration;
    }

    /// <summary>
    /// Endpoint principal para receber eventos do webhook dos Correios.
    /// 
    /// Mapeamento de eventos → Status do sistema:
    /// 
    /// 📦 PEDIDOS (Envio para Cliente):
    /// - PO-1, OEC-1, RO-1...     → Status: A Caminho
    /// - BDE-1, BDE-67, BDE-68... → Status: Finalizado (Entregue)
    /// - BDE-2 a BDE-10...        → Notificação de problema
    /// 
    /// 📦↩️ DEVOLUÇÕES (Cliente envia para Loja):
    /// - PO-1, CO-1              → Status: Enviado
    /// - BDE-14, BDE-23...       → Status: Em Análise (Chegou na loja)
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<CorreiosWebhookResponse>> ReceberEvento([FromBody] CorreiosWebhookPayload payload)
    {
        try
        {
            _logger.LogInformation("[Webhook Correios] ➡️ Evento recebido: {TipoEvento} para {CodigoObjeto}",
                payload.TipoEvento, payload.CodigoObjeto);

            // Validar payload
            if (string.IsNullOrEmpty(payload.CodigoObjeto) || string.IsNullOrEmpty(payload.TipoEvento))
            {
                _logger.LogWarning("[Webhook Correios] ⚠️ Payload inválido recebido");
                return BadRequest(new CorreiosWebhookResponse
                {
                    Sucesso = false,
                    Mensagem = "Payload inválido: CodigoObjeto e TipoEvento são obrigatórios"
                });
            }

            // Processar evento
            var resultado = await _webhookService.ProcessarEventoAsync(payload);

            var response = new CorreiosWebhookResponse
            {
                Sucesso = resultado.Processado,
                CodigoObjeto = payload.CodigoObjeto,
                TipoEvento = payload.TipoEvento,
                AcaoTomada = resultado.AcaoTomada,
                Mensagem = resultado.Processado
                    ? $"Evento processado com sucesso para {resultado.TipoObjeto}"
                    : "Evento recebido mas não processado"
            };

            _logger.LogInformation("[Webhook Correios] ✅ Processamento concluído: {AcaoTomada}", resultado.AcaoTomada);

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Webhook Correios] ❌ Erro ao processar evento: {TipoEvento} para {CodigoObjeto}",
                payload.TipoEvento, payload.CodigoObjeto);

            // Retornar 200 mesmo em caso de erro para não causar retentativas infinitas
            return Ok(new CorreiosWebhookResponse
            {
                Sucesso = false,
                CodigoObjeto = payload.CodigoObjeto,
                TipoEvento = payload.TipoEvento,
                Mensagem = $"Erro interno ao processar evento: {ex.Message}"
            });
        }
    }

    /// <summary>
    /// Endpoint para processar múltiplos eventos de uma vez (batch).
    /// Alguns sistemas enviam eventos em lote.
    /// </summary>
    [HttpPost("batch")]
    public async Task<ActionResult<List<CorreiosWebhookResponse>>> ReceberEventosBatch([FromBody] List<CorreiosWebhookPayload> payloads)
    {
        var responses = new List<CorreiosWebhookResponse>();

        foreach (var payload in payloads)
        {
            try
            {
                if (string.IsNullOrEmpty(payload.CodigoObjeto) || string.IsNullOrEmpty(payload.TipoEvento))
                {
                    responses.Add(new CorreiosWebhookResponse
                    {
                        Sucesso = false,
                        CodigoObjeto = payload.CodigoObjeto,
                        TipoEvento = payload.TipoEvento,
                        Mensagem = "Payload inválido"
                    });
                    continue;
                }

                var resultado = await _webhookService.ProcessarEventoAsync(payload);

                responses.Add(new CorreiosWebhookResponse
                {
                    Sucesso = resultado.Processado,
                    CodigoObjeto = payload.CodigoObjeto,
                    TipoEvento = payload.TipoEvento,
                    AcaoTomada = resultado.AcaoTomada
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Webhook Correios] Erro no batch para {CodigoObjeto}", payload.CodigoObjeto);
                responses.Add(new CorreiosWebhookResponse
                {
                    Sucesso = false,
                    CodigoObjeto = payload.CodigoObjeto,
                    TipoEvento = payload.TipoEvento,
                    Mensagem = ex.Message
                });
            }
        }

        return Ok(responses);
    }

    /// <summary>
    /// Endpoint para teste manual do webhook (útil para desenvolvimento).
    /// Permite simular eventos sem precisar dos Correios.
    /// </summary>
    [HttpPost("test")]
    public async Task<ActionResult<CorreiosWebhookResponse>> TestarEvento([FromBody] CorreiosWebhookPayload payload)
    {
        _logger.LogInformation("[Webhook Correios] 🧪 TESTE - Evento simulado: {TipoEvento} para {CodigoObjeto}",
            payload.TipoEvento, payload.CodigoObjeto);

        return await ReceberEvento(payload);
    }

    /// <summary>
    /// Endpoint GET para verificação de saúde do webhook (ping).
    /// Alguns sistemas de webhook fazem GET para verificar se o endpoint está ativo.
    /// </summary>
    [HttpGet]
    public ActionResult<object> VerificarWebhook()
    {
        return Ok(new
        {
            status = "online",
            service = "Chase a Flare - Correios Webhook",
            timestamp = DateTime.UtcNow,
            endpoints = new
            {
                post = "/api/CorreiosWebhook - Receber evento único",
                postBatch = "/api/CorreiosWebhook/batch - Receber múltiplos eventos",
                postTest = "/api/CorreiosWebhook/test - Testar evento manualmente"
            }
        });
    }

    /// <summary>
    /// Endpoint para consultar o mapeamento de eventos suportados.
    /// Útil para documentação e debug.
    /// </summary>
    [HttpGet("eventos")]
    public ActionResult<object> ListarEventosSuportados()
    {
        return Ok(new
        {
            pedidos = new
            {
                entregueAoDestinatario = new[]
                {
                    new { tipo = "BDE-1", descricao = "Objeto entregue ao destinatário", acao = "Pedido → Finalizado" },
                    new { tipo = "BDE-67", descricao = "Objeto entregue ao destinatário", acao = "Pedido → Finalizado" },
                    new { tipo = "BDE-68", descricao = "Objeto entregue na Caixa de Correios Inteligente", acao = "Pedido → Finalizado" },
                    new { tipo = "BDE-70", descricao = "Objeto entregue ao destinatário", acao = "Pedido → Finalizado" },
                    new { tipo = "BDE-77", descricao = "Objeto disponível em locker", acao = "Pedido → Finalizado" }
                },
                emTransito = new[]
                {
                    new { tipo = "PO-1", descricao = "Objeto postado", acao = "Pedido → A Caminho" },
                    new { tipo = "OEC-1", descricao = "Objeto saiu para entrega ao destinatário", acao = "Pedido → A Caminho" },
                    new { tipo = "OEC-3", descricao = "Objeto está em rota de entrega", acao = "Pedido → A Caminho" },
                    new { tipo = "RO-1", descricao = "Objeto em trânsito", acao = "Pedido → A Caminho" },
                    new { tipo = "BDE-15", descricao = "Recebido na unidade de distribuição", acao = "Pedido → A Caminho" }
                },
                problemas = new[]
                {
                    new { tipo = "BDE-2", descricao = "Objeto não entregue - carteiro não atendido", acao = "Notificação (sem mudança de status)" },
                    new { tipo = "BDE-4", descricao = "Cliente recusou receber", acao = "Notificação (sem mudança de status)" },
                    new { tipo = "BDE-7", descricao = "Endereço incorreto", acao = "Notificação (sem mudança de status)" },
                    new { tipo = "BDE-10", descricao = "Cliente mudou-se", acao = "Notificação (sem mudança de status)" }
                },
                criticos = new[]
                {
                    new { tipo = "BDE-28", descricao = "Objeto e/ou conteúdo avariado", acao = "Alerta crítico registrado" },
                    new { tipo = "BDE-50", descricao = "Objeto roubado dos Correios", acao = "Alerta crítico registrado" },
                    new { tipo = "BDE-80", descricao = "Objeto não localizado no fluxo postal", acao = "Alerta crítico registrado" }
                }
            },
            devolucoes = new
            {
                clienteEnviou = new[]
                {
                    new { tipo = "PO-1", descricao = "Objeto postado (cliente enviou)", acao = "Devolução → Enviado" },
                    new { tipo = "CO-1", descricao = "Objeto coletado", acao = "Devolução → Enviado" }
                },
                chegouNaLoja = new[]
                {
                    new { tipo = "BDE-14", descricao = "Objeto entregue ao remetente", acao = "Devolução → Em Análise" },
                    new { tipo = "BDE-23", descricao = "Objeto entregue ao remetente", acao = "Devolução → Em Análise" },
                    new { tipo = "BDR-79", descricao = "Objeto entregue ao contratante", acao = "Devolução → Em Análise" }
                }
            }
        });
    }
}
