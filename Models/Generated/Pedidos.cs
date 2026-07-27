using System;
using System.Collections.Generic;

namespace cafApi.Models.Generated;

public partial class Pedidos
{
    public int Id { get; set; }

    public int ClienteId { get; set; }

    public int CarrinhoId { get; set; }

    public int EnderecoEntregaId { get; set; }

    public int Status { get; set; }

    public decimal? PrecoFrete { get; set; }

    public decimal TotalPedido { get; set; }

    public DateTime DataPedido { get; set; }

    public DateTime? DataAtualizacao { get; set; }

    public string? CodigoRastreamento { get; set; }

    public string MetodoPagamento { get; set; } = null!;

    public string? Observacoes { get; set; }

    public decimal DescontoCupom { get; set; }

    public decimal DescontoPorUnidade { get; set; }

    public string? MotivoCancelamento { get; set; }

    public string EmailCliente { get; set; } = null!;

    public string NomeCliente { get; set; } = null!;

    public string? TelefoneCliente { get; set; }

    public string CodigoPedido { get; set; } = null!;

    public string? PagarmeOrderId { get; set; }

    public virtual Carrinhos Carrinho { get; set; } = null!;

    public virtual Clientes Cliente { get; set; } = null!;

    public virtual ICollection<Devolucoes> Devolucoes { get; set; } = new List<Devolucoes>();

    public virtual Enderecos EnderecoEntrega { get; set; } = null!;

    public virtual ICollection<Prepostagens> Prepostagens { get; set; } = new List<Prepostagens>();

    public virtual ICollection<Transacoes> Transacoes { get; set; } = new List<Transacoes>();
}
