using System;
using System.Collections.Generic;

namespace cafApi.Models.Generated;

public partial class Devolucoes
{
    public int Id { get; set; }

    public int PedidoId { get; set; }

    public string Cpf { get; set; } = null!;

    public string NomeCliente { get; set; } = null!;

    public string Email { get; set; } = null!;

    public int Status { get; set; }

    public DateTime DataCriacao { get; set; }

    public DateTime? DataAtualizacao { get; set; }

    public virtual ICollection<Devolucaoitens> Devolucaoitens { get; set; } = new List<Devolucaoitens>();

    public virtual Pedidos Pedido { get; set; } = null!;
}
