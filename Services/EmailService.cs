using System.Text;
using cafApi.Contexts;
using cafApi.Models;
using cafApi.Models.DTOs;
using Microsoft.EntityFrameworkCore;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using Ganss.Xss;

namespace cafApi.Services;

public class EmailService : IEmailService
{
    private readonly IConfiguration _configuration;
    private readonly ApplicationDbContext _context;
    private readonly ILogger<EmailService> _logger;
    private readonly DanfeService _danfeService;

    public EmailService(
        IConfiguration configuration,
        ApplicationDbContext context,
        ILogger<EmailService> logger,
        DanfeService danfeService)
    {
        _configuration = configuration;
        _context = context;
        _logger = logger;
        _danfeService = danfeService;
    }

    public async Task EnviarEmailStatusPedidoAsync(Pedido pedido, StatusPedido novoStatus, string? observacoes = null)
    {
        _logger.LogInformation($"[Email] Iniciando envio de email para pedido #{pedido.Id}, novo status: {novoStatus}");
        
        try
        {
            // Buscar pedido completo com relacionamentos
            var pedidoCompleto = await _context.Pedidos
                .Include(p => p.Cliente)
                .Include(p => p.Carrinho)
                    .ThenInclude(c => c.Itens)
                        .ThenInclude(i => i.Produto)
                .FirstOrDefaultAsync(p => p.Id == pedido.Id);

            if (pedidoCompleto == null)
            {
                _logger.LogWarning($"[Email] Pedido #{pedido.Id} não encontrado no banco de dados");
                return;
            }

            if (pedidoCompleto.Cliente == null)
            {
                _logger.LogWarning($"[Email] Cliente não encontrado para o pedido #{pedido.Id}");
                return;
            }

            _logger.LogInformation($"[Email] Pedido encontrado - Cliente: {pedidoCompleto.Cliente.Email}, Código: {pedidoCompleto.CodigoPedido}");

            var assunto = ObterAssuntoPorStatus(novoStatus, pedidoCompleto.CodigoPedido);
            _logger.LogInformation($"[Email] Assunto gerado: {assunto}");

            var corpoHtml = await GerarTemplateEmailPorStatusAsync(pedidoCompleto, novoStatus, observacoes);
            _logger.LogInformation($"[Email] Template HTML gerado com sucesso ({corpoHtml.Length} caracteres)");

            _logger.LogInformation($"[Email] Enviando email para: {pedidoCompleto.Cliente.Email}");
            
            // Se for status EmSeparacao (pagamento confirmado) e houver NF-e autorizada, anexar XML e DANFE
            if (novoStatus == StatusPedido.EmSeparacao)
            {
                var notaFiscal = await _context.Set<NotaFiscal>()
                    .Where(n => n.PedidoId == pedido.Id && n.Status == StatusNotaFiscal.Autorizada)
                    .OrderByDescending(n => n.DataEmissao)
                    .FirstOrDefaultAsync();

                if (notaFiscal != null)
                {
                    _logger.LogInformation($"[Email] NF-e encontrada. Gerando DANFE e anexando arquivos...");
                    await EnviarEmailComAnexosNfeAsync(pedidoCompleto.Cliente.Email, assunto, corpoHtml, notaFiscal);
                }
                else
                {
                    await EnviarEmailAsync(pedidoCompleto.Cliente.Email, assunto, corpoHtml);
                }
            }
            else
            {
                await EnviarEmailAsync(pedidoCompleto.Cliente.Email, assunto, corpoHtml);
            }
            
            _logger.LogInformation($"[Email] ✅ Email enviado com sucesso para {pedidoCompleto.Cliente.Email} - Pedido: {pedidoCompleto.CodigoPedido}, Status: {novoStatus}");
        }
        catch (Exception ex)
        {
            _logger.LogError($"[Email] ❌ Erro ao enviar email para pedido #{pedido.Id}: {ex.Message}");
            _logger.LogError($"[Email] Stack trace: {ex.StackTrace}");
            // Não falhar a operação principal se o email falhar
        }
    }

    public async Task EnviarEmailStatusDevolucaoAsync(Devolucao devolucao, DevolucaoStatus novoStatus, string? observacoes = null)
    {
        _logger.LogInformation($"[Email] Iniciando envio de email para devolução #{devolucao.Id}, novo status: {novoStatus}");
        
        try
        {
            // Buscar devolução completa com relacionamentos
            var devolucaoCompleta = await _context.Devolucoes
                .Include(d => d.Itens)
                .Include(d => d.Pedido)
                .FirstOrDefaultAsync(d => d.Id == devolucao.Id);

            if (devolucaoCompleta == null)
            {
                _logger.LogWarning($"[Email] Devolução #{devolucao.Id} não encontrada no banco de dados");
                return;
            }

            if (string.IsNullOrEmpty(devolucaoCompleta.Email))
            {
                _logger.LogWarning($"[Email] Email não encontrado para a devolução #{devolucao.Id}");
                return;
            }

            _logger.LogInformation($"[Email] Devolução encontrada - Email: {devolucaoCompleta.Email}");

            var assunto = ObterAssuntoDevolucaoPorStatus(novoStatus, devolucaoCompleta.Id);
            _logger.LogInformation($"[Email] Assunto gerado: {assunto}");

            var corpoHtml = GerarTemplateEmailDevolucaoAsync(devolucaoCompleta, novoStatus, observacoes);
            _logger.LogInformation($"[Email] Template HTML gerado com sucesso ({corpoHtml.Length} caracteres)");

            _logger.LogInformation($"[Email] Enviando email para: {devolucaoCompleta.Email}");
            await EnviarEmailAsync(devolucaoCompleta.Email, assunto, corpoHtml);
            
            _logger.LogInformation($"[Email] ✅ Email enviado com sucesso para {devolucaoCompleta.Email} - Devolução: #{devolucaoCompleta.Id}, Status: {novoStatus}");
        }
        catch (Exception ex)
        {
            _logger.LogError($"[Email] ❌ Erro ao enviar email para devolução #{devolucao.Id}: {ex.Message}");
            _logger.LogError($"[Email] Stack trace: {ex.StackTrace}");
            // Não falhar a operação principal se o email falhar
        }
    }

    public async Task EnviarEmailAsync(string destinatario, string assunto, string corpoHtml)
    {
        await EnviarEmailInternoAsync(destinatario, assunto, corpoHtml, null);
    }

    private async Task EnviarEmailComAnexosNfeAsync(string destinatario, string assunto, string corpoHtml, NotaFiscal notaFiscal)
    {
        try
        {
            // Gerar DANFE usando serviço injetado
            var danfePdf = _danfeService.GerarDanfe(notaFiscal);
            
            // Obter XML
            var xmlNfe = notaFiscal.XmlProtocolo ?? notaFiscal.XmlNfe;
            
            var anexos = new List<EmailAnexo>
            {
                new EmailAnexo
                {
                    Nome = $"NFe_{notaFiscal.Numero}_{notaFiscal.Serie}.xml",
                    Conteudo = System.Text.Encoding.UTF8.GetBytes(xmlNfe),
                    TipoConteudo = "application/xml"
                },
                new EmailAnexo
                {
                    Nome = $"DANFE_{notaFiscal.Numero}_{notaFiscal.Serie}.pdf",
                    Conteudo = danfePdf,
                    TipoConteudo = "application/pdf"
                }
            };
            
            await EnviarEmailInternoAsync(destinatario, assunto, corpoHtml, anexos);
            _logger.LogInformation($"[Email] ✅ Email enviado com anexos (XML + DANFE)");
        }
        catch (Exception ex)
        {
            _logger.LogError($"[Email] ❌ Erro ao enviar email com anexos: {ex.Message}");
            // Tentar enviar sem anexos como fallback
            await EnviarEmailInternoAsync(destinatario, assunto, corpoHtml, null);
        }
    }

    private async Task EnviarEmailInternoAsync(string destinatario, string assunto, string corpoHtml, List<EmailAnexo>? anexos)
    {
        _logger.LogInformation($"[Email] Configurando SMTP (MailKit) para envio...");
        
        var smtpHost = _configuration["Email:SmtpHost"];
        var smtpPort = int.Parse(_configuration["Email:SmtpPort"] ?? "587");
        var smtpUser = _configuration["Email:SmtpUser"];
        var smtpPassword = _configuration["Email:SmtpPassword"];
        var enableSsl = bool.Parse(_configuration["Email:EnableSsl"] ?? "true");
        var fromEmail = _configuration["Email:FromEmail"];
        var fromName = _configuration["Email:FromName"];

        _logger.LogInformation($"[Email] SMTP Config - Host: {smtpHost}, Port: {smtpPort}, User: {smtpUser}, SSL: {enableSsl}");

        if (string.IsNullOrEmpty(smtpHost) || string.IsNullOrEmpty(smtpUser) || string.IsNullOrEmpty(smtpPassword))
        {
            _logger.LogWarning("[Email] ⚠️ Configurações SMTP incompletas - Host: {0}, User: {1}, Password: {2}", 
                string.IsNullOrEmpty(smtpHost) ? "VAZIO" : "OK",
                string.IsNullOrEmpty(smtpUser) ? "VAZIO" : "OK",
                string.IsNullOrEmpty(smtpPassword) ? "VAZIO" : "OK");
            return;
        }

        try
        {
            // Criar mensagem usando MimeKit
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(fromName ?? "Chase a Flare", fromEmail ?? smtpUser));
            message.To.Add(new MailboxAddress("", destinatario));
            message.Subject = assunto;

            // Corpo HTML
            var bodyBuilder = new BodyBuilder
            {
                HtmlBody = corpoHtml
            };
            
            // Adicionar anexos se houver
            if (anexos != null && anexos.Count > 0)
            {
                foreach (var anexo in anexos)
                {
                    bodyBuilder.Attachments.Add(anexo.Nome, anexo.Conteudo, ContentType.Parse(anexo.TipoConteudo));
                    _logger.LogInformation($"[Email] Anexo adicionado: {anexo.Nome} ({anexo.Conteudo.Length} bytes)");
                }
            }
            
            message.Body = bodyBuilder.ToMessageBody();

            using var client = new SmtpClient();
            
            _logger.LogInformation($"[Email] Conectando ao servidor SMTP {smtpHost}:{smtpPort}...");
            
            // Determinar o tipo de segurança
            var secureSocketOptions = enableSsl ? SecureSocketOptions.StartTls : SecureSocketOptions.None;
            
            // Conectar
            await client.ConnectAsync(smtpHost, smtpPort, secureSocketOptions);
            _logger.LogInformation($"[Email] Conectado! Autenticando...");
            
            // Autenticar
            await client.AuthenticateAsync(smtpUser, smtpPassword);
            _logger.LogInformation($"[Email] Autenticado! Enviando mensagem...");
            
            // Enviar
            await client.SendAsync(message);
            _logger.LogInformation($"[Email] ✅ Mensagem enviada via SMTP com sucesso!");
            
            // Desconectar
            await client.DisconnectAsync(true);
        }
        catch (Exception ex)
        {
            _logger.LogError($"[Email] ❌ Erro ao enviar via SMTP: {ex.Message}");
            _logger.LogError($"[Email] Inner Exception: {ex.InnerException?.Message}");
            throw;
        }
    }

    private string ObterAssuntoPorStatus(StatusPedido status, string codigoPedido)
    {
        return status switch
        {
            StatusPedido.AguardandoConfirmacao => $"Pedido #{codigoPedido} - Aguardando Confirmação",
            StatusPedido.EmSeparacao => $"Pagamento Confirmado - Pedido #{codigoPedido}",
            StatusPedido.ACaminho => $"Pedido #{codigoPedido} foi Enviado!",
            StatusPedido.Finalizado => $"Pedido #{codigoPedido} foi Entregue!",
            StatusPedido.Cancelado => $"Pedido #{codigoPedido} foi Cancelado",
            _ => $"Atualização do Pedido #{codigoPedido}"
        };
    }

    private async Task<string> GerarTemplateEmailPorStatusAsync(Pedido pedido, StatusPedido status, string? observacoes)
    {
        // Cores da identidade visual Chase a Flare (fundo branco)
        const string corGraphite = "#1a1a1a";       // Grafite escuro (textos)
        const string corGraphiteLight = "#f8f8f8";  // Cinza claro (cards)
        const string corAmarelo = "#facc15";        // Amarelo neon (accent)
        const string corBranco = "#ffffff";         // Branco (fundo principal)
        const string corCinzaTexto = "#6b7280";     // Cinza para textos secundários
        const string corBorda = "#e5e7eb";          // Cinza para bordas

        // URLs do appsettings
        var backendUrl = _configuration["Backend:currSettingsUrl"];
        var frontendUrl = _configuration["Frontend:currSettingsUrl"];
        var contactEmail = _configuration["Email:ContactEmail"];
        var instagramUrl = _configuration["Email:Instagram"];
        var tiktokUrl = _configuration["Email:TikTok"];
        
        // Banner por status
        var statusKey = status.ToString();
        var bannerImageUrl = _configuration[$"Email:Banners:{statusKey}:ImageUrl"];
        var bannerRedirectUrl = _configuration[$"Email:Banners:{statusKey}:RedirectUrl"];
        
        // Construir URLs completas (assets vem do backend, links vão pro frontend)
        var logoUrl = $"{backendUrl}/chaseaflare/CAFLONG.png";
        var fullBannerImageUrl = $"{backendUrl}{bannerImageUrl}";
        var fullBannerRedirectUrl = bannerRedirectUrl!.StartsWith("http") ? bannerRedirectUrl : $"{frontendUrl}{bannerRedirectUrl}";

        var prePostagem = await _context.PrePostagens
            .FirstOrDefaultAsync(p => p.PedidoId == pedido.Id);

        // Buscar NF-e do pedido
        var notaFiscal = await _context.Set<NotaFiscal>()
            .Where(n => n.PedidoId == pedido.Id && n.Status == StatusNotaFiscal.Autorizada)
            .OrderByDescending(n => n.DataEmissao)
            .FirstOrDefaultAsync();

        var itensHtml = new StringBuilder();
        if (pedido.Carrinho?.Itens != null)
        {
            foreach (var item in pedido.Carrinho.Itens)
            {
                itensHtml.Append($@"
                    <tr>
                        <td style=""padding: 16px 20px; border-bottom: 1px solid {corBorda};"">
                            <div style=""font-weight: 600; color: {corGraphite}; margin-bottom: 4px; font-size: 15px;"">{item.Produto.Nome}</div>
                            <div style=""color: {corCinzaTexto}; font-size: 13px;"">Qtd: {item.Quantidade}</div>
                        </td>
                        <td style=""padding: 16px 20px; border-bottom: 1px solid {corBorda}; text-align: right; color: {corGraphite}; font-weight: 700; font-size: 15px;"">
                            R$ {(item.Quantidade * item.Produto.Preco):F2}
                        </td>
                    </tr>
                ");
            }
        }

        var mensagemStatus = ObterMensagemPorStatus(status, prePostagem?.CodigoRastreamento);
        
        // Formatar método de pagamento
        var metodoPagamentoFormatado = FormatarMetodoPagamento(pedido.MetodoPagamento);
        
        var trackingSection = "";
        if (status == StatusPedido.ACaminho && !string.IsNullOrEmpty(prePostagem?.CodigoRastreamento))
        {
            trackingSection = $@"
                <tr>
                    <td style=""padding: 0 30px 25px 30px;"">
                        <div style=""background: {corGraphiteLight}; padding: 24px; border-radius: 12px; border: 1px solid {corBorda};"">
                            <p style=""margin: 0 0 8px 0; color: {corCinzaTexto}; font-size: 12px; text-transform: uppercase; letter-spacing: 1px;"">Código de Rastreamento</p>
                            <p style=""margin: 0 0 16px 0; font-size: 28px; font-weight: 700; color: {corGraphite}; letter-spacing: 2px; font-family: monospace;"">{prePostagem.CodigoRastreamento}</p>
                            <a href=""https://www.linkcorreios.com.br/{prePostagem.CodigoRastreamento}"" 
                               style=""display: inline-block; background: {corAmarelo}; color: {corGraphite}; padding: 12px 24px; border-radius: 8px; text-decoration: none; font-weight: 600; font-size: 14px;"">
                                Rastrear Pedido
                            </a>
                        </div>
                    </td>
                </tr>
            ";
        }

        // Seção da NF-e (mostrar quando pagamento confirmado)
        var nfeSection = "";
        if (notaFiscal != null && (status == StatusPedido.EmSeparacao || status == StatusPedido.ACaminho || status == StatusPedido.Finalizado))
        {
            var ambienteLabel = notaFiscal.Ambiente == 2 ? " (Homologação)" : "";
            
            var anexoInfo = status == StatusPedido.EmSeparacao ? @"
                <div style=""margin-top: 12px; padding: 12px; background: #e8f5e9; border-radius: 8px; border-left: 4px solid #4caf50;"">
                    <p style=""margin: 0; color: #2e7d32; font-size: 12px; font-weight: 600;"">
                        📎 XML e DANFE enviados em anexo neste email
                    </p>
                </div>
            " : "";
            
            nfeSection = $@"
                <tr>
                    <td style=""padding: 0 30px 25px 30px;"">
                        <div style=""background: {corGraphiteLight}; padding: 20px; border-radius: 12px; border: 1px solid {corBorda};"">
                            <div style=""display: flex; align-items: center; justify-content: space-between;"">
                                <div>
                                    <p style=""margin: 0 0 4px 0; color: {corCinzaTexto}; font-size: 12px; text-transform: uppercase; letter-spacing: 1px;"">Nota Fiscal Eletrônica{ambienteLabel}</p>
                                    <p style=""margin: 0; font-size: 14px; color: {corGraphite}; font-weight: 600;"">Nº {notaFiscal.Numero} - Série {notaFiscal.Serie}</p>
                                </div>
                            </div>
                            <div style=""margin-top: 12px; padding-top: 12px; border-top: 1px solid {corBorda};"">
                                <p style=""margin: 0 0 8px 0; color: {corCinzaTexto}; font-size: 11px;"">Chave de Acesso:</p>
                                <p style=""margin: 0; font-size: 11px; color: {corGraphite}; font-family: monospace; word-break: break-all;"">{notaFiscal.ChaveAcesso}</p>
                            </div>
                            {anexoInfo}
                            <div style=""margin-top: 16px;"">
                                <a href=""{frontendUrl}/danfe/{notaFiscal.ChaveAcesso}"" 
                                   style=""display: inline-block; background: {corAmarelo}; color: {corGraphite}; padding: 10px 20px; border-radius: 8px; text-decoration: none; font-weight: 600; font-size: 13px;"" 
                                   target=""_blank"">
                                    Baixar DANFE
                                </a>
                            </div>
                        </div>
                    </td>
                </tr>
            ";
        }

        var observacoesSection = "";
        if (!string.IsNullOrEmpty(observacoes))
        {
            observacoesSection = $@"
                <tr>
                    <td style=""padding: 0 30px 25px 30px;"">
                        <div style=""background: {corGraphiteLight}; border-left: 4px solid {corAmarelo}; padding: 16px 20px; border-radius: 0 8px 8px 0;"">
                            <p style=""margin: 0; color: {corGraphite}; font-size: 14px;""><strong>Obs:</strong> {observacoes}</p>
                        </div>
                    </td>
                </tr>
            ";
        }

        return $@"
<!DOCTYPE html>
<html lang=""pt-BR"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>Chase a Flare - Atualização do Pedido</title>
</head>
<body style=""margin: 0; padding: 0; font-family: 'Segoe UI', -apple-system, BlinkMacSystemFont, Roboto, Helvetica, Arial, sans-serif; background-color: #f3f4f6;"">
    <table width=""100%"" cellpadding=""0"" cellspacing=""0"" style=""background-color: #f3f4f6; padding: 40px 20px;"">
        <tr>
            <td align=""center"">
                <table width=""600"" cellpadding=""0"" cellspacing=""0"" style=""background-color: {corBranco}; border-radius: 16px; overflow: hidden; box-shadow: 0 4px 6px rgba(0, 0, 0, 0.05);"">
                    
                    <!-- Header com Logo -->
                    <tr>
                        <td style=""background: {corBranco}; padding: 40px 30px 30px 30px; text-align: center; border-bottom: 1px solid {corBorda};"">
                            <img src=""{logoUrl}"" alt=""Chase a Flare"" style=""height: 50px; margin-bottom: 0;"" />
                        </td>
                    </tr>
                    
                    <!-- Status Badge -->
                    <tr>
                        <td style=""padding: 40px 30px 20px 30px; text-align: center;"">
                            <div style=""display: inline-block; background: {corAmarelo}; color: {corGraphite}; padding: 10px 28px; border-radius: 50px; font-weight: 700; font-size: 14px; text-transform: uppercase; letter-spacing: 1px;"">
                                {ObterTextoStatus(status)}
                            </div>
                        </td>
                    </tr>
                    
                    <!-- Saudação + Mensagem -->
                    <tr>
                        <td style=""padding: 20px 30px 30px 30px; text-align: center;"">
                            <h2 style=""margin: 0 0 12px 0; color: {corGraphite}; font-size: 22px; font-weight: 600;"">
                                Olá, {pedido.Cliente?.Nome?.Split(' ').FirstOrDefault() ?? "Cliente"}!
                            </h2>
                            <p style=""color: {corCinzaTexto}; font-size: 15px; line-height: 1.7; margin: 0; max-width: 480px; margin: 0 auto;"">
                                {mensagemStatus}
                            </p>
                        </td>
                    </tr>
                    
                    {trackingSection}
                    {nfeSection}
                    {observacoesSection}
                    
                    <!-- Card de Detalhes do Pedido -->
                    <tr>
                        <td style=""padding: 0 30px 25px 30px;"">
                            <div style=""background: {corGraphiteLight}; border-radius: 12px; overflow: hidden; border: 1px solid {corBorda};"">
                                <div style=""background: #f0f0f0; padding: 16px 20px; border-bottom: 1px solid {corBorda};"">
                                    <h3 style=""margin: 0; color: {corGraphite}; font-size: 16px; font-weight: 600;"">Detalhes do Pedido</h3>
                                </div>
                                <table width=""100%"" cellpadding=""0"" cellspacing=""0"" style=""padding: 20px;"">
                                    <tr>
                                        <td style=""padding: 8px 20px; color: {corCinzaTexto}; font-size: 14px;"">Pedido</td>
                                        <td style=""padding: 8px 20px; color: {corGraphite}; font-weight: 600; text-align: right; font-size: 14px;"">#{pedido.CodigoPedido}</td>
                                    </tr>
                                    <tr>
                                        <td style=""padding: 8px 20px; color: {corCinzaTexto}; font-size: 14px;"">Data</td>
                                        <td style=""padding: 8px 20px; color: {corGraphite}; font-weight: 600; text-align: right; font-size: 14px;"">{pedido.DataPedido:dd/MM/yyyy}</td>
                                    </tr>
                                    <tr>
                                        <td style=""padding: 8px 20px; color: {corCinzaTexto}; font-size: 14px;"">Pagamento</td>
                                        <td style=""padding: 8px 20px; color: {corGraphite}; font-weight: 600; text-align: right; font-size: 14px;"">{metodoPagamentoFormatado}</td>
                                    </tr>
                                    <tr>
                                        <td style=""padding: 16px 20px 8px 20px; color: {corCinzaTexto}; font-size: 14px; border-top: 1px solid {corBorda};"">Total</td>
                                        <td style=""padding: 16px 20px 8px 20px; color: {corGraphite}; font-weight: 700; text-align: right; font-size: 20px; border-top: 1px solid {corBorda};"">R$ {pedido.TotalPedido:F2}</td>
                                    </tr>
                                </table>
                            </div>
                        </td>
                    </tr>
                    
                    <!-- Itens do Pedido -->
                    <tr>
                        <td style=""padding: 0 30px 25px 30px;"">
                            <div style=""background: {corGraphiteLight}; border-radius: 12px; overflow: hidden; border: 1px solid {corBorda};"">
                                <div style=""background: #f0f0f0; padding: 16px 20px; border-bottom: 1px solid {corBorda};"">
                                    <h3 style=""margin: 0; color: {corGraphite}; font-size: 16px; font-weight: 600;"">Itens</h3>
                                </div>
                                <table width=""100%"" cellpadding=""0"" cellspacing=""0"">
                                    {itensHtml}
                                </table>
                            </div>
                        </td>
                    </tr>
                    
                    <!-- CTA Button -->
                    <tr>
                        <td style=""padding: 0 30px 30px 30px; text-align: center;"">
                            <a href=""{frontendUrl}/minha-conta/pedidos/{pedido.CodigoPedido}"" 
                               style=""display: inline-block; background: {corAmarelo}; color: {corGraphite}; padding: 16px 40px; border-radius: 10px; text-decoration: none; font-weight: 700; font-size: 15px;"">
                                Ver Pedido Completo
                            </a>
                        </td>
                    </tr>
                    
                    <!-- Banner Promocional -->
                    <tr>
                        <td style=""padding: 0 30px 30px 30px;"">
                            <a href=""{fullBannerRedirectUrl}"" style=""display: block;"">
                                <img src=""{fullBannerImageUrl}"" 
                                     alt=""Promoção Chase a Flare"" 
                                     style=""width: 100%; height: auto; border-radius: 12px; display: block;"" />
                            </a>
                        </td>
                    </tr>
                    
                    <!-- Footer -->
                    <tr>
                        <td style=""background: {corGraphiteLight}; padding: 30px; text-align: center; border-top: 1px solid {corBorda};"">
                            <p style=""margin: 0 0 8px 0; color: {corCinzaTexto}; font-size: 13px;"">
                                Dúvidas? Fale com a gente
                            </p>
                            <a href=""mailto:{contactEmail}"" style=""color: {corGraphite}; font-weight: 600; font-size: 14px; text-decoration: none;"">
                                {contactEmail}
                            </a>
                            
                            <!-- Redes Sociais -->
                            <div style=""margin: 24px 0;"">
                                <a href=""{instagramUrl}"" style=""display: inline-block; margin: 0 8px; color: {corGraphite}; text-decoration: none; font-size: 14px;"">
                                    Instagram
                                </a>
                                <span style=""color: {corBorda};"">|</span>
                                <a href=""{tiktokUrl}"" style=""display: inline-block; margin: 0 8px; color: {corGraphite}; text-decoration: none; font-size: 14px;"">
                                    TikTok
                                </a>
                            </div>
                            
                            <p style=""margin: 0; color: {corCinzaTexto}; font-size: 11px;"">
                                © {DateTime.Now.Year} Chase a Flare. Todos os direitos reservados.
                            </p>
                            <p style=""margin: 8px 0 0 0; color: {corCinzaTexto}; font-size: 10px;"">
                                CNPJ: 63.835.182/0001-30
                            </p>
                        </td>
                    </tr>
                    
                </table>
            </td>
        </tr>
    </table>
</body>
</html>
        ";
    }

    private string FormatarMetodoPagamento(string? metodoPagamento)
    {
        if (string.IsNullOrEmpty(metodoPagamento)) return "-";
        
        return metodoPagamento.ToUpper() switch
        {
            "CARTAO_DE_CREDITO" or "CREDIT_CARD" => "Cartão de Crédito",
            "CARTAO_DE_DEBITO" or "DEBIT_CARD" => "Cartão de Débito",
            "PIX" => "PIX",
            "BOLETO" => "Boleto",
            _ => metodoPagamento.Replace("_", " ")
        };
    }

    private string ObterMensagemPorStatus(StatusPedido status, string? codigoRastreamento = null)
    {
        return status switch
        {
            StatusPedido.AguardandoConfirmacao => 
                "Recebemos seu pedido e estamos aguardando a confirmação do pagamento. Assim que for confirmado, você receberá uma atualização.",
            
            StatusPedido.EmSeparacao => 
                "Pagamento confirmado! Estamos preparando seu pedido com muito carinho. Em breve você receberá o código de rastreamento.",
            
            StatusPedido.ACaminho => 
                "Seu pedido está a caminho! Use o código de rastreamento abaixo para acompanhar a entrega em tempo real.",
            
            StatusPedido.Finalizado => 
                "Pedido entregue! Esperamos que você ame seus produtos. Obrigado por escolher a Chase a Flare!",
            
            StatusPedido.Cancelado => 
                "Seu pedido foi cancelado. Se precisar de ajuda ou tiver dúvidas, estamos à disposição.",
            
            _ => "Houve uma atualização no status do seu pedido."
        };
    }

    private string ObterTextoStatus(StatusPedido status)
    {
        return status switch
        {
            StatusPedido.AguardandoConfirmacao => "Aguardando Pagamento",
            StatusPedido.EmSeparacao => "Em Preparação",
            StatusPedido.ACaminho => "Enviado",
            StatusPedido.Finalizado => "Entregue",
            StatusPedido.Cancelado => "Cancelado",
            _ => status.ToString()
        };
    }

    // ========== DEVOLUÇÃO ==========

    private string ObterAssuntoDevolucaoPorStatus(DevolucaoStatus status, int devolucaoId)
    {
        return status switch
        {
            DevolucaoStatus.Solicitado => $"Solicitação de Devolução #{devolucaoId} Recebida",
            DevolucaoStatus.SolicitacaoEnviada => $"Devolução #{devolucaoId} - Instruções de Envio",
            DevolucaoStatus.Enviado => $"Devolução #{devolucaoId} - Produto Enviado",
            DevolucaoStatus.EmAnalise => $"Devolução #{devolucaoId} - Em Análise",
            DevolucaoStatus.ReembolsoEmitido => $"Devolução #{devolucaoId} - Reembolso Emitido",
            DevolucaoStatus.Rejeitado => $"Devolução #{devolucaoId} - Solicitação Rejeitada",
            DevolucaoStatus.Reembolsado => $"Devolução #{devolucaoId} - Reembolso Concluído",
            _ => $"Atualização da Devolução #{devolucaoId}"
        };
    }

    private string ObterMensagemDevolucaoPorStatus(DevolucaoStatus status)
    {
        return status switch
        {
            DevolucaoStatus.Solicitado => 
                "Recebemos sua solicitação de devolução e estamos analisando. Em breve você receberá as instruções para envio do produto.",
            
            DevolucaoStatus.SolicitacaoEnviada => 
                "Sua solicitação foi aprovada! Siga as instruções abaixo para enviar o produto. Lembre-se de embalar bem para evitar danos no transporte.",
            
            DevolucaoStatus.Enviado => 
                "Recebemos a confirmação de que o produto foi enviado. Assim que chegar ao nosso centro de distribuição, iniciaremos a análise.",
            
            DevolucaoStatus.EmAnalise => 
                "Recebemos seu produto e ele está sendo analisado pela nossa equipe de qualidade. Você será notificado assim que a análise for concluída.",
            
            DevolucaoStatus.ReembolsoEmitido => 
                "A análise foi concluída e seu reembolso foi emitido! O valor será creditado em sua conta em até 10 dias úteis, dependendo do método de pagamento original.",
            
            DevolucaoStatus.Rejeitado => 
                "Após análise, infelizmente não foi possível aprovar sua solicitação de devolução. Entre em contato conosco para mais informações.",
            
            DevolucaoStatus.Reembolsado => 
                "Seu reembolso foi concluído com sucesso! O valor já deve estar disponível em sua conta. Agradecemos sua compreensão.",
            
            _ => "Houve uma atualização no status da sua devolução."
        };
    }

    private string ObterTextoStatusDevolucao(DevolucaoStatus status)
    {
        return status switch
        {
            DevolucaoStatus.Solicitado => "Solicitação Recebida",
            DevolucaoStatus.SolicitacaoEnviada => "Aguardando Envio",
            DevolucaoStatus.Enviado => "Produto Enviado",
            DevolucaoStatus.EmAnalise => "Em Análise",
            DevolucaoStatus.ReembolsoEmitido => "Reembolso Emitido",
            DevolucaoStatus.Rejeitado => "Rejeitado",
            DevolucaoStatus.Reembolsado => "Reembolsado",
            _ => status.ToString()
        };
    }

    private string ObterCorStatusDevolucao(DevolucaoStatus status)
    {
        // Todos usam amarelo como base, apenas Rejeitado usa vermelho
        return status switch
        {
            DevolucaoStatus.Rejeitado => "#ef4444",  // Vermelho apenas para rejeitado
            _ => "#facc15"                           // Amarelo (accent) para todos os outros
        };
    }

    private string GerarTemplateEmailDevolucaoAsync(Devolucao devolucao, DevolucaoStatus status, string? observacoes)
    {
        // Cores da identidade visual Chase a Flare (fundo branco)
        const string corGraphite = "#1a1a1a";       // Grafite escuro (textos)
        const string corGraphiteLight = "#f8f8f8";  // Cinza claro (cards)
        const string corAmarelo = "#facc15";        // Amarelo neon (accent)
        const string corBranco = "#ffffff";         // Branco (fundo principal)
        const string corCinzaTexto = "#6b7280";     // Cinza para textos secundários
        const string corBorda = "#e5e7eb";          // Cinza para bordas

        // URLs do appsettings
        var backendUrl = _configuration["Backend:currSettingsUrl"];
        var frontendUrl = _configuration["Frontend:currSettingsUrl"];
        var contactEmail = _configuration["Email:ContactEmail"];
        var instagramUrl = _configuration["Email:Instagram"];
        var tiktokUrl = _configuration["Email:TikTok"];
        
        // Logo
        var logoUrl = $"{backendUrl}/chaseaflare/CAFLONG.png";

        // Cor do badge baseada no status
        var corStatus = ObterCorStatusDevolucao(status);

        // Primeiro nome do cliente
        var primeiroNome = devolucao.NomeCliente?.Split(' ').FirstOrDefault() ?? "Cliente";

        // Mensagem por status
        var mensagemStatus = ObterMensagemDevolucaoPorStatus(status);

        // Itens da devolução
        var itensHtml = new StringBuilder();
        if (devolucao.Itens != null)
        {
            foreach (var item in devolucao.Itens)
            {
                itensHtml.Append($@"
                    <tr>
                        <td style=""padding: 16px 20px; border-bottom: 1px solid {corBorda};"">
                            <div style=""font-weight: 600; color: {corGraphite}; margin-bottom: 4px; font-size: 15px;"">{item.ProdutoNome}</div>
                            <div style=""color: {corCinzaTexto}; font-size: 13px;"">Cor: {item.CorNome} | Qtd: {item.Quantidade}</div>
                        </td>
                    </tr>
                ");
            }
        }

        // Seção de observações
        var observacoesSection = "";
        if (!string.IsNullOrEmpty(observacoes))
        {
            observacoesSection = $@"
                <tr>
                    <td style=""padding: 0 30px 25px 30px;"">
                        <div style=""background: {corGraphiteLight}; border-left: 4px solid {corAmarelo}; padding: 16px 20px; border-radius: 0 8px 8px 0;"">
                            <p style=""margin: 0; color: {corGraphite}; font-size: 14px;""><strong>Obs:</strong> {observacoes}</p>
                        </div>
                    </td>
                </tr>
            ";
        }

        // Seção de instruções de envio (apenas para status SolicitacaoEnviada)
        var instrucoesEnvioSection = "";
        if (status == DevolucaoStatus.SolicitacaoEnviada)
        {
            // Buscar dados da loja para mostrar no email
            var nomeLoja = _configuration["Correios:RemetenteNome"] ?? "Chase a Flare";
            var enderecoLoja = $"{_configuration["Correios:RemetenteLogradouro"]}, {_configuration["Correios:RemetenteNumero"]}";
            var bairroLoja = _configuration["Correios:RemetenteBairro"];
            var cidadeLoja = _configuration["Correios:RemetenteCidade"];
            var ufLoja = _configuration["Correios:RemetenteUF"];
            var cepLoja = _configuration["Correios:RemetenteCEP"];

            // Seção do código de postagem - Autorização de Logística Reversa
            var codigoPostagemSection = "";
            if (!string.IsNullOrEmpty(devolucao.CodigoPostagem))
            {
                var dataEmissao = DateTime.UtcNow.ToString("dd/MM/yyyy");
                var dataValidade = devolucao.DataLimitePostagem?.ToString("dd/MM/yyyy") ?? DateTime.UtcNow.AddDays(30).ToString("dd/MM/yyyy");

                codigoPostagemSection = $@"
                    <!-- Título da Autorização -->
                    <tr>
                        <td style=""padding: 0 30px 20px 30px;"">
                            <div style=""background: {corAmarelo}; padding: 20px; border-radius: 12px; text-align: center;"">
                                <h2 style=""margin: 0; color: {corGraphite}; font-size: 18px; font-weight: 700;"">📦 Autorização de Postagem - Logística Reversa</h2>
                            </div>
                        </td>
                    </tr>

                    <!-- Dados da Autorização -->
                    <tr>
                        <td style=""padding: 0 30px 20px 30px;"">
                            <div style=""background: {corGraphiteLight}; padding: 24px; border-radius: 12px; border: 2px solid {corAmarelo};"">
                                <table width=""100%"" cellpadding=""0"" cellspacing=""0"">
                                    <tr>
                                        <td style=""padding: 8px 0; border-bottom: 1px solid {corBorda};"">
                                            <span style=""color: {corCinzaTexto}; font-size: 13px;"">Código do Objeto:</span>
                                            <strong style=""color: {corGraphite}; font-size: 15px; float: right; font-family: monospace; letter-spacing: 1px;"">{devolucao.CodigoPostagem}</strong>
                                        </td>
                                    </tr>
                                    <tr>
                                        <td style=""padding: 8px 0; border-bottom: 1px solid {corBorda};"">
                                            <span style=""color: {corCinzaTexto}; font-size: 13px;"">Serviço Autorizado:</span>
                                            <strong style=""color: {corGraphite}; font-size: 15px; float: right;"">PAC Reverso</strong>
                                        </td>
                                    </tr>
                                    <tr>
                                        <td style=""padding: 8px 0; border-bottom: 1px solid {corBorda};"">
                                            <span style=""color: {corCinzaTexto}; font-size: 13px;"">Data de Emissão:</span>
                                            <strong style=""color: {corGraphite}; font-size: 15px; float: right;"">{dataEmissao}</strong>
                                        </td>
                                    </tr>
                                    <tr>
                                        <td style=""padding: 8px 0; border-bottom: 1px solid {corBorda};"">
                                            <span style=""color: {corCinzaTexto}; font-size: 13px;"">Data de Validade:</span>
                                            <strong style=""color: {corGraphite}; font-size: 15px; float: right;"">{dataValidade}</strong>
                                        </td>
                                    </tr>
                                    <tr>
                                        <td style=""padding: 8px 0;"">
                                            <span style=""color: {corCinzaTexto}; font-size: 13px;"">Quantidade de Objetos:</span>
                                            <strong style=""color: {corGraphite}; font-size: 15px; float: right;"">1</strong>
                                        </td>
                                    </tr>
                                </table>
                            </div>
                        </td>
                    </tr>

                    <!-- Código Grande em Destaque -->
                    <tr>
                        <td style=""padding: 0 30px 20px 30px;"">
                            <div style=""background: {corGraphite}; padding: 24px; border-radius: 12px; text-align: center;"">
                                <p style=""margin: 0 0 8px 0; color: {corAmarelo}; font-size: 12px; text-transform: uppercase; letter-spacing: 1px;"">Informe este código na agência</p>
                                <p style=""margin: 0; font-size: 36px; font-weight: 700; color: {corBranco}; letter-spacing: 4px; font-family: monospace;"">{devolucao.CodigoPostagem}</p>
                            </div>
                        </td>
                    </tr>

                    <!-- Destinatário (Loja) -->
                    <tr>
                        <td style=""padding: 0 30px 20px 30px;"">
                            <div style=""background: {corGraphiteLight}; padding: 20px; border-radius: 12px; border: 1px solid {corBorda};"">
                                <p style=""margin: 0 0 12px 0; color: {corCinzaTexto}; font-size: 12px; text-transform: uppercase; letter-spacing: 1px;"">Destinatário (onde será entregue):</p>
                                <p style=""margin: 0; color: {corGraphite}; font-size: 14px; line-height: 1.6;"">
                                    <strong>{nomeLoja}</strong><br>
                                    {enderecoLoja}<br>
                                    {bairroLoja} - {cidadeLoja}/{ufLoja}<br>
                                    CEP: {cepLoja}
                                </p>
                            </div>
                        </td>
                    </tr>
                ";
            }

            instrucoesEnvioSection = $@"
                {codigoPostagemSection}
                <tr>
                    <td style=""padding: 0 30px 25px 30px;"">
                        <div style=""background: {corGraphiteLight}; border-radius: 12px; overflow: hidden; border: 1px solid {corBorda};"">
                            <div style=""background: {corAmarelo}; padding: 16px 20px; border-bottom: 1px solid {corBorda};"">
                                <h3 style=""margin: 0; color: {corGraphite}; font-size: 16px; font-weight: 600;"">📋 Informações Importantes</h3>
                            </div>
                            <div style=""padding: 20px;"">
                                <ol style=""margin: 0; padding-left: 20px; color: {corGraphite}; font-size: 14px; line-height: 1.8;"">
                                    <li style=""margin-bottom: 12px;"">
                                        Para maior comodidade e agilidade no processo de postagem, sua encomenda deve estar <strong>adequadamente embalada e fechada</strong>, acompanhada da nota fiscal, se houver.
                                    </li>
                                    <li style=""margin-bottom: 12px;"">
                                        Caso não possua a nota fiscal, o atendente dos Correios fará a impressão da <strong>Declaração de Conteúdo</strong>.
                                    </li>
                                    <li style=""margin-bottom: 12px;"">
                                        A autorização de postagem pode ser utilizada em <strong>qualquer agência dos Correios</strong> através do sistema Correios Atende.
                                    </li>
                                    <li style=""margin-bottom: 12px;"">
                                        <strong>Não é necessário imprimir este e-mail.</strong> O atendente dos Correios irá imprimir o rótulo de endereçamento.
                                    </li>
                                    <li>
                                        Consulte o endereço das Agências Próprias e Franqueadas da sua cidade: <a href=""https://mais.correios.com.br/app/index.php"" style=""color: {corAmarelo}; text-decoration: underline;"">mais.correios.com.br</a>
                                    </li>
                                </ol>
                            </div>
                        </div>
                    </td>
                </tr>
            ";
        }

        // Seção de rastreamento (quando o produto foi enviado e tem código)
        var rastreamentoSection = "";
        if (!string.IsNullOrEmpty(devolucao.CodigoRastreamento) && 
            (status == DevolucaoStatus.Enviado || status == DevolucaoStatus.EmAnalise))
        {
            rastreamentoSection = $@"
                <tr>
                    <td style=""padding: 0 30px 25px 30px;"">
                        <div style=""background: {corGraphiteLight}; padding: 24px; border-radius: 12px; border: 1px solid {corBorda};"">
                            <p style=""margin: 0 0 8px 0; color: {corCinzaTexto}; font-size: 12px; text-transform: uppercase; letter-spacing: 1px;"">Código de Rastreamento</p>
                            <p style=""margin: 0 0 16px 0; font-size: 28px; font-weight: 700; color: {corGraphite}; letter-spacing: 2px; font-family: monospace;"">{devolucao.CodigoRastreamento}</p>
                            <a href=""https://www.linkcorreios.com.br/{devolucao.CodigoRastreamento}"" 
                               style=""display: inline-block; background: {corAmarelo}; color: {corGraphite}; padding: 12px 24px; border-radius: 8px; text-decoration: none; font-weight: 600; font-size: 14px;"">
                                Rastrear Envio
                            </a>
                        </div>
                    </td>
                </tr>
            ";
        }

        return $@"
<!DOCTYPE html>
<html lang=""pt-BR"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>Chase a Flare - Atualização da Devolução</title>
</head>
<body style=""margin: 0; padding: 0; font-family: 'Segoe UI', -apple-system, BlinkMacSystemFont, Roboto, Helvetica, Arial, sans-serif; background-color: #f3f4f6;"">
    <table width=""100%"" cellpadding=""0"" cellspacing=""0"" style=""background-color: #f3f4f6; padding: 40px 20px;"">
        <tr>
            <td align=""center"">
                <table width=""600"" cellpadding=""0"" cellspacing=""0"" style=""background-color: {corBranco}; border-radius: 16px; overflow: hidden; box-shadow: 0 4px 6px rgba(0, 0, 0, 0.05);"">
                    
                    <!-- Header com Logo -->
                    <tr>
                        <td style=""background: {corBranco}; padding: 40px 30px 30px 30px; text-align: center; border-bottom: 1px solid {corBorda};"">
                            <img src=""{logoUrl}"" alt=""Chase a Flare"" style=""height: 50px; margin-bottom: 0;"" />
                        </td>
                    </tr>
                    
                    <!-- Status Badge -->
                    <tr>
                        <td style=""padding: 40px 30px 20px 30px; text-align: center;"">
                            <div style=""display: inline-block; background: {corStatus}; color: {(status == DevolucaoStatus.Rejeitado ? "#ffffff" : corGraphite)}; padding: 10px 28px; border-radius: 50px; font-weight: 700; font-size: 14px; text-transform: uppercase; letter-spacing: 1px;"">
                                {ObterTextoStatusDevolucao(status)}
                            </div>
                        </td>
                    </tr>
                    
                    <!-- Saudação + Mensagem -->
                    <tr>
                        <td style=""padding: 20px 30px 30px 30px; text-align: center;"">
                            <h2 style=""margin: 0 0 12px 0; color: {corGraphite}; font-size: 22px; font-weight: 600;"">
                                Olá, {primeiroNome}!
                            </h2>
                            <p style=""color: {corCinzaTexto}; font-size: 15px; line-height: 1.7; margin: 0; max-width: 480px; margin: 0 auto;"">
                                {mensagemStatus}
                            </p>
                        </td>
                    </tr>
                    
                    {observacoesSection}
                    {instrucoesEnvioSection}
                    {rastreamentoSection}
                    
                    <!-- Card de Detalhes da Devolução -->
                    <tr>
                        <td style=""padding: 0 30px 25px 30px;"">
                            <div style=""background: {corGraphiteLight}; border-radius: 12px; overflow: hidden; border: 1px solid {corBorda};"">
                                <div style=""background: #f0f0f0; padding: 16px 20px; border-bottom: 1px solid {corBorda};"">
                                    <h3 style=""margin: 0; color: {corGraphite}; font-size: 16px; font-weight: 600;"">Detalhes da Devolução</h3>
                                </div>
                                <table width=""100%"" cellpadding=""0"" cellspacing=""0"" style=""padding: 20px;"">
                                    <tr>
                                        <td style=""padding: 8px 20px; color: {corCinzaTexto}; font-size: 14px;"">Devolução</td>
                                        <td style=""padding: 8px 20px; color: {corGraphite}; font-weight: 600; text-align: right; font-size: 14px;"">#{devolucao.Id}</td>
                                    </tr>
                                    <tr>
                                        <td style=""padding: 8px 20px; color: {corCinzaTexto}; font-size: 14px;"">Pedido Original</td>
                                        <td style=""padding: 8px 20px; color: {corGraphite}; font-weight: 600; text-align: right; font-size: 14px;"">{devolucao.Pedido?.CodigoPedido ?? $"#{devolucao.PedidoId}"}</td>
                                    </tr>
                                    <tr>
                                        <td style=""padding: 8px 20px; color: {corCinzaTexto}; font-size: 14px;"">Data da Solicitação</td>
                                        <td style=""padding: 8px 20px; color: {corGraphite}; font-weight: 600; text-align: right; font-size: 14px;"">{devolucao.DataCriacao:dd/MM/yyyy}</td>
                                    </tr>
                                </table>
                            </div>
                        </td>
                    </tr>
                    
                    <!-- Itens da Devolução -->
                    <tr>
                        <td style=""padding: 0 30px 25px 30px;"">
                            <div style=""background: {corGraphiteLight}; border-radius: 12px; overflow: hidden; border: 1px solid {corBorda};"">
                                <div style=""background: #f0f0f0; padding: 16px 20px; border-bottom: 1px solid {corBorda};"">
                                    <h3 style=""margin: 0; color: {corGraphite}; font-size: 16px; font-weight: 600;"">Itens para Devolução</h3>
                                </div>
                                <table width=""100%"" cellpadding=""0"" cellspacing=""0"">
                                    {itensHtml}
                                </table>
                            </div>
                        </td>
                    </tr>
                    
                    <!-- CTA Button -->
                    <tr>
                        <td style=""padding: 0 30px 30px 30px; text-align: center;"">
                            <a href=""{frontendUrl}/minha-conta/devolucoes"" 
                               style=""display: inline-block; background: {corAmarelo}; color: {corGraphite}; padding: 16px 40px; border-radius: 10px; text-decoration: none; font-weight: 700; font-size: 15px;"">
                                Acompanhar Devolução
                            </a>
                        </td>
                    </tr>
                    
                    <!-- Footer -->
                    <tr>
                        <td style=""background: {corGraphiteLight}; padding: 30px; text-align: center; border-top: 1px solid {corBorda};"">
                            <p style=""margin: 0 0 8px 0; color: {corCinzaTexto}; font-size: 13px;"">
                                Dúvidas? Fale com a gente
                            </p>
                            <a href=""mailto:{contactEmail}"" style=""color: {corGraphite}; font-weight: 600; font-size: 14px; text-decoration: none;"">
                                {contactEmail}
                            </a>
                            
                            <!-- Redes Sociais -->
                            <div style=""margin: 24px 0;"">
                                <a href=""{instagramUrl}"" style=""display: inline-block; margin: 0 8px; color: {corGraphite}; text-decoration: none; font-size: 14px;"">
                                    Instagram
                                </a>
                                <span style=""color: {corBorda};"">|</span>
                                <a href=""{tiktokUrl}"" style=""display: inline-block; margin: 0 8px; color: {corGraphite}; text-decoration: none; font-size: 14px;"">
                                    TikTok
                                </a>
                            </div>
                            
                            <p style=""margin: 0; color: {corCinzaTexto}; font-size: 11px;"">
                                © {DateTime.Now.Year} Chase a Flare. Todos os direitos reservados.
                            </p>
                            <p style=""margin: 8px 0 0 0; color: {corCinzaTexto}; font-size: 10px;"">
                                CNPJ: 63.835.182/0001-30
                            </p>
                        </td>
                    </tr>
                    
                </table>
            </td>
        </tr>
    </table>
</body>
</html>
        ";
    }

    // ========== EMAIL MARKETING ==========

    /// <summary>
    /// Número máximo de emails enviados em paralelo.
    /// Ajuste conforme a capacidade do seu servidor SMTP.
    /// </summary>
    private const int MaxParallelEmails = 10;

    public async Task<EmailMarketingResultDto> EnviarEmailMarketingAsync(EmailMarketingDto dto)
    {
        var result = new EmailMarketingResultDto();
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        
        _logger.LogInformation($"[EmailMarketing] Iniciando envio de campanha: {dto.Titulo}, Segmento: {dto.Segmento}");
        
        var clientes = await ObterClientesPorSegmentoAsync(dto.Segmento);
        
        _logger.LogInformation($"[EmailMarketing] Total de destinatários: {clientes.Count}");
        _logger.LogInformation($"[EmailMarketing] Enviando em paralelo (max {MaxParallelEmails} simultâneos)...");
        
        // Contadores thread-safe
        int enviados = 0;
        int falhas = 0;
        var erros = new System.Collections.Concurrent.ConcurrentBag<string>();
        
        // Envio paralelo com limite de concorrência
        await Parallel.ForEachAsync(
            clientes,
            new ParallelOptions { MaxDegreeOfParallelism = MaxParallelEmails },
            async (cliente, cancellationToken) =>
            {
                try
                {
                    var html = GerarTemplateEmailMarketing(dto, cliente.Nome);
                    await EnviarEmailAsync(cliente.Email, dto.Titulo, html);
                    
                    Interlocked.Increment(ref enviados);
                    _logger.LogInformation($"[EmailMarketing] ✅ Email enviado para: {cliente.Email}");
                }
                catch (Exception ex)
                {
                    Interlocked.Increment(ref falhas);
                    erros.Add($"{cliente.Email}: {ex.Message}");
                    _logger.LogError($"[EmailMarketing] ❌ Falha ao enviar para {cliente.Email}: {ex.Message}");
                }
            });
        
        stopwatch.Stop();
        
        result.TotalEnviados = enviados;
        result.TotalFalhas = falhas;
        result.Erros = erros.ToList();
        
        var tempoTotal = stopwatch.Elapsed;
        var emailsPorSegundo = clientes.Count > 0 ? clientes.Count / tempoTotal.TotalSeconds : 0;
        
        _logger.LogInformation($"[EmailMarketing] ✅ Campanha finalizada!");
        _logger.LogInformation($"[EmailMarketing] 📊 Enviados: {result.TotalEnviados}, Falhas: {result.TotalFalhas}");
        _logger.LogInformation($"[EmailMarketing] ⏱️ Tempo total: {tempoTotal.TotalSeconds:F1}s ({emailsPorSegundo:F1} emails/s)");
        
        return result;
    }

    public async Task<PreviewEmailDto> GerarPreviewEmailMarketingAsync(EmailMarketingDto dto)
    {
        var totalDestinatarios = await ContarDestinatariosPorSegmentoAsync(dto.Segmento);
        var html = GerarTemplateEmailMarketing(dto, "Cliente");
        
        return new PreviewEmailDto
        {
            Html = html,
            TotalDestinatarios = totalDestinatarios
        };
    }

    public async Task<int> ContarDestinatariosPorSegmentoAsync(SegmentoCliente segmento)
    {
        return segmento switch
        {
            SegmentoCliente.Todos => await _context.Clientes.Where(c => c.Ativo).CountAsync(),
            SegmentoCliente.Compradores => await _context.Clientes
                .Where(c => c.Ativo && c.Pedidos.Any(p => p.Status != StatusPedido.Cancelado))
                .CountAsync(),
            SegmentoCliente.NaoCompradores => await _context.Clientes
                .Where(c => c.Ativo && !c.Pedidos.Any(p => p.Status != StatusPedido.Cancelado))
                .CountAsync(),
            _ => 0
        };
    }

    private async Task<List<Cliente>> ObterClientesPorSegmentoAsync(SegmentoCliente segmento)
    {
        return segmento switch
        {
            SegmentoCliente.Todos => await _context.Clientes
                .Where(c => c.Ativo)
                .ToListAsync(),
            SegmentoCliente.Compradores => await _context.Clientes
                .Include(c => c.Pedidos)
                .Where(c => c.Ativo && c.Pedidos.Any(p => p.Status != StatusPedido.Cancelado))
                .ToListAsync(),
            SegmentoCliente.NaoCompradores => await _context.Clientes
                .Include(c => c.Pedidos)
                .Where(c => c.Ativo && !c.Pedidos.Any(p => p.Status != StatusPedido.Cancelado))
                .ToListAsync(),
            _ => new List<Cliente>()
        };
    }

    private string GerarTemplateEmailMarketing(EmailMarketingDto dto, string nomeCliente)
    {
        // Cores da identidade visual Chase a Flare (fundo branco)
        const string corGraphite = "#1a1a1a";
        const string corGraphiteLight = "#f8f8f8";
        const string corAmarelo = "#facc15";
        const string corBranco = "#ffffff";
        const string corCinzaTexto = "#6b7280";
        const string corBorda = "#e5e7eb";

        var backendUrl = _configuration["Backend:currSettingsUrl"];
        var frontendUrl = _configuration["Frontend:currSettingsUrl"];
        var contactEmail = _configuration["Email:ContactEmail"];
        var instagramUrl = _configuration["Email:Instagram"];
        var tiktokUrl = _configuration["Email:TikTok"];

        var logoUrl = $"{backendUrl}/chaseaflare/CAFLONG.png";
        var primeiroNome = nomeCliente.Split(' ').FirstOrDefault() ?? "Cliente";

        // Banner section (opcional)
        var bannerSection = "";
        if (!string.IsNullOrEmpty(dto.BannerUrl))
        {
            var bannerLink = !string.IsNullOrEmpty(dto.BannerLink) ? dto.BannerLink : frontendUrl;
            bannerSection = $@"
                <tr>
                    <td style=""padding: 0 30px 30px 30px;"">
                        <a href=""{bannerLink}"" style=""display: block;"">
                            <img src=""{dto.BannerUrl}"" 
                                 alt=""Banner promocional"" 
                                 style=""width: 100%; height: auto; border-radius: 12px; display: block;"" />
                        </a>
                    </td>
                </tr>
            ";
        }

        // Corpo do email já vem formatado em HTML do editor
        // Sanitizar para prevenir XSS (remove scripts, onclick, etc.)
        var sanitizer = new HtmlSanitizer();
        // Permitir tags de formatação básicas
        sanitizer.AllowedTags.Add("b");
        sanitizer.AllowedTags.Add("i");
        sanitizer.AllowedTags.Add("u");
        sanitizer.AllowedTags.Add("strong");
        sanitizer.AllowedTags.Add("em");
        sanitizer.AllowedTags.Add("br");
        sanitizer.AllowedTags.Add("p");
        sanitizer.AllowedTags.Add("div");
        sanitizer.AllowedTags.Add("span");
        sanitizer.AllowedTags.Add("ul");
        sanitizer.AllowedTags.Add("ol");
        sanitizer.AllowedTags.Add("li");
        sanitizer.AllowedTags.Add("a");
        sanitizer.AllowedAttributes.Add("href");
        sanitizer.AllowedAttributes.Add("style");
        var corpoFormatado = sanitizer.Sanitize(dto.Corpo);

        return $@"
<!DOCTYPE html>
<html lang=""pt-BR"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>{dto.Titulo}</title>
</head>
<body style=""margin: 0; padding: 0; font-family: 'Segoe UI', -apple-system, BlinkMacSystemFont, Roboto, Helvetica, Arial, sans-serif; background-color: #f3f4f6;"">
    <table width=""100%"" cellpadding=""0"" cellspacing=""0"" style=""background-color: #f3f4f6; padding: 40px 20px;"">
        <tr>
            <td align=""center"">
                <table width=""600"" cellpadding=""0"" cellspacing=""0"" style=""background-color: {corBranco}; border-radius: 16px; overflow: hidden; box-shadow: 0 4px 6px rgba(0, 0, 0, 0.05);"">
                    
                    <!-- Header com Logo -->
                    <tr>
                        <td style=""background: {corBranco}; padding: 20px 15px 15px 15px; text-align: center; border-bottom: 1px solid {corBorda};"">
                            <img src=""{logoUrl}"" alt=""Chase a Flare"" style=""height: 120px; margin-bottom: 0;"" />
                        </td>
                    </tr>
                    
                    <!-- Saudação -->
                    <tr>
                        <td style=""padding: 40px 30px 20px 30px; text-align: center;"">
                            <h2 style=""margin: 0 0 12px 0; color: {corGraphite}; font-size: 24px; font-weight: 600;"">
                                Olá, {primeiroNome}!
                            </h2>
                        </td>
                    </tr>
                    
                    <!-- Conteúdo Principal -->
                    <tr>
                        <td style=""padding: 0 30px 30px 30px;"">
                            <div style=""color: {corGraphite}; font-size: 15px; line-height: 1.8;"">
                                {corpoFormatado}
                            </div>
                        </td>
                    </tr>
                    
                    {bannerSection}
                    
                    <!-- CTA Button -->
                    <tr>
                        <td style=""padding: 0 30px 40px 30px; text-align: center;"">
                            <a href=""{frontendUrl}"" 
                               style=""display: inline-block; background: {corAmarelo}; color: {corGraphite}; padding: 16px 40px; border-radius: 10px; text-decoration: none; font-weight: 700; font-size: 15px;"">
                                Visitar a Loja
                            </a>
                        </td>
                    </tr>
                    
                    <!-- Footer -->
                    <tr>
                        <td style=""background: {corGraphiteLight}; padding: 30px; text-align: center; border-top: 1px solid {corBorda};"">
                            <p style=""margin: 0 0 8px 0; color: {corCinzaTexto}; font-size: 13px;"">
                                Dúvidas? Fale com a gente
                            </p>
                            <a href=""mailto:{contactEmail}"" style=""color: {corGraphite}; font-weight: 600; font-size: 14px; text-decoration: none;"">
                                {contactEmail}
                            </a>
                            
                            <!-- Redes Sociais -->
                            <div style=""margin: 24px 0;"">
                                <a href=""{instagramUrl}"" style=""display: inline-block; margin: 0 8px; color: {corGraphite}; text-decoration: none; font-size: 14px;"">
                                    Instagram
                                </a>
                                <span style=""color: {corBorda};"">|</span>
                                <a href=""{tiktokUrl}"" style=""display: inline-block; margin: 0 8px; color: {corGraphite}; text-decoration: none; font-size: 14px;"">
                                    TikTok
                                </a>
                            </div>
                            
                            <p style=""margin: 0; color: {corCinzaTexto}; font-size: 11px;"">
                                © {DateTime.Now.Year} Chase a Flare. Todos os direitos reservados.
                            </p>
                            <p style=""margin: 8px 0 0 0; color: {corCinzaTexto}; font-size: 10px;"">
                                CNPJ: 63.835.182/0001-30
                            </p>
                        </td>
                    </tr>
                    
                </table>
            </td>
        </tr>
    </table>
</body>
</html>
        ";
    }

    public async Task EnviarEmailCarrinhoAbandonadoAsync(Carrinho carrinho)
    {
        if (carrinho.Cliente == null)
        {
            _logger.LogWarning($"[Email] Cliente não encontrado para carrinho #{carrinho.Id}");
            return;
        }

        _logger.LogInformation($"[Email] Enviando email de carrinho abandonado para {carrinho.Cliente.Email}");

        try
        {
            var primeiroNome = carrinho.Cliente.Nome?.Split(' ').FirstOrDefault() ?? "Cliente";
            var assunto = GerarAssuntoCarrinhoAbandonado(carrinho.EmailRecuperacaoCount);
            var corpoHtml = GerarTemplateEmailCarrinhoAbandonado(carrinho, primeiroNome);

            await EnviarEmailAsync(carrinho.Cliente.Email, assunto, corpoHtml);
            
            _logger.LogInformation($"[Email] ✅ Email de carrinho abandonado enviado para {carrinho.Cliente.Email}");
        }
        catch (Exception ex)
        {
            _logger.LogError($"[Email] ❌ Erro ao enviar email de carrinho abandonado: {ex.Message}");
            throw;
        }
    }

    private string GerarAssuntoCarrinhoAbandonado(int emailCount)
    {
        return emailCount switch
        {
            0 => "🛒 Ei, você esqueceu algo especial no carrinho!",
            1 => "⏰ Seus itens ainda estão esperando por você!",
            2 => "🔥 Última chance! Seu carrinho está quase expirando",
            _ => "🛒 Seus produtos favoritos estão te esperando!"
        };
    }

    private string GerarTemplateEmailCarrinhoAbandonado(Carrinho carrinho, string primeiroNome)
    {
        // Cores da identidade visual Chase a Flare
        const string corGraphite = "#1a1a1a";
        const string corGraphiteLight = "#f8f8f8";
        const string corAmarelo = "#facc15";
        const string corBranco = "#ffffff";
        const string corCinzaTexto = "#6b7280";
        const string corBorda = "#e5e7eb";

        var backendUrl = _configuration["Backend:currSettingsUrl"];
        var frontendUrl = _configuration["Frontend:currSettingsUrl"];
        var contactEmail = _configuration["Email:ContactEmail"];
        var instagramUrl = _configuration["Email:Instagram"];
        var tiktokUrl = _configuration["Email:TikTok"];

        var logoUrl = $"{backendUrl}/chaseaflare/CAFLONG.png";
        
        // Link de recuperação do carrinho - vai para o frontend com parâmetro cart
        var recuperarCarrinhoUrl = $"{frontendUrl}?cart={carrinho.Token}";

        // Calcular totais
        var subtotal = carrinho.Itens.Sum(i => i.Quantidade * (i.Produto?.Preco ?? 0));
        var totalItens = carrinho.Itens.Sum(i => i.Quantidade);

        // Mensagem personalizada baseada no número de tentativas
        var (titulo, mensagem) = GerarMensagemPersonalizada(carrinho.EmailRecuperacaoCount, primeiroNome);

        // Gerar lista de produtos
        var produtosHtml = GerarListaProdutosHtml(carrinho, backendUrl);

        return $@"
<!DOCTYPE html>
<html lang=""pt-BR"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>Seu carrinho está te esperando</title>
</head>
<body style=""margin: 0; padding: 0; font-family: 'Segoe UI', -apple-system, BlinkMacSystemFont, Roboto, Helvetica, Arial, sans-serif; background-color: #f3f4f6;"">
    <table width=""100%"" cellpadding=""0"" cellspacing=""0"" style=""background-color: #f3f4f6; padding: 40px 20px;"">
        <tr>
            <td align=""center"">
                <table width=""600"" cellpadding=""0"" cellspacing=""0"" style=""background-color: {corBranco}; border-radius: 16px; overflow: hidden; box-shadow: 0 4px 6px rgba(0, 0, 0, 0.05);"">
                    
                    <!-- Header com Logo -->
                    <tr>
                        <td style=""background: {corBranco}; padding: 20px 15px 15px 15px; text-align: center; border-bottom: 1px solid {corBorda};"">
                            <img src=""{logoUrl}"" alt=""Chase a Flare"" style=""height: 120px; margin-bottom: 0;"" />
                        </td>
                    </tr>
                    
                    <!-- Título -->
                    <tr>
                        <td style=""padding: 20px 30px 15px 30px; text-align: center;"">
                            <h1 style=""margin: 0; color: {corGraphite}; font-size: 26px; font-weight: 700;"">
                                {titulo}
                            </h1>
                        </td>
                    </tr>
                    
                    <!-- Mensagem personalizada -->
                    <tr>
                        <td style=""padding: 0 30px 25px 30px; text-align: center;"">
                            <p style=""margin: 0; color: {corCinzaTexto}; font-size: 16px; line-height: 1.6;"">
                                {mensagem}
                            </p>
                        </td>
                    </tr>
                    
                    <!-- Box com produtos -->
                    <tr>
                        <td style=""padding: 0 30px 25px 30px;"">
                            <table width=""100%"" cellpadding=""0"" cellspacing=""0"" style=""background: {corGraphiteLight}; border-radius: 12px; padding: 20px;"">
                                <tr>
                                    <td>
                                        <p style=""margin: 0 0 15px 0; color: {corGraphite}; font-size: 14px; font-weight: 600; text-transform: uppercase; letter-spacing: 0.5px;"">
                                            📦 Itens no seu carrinho ({totalItens})
                                        </p>
                                        {produtosHtml}
                                    </td>
                                </tr>
                            </table>
                        </td>
                    </tr>
                    
                    <!-- Total -->
                    <tr>
                        <td style=""padding: 0 30px 25px 30px; text-align: center;"">
                            <p style=""margin: 0; color: {corCinzaTexto}; font-size: 14px;"">
                                Subtotal
                            </p>
                            <p style=""margin: 5px 0 0 0; color: {corGraphite}; font-size: 28px; font-weight: 700;"">
                                R$ {subtotal:N2}
                            </p>
                        </td>
                    </tr>
                    
                    <!-- CTA Button -->
                    <tr>
                        <td style=""padding: 0 30px 15px 30px; text-align: center;"">
                            <a href=""{recuperarCarrinhoUrl}"" 
                               style=""display: inline-block; background: {corAmarelo}; color: {corGraphite}; padding: 18px 50px; border-radius: 12px; text-decoration: none; font-weight: 700; font-size: 16px; box-shadow: 0 4px 14px rgba(250, 204, 21, 0.3);"">
                                Recuperar meu carrinho
                            </a>
                        </td>
                    </tr>
                    
                    <!-- Urgência sutil -->
                    <tr>
                        <td style=""padding: 0 30px 30px 30px; text-align: center;"">
                            <p style=""margin: 0; color: {corCinzaTexto}; font-size: 13px;"">
                                ⏳ Os produtos podem acabar a qualquer momento!
                            </p>
                        </td>
                    </tr>
                    
                    <!-- Footer -->
                    <tr>
                        <td style=""background: {corGraphiteLight}; padding: 30px; text-align: center; border-top: 1px solid {corBorda};"">
                            <p style=""margin: 0 0 8px 0; color: {corCinzaTexto}; font-size: 13px;"">
                                Dúvidas? Fale com a gente
                            </p>
                            <a href=""mailto:{contactEmail}"" style=""color: {corGraphite}; font-weight: 600; font-size: 14px; text-decoration: none;"">
                                {contactEmail}
                            </a>
                            
                            <!-- Redes Sociais -->
                            <div style=""margin: 24px 0;"">
                                <a href=""{instagramUrl}"" style=""display: inline-block; margin: 0 8px; color: {corGraphite}; text-decoration: none; font-size: 14px;"">
                                    Instagram
                                </a>
                                <span style=""color: {corBorda};"">|</span>
                                <a href=""{tiktokUrl}"" style=""display: inline-block; margin: 0 8px; color: {corGraphite}; text-decoration: none; font-size: 14px;"">
                                    TikTok
                                </a>
                            </div>
                            
                            <p style=""margin: 0; color: {corCinzaTexto}; font-size: 11px;"">
                                © {DateTime.Now.Year} Chase a Flare. Todos os direitos reservados.
                            </p>
                            <p style=""margin: 8px 0 0 0; color: {corCinzaTexto}; font-size: 10px;"">
                                CNPJ: 63.835.182/0001-30
                            </p>
                        </td>
                    </tr>
                    
                </table>
            </td>
        </tr>
    </table>
</body>
</html>
        ";
    }

    private (string titulo, string mensagem) GerarMensagemPersonalizada(int emailCount, string primeiroNome)
    {
        return emailCount switch
        {
            0 => (
                $"Ei {primeiroNome}, você esqueceu algo!",
                "Notamos que você deixou alguns produtos incríveis no seu carrinho. " +
                "Sabemos como é fácil se distrair, mas não queremos que você perca essas peças especiais! " +
                "Elas ainda estão te esperando. 💛"
            ),
            1 => (
                $"{primeiroNome}, sentimos sua falta!",
                "Seus produtos favoritos ainda estão guardados no carrinho, esperando por você. " +
                "Não deixe essa oportunidade escapar - complete sua compra e receba tudo que você escolheu com tanto carinho! ✨"
            ),
            2 => (
                $"Última chamada, {primeiroNome}!",
                "Esta é sua última chance de garantir os itens que você escolheu! " +
                "O estoque pode acabar a qualquer momento e não queremos que você fique na mão. " +
                "Finalize agora e garanta suas peças! 🔥"
            ),
            _ => (
                $"{primeiroNome}, seu carrinho te aguarda!",
                "Você tem produtos especiais esperando por você. " +
                "Complete sua compra e leve para casa tudo que você escolheu! 💛"
            )
        };
    }

    private string GerarListaProdutosHtml(Carrinho carrinho, string? backendUrl)
    {
        const string corGraphite = "#1a1a1a";
        const string corCinzaTexto = "#6b7280";
        const string corBranco = "#ffffff";

        var html = new StringBuilder();

        foreach (var item in carrinho.Itens.Take(5)) // Limita a 5 produtos no email
        {
            var produto = item.Produto;
            if (produto == null) continue;

            // Buscar imagem da cor selecionada
            var imagemUrl = produto.ImagemPrincipal;
            var corSelecionada = produto.CoresDisponiveis?.FirstOrDefault(c => c.Id == item.CorId);
            if (corSelecionada?.Imagens?.Any() == true)
            {
                imagemUrl = corSelecionada.Imagens.First().Url;
            }

            // Garantir URL absoluta
            if (!string.IsNullOrEmpty(imagemUrl) && !imagemUrl.StartsWith("http"))
            {
                imagemUrl = $"{backendUrl}{imagemUrl}";
            }

            var subtotalItem = item.Quantidade * produto.Preco;

            html.AppendLine($@"
                <table width=""100%"" cellpadding=""0"" cellspacing=""0"" style=""margin-bottom: 15px; background: {corBranco}; border-radius: 8px; overflow: hidden;"">
                    <tr>
                        <td width=""80"" style=""padding: 10px;"">
                            <img src=""{imagemUrl}"" alt=""{produto.Nome}"" style=""width: 60px; height: 60px; object-fit: cover; border-radius: 6px;"" />
                        </td>
                        <td style=""padding: 10px 10px 10px 0;"">
                            <p style=""margin: 0 0 4px 0; color: {corGraphite}; font-size: 14px; font-weight: 600;"">
                                {produto.Nome}
                            </p>
                            <p style=""margin: 0; color: {corCinzaTexto}; font-size: 12px;"">
                                Qtd: {item.Quantidade} × R$ {produto.Preco:N2}
                            </p>
                        </td>
                        <td width=""100"" style=""padding: 10px; text-align: right;"">
                            <p style=""margin: 0; color: {corGraphite}; font-size: 14px; font-weight: 700;"">
                                R$ {subtotalItem:N2}
                            </p>
                        </td>
                    </tr>
                </table>
            ");
        }

        // Se houver mais de 5 produtos
        if (carrinho.Itens.Count > 5)
        {
            var restante = carrinho.Itens.Count - 5;
            html.AppendLine($@"
                <p style=""margin: 10px 0 0 0; color: {corCinzaTexto}; font-size: 13px; text-align: center;"">
                    + {restante} {(restante == 1 ? "outro item" : "outros itens")}
                </p>
            ");
        }

        return html.ToString();
    }
}

// Classe auxiliar para anexos de email
public class EmailAnexo
{
    public string Nome { get; set; } = "";
    public byte[] Conteudo { get; set; } = Array.Empty<byte>();
    public string TipoConteudo { get; set; } = "application/octet-stream";
}
