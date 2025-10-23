namespace cafApi.Models.DTOs;

public class CheckoutDto
{
    public string Nome { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Telefone { get; set; }
    public string? Cpf { get; set; }
    
    // Endereço
    public string? Cep { get; set; }
    public string? Logradouro { get; set; }
    public string? Numero { get; set; }
    public string? Complemento { get; set; }
    public string? Bairro { get; set; }
    public string? Cidade { get; set; }
    public string? Estado { get; set; }
}

public class CheckoutResponseDto
{
    public int ClienteId { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public CarrinhoDto Carrinho { get; set; } = new CarrinhoDto();
    public string Mensagem { get; set; } = string.Empty;
}
