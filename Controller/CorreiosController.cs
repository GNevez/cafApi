using cafApi.Models;
using cafApi.Models.DTOs;
using cafApi.Services;
using cafApi.Attributes;
using cafApi.Contexts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace cafApi.Controller;

[ApiController]
[Route("api/[controller]")]
public class CorreiosController : ControllerBase
{
    private readonly ICorreiosService _correiosService;
    private readonly ILogger<CorreiosController> _logger;
    private readonly ApplicationDbContext _context;

    public CorreiosController(ICorreiosService correiosService, ILogger<CorreiosController> logger, ApplicationDbContext context)
    {
        _correiosService = correiosService;
        _logger = logger;
        _context = context;
    }

    #region Autenticação

    /// <summary>
    /// Obtém um novo token de autenticação dos Correios
    /// </summary>
    [HttpPost("token")]
    [RequireAdmin]
    public async Task<ActionResult<CorreiosTokenResponse>> ObterToken()
    {
        var token = await _correiosService.ObterTokenAsync();
        if (token == null)
        {
            return BadRequest(new { error = "Não foi possível obter token dos Correios" });
        }
        return Ok(token);
    }

    #endregion

    #region Pré-Postagem

    /// <summary>
    /// Lista pré-postagens com filtros e paginação
    /// </summary>
    [HttpGet("prepostagens")]
    [RequireAdmin]
    public async Task<ActionResult<PrePostagemPaginadoDto>> GetPrePostagens(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] StatusPrePostagem? status = null,
        [FromQuery] DateTime? dataInicio = null,
        [FromQuery] DateTime? dataFim = null)
    {
        var filtro = new PrePostagemFiltroDto
        {
            PageNumber = pageNumber,
            PageSize = pageSize,
            Status = status,
            DataInicio = dataInicio,
            DataFim = dataFim
        };

        var resultado = await _correiosService.GetPrePostagensAsync(filtro);
        return Ok(resultado);
    }

    /// <summary>
    /// Obtém uma pré-postagem por ID
    /// </summary>
    [HttpGet("prepostagens/{id}")]
    [RequireAdmin]
    public async Task<ActionResult<PrePostagemResponseDto>> GetPrePostagemById(int id)
    {
        var prePostagem = await _correiosService.GetPrePostagemByIdAsync(id);
        if (prePostagem == null)
        {
            return NotFound(new { error = "Pré-postagem não encontrada" });
        }
        return Ok(prePostagem);
    }

    /// <summary>
    /// Obtém a pré-postagem de um pedido
    /// </summary>
    [HttpGet("prepostagens/pedido/{pedidoId}")]
    [RequireAdmin]
    public async Task<ActionResult<PrePostagemResponseDto>> GetPrePostagemByPedidoId(int pedidoId)
    {
        var prePostagem = await _correiosService.GetPrePostagemByPedidoIdAsync(pedidoId);
        if (prePostagem == null)
        {
            return NotFound(new { error = "Pré-postagem não encontrada para este pedido" });
        }
        return Ok(prePostagem);
    }

    /// <summary>
    /// Cria uma nova pré-postagem
    /// </summary>
    [HttpPost("prepostagens")]
    [RequireAdmin]
    public async Task<ActionResult<PrePostagemResponseDto>> CriarPrePostagem([FromBody] CriarPrePostagemDto dto)
    {
        if (dto.PedidoId <= 0)
        {
            return BadRequest(new { error = "ID do pedido é obrigatório" });
        }

        var prePostagem = await _correiosService.CriarPrePostagemAsync(dto);
        if (prePostagem == null)
        {
            return BadRequest(new { error = "Não foi possível criar a pré-postagem" });
        }

        return CreatedAtAction(nameof(GetPrePostagemById), new { id = prePostagem.Id }, prePostagem);
    }

    /// <summary>
    /// Cria pré-postagens para múltiplos pedidos
    /// </summary>
    [HttpPost("prepostagens/lote")]
    [RequireAdmin]
    public async Task<ActionResult<List<PrePostagemResponseDto>>> CriarPrePostagensEmLote([FromBody] List<CriarPrePostagemDto> dtos)
    {
        if (dtos == null || dtos.Count == 0)
        {
            return BadRequest(new { error = "Nenhum pedido informado" });
        }

        var resultados = new List<PrePostagemResponseDto>();
        var erros = new List<object>();

        foreach (var dto in dtos)
        {
            try
            {
                var prePostagem = await _correiosService.CriarPrePostagemAsync(dto);
                if (prePostagem != null)
                {
                    resultados.Add(prePostagem);
                }
                else
                {
                    erros.Add(new { pedidoId = dto.PedidoId, erro = "Falha ao criar pré-postagem" });
                }
            }
            catch (Exception ex)
            {
                erros.Add(new { pedidoId = dto.PedidoId, erro = ex.Message });
            }
        }

        return Ok(new
        {
            sucesso = resultados,
            erros = erros,
            totalSucesso = resultados.Count,
            totalErros = erros.Count
        });
    }

    /// <summary>
    /// Cancela uma pré-postagem
    /// </summary>
    [HttpDelete("prepostagens/{id}")]
    [RequireAdmin]
    public async Task<IActionResult> CancelarPrePostagem(int id)
    {
        var cancelado = await _correiosService.CancelarPrePostagemAsync(id);
        if (!cancelado)
        {
            return BadRequest(new { error = "Não foi possível cancelar a pré-postagem" });
        }
        return Ok(new { message = "Pré-postagem cancelada com sucesso" });
    }

    #endregion

    #region Rastreamento (SRO - Rastro)

    /// <summary>
    /// Rastreia um objeto pelo código de rastreamento ou código do pedido (público)
    /// </summary>
    [HttpGet("rastreamento/publico/{codigo}")]
    public async Task<ActionResult<RastreamentoResponseDto>> RastrearObjetoPublico(string codigo)
    {
        if (string.IsNullOrWhiteSpace(codigo))
        {
            return BadRequest(new { error = "Código é obrigatório" });
        }

        string codigoRastreamento = codigo.ToUpper();

        // Se o código começa com CAF-, buscar o código de rastreamento pelo pedido
        if (codigo.ToUpper().StartsWith("CAF-"))
        {
            var pedido = await _context.Pedidos
                .FirstOrDefaultAsync(p => p.CodigoPedido == codigo.ToUpper());

            if (pedido == null)
            {
                return NotFound(new { error = "Pedido não encontrado" });
            }

            if (string.IsNullOrEmpty(pedido.CodigoRastreamento))
            {
                return Ok(new RastreamentoResponseDto
                {
                    CodigoObjeto = codigo.ToUpper(),
                    Mensagem = "Pedido encontrado, mas ainda não possui código de rastreamento. Aguarde a postagem do seu pedido.",
                    Eventos = new List<EventoRastreamentoDto>
                    {
                        new EventoRastreamentoDto
                        {
                            DataHora = pedido.DataPedido,
                            Descricao = "Pedido realizado",
                            Tipo = "Pedido",
                            Unidade = "Chase a Flare"
                        }
                    }
                });
            }

            codigoRastreamento = pedido.CodigoRastreamento;
        }

        var resultado = await _correiosService.RastrearObjetoAsync(codigoRastreamento);
        if (resultado == null)
        {
            return NotFound(new { error = "Objeto não encontrado" });
        }
        return Ok(resultado);
    }

    /// <summary>
    /// Rastreia um objeto pelo código de rastreamento
    /// </summary>
    [HttpGet("rastreamento/{codigo}")]
    public async Task<ActionResult<RastreamentoResponseDto>> RastrearObjeto(string codigo)
    {
        if (string.IsNullOrWhiteSpace(codigo))
        {
            return BadRequest(new { error = "Código de rastreamento é obrigatório" });
        }

        var resultado = await _correiosService.RastrearObjetoAsync(codigo.ToUpper());
        if (resultado == null)
        {
            return NotFound(new { error = "Objeto não encontrado" });
        }
        return Ok(resultado);
    }

    /// <summary>
    /// Rastreia múltiplos objetos
    /// </summary>
    [HttpPost("rastreamento/lote")]
    public async Task<ActionResult<List<RastreamentoResponseDto>>> RastrearMultiplosObjetos([FromBody] List<string> codigos)
    {
        if (codigos == null || codigos.Count == 0)
        {
            return BadRequest(new { error = "Nenhum código informado" });
        }

        if (codigos.Count > 50)
        {
            return BadRequest(new { error = "Máximo de 50 códigos por requisição" });
        }

        var resultados = await _correiosService.RastrearMultiplosObjetosAsync(codigos);
        return Ok(resultados);
    }

    #endregion

    #region Suspensão de Entrega (SRO - Interatividade)

    /// <summary>
    /// Suspende a entrega de um objeto
    /// </summary>
    [HttpPost("suspender")]
    [RequireAdmin]
    public async Task<ActionResult<SuspenderEntregaResponseDto>> SuspenderEntrega([FromBody] SuspenderEntregaRequestDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.CodigoRastreamento))
        {
            return BadRequest(new { error = "Código de rastreamento é obrigatório" });
        }

        if (string.IsNullOrWhiteSpace(dto.Motivo))
        {
            return BadRequest(new { error = "Motivo da suspensão é obrigatório" });
        }

        var resultado = await _correiosService.SuspenderEntregaAsync(dto);
        if (!resultado.Sucesso)
        {
            return BadRequest(resultado);
        }
        return Ok(resultado);
    }

    /// <summary>
    /// Reativa a entrega de um objeto suspenso
    /// </summary>
    [HttpPost("reativar/{codigo}")]
    [RequireAdmin]
    public async Task<ActionResult<SuspenderEntregaResponseDto>> ReativarEntrega(string codigo)
    {
        if (string.IsNullOrWhiteSpace(codigo))
        {
            return BadRequest(new { error = "Código de rastreamento é obrigatório" });
        }

        var resultado = await _correiosService.ReativarEntregaAsync(codigo.ToUpper());
        if (!resultado.Sucesso)
        {
            return BadRequest(resultado);
        }
        return Ok(resultado);
    }

    #endregion

    #region Serviços

    /// <summary>
    /// Lista os serviços de envio disponíveis
    /// </summary>
    [HttpGet("servicos")]
    public ActionResult<List<ServicoCorreiosDto>> GetServicosDisponiveis()
    {
        var servicos = _correiosService.GetServicosDisponiveis();
        return Ok(servicos);
    }

    #endregion

    #region Cálculo de Frete

    /// <summary>
    /// Calcula o frete para um CEP de destino
    /// </summary>
    [HttpPost("calcular-frete")]
    public async Task<ActionResult<List<CalcularFreteResponseDto>>> CalcularFrete([FromBody] CalcularFreteRequestDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.CepDestino))
        {
            return BadRequest(new { error = "CEP de destino é obrigatório" });
        }

        var resultados = await _correiosService.CalcularFreteAsync(dto);
        return Ok(resultados);
    }

    #endregion

    #region Atualização de Status

    /// <summary>
    /// Atualiza o status de todas as pré-postagens ativas
    /// </summary>
    [HttpPost("atualizar-status")]
    [RequireAdmin]
    public async Task<IActionResult> AtualizarStatusPrePostagens()
    {
        await _correiosService.AtualizarStatusPrePostagensAsync();
        return Ok(new { message = "Status das pré-postagens atualizados" });
    }

    #endregion

    #region Pré-Postagens API Correios (v2)

    /// <summary>
    /// Lista pré-postagens diretamente da API dos Correios (v2/prepostagens)
    /// </summary>
    [HttpGet("prepostagens-correios")]
    [RequireAdmin]
    public async Task<ActionResult<PrePostagemCorreiosPaginadoDto>> ListarPrePostagensCorreios(
        [FromQuery] string? id = null,
        [FromQuery] string? codigoObjeto = null,
        [FromQuery] string? eTicket = null,
        [FromQuery] string? codigoEstampa2D = null,
        [FromQuery] string? idCorreios = null,
        [FromQuery] string? status = null,
        [FromQuery] string? logisticaReversa = null,
        [FromQuery] string? tipoObjeto = null,
        [FromQuery] string? modalidadePagamento = null,
        [FromQuery] string? objetoCargo = null,
        [FromQuery] DateTime? dataInicialCriacaoPrePostagem = null,
        [FromQuery] DateTime? dataFinalCriacaoPrePostagem = null,
        [FromQuery] int page = 0,
        [FromQuery] int size = 10)
    {
        var filtro = new PrePostagemCorreiosFiltroDto
        {
            Id = id,
            CodigoObjeto = codigoObjeto,
            ETicket = eTicket,
            CodigoEstampa2D = codigoEstampa2D,
            IdCorreios = idCorreios,
            Status = status,
            LogisticaReversa = logisticaReversa,
            TipoObjeto = tipoObjeto,
            ModalidadePagamento = modalidadePagamento,
            ObjetoCargo = objetoCargo,
            DataInicialCriacaoPrePostagem = dataInicialCriacaoPrePostagem,
            DataFinalCriacaoPrePostagem = dataFinalCriacaoPrePostagem,
            Page = page,
            Size = Math.Min(size, 50) // Limite máximo de 50 por página
        };

        var resultado = await _correiosService.ListarPrePostagensCorreiosAsync(filtro);
        return Ok(resultado);
    }

    /// <summary>
    /// Consulta detalhes de uma pré-postagem postada pelo código do objeto
    /// </summary>
    [HttpGet("prepostagens-correios/postada/{codigoObjeto}")]
    [RequireAdmin]
    public async Task<ActionResult<PrePostagemPostadaDetalhesDto>> ConsultarPrePostagemPostada(string codigoObjeto)
    {
        if (string.IsNullOrWhiteSpace(codigoObjeto))
        {
            return BadRequest(new { error = "Código do objeto é obrigatório" });
        }

        var resultado = await _correiosService.ConsultarPrePostagemPostadaAsync(codigoObjeto.ToUpper());
        if (resultado == null)
        {
            return NotFound(new { error = "Pré-postagem não encontrada" });
        }
        return Ok(resultado);
    }

    /// <summary>
    /// Cancela uma pré-postagem diretamente nos Correios pelo ID
    /// </summary>
    [HttpDelete("prepostagens-correios/{idPrePostagem}")]
    [RequireAdmin]
    public async Task<IActionResult> CancelarPrePostagemCorreios(string idPrePostagem)
    {
        if (string.IsNullOrWhiteSpace(idPrePostagem))
        {
            return BadRequest(new { error = "ID da pré-postagem é obrigatório" });
        }

        var cancelado = await _correiosService.CancelarPrePostagemCorreiosAsync(idPrePostagem);
        if (!cancelado)
        {
            return BadRequest(new { error = "Não foi possível cancelar a pré-postagem nos Correios" });
        }
        return Ok(new { message = "Pré-postagem cancelada com sucesso nos Correios" });
    }

    #endregion

    #region Geração de Rótulos

    /// <summary>
    /// Gera rótulos por range de códigos de serviço (v1/prepostagens/rotulo/range)
    /// </summary>
    [HttpPost("rotulos/range")]
    [RequireAdmin]
    public async Task<ActionResult<GerarRotuloResponseDto>> GerarRotuloRange([FromBody] GerarRotuloRangeRequestDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.CodigoServico))
        {
            return BadRequest(new { error = "Código do serviço é obrigatório" });
        }

        if (dto.Quantidade <= 0)
        {
            return BadRequest(new { error = "Quantidade deve ser maior que zero" });
        }

        var resultado = await _correiosService.GerarRotuloRangeAsync(dto);
        
        // Se retornou PDF, retornar como arquivo
        if (resultado.Sucesso && resultado.PdfBytes != null)
        {
            return File(resultado.PdfBytes, "application/pdf", $"rotulos_{dto.CodigoServico}_{DateTime.Now:yyyyMMddHHmmss}.pdf");
        }

        if (!resultado.Sucesso)
        {
            return BadRequest(resultado);
        }

        return Ok(resultado);
    }

    /// <summary>
    /// Gera rótulos assíncronos em lote para objetos SIMPLES (v1/prepostagens/rotulo/lote/assincrono/pdf)
    /// </summary>
    [HttpPost("rotulos/lote-assincrono")]
    [RequireAdmin]
    public async Task<ActionResult<GerarRotuloResponseDto>> GerarRotuloLoteAsync([FromBody] GerarRotuloLoteAsyncRequestDto dto)
    {
        if (dto.IdsLotePrePostagem == null || dto.IdsLotePrePostagem.Count == 0)
        {
            return BadRequest(new { error = "Lista de pré-postagens é obrigatória" });
        }

        var resultado = await _correiosService.GerarRotuloLoteAsyncAsync(dto);
        if (!resultado.Sucesso)
        {
            return BadRequest(resultado);
        }

        return Ok(resultado);
    }

    /// <summary>
    /// Gera rótulos assíncronos para objetos REGISTRADOS (v1/prepostagens/rotulo/assincrono/pdf)
    /// </summary>
    [HttpPost("rotulos/registrado-assincrono")]
    [RequireAdmin]
    public async Task<ActionResult<GerarRotuloResponseDto>> GerarRotuloRegistradoAsync([FromBody] GerarRotuloRegistradoAsyncRequestDto dto)
    {
        if ((dto.CodigosObjeto == null || dto.CodigosObjeto.Count == 0) && 
            (dto.IdsPrePostagem == null || dto.IdsPrePostagem.Count == 0))
        {
            return BadRequest(new { error = "Códigos de objeto ou IDs de pré-postagem são obrigatórios" });
        }

        var resultado = await _correiosService.GerarRotuloRegistradoAsyncAsync(dto);
        if (!resultado.Sucesso)
        {
            return BadRequest(resultado);
        }

        return Ok(resultado);
    }

    /// <summary>
    /// Consulta/baixa um rótulo assíncrono pelo ID do recibo
    /// </summary>
    [HttpGet("rotulos/recibo/{idRecibo}")]
    [RequireAdmin]
    public async Task<ActionResult> ConsultarRotulo(
        string idRecibo,
        [FromQuery] int? idPedido = null,
        [FromQuery] string? idAtendimento = null,
        [FromQuery] string? codigosObjeto = null,
        [FromQuery] string? idsPrePostagem = null,
        [FromQuery] string? observacao = null,
        [FromQuery] string? tipoRotulo = null,
        [FromQuery] string? formatoRotulo = null)
    {
        // Criar contexto se foi passado
        GerarRotuloRegistradoAsyncRequestDto? contexto = null;
        if (idPedido.HasValue || !string.IsNullOrEmpty(idAtendimento))
        {
            contexto = new GerarRotuloRegistradoAsyncRequestDto
            {
                IdPedido = idPedido,
                IdAtendimento = idAtendimento,
                CodigosObjeto = !string.IsNullOrEmpty(codigosObjeto) 
                    ? JsonSerializer.Deserialize<List<string>>(codigosObjeto) ?? new List<string>()
                    : new List<string>(),
                IdsPrePostagem = !string.IsNullOrEmpty(idsPrePostagem)
                    ? JsonSerializer.Deserialize<List<string>>(idsPrePostagem) ?? new List<string>()
                    : new List<string>(),
                Observacao = observacao,
                TipoRotulo = tipoRotulo ?? "P",
                FormatoRotulo = formatoRotulo ?? "ET"
            };
        }
        
        var resultado = await _correiosService.ConsultarRotuloAsync(idRecibo, contexto);
        
        // Se tem PDF bytes, retornar como arquivo
        if (resultado.Sucesso && resultado.PdfBytes != null && resultado.PdfBytes.Length > 0)
        {
            return File(resultado.PdfBytes, "application/pdf", $"rotulo_{idRecibo}.pdf");
        }

        // Se tem URL mas não conseguiu baixar o PDF
        if (resultado.Sucesso && !string.IsNullOrEmpty(resultado.UrlRotulo))
        {
            return Ok(resultado);
        }

        // Se ainda está processando ou erro
        if (!resultado.Sucesso)
        {
            return StatusCode(202, resultado); // 202 Accepted - ainda processando
        }

        return Ok(resultado);
    }

    #endregion
}
