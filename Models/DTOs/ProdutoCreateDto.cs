using System.ComponentModel.DataAnnotations;

namespace cafApi.Models.DTOs
{
    public class ProdutoCreateDto
    {
        [Required]
        public string Nome { get; set; } = null!;
        
        [Required]
        public string SKU { get; set; } = null!;
        
        public string? CodigoExterno { get; set; }
        
        public string? Fabricante { get; set; }
        
        [Required]
        public string Slug { get; set; } = null!;
        
        [Required]
        public decimal Preco { get; set; }
        
        public decimal? PrecoOriginal { get; set; }
        
        public bool IsSale { get; set; }
        
        public bool IsNew { get; set; }
        
        [Required]
        public string ImagemPrincipal { get; set; } = null!;
        
        [Required]
        public string ImagemHover { get; set; } = null!;
        
        [Required]
        public int CategoriaId { get; set; }
        
        public List<ProdutoCorCreateDto> CoresDisponiveis { get; set; } = new List<ProdutoCorCreateDto>();
    }

    public class ProdutoCorCreateDto
    {
        [Required]
        public string Nome { get; set; } = null!;
        
        [Required]
        public int QuantidadeEstoque { get; set; }
        
        public List<ProdutoCorImagemCreateDto> Imagens { get; set; } = new List<ProdutoCorImagemCreateDto>();
    }

    public class ProdutoCorImagemCreateDto
    {
        [Required]
        public string Url { get; set; } = null!;
        
        [Required]
        public int Ordem { get; set; }
    }
}
