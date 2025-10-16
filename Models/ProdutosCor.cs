using System.Text.Json.Serialization;

namespace cafApi.Models;

public class ProdutosCor
{
    public int Id { get; set; }
    public string Nome { get; set; } = null!;
    public int QuantidadeEstoque { get; set; }
    public string? Hex1 { get; set; }
    public string? Hex2 { get; set; }

    public int ProdutosId { get; set; }           // FK
    
    [JsonIgnore]
    public Produtos Produtos { get; set; } = null!;  

    public ICollection<ProdutosCorImagem> Imagens { get; set; } = new List<ProdutosCorImagem>();
}

