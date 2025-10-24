namespace cafApi.Models.DTOs
{
    public class ProdutoSearchDto
    {
        public int Id { get; set; }
        public string Nome { get; set; } = null!;
        public string Slug { get; set; } = null!;
        public decimal Preco { get; set; }
        public string ImagemPrincipal { get; set; } = string.Empty;
    }
}
