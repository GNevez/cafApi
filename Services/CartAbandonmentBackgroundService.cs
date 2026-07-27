using cafApi.Contexts;
using cafApi.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace cafApi.Services;

/// <summary>
/// Configurações do serviço de recuperação de carrinho abandonado
/// </summary>
public class CartAbandonmentConfig
{
    /// <summary>
    /// Se o serviço de recuperação está habilitado
    /// </summary>
    public bool Habilitado { get; set; } = true;
    
    /// <summary>
    /// Intervalo em segundos entre cada verificação de carrinhos abandonados
    /// </summary>
    public int IntervaloSegundos { get; set; } = 20;
    
    /// <summary>
    /// Segundos sem atividade para considerar um carrinho abandonado (para testes, em produção usar horas * 3600)
    /// </summary>
    public int SegundosParaAbandono { get; set; } = 10;
    
    /// <summary>
    /// Máximo de emails de recuperação por carrinho
    /// </summary>
    public int MaxEmailsPorCarrinho { get; set; } = 1;
}

/// <summary>
/// Serviço de background que verifica periodicamente carrinhos abandonados
/// e envia emails de recuperação para os clientes
/// </summary>
public class CartAbandonmentBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<CartAbandonmentBackgroundService> _logger;
    private readonly CartAbandonmentConfig _config;

    public CartAbandonmentBackgroundService(
        IServiceProvider serviceProvider,
        ILogger<CartAbandonmentBackgroundService> logger,
        IOptions<CartAbandonmentConfig> config)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _config = config.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_config.Habilitado)
        {
            _logger.LogInformation("[CartAbandonment] Serviço de recuperação de carrinho abandonado está desabilitado");
            return;
        }

        _logger.LogInformation(
            "[CartAbandonment] Serviço iniciado. Intervalo: {Segundos}s, Abandono após: {SegundosAbandono}s", 
            _config.IntervaloSegundos,
            _config.SegundosParaAbandono);

        // Aguarda 5 segundos antes da primeira execução
        await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessarCarrinhosAbandonadosAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[CartAbandonment] Erro ao processar carrinhos abandonados");
            }

            // Aguarda o intervalo configurado antes da próxima execução
            await Task.Delay(TimeSpan.FromSeconds(_config.IntervaloSegundos), stoppingToken);
        }
    }

    private async Task ProcessarCarrinhosAbandonadosAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("[CartAbandonment] Iniciando processamento de carrinhos...");

        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();
        var cartService = scope.ServiceProvider.GetRequiredService<ICartService>();

        var limiteDataAtualizacao = DateTime.UtcNow.AddSeconds(-_config.SegundosParaAbandono);

        // PASSO 1: Marcar carrinhos ativos como abandonados (após tempo sem atividade)
        var carrinhosParaAbandonar = await context.Carrinhos
            .Where(c => 
                c.Ativo &&
                c.ClienteId != null &&
                c.Status == StatusCarrinho.Ativo &&
                c.Itens.Any() &&
                (c.DataAtualizacao ?? c.DataCriacao) < limiteDataAtualizacao
            )
            .ToListAsync(stoppingToken);

        if (carrinhosParaAbandonar.Any())
        {
            _logger.LogInformation("[CartAbandonment] Marcando {Count} carrinhos como abandonados", 
                carrinhosParaAbandonar.Count);
            
            foreach (var carrinho in carrinhosParaAbandonar)
            {
                carrinho.Status = StatusCarrinho.Abandonado;
                carrinho.DataAtualizacao = DateTime.UtcNow;
            }
            await context.SaveChangesAsync(stoppingToken);
        }

        // PASSO 2: Enviar email para carrinhos abandonados que ainda não receberam
        var carrinhosParaEmail = await context.Carrinhos
            .Include(c => c.Cliente)
            .Include(c => c.Itens)
                .ThenInclude(i => i.Produto)
                    .ThenInclude(p => p.CoresDisponiveis)
                        .ThenInclude(pc => pc.Imagens)
            .Include(c => c.Itens)
                .ThenInclude(i => i.Cor)
            .Where(c => 
                c.Ativo &&
                c.ClienteId != null &&
                c.Cliente != null &&
                c.Status == StatusCarrinho.Abandonado &&
                c.Itens.Any() &&
                c.EmailRecuperacaoCount < _config.MaxEmailsPorCarrinho
            )
            .ToListAsync(stoppingToken);

        if (!carrinhosParaEmail.Any())
        {
            _logger.LogInformation("[CartAbandonment] Nenhum carrinho abandonado para enviar email");
            return;
        }

        _logger.LogInformation("[CartAbandonment] Enviando email para {Count} carrinhos abandonados", 
            carrinhosParaEmail.Count);

        var enviados = 0;
        var erros = 0;

        foreach (var carrinho in carrinhosParaEmail)
        {
            if (stoppingToken.IsCancellationRequested) break;

            try
            {
                // Enviar email de recuperação
                await emailService.EnviarEmailCarrinhoAbandonadoAsync(carrinho);
                
                // Marcar email como enviado e mudar status para Expirado
                await cartService.MarkAbandonmentEmailSentAsync(carrinho.Id);
                
                enviados++;
                
                _logger.LogInformation(
                    "[CartAbandonment] Email enviado para carrinho #{CarrinhoId} (Cliente: {Email}) - Status: Expirado",
                    carrinho.Id,
                    carrinho.Cliente!.Email);

                // Pequeno delay entre emails para não sobrecarregar o servidor SMTP
                await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
            }
            catch (Exception ex)
            {
                erros++;
                _logger.LogError(ex, 
                    "[CartAbandonment] Erro ao enviar email para carrinho #{CarrinhoId}", 
                    carrinho.Id);
            }
        }

        _logger.LogInformation(
            "[CartAbandonment] Processamento concluído. Enviados: {Enviados}, Erros: {Erros}", 
            enviados, erros);
    }
}
