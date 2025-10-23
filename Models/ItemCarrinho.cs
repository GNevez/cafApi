using System.Text.Json.Serialization;

namespace cafApi.Models;

public class ItemCarrinho
{
    public int Id { get; set; }
    public int CarrinhoId { get; set; }
    public int ProdutoId { get; set; }
    public int CorId { get; set; } // Cor selecionada do produto
    public int Quantidade { get; set; }
    public DateTime DataAdicao { get; set; } = DateTime.UtcNow;
    public DateTime? DataAtualizacao { get; set; }

    // Relacionamentos
    [JsonIgnore]
    public Carrinho Carrinho { get; set; } = null!;
    
    public Produtos Produto { get; set; } = null!;
    public ProdutosCor Cor { get; set; } = null!;
}
