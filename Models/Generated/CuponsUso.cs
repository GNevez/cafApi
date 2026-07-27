using System;
using System.Collections.Generic;

namespace cafApi.Models.Generated;

public partial class CuponsUso
{
    public int Id { get; set; }

    public int CupomId { get; set; }

    public int? UsuarioId { get; set; }

    public int? PedidoId { get; set; }

    public decimal ValorDescontoAplicado { get; set; }

    public DateTime DataUso { get; set; }

    public virtual Cupons Cupom { get; set; } = null!;

    public virtual Usuarios? Usuario { get; set; }
}
