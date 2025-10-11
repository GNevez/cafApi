using System.Text.Json.Serialization;

namespace cafApi.Models;

public class ProdutosCor
{
    public int Id { get; set; }
    public string Nome { get; set; } = null!;
    public int QuantidadeEstoque { get; set; }

    public int ProdutosId { get; set; }           // FK
    
    [JsonIgnore]
    public Produtos Produtos { get; set; } = null!;  

    public ICollection<Cor> Cores { get; set; } = new List<Cor>();
    public ICollection<ProdutosCorImagem> Imagens { get; set; } = new List<ProdutosCorImagem>();
}

