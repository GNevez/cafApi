using System.ComponentModel.DataAnnotations;

namespace cafApi.Models.DTOs
{
    public class CategoriaCreateDto
    {
        [Required]
        public string Nome { get; set; } = null!;
        
        public string? Banner { get; set; }
        
        public string? Titulo { get; set; }
        
        public string? Mensagem { get; set; }
    }
}
