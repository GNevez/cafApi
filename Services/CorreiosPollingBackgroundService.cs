using cafApi.Contexts;
using cafApi.Models;
using cafApi.Models.DTOs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace cafApi.Services;

/// <summary>
/// Serviço de background que consulta automaticamente o rastreamento dos Correios
/// para atualizar status de pedidos e devoluções em aberto
/// </summary>
public class CorreiosPollingBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<CorreiosPollingBackgroundService> _logger;
    private readonly CorreiosConfig _config;

    public CorreiosPollingBackgroundService(
        IServiceProvider serviceProvider,
        ILogger<CorreiosPollingBackgroundService> logger,
        IOptions<CorreiosConfig> config)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _config = config.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_config.PollingHabilitado)
        {
            _logger.LogInformation("Polling dos Correios está desabilitado");
            return;
        }

        _logger.LogInformation("Serviço de polling dos Correios iniciado. Intervalo: {Segundos} segundos", 
            _config.PollingIntervaloSegundos);

        // Aguarda 5 segundos antes da primeira execução
        await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessarRastreamentosAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao processar rastreamentos dos Correios");
            }

            // Aguarda o intervalo configurado antes da próxima execução
            await Task.Delay(TimeSpan.FromSeconds(_config.PollingIntervaloSegundos), stoppingToken);
        }
    }

    private async Task ProcessarRastreamentosAsync()
    {
        _logger.LogInformation("Iniciando verificação de rastreamentos...");

        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var rastreamentoService = scope.ServiceProvider.GetRequiredService<ICorreiosRastreamentoService>();
        var webhookService = scope.ServiceProvider.GetRequiredService<ICorreiosWebhookService>();

        // 1. Buscar pedidos em aberto com código de rastreamento
        var pedidosEmAberto = await context.Pedidos
            .Where(p => p.CodigoRastreamento != null && p.CodigoRastreamento != "" &&
                       (p.Status == StatusPedido.EmSeparacao || 
                        p.Status == StatusPedido.ACaminho))
            .ToListAsync();

        _logger.LogInformation("Encontrados {Count} pedidos em aberto para rastrear", pedidosEmAberto.Count);

        // 2. Buscar devoluções em aberto com código de rastreamento
        var devolucoesEmAberto = await context.Devolucoes
            .Where(d => d.CodigoRastreamento != null && d.CodigoRastreamento != "" &&
                       (d.Status == DevolucaoStatus.Enviado))
            .ToListAsync();

        _logger.LogInformation("Encontradas {Count} devoluções em aberto para rastrear", devolucoesEmAberto.Count);

        // 3. Consolidar todos os códigos de rastreamento
        var codigosPedidos = pedidosEmAberto
            .Select(p => p.CodigoRastreamento!)
            .ToList();

        var codigosDevolucoes = devolucoesEmAberto
            .Select(d => d.CodigoRastreamento!)
            .ToList();

        var todosOsCodigos = codigosPedidos.Concat(codigosDevolucoes).Distinct().ToList();

        if (!todosOsCodigos.Any())
        {
            _logger.LogInformation("Nenhum objeto para rastrear");
            return;
        }

        // 4. Consultar rastreamentos na API dos Correios
        var rastreamentos = await rastreamentoService.RastrearVariosObjetosAsync(todosOsCodigos);

        _logger.LogInformation("Obtidos {Count} rastreamentos da API dos Correios", rastreamentos.Count);

        // 5. Processar cada rastreamento
        int pedidosAtualizados = 0;
        int devolucoesAtualizadas = 0;

        foreach (var rastreamento in rastreamentos)
        {
            if (rastreamento.Eventos == null || !rastreamento.Eventos.Any())
            {
                continue;
            }

            // Pega o evento mais recente
            var eventoMaisRecente = rastreamento.Eventos
                .OrderByDescending(e => e.DtHrCriado)
                .First();

            // Criar payload no formato do webhook para reutilizar a lógica
            var payload = new CorreiosWebhookPayload
            {
                CodigoObjeto = rastreamento.CodObjeto,
                TipoEvento = eventoMaisRecente.Codigo,
                DescricaoEvento = eventoMaisRecente.Descricao,
                DataEvento = eventoMaisRecente.DtHrCriado,
                Unidade = eventoMaisRecente.Unidade?.Nome,
                Cidade = eventoMaisRecente.Unidade?.EnderecoCompleto?.Cidade,
                Uf = eventoMaisRecente.Unidade?.EnderecoCompleto?.Uf
            };

            try
            {
                // Processar através do serviço de webhook (reutiliza toda a lógica)
                var resultado = await webhookService.ProcessarEventoAsync(payload);

                if (resultado.Processado)
                {
                    if (resultado.TipoObjeto == TipoObjetoCorreios.Pedido)
                    {
                        pedidosAtualizados++;
                    }
                    else if (resultado.TipoObjeto == TipoObjetoCorreios.Devolucao)
                    {
                        devolucoesAtualizadas++;
                    }

                    _logger.LogInformation("Rastreamento processado: {Codigo} - {Acao}", 
                        rastreamento.CodObjeto, resultado.AcaoTomada);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao processar rastreamento {Codigo}", rastreamento.CodObjeto);
            }
        }

        _logger.LogInformation(
            "Verificação concluída: {Pedidos} pedidos e {Devolucoes} devoluções atualizados",
            pedidosAtualizados, devolucoesAtualizadas);
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Serviço de polling dos Correios parado");
        return base.StopAsync(cancellationToken);
    }
}
