namespace cafApi.Models
{
    public class Pedido
    {
        public int Id { get; set; }
        public string CodigoPedido { get; set; } = string.Empty;
        public int ClienteId { get; set; }
        public int CarrinhoId { get; set; }
        public int EnderecoEntregaId { get; set; }
        public StatusPedido Status { get; set; } = StatusPedido.AguardandoConfirmacao;
        public decimal? PrecoFrete { get; set; }
        // TotalPedido armazena o valor final do pedido (inclui frete e aplica descontos)
        public decimal TotalPedido { get; set; }
        // Novos campos de desconto
        public decimal DescontoPorUnidade { get; set; } = 0m;
        public decimal DescontoCupom { get; set; } = 0m; 
        public DateTime DataPedido { get; set; } = DateTime.UtcNow;
        public DateTime? DataAtualizacao { get; set; }
        public string? CodigoRastreamento { get; set; }
        public string MetodoPagamento { get; set; } = null!; 
        public string? Observacoes { get; set; }
        public string? MotivoCancelamento { get; set; }
        public string? PagarmeOrderId { get; set; }

        // Snapshot dos dados do cliente no momento da compra (histórico imutável)
        public string NomeCliente { get; set; } = null!;
        public string EmailCliente { get; set; } = null!;
        public string? TelefoneCliente { get; set; }

        // Propriedades de navegação
        public Cliente Cliente { get; set; } = null!;
        public Carrinho Carrinho { get; set; } = null!;
        public Endereco EnderecoEntrega { get; set; } = null!;
    }
}
