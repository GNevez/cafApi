using System;
using System.Collections.Generic;

namespace cafApi.Models.Generated;

public partial class Carrinhos
{
    public int Id { get; set; }

    public string Token { get; set; } = null!;

    public DateTime DataCriacao { get; set; }

    public DateTime? DataAtualizacao { get; set; }

    public bool Ativo { get; set; }

    public int? ClienteId { get; set; }

    public int Status { get; set; }

    public int? CupomId { get; set; }

    public virtual Clientes? Cliente { get; set; }

    public virtual Cupons? Cupom { get; set; }

    public virtual ICollection<Itenscarrinho> Itenscarrinho { get; set; } = new List<Itenscarrinho>();

    public virtual ICollection<Pedidos> Pedidos { get; set; } = new List<Pedidos>();
}
