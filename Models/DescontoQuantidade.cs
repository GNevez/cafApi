using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace cafApi.Models
{
    [Table("descontos_quantidade")]
    public class DescontoQuantidade
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Required]
        [Column("quantidade_minima")]
        public int QuantidadeMinima { get; set; }

        [Required]
        [Column("quantidade_maxima")]
        public int? QuantidadeMaxima { get; set; }

        [Required]
        [Column("valor_promocional")]
        [Precision(10, 2)]
        public decimal ValorPromocional { get; set; }

        [Column("descricao")]
        [MaxLength(255)]
        public string? Descricao { get; set; }

        [Required]
        [Column("ativo")]
        public bool Ativo { get; set; } = true;

        [Column("data_criacao")]
        public DateTime DataCriacao { get; set; } = DateTime.UtcNow;

        [Column("data_atualizacao")]
        public DateTime? DataAtualizacao { get; set; }
    }
}
