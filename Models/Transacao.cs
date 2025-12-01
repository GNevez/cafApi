namespace cafApi.Models
{
    public enum TipoTransacao
    {
        Entrada = 0,
        Saida = 1
    }

    public class Transacao
    {
        public int Id { get; set; }
        public TipoTransacao Tipo { get; set; }
        public decimal Valor { get; set; }
        public string Descricao { get; set; } = null!;
        public string MetodoPagamento { get; set; } = null!;
        public int? PedidoId { get; set; } // Nullable - só preenchido se for entrada de venda
        public DateTime DataTransacao { get; set; } = DateTime.UtcNow;
        public DateTime DataCriacao { get; set; } = DateTime.UtcNow;

        // Propriedade de navegação
        public Pedido? Pedido { get; set; }
    }
}
