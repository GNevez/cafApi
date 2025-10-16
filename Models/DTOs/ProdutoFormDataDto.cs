using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;

namespace cafApi.Models.DTOs
{
    public class ProdutoFormDataDto
    {
        [Required]
        public string Nome { get; set; } = null!;
        
        [Required]
        public int CategoriaId { get; set; }
        
        [Required]
        public decimal Preco { get; set; }
        
        public decimal? PrecoOriginal { get; set; }
        
        public bool IsSale { get; set; }
        
        public bool IsNew { get; set; }
        
        [Required]
        public IFormFile ImagemPrincipal { get; set; } = null!;
        
        [Required]
        public IFormFile ImagemHover { get; set; } = null!;
        
        public List<CorFormDataDto> Cores { get; set; } = new List<CorFormDataDto>();
    }

    public class CorFormDataDto
    {
        [Required]
        public string Cor1 { get; set; } = null!;
        
        [Required]
        public string Cor2 { get; set; } = null!;
        
        [Required]
        public int Estoque { get; set; }
        
        public IFormFileCollection? Imagens { get; set; }
    }
}
