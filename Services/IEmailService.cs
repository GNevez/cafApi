using cafApi.Models;
using cafApi.Models.DTOs;

namespace cafApi.Services;

public interface IEmailService
{
    /// <summary>
    /// Envia email de notificação de atualização de status do pedido
    /// </summary>
    Task EnviarEmailStatusPedidoAsync(Pedido pedido, StatusPedido novoStatus, string? observacoes = null);
    
    /// <summary>
    /// Envia email de notificação de atualização de status da devolução
    /// </summary>
    Task EnviarEmailStatusDevolucaoAsync(Devolucao devolucao, DevolucaoStatus novoStatus, string? observacoes = null);
    
    /// <summary>
    /// Envia email genérico
    /// </summary>
    Task EnviarEmailAsync(string destinatario, string assunto, string corpoHtml);
    
    /// <summary>
    /// Envia email marketing para clientes
    /// </summary>
    Task<EmailMarketingResultDto> EnviarEmailMarketingAsync(EmailMarketingDto dto);
    
    /// <summary>
    /// Gera preview do email marketing
    /// </summary>
    Task<PreviewEmailDto> GerarPreviewEmailMarketingAsync(EmailMarketingDto dto);
    
    /// <summary>
    /// Conta destinatários por segmento
    /// </summary>
    Task<int> ContarDestinatariosPorSegmentoAsync(SegmentoCliente segmento);
    
    /// <summary>
    /// Envia email de recuperação de carrinho abandonado
    /// </summary>
    Task EnviarEmailCarrinhoAbandonadoAsync(Carrinho carrinho);
}
