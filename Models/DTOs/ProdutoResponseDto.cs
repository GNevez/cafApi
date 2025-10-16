namespace cafApi.Models.DTOs
{
    public class ProdutoResponseDto
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
        public string ImagemPrincipal { get; set; } = null!;
        public string ImagemHover { get; set; } = null!;
        public int CategoriaId { get; set; }
        public string CategoriaNome { get; set; } = null!;
        public List<ProdutoCorResponseDto> CoresDisponiveis { get; set; } = new List<ProdutoCorResponseDto>();
    }

    public class ProdutoCorResponseDto
    {
        public int Id { get; set; }
        public string Nome { get; set; } = null!;
        public int QuantidadeEstoque { get; set; }
        public string? Hex1 { get; set; }
        public string? Hex2 { get; set; }
        public List<ProdutoCorImagemResponseDto> Imagens { get; set; } = new List<ProdutoCorImagemResponseDto>();
    }

    public class ProdutoCorImagemResponseDto
    {
        public int Id { get; set; }
        public string Url { get; set; } = null!;
        public int Ordem { get; set; }
    }
}
