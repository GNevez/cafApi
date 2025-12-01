namespace cafApi.Models.DTOs
{
    public class TransacaoDto
    {
        public int Id { get; set; }
        public TipoTransacao Tipo { get; set; }
        public decimal Valor { get; set; }
        public string Descricao { get; set; } = null!;
        public string MetodoPagamento { get; set; } = null!;
        public int? PedidoId { get; set; }
        public DateTime DataTransacao { get; set; }
        public DateTime DataCriacao { get; set; }
    }

    public class CriarTransacaoDto
    {
        public TipoTransacao Tipo { get; set; }
        public decimal Valor { get; set; }
        public string Descricao { get; set; } = null!;
        public string MetodoPagamento { get; set; } = null!;
        public DateTime? DataTransacao { get; set; } // Opcional, usa DateTime.UtcNow se null
    }

    public class AtualizarTransacaoDto
    {
        public decimal? Valor { get; set; }
        public string? Descricao { get; set; }
        public string? MetodoPagamento { get; set; }
        public DateTime? DataTransacao { get; set; }
    }
}
