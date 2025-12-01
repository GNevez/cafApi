namespace cafApi.Models.DTOs;

public class CarrinhoDto
{
    public string Token { get; set; } = string.Empty;
    public DateTime DataCriacao { get; set; }
    public DateTime? DataAtualizacao { get; set; }
    public List<ItemCarrinhoDto> Itens { get; set; } = new List<ItemCarrinhoDto>();
    public decimal Subtotal { get; set; }
    public int TotalItens { get; set; }
    // Cupom aplicado
    public int? CupomId { get; set; }
    public string? CupomCodigo { get; set; }
    public decimal CupomValorDesconto { get; set; }
}

public class ItemCarrinhoDto
{
    public int Id { get; set; }
    public int ProdutoId { get; set; }
    public int CorId { get; set; }
    public int Quantidade { get; set; }
    public DateTime DataAdicao { get; set; }
    
    // Informações do produto
    public string ProdutoNome { get; set; } = string.Empty;
    public string ProdutoSlug { get; set; } = string.Empty;
    public string ProdutoSKU { get; set; } = string.Empty;
    public decimal ProdutoPreco { get; set; }
    public string ProdutoImagem { get; set; } = string.Empty;
    // Parcelamento / juros
    public int ProdutoMaxParcelas { get; set; }
    public decimal ProdutoTaxaJuros { get; set; }

    // Informações da cor
    public string CorNome { get; set; } = string.Empty;
    public string? CorHex1 { get; set; }
    public string? CorHex2 { get; set; }
    
    // Calculado
    public decimal Subtotal => ProdutoPreco * Quantidade;
}

public class AdicionarItemCarrinhoDto
{
    public int ProdutoId { get; set; }
    public int CorId { get; set; }
    public int Quantidade { get; set; } = 1;
}

public class AtualizarItemCarrinhoDto
{
    public int ItemId { get; set; }
    public int Quantidade { get; set; }
}
