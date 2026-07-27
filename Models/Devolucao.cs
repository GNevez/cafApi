namespace cafApi.Models
{
    public enum DevolucaoStatus
    {
        Solicitado = 0,
        SolicitacaoEnviada = 1,
        Enviado = 2,
        EmAnalise = 3,
        ReembolsoEmitido = 4,
        Rejeitado = 5,
        Reembolsado = 6
    }

    public class Devolucao
    {
        public int Id { get; set; }
        public int PedidoId { get; set; }
        public string Cpf { get; set; } = null!;
        public string NomeCliente { get; set; } = null!;
        public string Email { get; set; } = null!;
        public DevolucaoStatus Status { get; set; } = DevolucaoStatus.Solicitado;
        public DateTime DataCriacao { get; set; } = DateTime.UtcNow;
        public DateTime? DataAtualizacao { get; set; }

        // Dados do Correios - Logística Reversa
        public string? CodigoPostagem { get; set; }          // Código de autorização de postagem (gerado pelos Correios)
        public string? CodigoRastreamento { get; set; }      // Código de rastreamento do envio
        public DateTime? DataLimitePostagem { get; set; }    // Data limite para o cliente postar
        public string? UrlEtiqueta { get; set; }             // URL da etiqueta para impressão (se aplicável)

        // Dados do Reembolso - Pagar.me
        public decimal? ValorReembolsado { get; set; }       // Valor reembolsado ao cliente
        public DateTime? DataReembolso { get; set; }         // Data/hora do reembolso
        public string? PagarmeChargeId { get; set; }         // ID da charge no Pagar.me
        public string? PagarmeRefundStatus { get; set; }     // Status do reembolso retornado pelo Pagar.me

        public Pedido Pedido { get; set; } = null!;
        public ICollection<DevolucaoItem> Itens { get; set; } = new List<DevolucaoItem>();
    }

    public class DevolucaoItem
    {
        public int Id { get; set; }
        public int DevolucaoId { get; set; }
        public int ItemCarrinhoId { get; set; }
        public int ProdutoId { get; set; }
        public string ProdutoNome { get; set; } = null!;
        public int CorId { get; set; }
        public string CorNome { get; set; } = null!;
        public int Quantidade { get; set; }

        public Devolucao Devolucao { get; set; } = null!;
    }
}
