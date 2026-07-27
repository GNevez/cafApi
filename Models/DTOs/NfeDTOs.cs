namespace cafApi.Models.DTOs;

/// <summary>
/// DTO para emissão de NF-e
/// </summary>
public class EmitirNfeDto
{
    public int PedidoId { get; set; }
    
    /// <summary>
    /// Natureza da operação (ex: "Venda de mercadoria")
    /// </summary>
    public string? NaturezaOperacao { get; set; }
}

/// <summary>
/// DTO de resposta da emissão de NF-e
/// </summary>
public class NfeResponseDto
{
    public bool Sucesso { get; set; }
    public string? Mensagem { get; set; }
    public int? NotaFiscalId { get; set; }
    public string? ChaveAcesso { get; set; }
    public string? ProtocoloAutorizacao { get; set; }
    public int? CodigoStatus { get; set; }
    public string? MensagemStatus { get; set; }
    public string? XmlNfe { get; set; }
    public string? LinkDanfe { get; set; }
}

/// <summary>
/// DTO para cancelamento de NF-e
/// </summary>
public class CancelarNfeDto
{
    public int NotaFiscalId { get; set; }
    public string Motivo { get; set; } = string.Empty;
}

/// <summary>
/// DTO para consulta de NF-e
/// </summary>
public class ConsultaNfeDto
{
    public int Id { get; set; }
    public int PedidoId { get; set; }
    public string CodigoPedido { get; set; } = string.Empty;
    public int Numero { get; set; }
    public int Serie { get; set; }
    public string ChaveAcesso { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int? CodigoStatus { get; set; }
    public string? MensagemStatus { get; set; }
    public string Ambiente { get; set; } = string.Empty;
    public decimal ValorTotal { get; set; }
    public DateTime DataEmissao { get; set; }
    public DateTime? DataAutorizacao { get; set; }
    public string? ProtocoloAutorizacao { get; set; }
}

/// <summary>
/// DTO para inutilização de numeração
/// </summary>
public class InutilizarNfeDto
{
    public int Serie { get; set; }
    public int NumeroInicial { get; set; }
    public int NumeroFinal { get; set; }
    public string Justificativa { get; set; } = string.Empty;
}

/// <summary>
/// DTO para listagem de pedidos com informações de NF-e
/// </summary>
public class PedidoNfeListDto
{
    public int Id { get; set; }
    public string CodigoPedido { get; set; } = string.Empty;
    public string NomeCliente { get; set; } = string.Empty;
    public string EmailCliente { get; set; } = string.Empty;
    public decimal TotalPedido { get; set; }
    public DateTime DataPedido { get; set; }
    public int Status { get; set; }
    public string StatusDescricao { get; set; } = string.Empty;
    
    // Informações da NF-e (se houver)
    public bool TemNotaFiscal { get; set; }
    public int? NotaFiscalId { get; set; }
    public int? NumeroNfe { get; set; }
    public int? SerieNfe { get; set; }
    public string? ChaveAcesso { get; set; }
    public string? StatusNfe { get; set; }
    public DateTime? DataEmissaoNfe { get; set; }
}

/// <summary>
/// DTO paginado para listagem de pedidos com NF-e
/// </summary>
public class PaginatedPedidoNfeDto
{
    public List<PedidoNfeListDto> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int PageNumber { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
}

/// <summary>
/// DTO com estatísticas de NF-e
/// </summary>
public class NfeEstatisticasDto
{
    public int TotalPedidos { get; set; }
    public int PedidosComNota { get; set; }
    public int PedidosSemNota { get; set; }
    public int NotasAutorizadas { get; set; }
    public int NotasCanceladas { get; set; }
    public int NotasPendentes { get; set; }
    public int NotasRejeitadas { get; set; }
    public decimal ValorTotalNotas { get; set; }
}

/// <summary>
/// Configurações do emitente para NF-e
/// </summary>
public class EmitenteNfeConfig
{
    /// <summary>
    /// CNPJ do emitente (apenas números)
    /// </summary>
    public string Cnpj { get; set; } = string.Empty;
    
    /// <summary>
    /// Inscrição Estadual
    /// </summary>
    public string InscricaoEstadual { get; set; } = string.Empty;
    
    /// <summary>
    /// Razão Social
    /// </summary>
    public string RazaoSocial { get; set; } = string.Empty;
    
    /// <summary>
    /// Nome Fantasia
    /// </summary>
    public string NomeFantasia { get; set; } = string.Empty;
    
    /// <summary>
    /// Código do Regime Tributário: 1=Simples Nacional, 2=Simples Excesso, 3=Regime Normal
    /// </summary>
    public int RegimeTributario { get; set; } = 1;
    
    /// <summary>
    /// CNAE Principal
    /// </summary>
    public string CnaePrincipal { get; set; } = string.Empty;
    
    // Endereço
    public string Logradouro { get; set; } = string.Empty;
    public string Numero { get; set; } = string.Empty;
    public string? Complemento { get; set; }
    public string Bairro { get; set; } = string.Empty;
    public string CodigoMunicipio { get; set; } = string.Empty; // Código IBGE
    public string NomeMunicipio { get; set; } = string.Empty;
    public string UF { get; set; } = string.Empty;
    public string Cep { get; set; } = string.Empty;
    public string CodigoPais { get; set; } = "1058"; // Brasil
    public string NomePais { get; set; } = "Brasil";
    public string? Telefone { get; set; }
}

/// <summary>
/// Configurações do certificado digital
/// </summary>
public class CertificadoDigitalConfig
{
    /// <summary>
    /// Caminho do arquivo .pfx do certificado
    /// </summary>
    public string CaminhoArquivo { get; set; } = string.Empty;
    
    /// <summary>
    /// Senha do certificado (deve ser criptografada em produção)
    /// </summary>
    public string Senha { get; set; } = string.Empty;
}

/// <summary>
/// Configurações gerais de NF-e
/// </summary>
public class NfeConfig
{
    /// <summary>
    /// Ambiente: 1=Produção, 2=Homologação
    /// </summary>
    public int Ambiente { get; set; } = 2;
    
    /// <summary>
    /// UF do emitente (código numérico - ex: 35 para SP)
    /// </summary>
    public int UfEmitente { get; set; }
    
    /// <summary>
    /// Série da NF-e
    /// </summary>
    public int Serie { get; set; } = 1;
    
    /// <summary>
    /// Modelo do documento: 55=NF-e, 65=NFC-e
    /// </summary>
    public int Modelo { get; set; } = 55;
    
    /// <summary>
    /// Tipo de emissão: 1=Normal, 2=Contingência FS-IA, etc.
    /// </summary>
    public int TipoEmissao { get; set; } = 1;
    
    /// <summary>
    /// Finalidade: 1=Normal, 2=Complementar, 3=Ajuste, 4=Devolução
    /// </summary>
    public int Finalidade { get; set; } = 1;
    
    /// <summary>
    /// Indicador de presença do comprador: 0=Não se aplica, 1=Presencial, 9=Internet
    /// </summary>
    public int IndicadorPresenca { get; set; } = 9;
    
    /// <summary>
    /// CST padrão para ICMS (ex: "102" para Simples Nacional)
    /// </summary>
    public string CstIcms { get; set; } = "102";
    
    /// <summary>
    /// CSOSN para Simples Nacional
    /// </summary>
    public string Csosn { get; set; } = "102";
    
    /// <summary>
    /// CST PIS (ex: "99" para outras operações)
    /// </summary>
    public string CstPis { get; set; } = "99";
    
    /// <summary>
    /// CST COFINS (ex: "99" para outras operações)
    /// </summary>
    public string CstCofins { get; set; } = "99";
    
    /// <summary>
    /// NCM padrão para produtos (óculos de sol = 9004.10.00)
    /// </summary>
    public string NcmPadrao { get; set; } = "90041000";
    
    /// <summary>
    /// CFOP para venda dentro do estado
    /// </summary>
    public string CfopDentroEstado { get; set; } = "5102";
    
    /// <summary>
    /// CFOP para venda fora do estado
    /// </summary>
    public string CfopForaEstado { get; set; } = "6102";
    
    public EmitenteNfeConfig Emitente { get; set; } = new();
    public CertificadoDigitalConfig Certificado { get; set; } = new();
}
