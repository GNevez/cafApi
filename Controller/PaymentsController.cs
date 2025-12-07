using cafApi.Models.DTOs;
using cafApi.Services;
using cafApi.Models;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using cafApi.Contexts;
using Microsoft.EntityFrameworkCore;

namespace cafApi.Controller;

[ApiController]
[Route("api/[controller]")]
public class PaymentsController : ControllerBase
{
    private readonly IPedidoService _pedidoService;
    private readonly IPagarmeService _pagarmeService;
    private readonly ApplicationDbContext _context;
    private readonly IConfiguration _configuration;

    public PaymentsController(IPedidoService pedidoService, IPagarmeService pagarmeService, ApplicationDbContext context, IConfiguration configuration)
    {
        _pedidoService = pedidoService;
        _pagarmeService = pagarmeService;
        _context = context;
        _configuration = configuration;
    }

    [HttpPost("create-order")]
    public async Task<ActionResult<object>> CreateOrder([FromBody] CriarPedidoDto checkoutData)
    {
        try
        {
            var cartToken = Request.Cookies["cart_token"];
            if (string.IsNullOrEmpty(cartToken))
            {
                return BadRequest(new { message = "Token do carrinho não encontrado" });
            }

            var pedidoSvc = _pedidoService as PedidoService;
            if (pedidoSvc == null) throw new InvalidOperationException("Serviço de pedidos indisponível");
            var total = await pedidoSvc.CalculateTotalAsync(checkoutData, cartToken);

            var cart = await _context.Carrinhos
                .Include(c => c.Itens).ThenInclude(i => i.Produto)
                .FirstOrDefaultAsync(c => c.Token == cartToken);
            if (cart == null) return BadRequest(new { message = "Carrinho não encontrado" });

            // Calcular valores para ajuste proporcional nos itens
            var subtotal = cart.Itens.Sum(i => i.Quantidade * i.Produto.Preco);
            var descontoPorUnidade = checkoutData.DescontoPorUnidade ?? 0m;
            var subtotalComPromo = subtotal - descontoPorUnidade;
            
            var parcelasNum = checkoutData.ParcelasNum ?? 1;
            var cartMaxTaxa = cart.Itens.Any() ? cart.Itens.Max(i => i.Produto.TaxaJuros) : 0m;
            var taxaUsada = parcelasNum > 1 ? cartMaxTaxa : 0m;
            var totalComJuros = subtotalComPromo * (1 + taxaUsada);
            
            // Fator de ajuste para distribuir descontos/juros proporcionalmente nos itens
            var fatorAjuste = subtotal > 0 ? totalComJuros / subtotal : 1m;

            // Build Pagar.me order request com valores ajustados
            var items = cart.Itens.Select(i => new PagarmeOrderItem
            {
                Code = i.Produto.SKU ?? i.ProdutoId.ToString(),
                Description = i.Produto.Nome,
                Amount = (int)(Math.Round(i.Produto.Preco * fatorAjuste, 2) * 100m),
                Quantity = i.Quantidade
            }).ToList();

            // Extrair apenas dígitos do telefone
            var telefoneDigits = checkoutData.Telefone?.Replace("(", "").Replace(")", "").Replace(" ", "").Replace("-", "") ?? "";
            var areaCode = telefoneDigits.Length >= 2 ? telefoneDigits.Substring(0, 2) : "";
            var phoneNumber = telefoneDigits.Length > 2 ? telefoneDigits.Substring(2) : "";

            var customer = new PagarmeCustomer
            {
                Name = checkoutData.Nome,
                Email = checkoutData.Email,
                Type = "individual",
                Document = checkoutData.Cpf?.Replace(".", "").Replace("-", "") ?? "",
                Phones = new PagarmePhones
                {
                    MobilePhone = new PagarmePhone
                    {
                        CountryCode = "55",
                        AreaCode = areaCode,
                        Number = phoneNumber
                    }
                },
                Address = new PagarmeAddress
                {
                    Line1 = $"{checkoutData.Logradouro}, {checkoutData.Numero}",
                    Line2 = checkoutData.Complemento,
                    ZipCode = checkoutData.Cep?.Replace("-", "") ?? "",
                    City = checkoutData.Cidade,
                    State = checkoutData.Estado,
                    Country = "BR"
                }
            };

            var payments = new List<PagarmePayment>();
            if (checkoutData.MetodoPagamento?.Equals("pix", StringComparison.OrdinalIgnoreCase) == true)
            {
                payments.Add(new PagarmePayment
                {
                    PaymentMethod = "pix",
                    Pix = new PagarmePix
                    {
                        ExpiresIn = 1800 // 30 minutos
                    }
                });
            }
            else
            {
                if (string.IsNullOrEmpty(checkoutData.CardToken))
                {
                    return BadRequest(new { message = "CardToken é obrigatório para pagamento com cartão" });
                }

                payments.Add(new PagarmePayment
                {
                    PaymentMethod = "credit_card",
                    CreditCard = new PagarmeCreditCard
                    {
                        Installments = checkoutData.ParcelasNum ?? 1,
                        CardToken = checkoutData.CardToken,
                        StatementDescriptor = "CHASE A FLARE",
                        Card = new PagarmeCard
                        {
                            BillingAddress = new PagarmeAddress
                            {
                                Line1 = $"{checkoutData.Logradouro}, {checkoutData.Numero}",
                                Line2 = checkoutData.Complemento,
                                ZipCode = checkoutData.Cep?.Replace("-", "") ?? "",
                                City = checkoutData.Cidade,
                                State = checkoutData.Estado,
                                Country = "BR"
                            }
                        }
                    }
                });
            }

            var request = new PagarmeCreateOrderRequest
            {
                Items = items,
                Customer = customer,
                Payments = payments,
                Metadata = new Dictionary<string, string>
                {
                    { "cart_token", cartToken },
                    { "expected_total", total.ToString("F2") }
                },
                Shipping = new PagarmeShipping
                {
                    Amount = (int)((checkoutData.PrecoFrete ?? 0) * 100m),
                    Description = "Entrega padrão",
                    RecipientName = checkoutData.Nome,
                    RecipientPhone = telefoneDigits,
                    Address = customer.Address
                }
            };

            // Primeiro, criar o pedido no nosso sistema
            Console.WriteLine($"[Payments] Creating local order - amount={total:F2}, cartToken={cartToken}");
            var pedidoDto = await _pedidoService.CreateAsync(checkoutData, cartToken);
            
            if (pedidoDto == null || string.IsNullOrEmpty(pedidoDto.CodigoPedido))
            {
                return BadRequest(new { message = "Erro ao criar pedido no sistema" });
            }

            var codigoPedido = pedidoDto.CodigoPedido;
            Console.WriteLine($"[Payments] Local order created - code={codigoPedido}");

            // Adicionar código do pedido nos metadados
            request.Metadata["codigo_pedido"] = codigoPedido;

            // Criar order no Pagar.me
            Console.WriteLine($"[Payments] Creating Pagar.me order - amount={total:F2}, codigoPedido={codigoPedido}");
            var orderResponse = await _pagarmeService.CreateOrderAsync(request);
            Console.WriteLine($"[Payments] Pagar.me order created - orderId={orderResponse.Id}, status={orderResponse.Status}");

            // Salvar PagarmeOrderId no pedido
            var pedidoEntity = await _context.Pedidos.FirstOrDefaultAsync(p => p.CodigoPedido == codigoPedido);
            if (pedidoEntity != null)
            {
                pedidoEntity.PagarmeOrderId = orderResponse.Id;
                await _context.SaveChangesAsync();
                Console.WriteLine($"[Payments] PagarmeOrderId saved for order {codigoPedido}");
            }

            Response.Cookies.Delete("cart_token");
            //Console.WriteLine($"[Payments] Cart cookie deleted for order {codigoPedido}");

            object? pixInfo = null;
            if (orderResponse.Charges != null && orderResponse.Charges.Any())
            {
                var charge = orderResponse.Charges.First();
                // Buscar dados do PIX na última transação
                if (charge.LastTransaction != null)
                {
                    var transaction = charge.LastTransaction;
                    // Verificar se tem os dados do PIX na transação
                    if (!string.IsNullOrEmpty(transaction.QrCode) || !string.IsNullOrEmpty(transaction.QrCodeUrl))
                    {
                        pixInfo = new
                        {
                            qr_code = transaction.QrCode,
                            qr_code_url = transaction.QrCodeUrl,
                            expires_at = transaction.ExpiresAt
                        };
                        Console.WriteLine($"[Payments] PIX data extracted - QR Code: {transaction.QrCode?.Substring(0, 50)}...");
                    }
                }
            }

            return Ok(new
            {
                orderId = codigoPedido,
                pagarmeOrderId = orderResponse.Id,
                orderCode = orderResponse.Code,
                status = orderResponse.Status,
                pix = pixInfo
            });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Payments] Error creating order: {ex.Message}");
            return StatusCode(500, new { message = ex.Message });
        }
    }

    [HttpGet("public-key")]
    public ActionResult<object> GetPublicKey()
    {
        try
        {
            var publicKey = _pagarmeService.GetPublicKey();
            return Ok(new { publicKey });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = ex.Message });
        }
    }

    [HttpGet("order/{orderId}")]
    public async Task<ActionResult<object>> GetOrder(string orderId)
    {
        try
        {
            var order = await _pagarmeService.GetOrderAsync(orderId);
            return Ok(order);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = ex.Message });
        }
    }

    [HttpGet("order-by-code/{codigoPedido}")]
    public async Task<ActionResult<object>> GetOrderByCode(string codigoPedido)
    {
        try
        {
            // Buscar pedido no nosso sistema
            var pedido = await _context.Pedidos
                .FirstOrDefaultAsync(p => p.CodigoPedido == codigoPedido);

            if (pedido == null)
            {
                return NotFound(new { message = "Pedido não encontrado" });
            }

            // Se tem PagarmeOrderId, buscar dados completos no Pagar.me
            if (!string.IsNullOrEmpty(pedido.PagarmeOrderId))
            {
                try
                {
                    var pagarmeOrder = await _pagarmeService.GetOrderAsync(pedido.PagarmeOrderId);
                    return Ok(pagarmeOrder);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Payments] Error fetching from Pagar.me: {ex.Message}");
                    // Continuar e retornar dados básicos
                }
            }

            // Fallback: retornar dados básicos do nosso sistema
            return Ok(new 
            { 
                codigoPedido = pedido.CodigoPedido,
                metodoPagamento = pedido.MetodoPagamento,
                totalPedido = pedido.TotalPedido,
                status = pedido.Status.ToString(),
                charges = new object[] { } // Array vazio para não quebrar o frontend
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Payments] Error getting order by code: {ex.Message}");
            return StatusCode(500, new { message = ex.Message });
        }
    }

    [HttpPost("simulate-payment-failure/{codigoPedido}")]
    public async Task<IActionResult> SimulatePaymentFailure(string codigoPedido)
    {
        try
        {
            var pedido = await _context.Pedidos
                .FirstOrDefaultAsync(p => p.CodigoPedido == codigoPedido);

            if (pedido == null)
            {
                return NotFound(new { message = "Pedido não encontrado" });
            }

            Console.WriteLine($"[TEST] Simulating payment failure for order {codigoPedido}");

            var updateDto = new AtualizarStatusPedidoDto
            {
                Status = StatusPedido.Cancelado,
                MotivoCancelamento = "Pagamento recusado (simulação de teste)"
            };

            await _pedidoService.UpdateStatusAsync(pedido.Id, updateDto);

            return Ok(new { 
                message = "Falha simulada com sucesso",
                codigoPedido = codigoPedido,
                redirectUrl = $"/carrinho/checkout/falha/{codigoPedido}"
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpPost("webhook")]
    public async Task<IActionResult> PagarmeWebhook()
    {
        // Validar autenticação Basic Auth
        var authHeader = Request.Headers["Authorization"].ToString();
        if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Basic "))
        {
            Console.WriteLine("[Webhook] Missing or invalid Authorization header");
            return Unauthorized(new { error = "Unauthorized" });
        }

        try
        {
            var encodedCredentials = authHeader.Substring("Basic ".Length).Trim();
            var credentials = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(encodedCredentials));
            var parts = credentials.Split(':', 2);
            
            if (parts.Length != 2)
            {
                Console.WriteLine("[Webhook] Invalid credentials format");
                return Unauthorized(new { error = "Invalid credentials format" });
            }

            var username = parts[0];
            var password = parts[1];

            var expectedUsername = _configuration["Pagarme:WebhookUsername"];
            var expectedPassword = _configuration["Pagarme:WebhookPassword"];

            if (username != expectedUsername || password != expectedPassword)
            {
                Console.WriteLine($"[Webhook] Invalid credentials - received: {username}");
                return Unauthorized(new { error = "Invalid credentials" });
            }

            Console.WriteLine("[Webhook] Authentication successful");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Webhook] Error validating authentication: {ex.Message}");
            return Unauthorized(new { error = "Authentication failed" });
        }

        var json = await new StreamReader(HttpContext.Request.Body).ReadToEndAsync();
        try
        {
            Console.WriteLine($"[Webhook] Received Pagar.me webhook: {json}");

            using var eventDoc = JsonDocument.Parse(json);
            var eventType = eventDoc.RootElement.GetProperty("type").GetString();
            Console.WriteLine($"[Webhook] Processing event type: {eventType}");

            // Processar eventos de pagamento
            if (eventType == "order.paid" || eventType == "charge.paid")
            {
                // Pagamento aprovado
                string? pagarmeOrderId = null;
                string? cartToken = null;
                string? codigoPedido = null;

                // Extrair dados do evento
                var dataObj = eventDoc.RootElement.GetProperty("data");
                if (dataObj.TryGetProperty("id", out var orderIdEl))
                {
                    pagarmeOrderId = orderIdEl.GetString();
                }

                if (dataObj.TryGetProperty("metadata", out var metadataEl))
                {
                    if (metadataEl.TryGetProperty("cart_token", out var tokenEl))
                    {
                        cartToken = tokenEl.GetString();
                    }
                    if (metadataEl.TryGetProperty("codigo_pedido", out var codigoEl))
                    {
                        codigoPedido = codigoEl.GetString();
                    }
                }

                Console.WriteLine($"[Webhook] Payment confirmed - PagarmeOrderId: {pagarmeOrderId}, CodigoPedido: {codigoPedido}, CartToken: {cartToken}");

                // Buscar pedido pelo código ou cart_token
                Models.Pedido? pedido = null;
                
                if (!string.IsNullOrEmpty(codigoPedido))
                {
                    pedido = await _context.Pedidos
                        .FirstOrDefaultAsync(p => p.CodigoPedido == codigoPedido && p.Status == StatusPedido.AguardandoConfirmacao);
                }
                else if (!string.IsNullOrEmpty(cartToken))
                {
                    pedido = await _context.Pedidos
                        .Include(p => p.Carrinho)
                        .FirstOrDefaultAsync(p => p.Carrinho.Token == cartToken && p.Status == StatusPedido.AguardandoConfirmacao);
                }
                else
                {
                    // Fallback: buscar o pedido mais recente aguardando confirmação
                    Console.WriteLine("[Webhook] No codigo_pedido or cart_token, trying to find most recent pending order...");
                    pedido = await _context.Pedidos
                        .Where(p => p.Status == StatusPedido.AguardandoConfirmacao)
                        .OrderByDescending(p => p.DataPedido)
                        .FirstOrDefaultAsync();
                }

                if (pedido != null)
                {
                    Console.WriteLine($"[Webhook] Found order #{pedido.Id}, updating status to EmSeparacao");
                    
                    var updateDto = new AtualizarStatusPedidoDto
                    {
                        Status = StatusPedido.EmSeparacao,
                        Observacoes = $"Pagamento confirmado via Pagar.me ({eventType}: {pagarmeOrderId})"
                    };

                    await _pedidoService.UpdateStatusAsync(pedido.Id, updateDto);
                    Console.WriteLine($"[Webhook] Order #{pedido.Id} ({pedido.CodigoPedido}) updated successfully");

                    // Finalizar o carrinho agora que o pagamento foi confirmado
                    var carrinho = await _context.Carrinhos.FirstOrDefaultAsync(c => c.Id == pedido.CarrinhoId);
                    if (carrinho != null)
                    {
                        carrinho.Status = StatusCarrinho.Finalizado;
                        carrinho.Ativo = false;
                        carrinho.DataAtualizacao = DateTime.UtcNow;
                        await _context.SaveChangesAsync();
                        Console.WriteLine($"[Webhook] Cart finalized for order #{pedido.Id}");
                    }
                }
                else
                {
                    Console.WriteLine($"[Webhook] No pending order found (codigo: {codigoPedido ?? "null"}, cart_token: {cartToken ?? "null"})");
                }
            }
            else if (eventType == "order.payment_failed" || eventType == "charge.payment_failed")
            {
                // Pagamento falhou
                string? pagarmeOrderId = null;
                string? cartToken = null;
                string? codigoPedido = null;
                string? chargeStatus = null;

                var dataObj = eventDoc.RootElement.GetProperty("data");
                if (dataObj.TryGetProperty("id", out var orderIdEl))
                {
                    pagarmeOrderId = orderIdEl.GetString();
                }

                // Verificar o status real da charge para não cancelar pedidos já pagos
                if (dataObj.TryGetProperty("status", out var statusEl))
                {
                    chargeStatus = statusEl.GetString();
                }

                if (dataObj.TryGetProperty("metadata", out var metadataEl))
                {
                    if (metadataEl.TryGetProperty("cart_token", out var tokenEl))
                    {
                        cartToken = tokenEl.GetString();
                    }
                    if (metadataEl.TryGetProperty("codigo_pedido", out var codigoEl))
                    {
                        codigoPedido = codigoEl.GetString();
                    }
                }

                Console.WriteLine($"[Webhook] Payment failed - PagarmeOrderId: {pagarmeOrderId}, CodigoPedido: {codigoPedido}, Status: {chargeStatus}");

                // Se o status é 'paid', ignorar este evento de falha (inconsistência do Pagar.me em dev)
                if (chargeStatus == "paid")
                {
                    Console.WriteLine($"[Webhook] Ignoring payment_failed event for paid charge - Status: {chargeStatus}");
                    return Ok();
                }

                Models.Pedido? pedido = null;
                
                if (!string.IsNullOrEmpty(codigoPedido))
                {
                    pedido = await _context.Pedidos
                        .FirstOrDefaultAsync(p => p.CodigoPedido == codigoPedido);
                }
                else if (!string.IsNullOrEmpty(cartToken))
                {
                    pedido = await _context.Pedidos
                        .Include(p => p.Carrinho)
                        .FirstOrDefaultAsync(p => p.Carrinho.Token == cartToken);
                }

                if (pedido != null)
                {
                    // Não cancelar se pedido já está em separação (já foi pago)
                    if (pedido.Status == StatusPedido.EmSeparacao)
                    {
                        Console.WriteLine($"[Webhook] Order #{pedido.Id} already in EmSeparacao, ignoring failure event");
                        return Ok();
                    }

                    Console.WriteLine($"[Webhook] Found order #{pedido.Id}, updating status to Cancelado");
                    
                    var updateDto = new AtualizarStatusPedidoDto
                    {
                        Status = StatusPedido.Cancelado,
                        MotivoCancelamento = $"Pagamento recusado pelo Pagar.me ({eventType}: {pagarmeOrderId})"
                    };

                    await _pedidoService.UpdateStatusAsync(pedido.Id, updateDto);
                    Console.WriteLine($"[Webhook] Order #{pedido.Id} ({pedido.CodigoPedido}) marked as Cancelado");
                }
            }
            else if (eventType == "order.canceled")
            {
                // Pedido cancelado
                string? codigoPedido = null;

                var dataObj = eventDoc.RootElement.GetProperty("data");
                if (dataObj.TryGetProperty("metadata", out var metadataEl))
                {
                    if (metadataEl.TryGetProperty("codigo_pedido", out var codigoEl))
                    {
                        codigoPedido = codigoEl.GetString();
                    }
                }

                Console.WriteLine($"[Webhook] Order canceled - CodigoPedido: {codigoPedido}");

                if (!string.IsNullOrEmpty(codigoPedido))
                {
                    var pedido = await _context.Pedidos
                        .FirstOrDefaultAsync(p => p.CodigoPedido == codigoPedido);

                    if (pedido != null)
                    {
                        Console.WriteLine($"[Webhook] Found order #{pedido.Id}, updating status to Cancelado");
                        
                        var updateDto = new AtualizarStatusPedidoDto
                        {
                            Status = StatusPedido.Cancelado,
                            MotivoCancelamento = "Pedido cancelado via Pagar.me"
                        };

                        await _pedidoService.UpdateStatusAsync(pedido.Id, updateDto);
                        Console.WriteLine($"[Webhook] Order #{pedido.Id} ({pedido.CodigoPedido}) marked as Cancelado");
                    }
                }
            }
            else
            {
                Console.WriteLine($"[Webhook] Unhandled event type: {eventType}");
            }

            return Ok();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Webhook] Error processing webhook: {ex.Message}");
            return StatusCode(500, new { error = ex.Message });
        }
    }
}
