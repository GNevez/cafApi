using cafApi.Contexts;
using cafApi.Models;
using cafApi.Models.DTOs;
using Microsoft.EntityFrameworkCore;

namespace cafApi.Services;

public interface IRotuloAutomaticoService
{
    Task<Rotulo?> GerarRotuloParaPedidoAsync(int pedidoId, PrePostagem prePostagem);
    Task AdicionarFilaImpressaoAsync(Rotulo rotulo, int? pedidoId = null, string? codigoPedido = null);
}

public class RotuloAutomaticoService : IRotuloAutomaticoService
{
    private readonly ICorreiosService _correiosService;
    private readonly ApplicationDbContext _context;
    private readonly ILogger<RotuloAutomaticoService> _logger;
    private readonly IConfiguration _configuration;

    public RotuloAutomaticoService(
        ICorreiosService correiosService,
        ApplicationDbContext context,
        ILogger<RotuloAutomaticoService> logger,
        IConfiguration configuration)
    {
        _correiosService = correiosService;
        _context = context;
        _logger = logger;
        _configuration = configuration;
    }

    public async Task<Rotulo?> GerarRotuloParaPedidoAsync(int pedidoId, PrePostagem prePostagem)
    {
        try
        {
            if (string.IsNullOrEmpty(prePostagem.CodigoRastreamento) || string.IsNullOrEmpty(prePostagem.IdPrePostagem))
            {
                _logger.LogWarning("[RotuloAutomatico] Pré-postagem sem código de rastreamento ou ID - PedidoId: {PedidoId}", pedidoId);
                return null;
            }

            _logger.LogInformation("[RotuloAutomatico] Iniciando geração de rótulo para Pedido #{PedidoId} - Código: {Codigo}", 
                pedidoId, prePostagem.CodigoRastreamento);

            // Gerar rótulo via API dos Correios
            var request = new GerarRotuloRegistradoAsyncRequestDto
            {
                IdPedido = pedidoId,
                CodigosObjeto = new List<string> { prePostagem.CodigoRastreamento },
                IdsPrePostagem = new List<string> { prePostagem.IdPrePostagem },
                TipoRotulo = "P", // Padrão
                FormatoRotulo = "ET", // Etiqueta
                ImprimeRemetente = "S",
                LayoutImpressao = "PADRAO",
                Observacao = $"Gerado automaticamente via webhook - Pedido #{pedidoId}"
            };

            var resultado = await _correiosService.GerarRotuloRegistradoAsyncAsync(request);

            if (!resultado.Sucesso || string.IsNullOrEmpty(resultado.IdRecibo))
            {
                _logger.LogError("[RotuloAutomatico] Erro ao solicitar rótulo: {Mensagem}", resultado.Mensagem);
                return null;
            }

            _logger.LogInformation("[RotuloAutomatico] Rótulo solicitado - IdRecibo: {IdRecibo}. Aguardando processamento...", resultado.IdRecibo);

            // Aguardar processamento e baixar o PDF (polling)
            GerarRotuloResponseDto? pdfResultado = null;
            for (int tentativa = 0; tentativa < 15; tentativa++) // Máximo 30 segundos
            {
                await Task.Delay(2000); // Aguardar 2 segundos entre tentativas

                pdfResultado = await _correiosService.ConsultarRotuloAsync(resultado.IdRecibo, request);

                if (pdfResultado.Sucesso && pdfResultado.PdfBytes != null && pdfResultado.PdfBytes.Length > 0)
                {
                    _logger.LogInformation("[RotuloAutomatico] ✓ PDF recebido na tentativa {Tentativa}", tentativa + 1);
                    break;
                }

                _logger.LogDebug("[RotuloAutomatico] Tentativa {Tentativa}/15 - Ainda processando...", tentativa + 1);
            }

            if (pdfResultado == null || pdfResultado.PdfBytes == null || pdfResultado.PdfBytes.Length == 0)
            {
                _logger.LogError("[RotuloAutomatico] Timeout ao aguardar PDF do rótulo");
                return null;
            }

            // O PDF já foi salvo automaticamente pelo ConsultarRotuloAsync
            // Buscar o registro do rótulo que foi criado
            var rotulo = await _context.Rotulos
                .OrderByDescending(r => r.Id)
                .FirstOrDefaultAsync(r => r.IdRecibo == resultado.IdRecibo);

            if (rotulo == null)
            {
                _logger.LogWarning("[RotuloAutomatico] Rótulo não encontrado no banco após geração");
                return null;
            }

            _logger.LogInformation("[RotuloAutomatico] ✓ Rótulo gerado e salvo - ID: {RotuloId}, Arquivo: {Arquivo}", 
                rotulo.Id, rotulo.NomeArquivo);

            return rotulo;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[RotuloAutomatico] Exceção ao gerar rótulo para Pedido #{PedidoId}", pedidoId);
            return null;
        }
    }

    public async Task AdicionarFilaImpressaoAsync(Rotulo rotulo, int? pedidoId = null, string? codigoPedido = null)
    {
        try
        {
            var impressoraPadrao = _configuration["Impressao:ImpressoraPadrao"];

            var item = new FilaImpressao
            {
                RotuloId = rotulo.Id,
                PedidoId = pedidoId ?? rotulo.IdPedido,
                CodigoPedido = codigoPedido,
                NomeArquivo = rotulo.NomeArquivo,
                CaminhoArquivo = rotulo.CaminhoArquivo,
                ImpressoraDestino = impressoraPadrao,
                Copias = 1,
                Status = StatusImpressao.Pendente,
                DataCriacao = DateTime.UtcNow
            };

            _context.FilaImpressao.Add(item);
            await _context.SaveChangesAsync();

            _logger.LogInformation("[RotuloAutomatico] 🖨️ Rótulo adicionado à fila de impressão - FilaId: {FilaId}, Pedido: {CodigoPedido}", 
                item.Id, codigoPedido);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[RotuloAutomatico] Erro ao adicionar rótulo à fila de impressão");
        }
    }
}
