using System;
using System.Collections.Generic;

namespace cafApi.Models.Generated;

public partial class Transacoes
{
    public int Id { get; set; }

    public int Tipo { get; set; }

    public decimal Valor { get; set; }

    public string Descricao { get; set; } = null!;

    public string MetodoPagamento { get; set; } = null!;

    public int? PedidoId { get; set; }

    public DateTime DataTransacao { get; set; }

    public DateTime DataCriacao { get; set; }

    public virtual Pedidos? Pedido { get; set; }
}
