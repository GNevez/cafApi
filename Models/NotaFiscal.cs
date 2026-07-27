namespace cafApi.Models;

/// <summary>
/// Modelo para armazenar as Notas Fiscais Eletrônicas emitidas
/// </summary>
public class NotaFiscal
{
    public int Id { get; set; }
    public int PedidoId { get; set; }
    
    /// <summary>
    /// Número da NF-e (sequencial)
    /// </summary>
    public int Numero { get; set; }
    
    /// <summary>
    /// Série da NF-e (geralmente 1 para produção, 999 para homologação)
    /// </summary>
    public int Serie { get; set; }
    
    /// <summary>
    /// Chave de acesso da NF-e (44 dígitos)
    /// </summary>
    public string ChaveAcesso { get; set; } = string.Empty;
    
    /// <summary>
    /// Protocolo de autorização retornado pela SEFAZ
    /// </summary>
    public string? ProtocoloAutorizacao { get; set; }
    
    /// <summary>
    /// XML da NF-e assinada
    /// </summary>
    public string XmlNfe { get; set; } = string.Empty;
    
    /// <summary>
    /// XML do protocolo de autorização
    /// </summary>
    public string? XmlProtocolo { get; set; }
    
    /// <summary>
    /// Status da NF-e: Pendente, Autorizada, Cancelada, Rejeitada, Inutilizada
    /// </summary>
    public StatusNotaFiscal Status { get; set; } = StatusNotaFiscal.Pendente;
    
    /// <summary>
    /// Código de status retornado pela SEFAZ
    /// </summary>
    public int CodigoStatus { get; set; } = 0;
    
    /// <summary>
    /// Mensagem de retorno da SEFAZ
    /// </summary>
    public string? MensagemStatus { get; set; }
    
    /// <summary>
    /// Ambiente: 1 = Produção, 2 = Homologação
    /// </summary>
    public int Ambiente { get; set; } = 2;
    
    /// <summary>
    /// Valor total da NF-e
    /// </summary>
    public decimal ValorTotal { get; set; }
    
    /// <summary>
    /// Data de emissão
    /// </summary>
    public DateTime DataEmissao { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// Data de autorização pela SEFAZ
    /// </summary>
    public DateTime? DataAutorizacao { get; set; }
    
    /// <summary>
    /// Motivo do cancelamento (se aplicável)
    /// </summary>
    public string? MotivoCancelamento { get; set; }
    
    /// <summary>
    /// Data do cancelamento (se aplicável)
    /// </summary>
    public DateTime? DataCancelamento { get; set; }
    
    /// <summary>
    /// Protocolo de cancelamento (se aplicável)
    /// </summary>
    public string? ProtocoloCancelamento { get; set; }
    
    // Navegação
    public Pedido Pedido { get; set; } = null!;
}

public enum StatusNotaFiscal
{
    Pendente = 0,
    Autorizada = 1,
    Cancelada = 2,
    Rejeitada = 3,
    Inutilizada = 4,
    Contingencia = 5
}
