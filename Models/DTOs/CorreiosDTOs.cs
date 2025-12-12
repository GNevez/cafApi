namespace cafApi.Models.DTOs;

// DTOs para autenticação
public class CorreiosTokenResponse
{
    public string? Token { get; set; }
    public DateTime? Expira { get; set; }
    public string? CartaoPostagem { get; set; }
    public string? Contrato { get; set; }
}

// DTOs para pré-postagem
public class CriarPrePostagemDto
{
    public int PedidoId { get; set; }
    public string CodigoServico { get; set; } = "03220"; // SEDEX
    public decimal? Peso { get; set; }
    public int? Altura { get; set; }
    public int? Largura { get; set; }
    public int? Comprimento { get; set; }
    public decimal? ValorDeclarado { get; set; }
}

public class PrePostagemResponseDto
{
    public int Id { get; set; }
    public int PedidoId { get; set; }
    public string? CodigoPedido { get; set; }
    public string? ClienteNome { get; set; }
    public string? CodigoRastreamento { get; set; }
    public string? IdPrePostagem { get; set; }
    public string? NumeroEtiqueta { get; set; }
    public string CodigoServico { get; set; } = string.Empty;
    public string NomeServico { get; set; } = string.Empty;
    public decimal Peso { get; set; }
    public int Altura { get; set; }
    public int Largura { get; set; }
    public int Comprimento { get; set; }
    public decimal? ValorDeclarado { get; set; }
    public StatusPrePostagem Status { get; set; }
    public string StatusNome { get; set; } = string.Empty;
    public DateTime DataCriacao { get; set; }
    public DateTime? DataPostagem { get; set; }
    public DateTime? DataEntrega { get; set; }
    public string? Observacoes { get; set; }
    public string? MensagemErro { get; set; }
    
    // Dados do destinatário
    public DestinatarioDto? Destinatario { get; set; }
}

public class DestinatarioDto
{
    public string? Nome { get; set; }
    public string? Logradouro { get; set; }
    public string? Numero { get; set; }
    public string? Complemento { get; set; }
    public string? Bairro { get; set; }
    public string? Cidade { get; set; }
    public string? UF { get; set; }
    public string? CEP { get; set; }
}

// DTOs para rastreamento
public class RastreamentoRequestDto
{
    public string CodigoRastreamento { get; set; } = string.Empty;
}

public class RastreamentoResponseDto
{
    public string CodigoObjeto { get; set; } = string.Empty;
    public string? TipoPostal { get; set; }
    public List<EventoRastreamentoDto> Eventos { get; set; } = new();
    public string? Mensagem { get; set; }
}

public class EventoRastreamentoDto
{
    public DateTime DataHora { get; set; }
    public string? Descricao { get; set; }
    public string? Tipo { get; set; }
    public string? Unidade { get; set; }
    public string? Cidade { get; set; }
    public string? UF { get; set; }
}

// DTOs para suspensão de entrega
public class SuspenderEntregaRequestDto
{
    public string CodigoRastreamento { get; set; } = string.Empty;
    public string Motivo { get; set; } = string.Empty;
}

public class SuspenderEntregaResponseDto
{
    public bool Sucesso { get; set; }
    public string? Mensagem { get; set; }
    public string? CodigoRastreamento { get; set; }
    public DateTime? DataSuspensao { get; set; }
}

// DTOs para listagem
public class PrePostagemFiltroDto
{
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public StatusPrePostagem? Status { get; set; }
    public DateTime? DataInicio { get; set; }
    public DateTime? DataFim { get; set; }
}

public class PrePostagemPaginadoDto
{
    public List<PrePostagemResponseDto> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int PageNumber { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
}

// Serviços disponíveis dos Correios
public class ServicoCorreiosDto
{
    public string Codigo { get; set; } = string.Empty;
    public string Nome { get; set; } = string.Empty;
    public string Descricao { get; set; } = string.Empty;
}

public static class ServicosCorreios
{
    public static readonly List<ServicoCorreiosDto> Lista = new()
    {
        new() { Codigo = "03220", Nome = "SEDEX", Descricao = "Entrega expressa" },
        new() { Codigo = "03298", Nome = "PAC", Descricao = "Entrega econômica" },
        new() { Codigo = "03140", Nome = "SEDEX 10", Descricao = "Entrega até às 10h" },
        new() { Codigo = "03204", Nome = "SEDEX 12", Descricao = "Entrega até às 12h" },
        new() { Codigo = "03158", Nome = "SEDEX Hoje", Descricao = "Entrega no mesmo dia" },
    };
    
    public static string GetNome(string codigo)
    {
        return Lista.FirstOrDefault(s => s.Codigo == codigo)?.Nome ?? "Desconhecido";
    }
}

// DTOs para cálculo de frete
public class CalcularFreteRequestDto
{
    public string CepDestino { get; set; } = string.Empty;
    public decimal Peso { get; set; } // em kg
    public int Altura { get; set; } // em cm
    public int Largura { get; set; } // em cm
    public int Comprimento { get; set; } // em cm
    public decimal? ValorDeclarado { get; set; }
    public List<string>? CodigosServico { get; set; } // Se null, calcula todos
    public int? CarrinhoId { get; set; } // Opcional, para rastreamento
}

public class CalcularFreteResponseDto
{
    public string CodigoServico { get; set; } = string.Empty;
    public string NomeServico { get; set; } = string.Empty;
    public decimal Preco { get; set; }
    public int PrazoEntrega { get; set; } // em dias úteis
    public DateTime? DataPrevistaEntrega { get; set; }
    public string? Mensagem { get; set; }
    public bool Erro { get; set; }
}

// ============== DTOs para Rótulos ==============

/// <summary>
/// Request para geração de rótulos por range (v1/prepostagens/rotulo/range)
/// </summary>
public class GerarRotuloRangeRequestDto
{
    public string CodigoServico { get; set; } = string.Empty;
    public int Quantidade { get; set; }
}

/// <summary>
/// Request para geração de rótulos assíncronos em lote para objetos SIMPLES
/// (v1/prepostagens/rotulo/lote/assincrono/pdf)
/// </summary>
public class GerarRotuloLoteAsyncRequestDto
{
    public string? IdCorreios { get; set; }
    public string? NumeroCartaoPostagem { get; set; }
    public string TipoRotulo { get; set; } = "P"; // P = Padrão
    public string FormatoRotulo { get; set; } = "ET"; // ET = Etiqueta
    public string? IdAtendimento { get; set; }
    public string ImprimeRemetente { get; set; } = "S"; // S = Sim
    public List<ItemLotePrePostagem> IdsLotePrePostagem { get; set; } = new();
    public string LayoutImpressao { get; set; } = "PADRAO";
}

public class ItemLotePrePostagem
{
    public string IdPrePostagem { get; set; } = string.Empty;
    public string CodigoObjeto { get; set; } = string.Empty;
    public int Sequencial { get; set; }
}

/// <summary>
/// Request para geração de rótulos assíncronos para objetos REGISTRADOS
/// (v1/prepostagens/rotulo/assincrono/pdf)
/// </summary>
public class GerarRotuloRegistradoAsyncRequestDto
{
    public int? IdPedido { get; set; }
    public List<string> CodigosObjeto { get; set; } = new();
    public string? IdCorreios { get; set; }
    public string? NumeroCartaoPostagem { get; set; }
    public string TipoRotulo { get; set; } = "P"; // P = Padrão
    public string FormatoRotulo { get; set; } = "ET"; // ET = Etiqueta
    public string? IdAtendimento { get; set; }
    public string ImprimeRemetente { get; set; } = "S"; // S = Sim
    public List<string> IdsPrePostagem { get; set; } = new();
    public string LayoutImpressao { get; set; } = "PADRAO";
    public string? Observacao { get; set; }
}

/// <summary>
/// Resposta da geração de rótulos
/// </summary>
public class GerarRotuloResponseDto
{
    public bool Sucesso { get; set; }
    public string? IdRecibo { get; set; }
    public string? UrlRotulo { get; set; }
    public byte[]? PdfBytes { get; set; }
    public string? Mensagem { get; set; }
    
    // Contexto para salvar no banco quando o PDF estiver pronto
    public int? IdPedido { get; set; }
    public string? IdAtendimento { get; set; }
    public List<string>? CodigosObjeto { get; set; }
    public List<string>? IdsPrePostagem { get; set; }
    public string? Observacao { get; set; }
    public string? TipoRotulo { get; set; }
    public string? FormatoRotulo { get; set; }
}

// ============== DTOs para Listagem API Correios (v2/prepostagens) ==============

/// <summary>
/// Filtro para listagem de pré-postagens da API Correios (v2/prepostagens)
/// </summary>
public class PrePostagemCorreiosFiltroDto
{
    public string? Id { get; set; }
    public string? CodigoObjeto { get; set; }
    public string? ETicket { get; set; }
    public string? CodigoEstampa2D { get; set; }
    public string? IdCorreios { get; set; }
    public string? Status { get; set; }
    public string? LogisticaReversa { get; set; }
    public string? TipoObjeto { get; set; }
    public string? ModalidadePagamento { get; set; }
    public string? ObjetoCargo { get; set; }
    public DateTime? DataInicialCriacaoPrePostagem { get; set; }
    public DateTime? DataFinalCriacaoPrePostagem { get; set; }
    public int Page { get; set; } = 0;
    public int Size { get; set; } = 10;
}

/// <summary>
/// Item de pré-postagem retornado pela API Correios
/// </summary>
public class PrePostagemCorreiosItemDto
{
    public string? Id { get; set; }
    public string? IdCorreios { get; set; }
    public string? CodigoObjeto { get; set; }
    public string? CodigoServico { get; set; }
    public string? Status { get; set; }
    public string? StatusDescricao { get; set; }
    public DateTime? DataCriacao { get; set; }
    public DateTime? DataPostagem { get; set; }
    public decimal? Peso { get; set; }
    public decimal? PrecoServico { get; set; }
    public RemetenteDestinatarioDto? Remetente { get; set; }
    public RemetenteDestinatarioDto? Destinatario { get; set; }
}

public class RemetenteDestinatarioDto
{
    public string? Nome { get; set; }
    public string? CpfCnpj { get; set; }
    public string? Email { get; set; }
    public string? Telefone { get; set; }
    public EnderecoCorreiosDto? Endereco { get; set; }
}

public class EnderecoCorreiosDto
{
    public string? Cep { get; set; }
    public string? Logradouro { get; set; }
    public string? Numero { get; set; }
    public string? Complemento { get; set; }
    public string? Bairro { get; set; }
    public string? Cidade { get; set; }
    public string? Uf { get; set; }
}

/// <summary>
/// Resposta paginada da API Correios (v2/prepostagens)
/// </summary>
public class PrePostagemCorreiosPaginadoDto
{
    public List<PrePostagemCorreiosItemDto> Itens { get; set; } = new();
    public int TotalElements { get; set; }
    public int TotalPages { get; set; }
    public int Page { get; set; }
    public int Size { get; set; }
    public bool First { get; set; }
    public bool Last { get; set; }
}

/// <summary>
/// Detalhes de uma pré-postagem postada (v1/prepostagens/postada/{codigoObjeto})
/// </summary>
public class PrePostagemPostadaDetalhesDto
{
    public string? CodigoObjeto { get; set; }
    public string? IdPrePostagem { get; set; }
    public string? CodigoServico { get; set; }
    public string? NomeServico { get; set; }
    public string? Status { get; set; }
    public DateTime? DataCriacao { get; set; }
    public DateTime? DataPostagem { get; set; }
    public decimal? Peso { get; set; }
    public decimal? Altura { get; set; }
    public decimal? Largura { get; set; }
    public decimal? Comprimento { get; set; }
    public decimal? ValorDeclarado { get; set; }
    public decimal? PrecoServico { get; set; }
    public decimal? PrecoPrePostagem { get; set; }
    public string? NumeroNotaFiscal { get; set; }
    public string? NumeroCartaoPostagem { get; set; }
    public string? IdAtendimento { get; set; }
    public string? Eticket { get; set; }
    public DateTime? DataEticket { get; set; }
    public DateTime? PrazoPostagem { get; set; }
    public int? ModalidadePagamento { get; set; }
    public RemetenteDestinatarioDto? Remetente { get; set; }
    public RemetenteDestinatarioDto? Destinatario { get; set; }
    public List<ServicoAdicionalDto>? ServicosAdicionais { get; set; }
    public List<ItemDeclaracaoConteudoDto>? ItensDeclaracao { get; set; }
    public string? Observacao { get; set; }
    public string? RespostaJson { get; set; } // JSON completo da resposta
}

public class ServicoAdicionalDto
{
    public string? Codigo { get; set; }
    public string? Nome { get; set; }
    public decimal? Valor { get; set; }
    public decimal? ValorDeclarado { get; set; }
}

public class ItemDeclaracaoConteudoDto
{
    public string? Conteudo { get; set; }
    public string? Quantidade { get; set; }
    public string? Valor { get; set; }
}
