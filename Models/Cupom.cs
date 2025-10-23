using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace cafApi.Models
{
    [Table("cupons")]
    public class Cupom
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Required]
        [Column("codigo")]
        [MaxLength(50)]
        public string Codigo { get; set; } = string.Empty;

        [Column("descricao")]
        [MaxLength(255)]
        public string? Descricao { get; set; }

        [Required]
        [Column("tipo_desconto")]
        [MaxLength(20)]
        public string TipoDesconto { get; set; } = "percentual"; // percentual ou fixo

        [Required]
        [Column("valor_desconto")]
        [Precision(10, 2)]
        public decimal ValorDesconto { get; set; }

        [Column("valor_minimo_compra")]
        [Precision(10, 2)]
        public decimal? ValorMinimoCompra { get; set; }

        [Column("valor_maximo_desconto")]
        [Precision(10, 2)]
        public decimal? ValorMaximoDesconto { get; set; }

        [Column("quantidade_maxima_usos")]
        public int? QuantidadeMaximaUsos { get; set; }

        [Column("quantidade_usos_atual")]
        public int QuantidadeUsosAtual { get; set; } = 0;

        [Column("uso_por_usuario")]
        public int UsosPorUsuario { get; set; } = 1;

        [Required]
        [Column("data_inicio")]
        public DateTime DataInicio { get; set; }

        [Column("data_expiracao")]
        public DateTime? DataExpiracao { get; set; }

        [Required]
        [Column("ativo")]
        public bool Ativo { get; set; } = true;

        [Column("data_criacao")]
        public DateTime DataCriacao { get; set; } = DateTime.UtcNow;

        [Column("data_atualizacao")]
        public DateTime? DataAtualizacao { get; set; }

        // Relacionamento com uso de cupons por usuário
        public virtual ICollection<CupomUso>? CupomUsos { get; set; }
    }

    [Table("cupons_uso")]
    public class CupomUso
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Required]
        [Column("cupom_id")]
        public int CupomId { get; set; }

        [Column("usuario_id")]
        public int? UsuarioId { get; set; }

        [Column("pedido_id")]
        public int? PedidoId { get; set; }

        [Required]
        [Column("valor_desconto_aplicado")]
        [Precision(10, 2)]
        public decimal ValorDescontoAplicado { get; set; }

        [Column("data_uso")]
        public DateTime DataUso { get; set; } = DateTime.UtcNow;

        // Relacionamentos
        [ForeignKey("CupomId")]
        public virtual Cupom? Cupom { get; set; }

        [ForeignKey("UsuarioId")]
        public virtual Usuario? Usuario { get; set; }
    }
}
