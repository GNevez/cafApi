using System.ComponentModel.DataAnnotations;

namespace cafApi.Models
{
    public class Usuario
    {
        public int Id { get; set; }
        
        [Required]
        public string Nome { get; set; } = null!;
        
        [Required]
        [EmailAddress]
        public string Email { get; set; } = null!;
        
        [Required]
        public string Senha { get; set; } = null!; // Será hash da senha
        
        public int RoleId { get; set; }
        public Role Role { get; set; } = null!;
        
        public DateTime DataCriacao { get; set; } = DateTime.UtcNow;
        public bool Ativo { get; set; } = true;
    }
}
