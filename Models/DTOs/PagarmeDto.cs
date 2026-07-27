using System.Text.Json.Serialization;

namespace cafApi.Models.DTOs;

// DTO para criar pedido no Pagar.me
public class PagarmeCreateOrderRequest
{
    [JsonPropertyName("items")]
    public List<PagarmeOrderItem> Items { get; set; } = new();

    [JsonPropertyName("customer")]
    public PagarmeCustomer? Customer { get; set; }

    [JsonPropertyName("customer_id")]
    public string? CustomerId { get; set; }

    [JsonPropertyName("payments")]
    public List<PagarmePayment> Payments { get; set; } = new();

    [JsonPropertyName("metadata")]
    public Dictionary<string, string>? Metadata { get; set; }

    [JsonPropertyName("shipping")]
    public PagarmeShipping? Shipping { get; set; }
}

public class PagarmeOrderItem
{
    [JsonPropertyName("amount")]
    public int Amount { get; set; } // valor em centavos

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("quantity")]
    public int Quantity { get; set; }

    [JsonPropertyName("code")]
    public string? Code { get; set; }
}

public class PagarmeCustomer
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("email")]
    public string Email { get; set; } = string.Empty;

    [JsonPropertyName("document")]
    public string Document { get; set; } = string.Empty; // CPF/CNPJ

    [JsonPropertyName("type")]
    public string Type { get; set; } = "individual"; // individual ou company

    [JsonPropertyName("phones")]
    public PagarmePhones? Phones { get; set; }

    [JsonPropertyName("address")]
    public PagarmeAddress? Address { get; set; }
}

public class PagarmePhones
{
    [JsonPropertyName("home_phone")]
    public PagarmePhone? HomePhone { get; set; }

    [JsonPropertyName("mobile_phone")]
    public PagarmePhone? MobilePhone { get; set; }
}

public class PagarmePhone
{
    [JsonPropertyName("country_code")]
    public string CountryCode { get; set; } = "55";

    [JsonPropertyName("area_code")]
    public string AreaCode { get; set; } = string.Empty;

    [JsonPropertyName("number")]
    public string Number { get; set; } = string.Empty;
}

public class PagarmeAddress
{
    [JsonPropertyName("line_1")]
    public string Line1 { get; set; } = string.Empty; // logradouro, número

    [JsonPropertyName("line_2")]
    public string? Line2 { get; set; } // complemento

    [JsonPropertyName("zip_code")]
    public string ZipCode { get; set; } = string.Empty;

    [JsonPropertyName("city")]
    public string City { get; set; } = string.Empty;

    [JsonPropertyName("state")]
    public string State { get; set; } = string.Empty;

    [JsonPropertyName("country")]
    public string Country { get; set; } = "BR";
}

public class PagarmeShipping
{
    [JsonPropertyName("amount")]
    public int Amount { get; set; } // valor do frete em centavos

    [JsonPropertyName("description")]
    public string Description { get; set; } = "Frete";

    [JsonPropertyName("recipient_name")]
    public string RecipientName { get; set; } = string.Empty;

    [JsonPropertyName("recipient_phone")]
    public string RecipientPhone { get; set; } = string.Empty;

    [JsonPropertyName("address")]
    public PagarmeAddress Address { get; set; } = new();
}

public class PagarmePayment
{
    [JsonPropertyName("payment_method")]
    public string PaymentMethod { get; set; } = string.Empty; // credit_card, pix, boleto

    [JsonPropertyName("credit_card")]
    public PagarmeCreditCard? CreditCard { get; set; }

    [JsonPropertyName("pix")]
    public PagarmePix? Pix { get; set; }
}

public class PagarmeCreditCard
{
    [JsonPropertyName("installments")]
    public int Installments { get; set; } = 1;

    [JsonPropertyName("statement_descriptor")]
    public string? StatementDescriptor { get; set; }

    [JsonPropertyName("card_id")]
    public string? CardId { get; set; } // usar card_id ou card_token (recomendado)

    [JsonPropertyName("card_token")]
    public string? CardToken { get; set; } // token do cartão gerado no frontend

    // Mesmo usando card_token, o Pagar.me exige o objeto card com billing_address
    [JsonPropertyName("card")]
    public PagarmeCard? Card { get; set; }
}

public class PagarmeCard
{
    [JsonPropertyName("number")]
    public string? Number { get; set; }

    [JsonPropertyName("holder_name")]
    public string? HolderName { get; set; }

    [JsonPropertyName("exp_month")]
    public int ExpMonth { get; set; }

    [JsonPropertyName("exp_year")]
    public int ExpYear { get; set; }

    [JsonPropertyName("cvv")]
    public string? Cvv { get; set; }

    [JsonPropertyName("billing_address")]
    public PagarmeAddress? BillingAddress { get; set; }
}

public class PagarmePix
{
    [JsonPropertyName("expires_in")]
    public int ExpiresIn { get; set; } = 1800; // tempo para expiração do QR code em segundos (padrão 30 minutos)
}

// DTO para resposta do Pagar.me
public class PagarmeOrderResponse
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("code")]
    public string Code { get; set; } = string.Empty;

    [JsonPropertyName("amount")]
    public int Amount { get; set; }

    [JsonPropertyName("currency")]
    public string Currency { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; set; }

    [JsonPropertyName("updated_at")]
    public DateTime UpdatedAt { get; set; }

    [JsonPropertyName("charges")]
    public List<PagarmeCharge>? Charges { get; set; }

    [JsonPropertyName("customer")]
    public PagarmeCustomer? Customer { get; set; }

    [JsonPropertyName("metadata")]
    public Dictionary<string, string>? Metadata { get; set; }
}

public class PagarmeCharge
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("code")]
    public string Code { get; set; } = string.Empty;

    [JsonPropertyName("amount")]
    public int Amount { get; set; }

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("payment_method")]
    public string PaymentMethod { get; set; } = string.Empty;

    [JsonPropertyName("paid_at")]
    public DateTime? PaidAt { get; set; }

    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; set; }

    [JsonPropertyName("last_transaction")]
    public PagarmeTransaction? LastTransaction { get; set; }
}

public class PagarmeTransaction
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("transaction_type")]
    public string TransactionType { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("gateway_response")]
    public PagarmeGatewayResponse? GatewayResponse { get; set; }

    [JsonPropertyName("pix")]
    public PagarmePixTransaction? Pix { get; set; }

    // Dados do PIX vêm diretamente na transaction
    [JsonPropertyName("qr_code")]
    public string? QrCode { get; set; }

    [JsonPropertyName("qr_code_url")]
    public string? QrCodeUrl { get; set; }

    [JsonPropertyName("expires_at")]
    public string? ExpiresAt { get; set; }
}

public class PagarmeGatewayResponse
{
    [JsonPropertyName("code")]
    public string Code { get; set; } = string.Empty;

    [JsonPropertyName("errors")]
    public List<object>? Errors { get; set; }
}

public class PagarmePixTransaction
{
    [JsonPropertyName("qr_code")]
    public string? QrCode { get; set; }

    [JsonPropertyName("qr_code_url")]
    public string? QrCodeUrl { get; set; }

    [JsonPropertyName("expires_at")]
    public DateTime? ExpiresAt { get; set; }
}

// DTO para resposta de reembolso do Pagar.me
public class PagarmeRefundResponse
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("code")]
    public string? Code { get; set; }

    [JsonPropertyName("gateway_id")]
    public string? GatewayId { get; set; }

    [JsonPropertyName("amount")]
    public int Amount { get; set; }

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("canceled_amount")]
    public int CanceledAmount { get; set; }

    [JsonPropertyName("paid_amount")]
    public int PaidAmount { get; set; }

    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; set; }

    [JsonPropertyName("updated_at")]
    public DateTime UpdatedAt { get; set; }

    [JsonPropertyName("last_transaction")]
    public PagarmeTransaction? LastTransaction { get; set; }
}

// DTO para webhook do Pagar.me
public class PagarmeWebhookEvent
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; set; }

    [JsonPropertyName("data")]
    public PagarmeOrderResponse? Data { get; set; }
}
