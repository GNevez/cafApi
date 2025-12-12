using cafApi.Models;
using cafApi.Models.DTOs;

namespace cafApi.Services;

public interface ICorreiosService
{
    // Autenticação
    Task<CorreiosTokenResponse?> ObterTokenAsync();
    
    // Pré-Postagem
    Task<PrePostagemResponseDto?> CriarPrePostagemAsync(CriarPrePostagemDto dto);
    Task<PrePostagemResponseDto?> CriarPrePostagemParaPedidoAsync(int pedidoId, string? codigoServico = null);
    Task<PrePostagemResponseDto?> GetPrePostagemByIdAsync(int id);
    Task<PrePostagemResponseDto?> GetPrePostagemByPedidoIdAsync(int pedidoId);
    Task<PrePostagemPaginadoDto> GetPrePostagensAsync(PrePostagemFiltroDto filtro);
    Task<bool> CancelarPrePostagemAsync(int id);
    
    // Pré-Postagem API Correios (v2)
    Task<PrePostagemCorreiosPaginadoDto> ListarPrePostagensCorreiosAsync(PrePostagemCorreiosFiltroDto filtro);
    Task<PrePostagemPostadaDetalhesDto?> ConsultarPrePostagemPostadaAsync(string codigoObjeto);
    Task<bool> CancelarPrePostagemCorreiosAsync(string idPrePostagem);
    
    // Geração de Rótulos
    Task<GerarRotuloResponseDto> GerarRotuloRangeAsync(GerarRotuloRangeRequestDto dto);
    Task<GerarRotuloResponseDto> GerarRotuloLoteAsyncAsync(GerarRotuloLoteAsyncRequestDto dto);
    Task<GerarRotuloResponseDto> GerarRotuloRegistradoAsyncAsync(GerarRotuloRegistradoAsyncRequestDto dto);
    Task<GerarRotuloResponseDto> ConsultarRotuloAsync(string idRecibo);
    Task<GerarRotuloResponseDto> ConsultarRotuloAsync(string idRecibo, GerarRotuloRegistradoAsyncRequestDto? contexto);
    
    // Rastreamento (SRO - Rastro)
    Task<RastreamentoResponseDto?> RastrearObjetoAsync(string codigoRastreamento);
    Task<List<RastreamentoResponseDto>> RastrearMultiplosObjetosAsync(List<string> codigos);
    
    // Suspensão de entrega (SRO - Interatividade)
    Task<SuspenderEntregaResponseDto> SuspenderEntregaAsync(SuspenderEntregaRequestDto dto);
    Task<SuspenderEntregaResponseDto> ReativarEntregaAsync(string codigoRastreamento);
    
    // Serviços
    List<ServicoCorreiosDto> GetServicosDisponiveis();
    
    // Cálculo de Frete
    Task<List<CalcularFreteResponseDto>> CalcularFreteAsync(CalcularFreteRequestDto dto);
    
    // Atualização de status via rastreamento
    Task AtualizarStatusPrePostagensAsync();
}
