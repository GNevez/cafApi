using System.Text.Json.Serialization;

namespace cafApi.Models;

public class ProdutosCorImagem
{
    public int Id { get; set; }
    public string Url { get; set; } = null!; // URL da imagem no servidor/CDN
    public int Ordem { get; set; }           // 1 a 5 (define a sequência de exibição)
    // 🔗 Chave estrangeira
    public int ProdutosCorId { get; set; }

    [JsonIgnore] 
    public ProdutosCor ProdutosCor { get; set; } = null!;
}
