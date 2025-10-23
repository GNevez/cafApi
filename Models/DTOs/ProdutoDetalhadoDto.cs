namespace cafApi.Models.DTOs
{
    public class ProdutoDetalhadoDto
    {
        public int Id { get; set; }
        public string Nome { get; set; } = string.Empty;
        public string SKU { get; set; } = string.Empty;
        public string? CodigoExterno { get; set; }
        public string? Fabricante { get; set; }
        public string Slug { get; set; } = string.Empty;
        public decimal Preco { get; set; }
        public decimal? PrecoOriginal { get; set; }
        public bool IsSale { get; set; }
        public bool IsNew { get; set; }
        public string ImagemPrincipal { get; set; } = string.Empty;
        public string? ImagemHover { get; set; }
        public int MaxParcelas { get; set; }
        public decimal TaxaJuros { get; set; }
        public string? Descricao { get; set; }
        
        // Informações da categoria
        public int CategoriaId { get; set; }
        public string CategoriaNome { get; set; } = string.Empty;
        
        // Cores disponíveis
        public List<CorDetalhadaDto> CoresDisponiveis { get; set; } = new List<CorDetalhadaDto>();
    }

    public class CorDetalhadaDto
    {
        public int Id { get; set; }
        public string Nome { get; set; } = string.Empty;
        public string? Hex1 { get; set; }
        public string? Hex2 { get; set; }
        public int QuantidadeEstoque { get; set; }
        public List<ImagemCorDto> Imagens { get; set; } = new List<ImagemCorDto>();
    }

    public class ImagemCorDto
    {
        public int Id { get; set; }
        public string Url { get; set; } = string.Empty;
    }
}
