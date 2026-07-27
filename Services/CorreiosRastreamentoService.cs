using System.Net.Http.Headers;
using System.Text.Json;
using cafApi.Models.DTOs;

namespace cafApi.Services;

public interface ICorreiosRastreamentoService
{
    Task<CorreiosRastreamentoResponse?> RastrearObjetoAsync(string codigoRastreamento);
    Task<List<CorreiosRastreamentoResponse>> RastrearVariosObjetosAsync(List<string> codigosRastreamento);
}

/// <summary>
/// Serviço para consultar rastreamento na API dos Correios
/// Reutiliza a autenticação do CorreiosService existente
/// </summary>
public class CorreiosRastreamentoService : ICorreiosRastreamentoService
{
    private readonly ICorreiosService _correiosService;
    private readonly HttpClient _httpClient;
    private readonly ILogger<CorreiosRastreamentoService> _logger;
    private readonly IConfiguration _configuration;

    public CorreiosRastreamentoService(
        ICorreiosService correiosService,
        HttpClient httpClient,
        ILogger<CorreiosRastreamentoService> logger,
        IConfiguration configuration)
    {
        _correiosService = correiosService;
        _httpClient = httpClient;
        _logger = logger;
        _configuration = configuration;
        
        var baseUrl = _configuration["Correios:BaseUrl"] ?? "https://api.correios.com.br";
        _httpClient.BaseAddress = new Uri(baseUrl);
        _httpClient.Timeout = TimeSpan.FromSeconds(30);
    }

    /// <summary>
    /// Rastreia um único objeto pelos Correios
    /// </summary>
    public async Task<CorreiosRastreamentoResponse?> RastrearObjetoAsync(string codigoRastreamento)
    {
        try
        {
            if (string.IsNullOrEmpty(codigoRastreamento))
            {
                _logger.LogWarning("Código de rastreamento vazio");
                return null;
            }

            // Reutiliza o método ObterTokenAsync do CorreiosService
            var tokenResponse = await _correiosService.ObterTokenAsync();
            if (tokenResponse == null || string.IsNullOrEmpty(tokenResponse.Token))
            {
                _logger.LogError("Não foi possível obter token para rastreamento");
                return null;
            }

            var request = new HttpRequestMessage(
                HttpMethod.Get,
                $"/srorastro/v1/objetos/{codigoRastreamento}"
            );
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenResponse.Token);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var response = await _httpClient.SendAsync(request);

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                _logger.LogWarning("Objeto {Codigo} não encontrado no sistema dos Correios", codigoRastreamento);
                return null;
            }

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Erro ao rastrear objeto {Codigo}: {StatusCode}", 
                    codigoRastreamento, response.StatusCode);
                return null;
            }

            var content = await response.Content.ReadAsStringAsync();
            var rastreamento = JsonSerializer.Deserialize<CorreiosRastreamentoResponse>(content, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            return rastreamento;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao rastrear objeto {Codigo}", codigoRastreamento);
            return null;
        }
    }

    /// <summary>
    /// Rastreia múltiplos objetos em paralelo (com limite de concorrência)
    /// </summary>
    public async Task<List<CorreiosRastreamentoResponse>> RastrearVariosObjetosAsync(List<string> codigosRastreamento)
    {
        var resultados = new List<CorreiosRastreamentoResponse>();

        if (codigosRastreamento == null || !codigosRastreamento.Any())
        {
            return resultados;
        }

        _logger.LogInformation("Iniciando rastreamento de {Count} objetos", codigosRastreamento.Count);

        // Processa em lotes de 5 para não sobrecarregar a API
        var lotes = codigosRastreamento.Chunk(5);

        foreach (var lote in lotes)
        {
            var tasks = lote.Select(codigo => RastrearObjetoAsync(codigo));
            var resultadosLote = await Task.WhenAll(tasks);

            resultados.AddRange(resultadosLote.Where(r => r != null)!);

            // Delay entre lotes para respeitar rate limit
            await Task.Delay(1000);
        }

        _logger.LogInformation("Rastreamento concluído: {Sucesso}/{Total} objetos", 
            resultados.Count, codigosRastreamento.Count);

        return resultados;
    }

}
