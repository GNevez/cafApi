namespace cafApi.Models.DTOs
{
    public class PedidoDto
    {
        public int Id { get; set; }
        public int ClienteId { get; set; }
        public string ClienteNome { get; set; } = null!;
        public string ClienteEmail { get; set; } = null!;
        public StatusPedido Status { get; set; }
        public decimal? PrecoFrete { get; set; }
    public decimal TotalPedido { get; set; } // bruto, sem descontos
    public decimal DescontoPorUnidade { get; set; }
    public decimal DescontoCupom { get; set; }
        public DateTime DataPedido { get; set; }
        public DateTime? DataAtualizacao { get; set; }
        public string? CodigoRastreamento { get; set; }
        public string MetodoPagamento { get; set; } = null!;
        public string? Observacoes { get; set; }
        public EnderecoDto EnderecoEntrega { get; set; } = null!;
        public List<ItemPedidoDto> Itens { get; set; } = new List<ItemPedidoDto>();
    }

    public class ItemPedidoDto
    {
        public int Id { get; set; }
        public int ProdutoId { get; set; }
        public string ProdutoNome { get; set; } = null!;
        public string ProdutoSlug { get; set; } = null!;
        public decimal ProdutoPreco { get; set; }
        public string ProdutoImagem { get; set; } = null!;
        public int CorId { get; set; }
        public string CorNome { get; set; } = null!;
        public string? CorHex1 { get; set; }
        public string? CorHex2 { get; set; }
        public int Quantidade { get; set; }
        public decimal PrecoTotalItem { get; set; }
    }

    public class EnderecoDto
    {
        public int Id { get; set; }
        public string Cep { get; set; } = null!;
        public string Logradouro { get; set; } = null!;
        public string Numero { get; set; } = null!;
        public string? Complemento { get; set; }
        public string Bairro { get; set; } = null!;
        public string Cidade { get; set; } = null!;
        public string Estado { get; set; } = null!;
    }

    public class CriarPedidoDto
    {
        public string Nome { get; set; } = null!;
        public string Email { get; set; } = null!;
        public string? Telefone { get; set; }
        public string? Cpf { get; set; }
        public string Cep { get; set; } = null!;
        public string Logradouro { get; set; } = null!;
        public string Numero { get; set; } = null!;
        public string? Complemento { get; set; }
        public string Bairro { get; set; } = null!;
        public string Cidade { get; set; } = null!;
        public string Estado { get; set; } = null!;
        public string MetodoPagamento { get; set; } = null!;
        public decimal? PrecoFrete { get; set; }
        public string? Observacoes { get; set; }
        public decimal? DescontoPorUnidade { get; set; }
        public decimal? DescontoCupom { get; set; }
    }

    public class AtualizarStatusPedidoDto
    {
        public StatusPedido Status { get; set; }
        public string? CodigoRastreamento { get; set; }
        public string? Observacoes { get; set; }
    }
}
