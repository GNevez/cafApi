namespace cafApi.Models
{
    public class Produtos
{
    public int Id { get; set; }
    public string Nome { get; set; } = null!;
    public string SKU { get; set; } = null!;
    public string? CodigoExterno { get; set; }
    public string? Fabricante { get; set; }
    public string Slug { get; set; } = null!;
    public decimal Preco { get; set; }
    public decimal? PrecoOriginal { get; set; }
    public bool IsSale { get; set; }
    public bool IsNew { get; set; }
    public bool Active { get; set; } = true; // 🔹 Soft delete - produtos ativos por padrão

    // 🔹 Imagens principais do modelo
    public string ImagemPrincipal { get; set; } = null!;
    public string ImagemHover { get; set; } = null!;

    // 🔗 Relacionamentos
    public int CategoriaId { get; set; }
    public Categoria Categoria { get; set; } = null!;

    public ICollection<ProdutosCor> CoresDisponiveis { get; set; } = new List<ProdutosCor>();
}
}
