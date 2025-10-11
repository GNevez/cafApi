using System.ComponentModel.DataAnnotations;

namespace cafApi.Models.DTOs
{
    public class CategoriaCreateDto
    {
        [Required]
        public string Nome { get; set; } = null!;
        
        [Required]
        public string Slug { get; set; } = null!;
    }
}
