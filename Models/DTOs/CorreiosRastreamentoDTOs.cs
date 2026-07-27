namespace cafApi.Models.DTOs;

/// <summary>
/// Resposta da API de rastreamento dos Correios
/// GET /srorastro/v1/objetos/{codigoObjeto}
/// </summary>
public class CorreiosRastreamentoResponse
{
    public string CodObjeto { get; set; } = string.Empty;
    public List<CorreiosEvento> Eventos { get; set; } = new();
    public string TipoPostal { get; set; } = string.Empty;
    public string Modalidade { get; set; } = string.Empty;
}

/// <summary>
/// Evento de rastreamento individual
/// </summary>
public class CorreiosEvento
{
    public string Codigo { get; set; } = string.Empty;
    public string Descricao { get; set; } = string.Empty;
    public DateTime DtHrCriado { get; set; }
    public string Tipo { get; set; } = string.Empty;
    public CorreiosUnidade? Unidade { get; set; }
    public List<CorreiosObjeto>? UrlsIcone { get; set; }
    public CorreiosDestinatario? Destinatario { get; set; }
}

/// <summary>
/// Informações da unidade dos Correios
/// </summary>
public class CorreiosUnidade
{
    public string Endereco { get; set; } = string.Empty;
    public string Nome { get; set; } = string.Empty;
    public string Tipo { get; set; } = string.Empty;
    public CorreiosEndereco? EnderecoCompleto { get; set; }
}

/// <summary>
/// Endereço completo da unidade
/// </summary>
public class CorreiosEndereco
{
    public string Cidade { get; set; } = string.Empty;
    public string Uf { get; set; } = string.Empty;
    public string Bairro { get; set; } = string.Empty;
    public string Cep { get; set; } = string.Empty;
}

/// <summary>
/// Informações do destinatário
/// </summary>
public class CorreiosDestinatario
{
    public string Nome { get; set; } = string.Empty;
    public string Documento { get; set; } = string.Empty;
}

/// <summary>
/// Objeto/ícone adicional
/// </summary>
public class CorreiosObjeto
{
    public string Url { get; set; } = string.Empty;
    public string Descricao { get; set; } = string.Empty;
}

/// <summary>
/// Configurações dos Correios (incluindo polling)
/// </summary>
public class CorreiosConfig
{
    public string BaseUrl { get; set; } = "https://api.correios.com.br";
    public string Usuario { get; set; } = string.Empty;
    public string CodigoAcesso { get; set; } = string.Empty;
    public string CartaoPostagem { get; set; } = string.Empty;
    public int PollingIntervaloSegundos { get; set; } = 30;
    public bool PollingHabilitado { get; set; } = true;
}
