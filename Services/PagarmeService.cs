using System.Text;
using System.Text.Json;
using cafApi.Models.DTOs;

namespace cafApi.Services;

public interface IPagarmeService
{
    Task<PagarmeOrderResponse> CreateOrderAsync(PagarmeCreateOrderRequest request);
    Task<PagarmeOrderResponse> GetOrderAsync(string orderId);
    Task<PagarmeRefundResponse> RefundChargeAsync(string chargeId, decimal amount);
    string GetPublicKey();
}

public class PagarmeService : IPagarmeService
{
    private readonly IConfiguration _configuration;
    private readonly HttpClient _httpClient;
    private readonly string _secretKey;
    private readonly string _publicKey;
    private readonly string _baseEndpoint;

    public PagarmeService(IConfiguration configuration, IHttpClientFactory httpClientFactory)
    {
        _configuration = configuration;
        _httpClient = httpClientFactory.CreateClient();
        
        _secretKey = Environment.GetEnvironmentVariable("PAGARME_SECRET_KEY") 
                     ?? _configuration["Pagarme:SecretKey"] 
                     ?? throw new InvalidOperationException("Pagar.me Secret Key não configurada");
        
        _publicKey = Environment.GetEnvironmentVariable("PAGARME_PUBLIC_KEY") 
                     ?? _configuration["Pagarme:PublicKey"] 
                     ?? throw new InvalidOperationException("Pagar.me Public Key não configurada");
        
        _baseEndpoint = Environment.GetEnvironmentVariable("PAGARME_BASE_ENDPOINT") 
                        ?? _configuration["Pagarme:BaseEndpoint"] 
                        ?? "https://api.pagar.me/core/v5";

        // Configurar autenticação básica (Basic Auth com secret_key)
        var authBytes = Encoding.UTF8.GetBytes($"{_secretKey}:");
        var authHeader = Convert.ToBase64String(authBytes);
        _httpClient.DefaultRequestHeaders.Authorization = 
            new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", authHeader);
    }

    public string GetPublicKey()
    {
        return _publicKey;
    }

    public async Task<PagarmeOrderResponse> CreateOrderAsync(PagarmeCreateOrderRequest request)
    {
        try
        {
            var jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            };

            var json = JsonSerializer.Serialize(request, jsonOptions);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            Console.WriteLine($"[Pagarme] Creating order - Endpoint: {_baseEndpoint}/orders");
            Console.WriteLine($"[Pagarme] Request body: {json}");

            var response = await _httpClient.PostAsync($"{_baseEndpoint}/orders", content);
            var responseBody = await response.Content.ReadAsStringAsync();

            Console.WriteLine($"[Pagarme] Response status: {response.StatusCode}");
            Console.WriteLine($"[Pagarme] Response body: {responseBody}");

            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException($"Erro ao criar pedido no Pagar.me: {response.StatusCode} - {responseBody}");
            }

            var orderResponse = JsonSerializer.Deserialize<PagarmeOrderResponse>(responseBody, jsonOptions);
            if (orderResponse == null)
            {
                throw new InvalidOperationException("Resposta do Pagar.me inválida");
            }

            return orderResponse;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Pagarme] Error creating order: {ex.Message}");
            throw;
        }
    }

    public async Task<PagarmeOrderResponse> GetOrderAsync(string orderId)
    {
        try
        {
            Console.WriteLine($"[Pagarme] Getting order - ID: {orderId}");

            var response = await _httpClient.GetAsync($"{_baseEndpoint}/orders/{orderId}");
            var responseBody = await response.Content.ReadAsStringAsync();

            Console.WriteLine($"[Pagarme] Response status: {response.StatusCode}");
            Console.WriteLine($"[Pagarme] Response body: {responseBody}");

            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException($"Erro ao buscar pedido no Pagar.me: {response.StatusCode} - {responseBody}");
            }

            var jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
            };

            var orderResponse = JsonSerializer.Deserialize<PagarmeOrderResponse>(responseBody, jsonOptions);
            if (orderResponse == null)
            {
                throw new InvalidOperationException("Resposta do Pagar.me inválida");
            }

            return orderResponse;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Pagarme] Error getting order: {ex.Message}");
            throw;
        }
    }

    public async Task<PagarmeRefundResponse> RefundChargeAsync(string chargeId, decimal amount)
    {
        try
        {
            Console.WriteLine($"[Pagarme] Refunding charge - ID: {chargeId}, Amount: R$ {amount:F2}");

            var requestBody = new { amount = amount };
            var jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            };

            var json = JsonSerializer.Serialize(requestBody, jsonOptions);
            var request = new HttpRequestMessage(HttpMethod.Delete, $"{_baseEndpoint}/charges/{chargeId}")
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };

            Console.WriteLine($"[Pagarme] Refund request body: {json}");

            var response = await _httpClient.SendAsync(request);
            var responseBody = await response.Content.ReadAsStringAsync();

            Console.WriteLine($"[Pagarme] Refund response status: {response.StatusCode}");
            Console.WriteLine($"[Pagarme] Refund response body: {responseBody}");

            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException($"Erro ao realizar reembolso no Pagar.me: {response.StatusCode} - {responseBody}");
            }

            var refundResponse = JsonSerializer.Deserialize<PagarmeRefundResponse>(responseBody, jsonOptions);
            if (refundResponse == null)
            {
                throw new InvalidOperationException("Resposta do Pagar.me inválida");
            }

            return refundResponse;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Pagarme] Error refunding charge: {ex.Message}");
            throw;
        }
    }
}
