using cafApi.Contexts;
using cafApi.Models;
using cafApi.Models.DTOs;
using Microsoft.EntityFrameworkCore;

namespace cafApi.Services
{
    public interface ICorreiosWebhookService
    {
        Task<EventoProcessadoResult> ProcessarEventoAsync(CorreiosWebhookPayload payload);
    }

    /// <summary>
    /// Serviço responsável por processar os eventos do webhook dos Correios
    /// e atualizar os status de Pedidos e Devoluções automaticamente.
    /// 
    /// ╔══════════════════════════════════════════════════════════════════════════════╗
    /// ║                    MAPEAMENTO DE EVENTOS DOS CORREIOS                        ║
    /// ╠══════════════════════════════════════════════════════════════════════════════╣
    /// ║                                                                              ║
    /// ║  📦 PEDIDOS (Envio Loja → Cliente)                                           ║
    /// ║  ─────────────────────────────────────────────────────────────────────────── ║
    /// ║                                                                              ║
    /// ║  Status: A CAMINHO (mantém ou atualiza)                                      ║
    /// ║  • PO-1    → Objeto postado                                                  ║
    /// ║  • OEC-1   → Objeto saiu para entrega ao destinatário                        ║
    /// ║  • OEC-3   → Objeto está em rota de entrega                                  ║
    /// ║  • RO-1    → Objeto em trânsito                                              ║
    /// ║  • DO-1/2  → Objeto em trânsito                                              ║
    /// ║  • TRI-0   → Objeto encaminhado                                              ║
    /// ║  • BDE-15  → Recebido na unidade de distribuição                             ║
    /// ║  • BDE-45  → Objeto recebido na unidade de distribuição                      ║
    /// ║                                                                              ║
    /// ║  Status: FINALIZADO (Entregue)                                               ║
    /// ║  • BDE-1   → Objeto entregue ao destinatário                                 ║
    /// ║  • BDE-67  → Objeto entregue ao destinatário                                 ║
    /// ║  • BDE-68  → Objeto entregue na Caixa de Correios Inteligente                ║
    /// ║  • BDE-70  → Objeto entregue ao destinatário                                 ║
    /// ║  • BDI-1   → Objeto entregue ao destinatário                                 ║
    /// ║  • BDI-67  → Objeto entregue ao destinatário                                 ║
    /// ║  • BDI-68  → Objeto entregue na Caixa de Correios Inteligente                ║
    /// ║  • BDI-70  → Objeto entregue ao destinatário                                 ║
    /// ║  • BDR-1   → Objeto entregue ao destinatário                                 ║
    /// ║  • BDR-67  → Objeto entregue ao destinatário                                 ║
    /// ║  • BDR-68  → Objeto entregue na Caixa de Correios Inteligente                ║
    /// ║  • BDR-70  → Objeto entregue ao destinatário                                 ║
    /// ║  • CO-8    → Objeto entregue ao destinatário                                 ║
    /// ║  • BDE-77  → Objeto disponível em locker                                     ║
    /// ║  • BDI-77  → Objeto disponível em locker                                     ║
    /// ║  • BDR-77  → Objeto disponível em locker                                     ║
    /// ║                                                                              ║
    /// ║  ⚠️ PROBLEMAS DE ENTREGA (Notifica, não muda status)                         ║
    /// ║  • BDE-2/18/20/21  → Carteiro não atendido                                   ║
    /// ║  • BDE-4           → Cliente recusou receber                                 ║
    /// ║  • BDE-6           → Cliente desconhecido no local                           ║
    /// ║  • BDE-7/8/19/34   → Endereço incorreto/não encontrado                       ║
    /// ║  • BDE-10          → Cliente mudou-se                                        ║
    /// ║  • BDE-25          → Empresa sem expediente                                  ║
    /// ║  • BDE-28/37/86    → Objeto avariado                                         ║
    /// ║  • BDE-50/51/52    → Objeto roubado                                          ║
    /// ║  • BDE-80/98       → Objeto não localizado                                   ║
    /// ║                                                                              ║
    /// ║  🔄 DEVOLUÇÃO AO REMETENTE (Problema grave)                                  ║
    /// ║  • BDE-5/33/49     → Objeto em devolução                                     ║
    /// ║  • BDE-22          → Objeto devolvido aos Correios                           ║
    /// ║  • BDE-14/23       → Objeto entregue ao remetente                            ║
    /// ║                                                                              ║
    /// ╠══════════════════════════════════════════════════════════════════════════════╣
    /// ║                                                                              ║
    /// ║  📦↩️ DEVOLUÇÕES (Logística Reversa - Cliente → Loja)                         ║
    /// ║  ─────────────────────────────────────────────────────────────────────────── ║
    /// ║                                                                              ║
    /// ║  Status: ENVIADO (Cliente postou o produto)                                  ║
    /// ║  • PO-1    → Objeto postado (cliente enviou)                                 ║
    /// ║  • CO-1    → Objeto coletado                                                 ║
    /// ║                                                                              ║
    /// ║  Status: ENVIADO (Em trânsito para a loja)                                   ║
    /// ║  • RO-1    → Objeto em trânsito                                              ║
    /// ║  • DO-1/2  → Objeto em trânsito                                              ║
    /// ║  • TRI-0   → Objeto encaminhado                                              ║
    /// ║  • OEC-9   → Objeto saiu para entrega ao remetente (loja)                    ║
    /// ║                                                                              ║
    /// ║  Status: EM ANÁLISE (Produto chegou na loja)                                 ║
    /// ║  • BDE-14  → Objeto entregue ao remetente (loja recebeu)                     ║
    /// ║  • BDI-14  → Objeto entregue ao remetente                                    ║
    /// ║  • BDR-14  → Objeto entregue ao remetente                                    ║
    /// ║  • BDE-23  → Objeto entregue ao remetente                                    ║
    /// ║  • BDI-23  → Objeto entregue ao remetente                                    ║
    /// ║  • BDR-23  → Objeto entregue ao remetente                                    ║
    /// ║  • BDR-79  → Objeto entregue ao contratante                                  ║
    /// ║                                                                              ║
    /// ║  ⚠️ PROBLEMAS (Notifica admin)                                               ║
    /// ║  • BDE-28/37/86  → Objeto avariado                                           ║
    /// ║  • BDE-50/51/52  → Objeto roubado                                            ║
    /// ║  • BDE-80/98     → Objeto não localizado                                     ║
    /// ║                                                                              ║
    /// ╚══════════════════════════════════════════════════════════════════════════════╝
    /// </summary>
    public class CorreiosWebhookService : ICorreiosWebhookService
    {
        private readonly ApplicationDbContext _context;
        private readonly IEmailService _emailService;
        private readonly ILogger<CorreiosWebhookService> _logger;

        // Eventos que indicam ENTREGA AO DESTINATÁRIO (Pedido → Finalizado)
        private static readonly HashSet<string> EventosEntregueDestinatario = new(StringComparer.OrdinalIgnoreCase)
        {
            "BDE-1", "BDE-67", "BDE-68", "BDE-70",
            "BDI-1", "BDI-67", "BDI-68", "BDI-70",
            "BDR-1", "BDR-67", "BDR-68", "BDR-70",
            "CO-8", "BDE-77", "BDI-77", "BDR-77"
        };

        // Eventos que indicam OBJETO EM TRÂNSITO
        private static readonly HashSet<string> EventosEmTransito = new(StringComparer.OrdinalIgnoreCase)
        {
            "PO-1", "PO-9", "OEC-1", "OEC-3", "RO-0", "RO-1",
            "DO-1", "DO-2", "TRI-0", "PMT-1", "CAR-5",
            "BDE-15", "BDE-45", "BDI-15", "BDI-45", "BDR-15", "BDR-45",
            "FC-10", "PAR-28"
        };

        // Eventos que indicam ENTREGA AO REMETENTE (Devolução → Em Análise)
        private static readonly HashSet<string> EventosEntregueRemetente = new(StringComparer.OrdinalIgnoreCase)
        {
            "BDE-14", "BDE-23", "BDI-14", "BDI-23", "BDR-14", "BDR-23", "BDR-79"
        };

        // Eventos que indicam OBJETO POSTADO (Cliente enviou a devolução)
        private static readonly HashSet<string> EventosPostado = new(StringComparer.OrdinalIgnoreCase)
        {
            "PO-1", "CO-1", "CO-8", "CO-15", "CO-16"
        };

        // Eventos que indicam SAÍDA PARA ENTREGA AO REMETENTE
        private static readonly HashSet<string> EventosSaidaParaRemetente = new(StringComparer.OrdinalIgnoreCase)
        {
            "OEC-9", "LDE-0"
        };

        // Eventos de PROBLEMA (notificam mas não alteram status automaticamente)
        private static readonly HashSet<string> EventosProblema = new(StringComparer.OrdinalIgnoreCase)
        {
            // Carteiro não atendido
            "BDE-2", "BDE-18", "BDE-20", "BDE-21", "BDI-2", "BDI-18", "BDI-20", "BDI-21", "BDR-2", "BDR-18", "BDR-20", "BDR-21",
            // Cliente recusou
            "BDE-4", "BDI-4", "BDR-4",
            // Cliente desconhecido
            "BDE-6", "BDI-6", "BDR-6",
            // Endereço incorreto
            "BDE-7", "BDE-8", "BDE-19", "BDE-34", "BDI-7", "BDI-8", "BDI-19", "BDI-34", "BDR-7", "BDR-8", "BDR-19", "BDR-34",
            // Cliente mudou-se
            "BDE-10", "BDI-10", "BDR-10",
            // Empresa sem expediente
            "BDE-25", "BDI-25", "BDR-25",
            // Tentativa não efetuada
            "BDE-46", "BDI-46", "BDR-46",
            // Saída cancelada
            "BDE-47", "BDI-47", "BDR-47"
        };

        // Eventos CRÍTICOS (avariado, roubado, não localizado)
        private static readonly HashSet<string> EventosCriticos = new(StringComparer.OrdinalIgnoreCase)
        {
            // Avariado
            "BDE-28", "BDE-37", "BDE-86", "BDI-28", "BDI-37", "BDI-86", "BDR-28", "BDR-37", "BDR-86",
            // Roubado
            "BDE-50", "BDE-51", "BDE-52", "BDI-50", "BDI-51", "BDI-52", "BDR-50", "BDR-51", "BDR-52",
            // Não localizado
            "BDE-80", "BDE-98", "BDI-80", "BDI-98", "BDR-80"
        };

        // Eventos de DEVOLUÇÃO (objeto retornando ao remetente - problema em pedido normal)
        private static readonly HashSet<string> EventosDevolucaoProblema = new(StringComparer.OrdinalIgnoreCase)
        {
            "BDE-5", "BDE-22", "BDE-33", "BDE-49",
            "BDI-5", "BDI-22", "BDI-33", "BDI-49",
            "BDR-5", "BDR-22", "BDR-33", "BDR-49"
        };

        public CorreiosWebhookService(
            ApplicationDbContext context,
            IEmailService emailService,
            ILogger<CorreiosWebhookService> logger)
        {
            _context = context;
            _emailService = emailService;
            _logger = logger;
        }

        public async Task<EventoProcessadoResult> ProcessarEventoAsync(CorreiosWebhookPayload payload)
        {
            var result = new EventoProcessadoResult
            {
                Processado = false,
                TipoObjeto = TipoObjetoCorreios.NaoIdentificado
            };

            _logger.LogInformation("[Webhook Correios] Recebido evento {TipoEvento} para objeto {CodigoObjeto}",
                payload.TipoEvento, payload.CodigoObjeto);

            // 1. Tentar encontrar como PEDIDO (PrePostagem)
            var prePostagem = await _context.PrePostagens
                .Include(p => p.Pedido)
                .FirstOrDefaultAsync(p => p.CodigoRastreamento == payload.CodigoObjeto);

            if (prePostagem != null)
            {
                result.TipoObjeto = TipoObjetoCorreios.Pedido;
                result.PedidoId = prePostagem.PedidoId;
                return await ProcessarEventoPedidoAsync(prePostagem, payload, result);
            }

            // 2. Tentar encontrar como DEVOLUÇÃO
            var devolucao = await _context.Devolucoes
                .Include(d => d.Itens)
                .FirstOrDefaultAsync(d => d.CodigoRastreamento == payload.CodigoObjeto);

            if (devolucao != null)
            {
                result.TipoObjeto = TipoObjetoCorreios.Devolucao;
                result.DevolucaoId = devolucao.Id;
                return await ProcessarEventoDevolucaoAsync(devolucao, payload, result);
            }

            // 3. Não encontrado - verificar se é um código de postagem de devolução
            var devolucaoPorPostagem = await _context.Devolucoes
                .Include(d => d.Itens)
                .FirstOrDefaultAsync(d => d.CodigoPostagem == payload.CodigoObjeto);

            if (devolucaoPorPostagem != null)
            {
                // Atualizar o código de rastreamento se ainda não tiver
                if (string.IsNullOrEmpty(devolucaoPorPostagem.CodigoRastreamento))
                {
                    devolucaoPorPostagem.CodigoRastreamento = payload.CodigoObjeto;
                    await _context.SaveChangesAsync();
                }

                result.TipoObjeto = TipoObjetoCorreios.Devolucao;
                result.DevolucaoId = devolucaoPorPostagem.Id;
                return await ProcessarEventoDevolucaoAsync(devolucaoPorPostagem, payload, result);
            }

            _logger.LogWarning("[Webhook Correios] Código de rastreamento {CodigoObjeto} não encontrado no sistema",
                payload.CodigoObjeto);

            result.AcaoTomada = "Código de rastreamento não encontrado no sistema";
            return result;
        }

        private async Task<EventoProcessadoResult> ProcessarEventoPedidoAsync(
            PrePostagem prePostagem,
            CorreiosWebhookPayload payload,
            EventoProcessadoResult result)
        {
            var pedido = prePostagem.Pedido;
            var tipoEvento = payload.TipoEvento.ToUpperInvariant();

            _logger.LogInformation("[Webhook Correios] Processando evento {TipoEvento} para Pedido #{PedidoId}",
                tipoEvento, pedido.Id);

            // ENTREGUE AO DESTINATÁRIO → Finalizado
            if (EventosEntregueDestinatario.Contains(tipoEvento))
            {
                if (pedido.Status != StatusPedido.Finalizado)
                {
                    var statusAnterior = pedido.Status;
                    pedido.Status = StatusPedido.Finalizado;
                    pedido.DataAtualizacao = DateTime.UtcNow;
                    pedido.Observacoes = $"Entregue automaticamente via webhook Correios ({payload.DescricaoEvento ?? tipoEvento})";

                    await _context.SaveChangesAsync();

                    // Enviar email de entrega
                    await _emailService.EnviarEmailStatusPedidoAsync(pedido, StatusPedido.Finalizado);
                    result.EmailEnviado = true;

                    result.Processado = true;
                    result.AcaoTomada = $"Pedido #{pedido.CodigoPedido} atualizado para FINALIZADO (Entregue)";

                    _logger.LogInformation("[Webhook Correios] ✅ Pedido #{CodigoPedido} marcado como ENTREGUE",
                        pedido.CodigoPedido);
                }
                else
                {
                    result.Processado = true;
                    result.AcaoTomada = "Pedido já estava finalizado";
                }
                return result;
            }

            // EM TRÂNSITO → ACaminho (se ainda não estiver)
            if (EventosEmTransito.Contains(tipoEvento))
            {
                if (pedido.Status == StatusPedido.EmSeparacao)
                {
                    pedido.Status = StatusPedido.ACaminho;
                    pedido.DataAtualizacao = DateTime.UtcNow;
                    await _context.SaveChangesAsync();

                    // Enviar email de envio
                    await _emailService.EnviarEmailStatusPedidoAsync(pedido, StatusPedido.ACaminho);
                    result.EmailEnviado = true;

                    result.Processado = true;
                    result.AcaoTomada = $"Pedido #{pedido.CodigoPedido} atualizado para A CAMINHO";

                    _logger.LogInformation("[Webhook Correios] ✅ Pedido #{CodigoPedido} marcado como A CAMINHO",
                        pedido.CodigoPedido);
                }
                else
                {
                    result.Processado = true;
                    result.AcaoTomada = $"Pedido #{pedido.CodigoPedido} já está em status {pedido.Status}";
                }
                return result;
            }

            // PROBLEMAS DE ENTREGA (apenas log, não muda status)
            if (EventosProblema.Contains(tipoEvento))
            {
                _logger.LogWarning("[Webhook Correios] ⚠️ Problema de entrega no Pedido #{CodigoPedido}: {Descricao}",
                    pedido.CodigoPedido, payload.DescricaoEvento ?? tipoEvento);

                result.Processado = true;
                result.AcaoTomada = $"Problema de entrega registrado: {payload.DescricaoEvento ?? tipoEvento}";
                return result;
            }

            // EVENTOS CRÍTICOS (avariado, roubado, não localizado)
            if (EventosCriticos.Contains(tipoEvento))
            {
                _logger.LogError("[Webhook Correios] 🚨 EVENTO CRÍTICO no Pedido #{CodigoPedido}: {Descricao}",
                    pedido.CodigoPedido, payload.DescricaoEvento ?? tipoEvento);

                // Adicionar observação ao pedido
                pedido.Observacoes = $"⚠️ ALERTA CORREIOS: {payload.DescricaoEvento ?? tipoEvento} em {payload.DataEvento:dd/MM/yyyy HH:mm}";
                pedido.DataAtualizacao = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                result.Processado = true;
                result.AcaoTomada = $"EVENTO CRÍTICO registrado: {payload.DescricaoEvento ?? tipoEvento}";
                return result;
            }

            // DEVOLUÇÃO (pedido sendo devolvido ao remetente - problema)
            if (EventosDevolucaoProblema.Contains(tipoEvento))
            {
                _logger.LogWarning("[Webhook Correios] ⚠️ Pedido #{CodigoPedido} está sendo DEVOLVIDO aos Correios: {Descricao}",
                    pedido.CodigoPedido, payload.DescricaoEvento ?? tipoEvento);

                pedido.Observacoes = $"⚠️ OBJETO EM DEVOLUÇÃO: {payload.DescricaoEvento ?? tipoEvento} em {payload.DataEvento:dd/MM/yyyy HH:mm}";
                pedido.DataAtualizacao = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                result.Processado = true;
                result.AcaoTomada = $"Pedido em devolução aos Correios: {payload.DescricaoEvento ?? tipoEvento}";
                return result;
            }

            // Evento não mapeado
            result.Processado = true;
            result.AcaoTomada = $"Evento {tipoEvento} registrado (sem ação automática)";
            return result;
        }

        private async Task<EventoProcessadoResult> ProcessarEventoDevolucaoAsync(
            Devolucao devolucao,
            CorreiosWebhookPayload payload,
            EventoProcessadoResult result)
        {
            var tipoEvento = payload.TipoEvento.ToUpperInvariant();

            _logger.LogInformation("[Webhook Correios] Processando evento {TipoEvento} para Devolução #{DevolucaoId}",
                tipoEvento, devolucao.Id);

            // OBJETO POSTADO → Enviado (cliente enviou o produto)
            if (EventosPostado.Contains(tipoEvento))
            {
                if (devolucao.Status == DevolucaoStatus.SolicitacaoEnviada)
                {
                    devolucao.Status = DevolucaoStatus.Enviado;
                    devolucao.DataAtualizacao = DateTime.UtcNow;
                    
                    // Se ainda não tem código de rastreamento, usar o do evento
                    if (string.IsNullOrEmpty(devolucao.CodigoRastreamento))
                    {
                        devolucao.CodigoRastreamento = payload.CodigoObjeto;
                    }

                    await _context.SaveChangesAsync();

                    // Enviar email de confirmação de envio
                    await _emailService.EnviarEmailStatusDevolucaoAsync(devolucao, DevolucaoStatus.Enviado);
                    result.EmailEnviado = true;

                    result.Processado = true;
                    result.AcaoTomada = $"Devolução #{devolucao.Id} atualizada para ENVIADO (cliente postou)";

                    _logger.LogInformation("[Webhook Correios] ✅ Devolução #{Id} marcada como ENVIADO",
                        devolucao.Id);
                }
                else
                {
                    result.Processado = true;
                    result.AcaoTomada = $"Devolução #{devolucao.Id} já está em status {devolucao.Status}";
                }
                return result;
            }

            // EM TRÂNSITO ou SAÍDA PARA REMETENTE (mantém como Enviado)
            if (EventosEmTransito.Contains(tipoEvento) || EventosSaidaParaRemetente.Contains(tipoEvento))
            {
                result.Processado = true;
                result.AcaoTomada = $"Devolução #{devolucao.Id} em trânsito (status mantido: {devolucao.Status})";
                return result;
            }

            // ENTREGUE AO REMETENTE → Em Análise (produto chegou na loja)
            if (EventosEntregueRemetente.Contains(tipoEvento))
            {
                if (devolucao.Status == DevolucaoStatus.Enviado)
                {
                    devolucao.Status = DevolucaoStatus.EmAnalise;
                    devolucao.DataAtualizacao = DateTime.UtcNow;
                    await _context.SaveChangesAsync();

                    // Enviar email informando que o produto chegou
                    await _emailService.EnviarEmailStatusDevolucaoAsync(devolucao, DevolucaoStatus.EmAnalise);
                    result.EmailEnviado = true;

                    result.Processado = true;
                    result.AcaoTomada = $"Devolução #{devolucao.Id} atualizada para EM ANÁLISE (produto recebido na loja)";

                    _logger.LogInformation("[Webhook Correios] ✅ Devolução #{Id} marcada como EM ANÁLISE (chegou na loja)",
                        devolucao.Id);
                }
                else
                {
                    result.Processado = true;
                    result.AcaoTomada = $"Devolução #{devolucao.Id} já está em status {devolucao.Status}";
                }
                return result;
            }

            // EVENTOS CRÍTICOS
            if (EventosCriticos.Contains(tipoEvento))
            {
                _logger.LogError("[Webhook Correios] 🚨 EVENTO CRÍTICO na Devolução #{Id}: {Descricao}",
                    devolucao.Id, payload.DescricaoEvento ?? tipoEvento);

                result.Processado = true;
                result.AcaoTomada = $"EVENTO CRÍTICO na devolução: {payload.DescricaoEvento ?? tipoEvento}";
                return result;
            }

            // Evento não mapeado
            result.Processado = true;
            result.AcaoTomada = $"Evento {tipoEvento} registrado para devolução (sem ação automática)";
            return result;
        }
    }
}
