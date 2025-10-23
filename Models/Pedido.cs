namespace cafApi.Models
{
    public class Pedido
    {
        public int Id { get; set; }
        public int ClienteId { get; set; }
        public int CarrinhoId { get; set; }
        public int EnderecoEntregaId { get; set; }
        public StatusPedido Status { get; set; } = StatusPedido.Pendente;
        public decimal? PrecoFrete { get; set; }
    // TotalPedido armazena o valor bruto (sem descontos)
    public decimal TotalPedido { get; set; }
    // Novos campos de desconto
    public decimal DescontoPorUnidade { get; set; } = 0m; // promo por quantidade
    public decimal DescontoCupom { get; set; } = 0m; // desconto aplicado via cupom
        public DateTime DataPedido { get; set; } = DateTime.UtcNow;
        public DateTime? DataAtualizacao { get; set; }
        public string? CodigoRastreamento { get; set; }
        public string MetodoPagamento { get; set; } = null!; // "cartao_credito", "pix", etc.
        public string? Observacoes { get; set; }

        // Propriedades de navegação
        public Cliente Cliente { get; set; } = null!;
        public Carrinho Carrinho { get; set; } = null!;
        public Endereco EnderecoEntrega { get; set; } = null!;
    }
}
