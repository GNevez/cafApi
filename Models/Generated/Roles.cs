using System;
using System.Collections.Generic;

namespace cafApi.Models.Generated;

public partial class Roles
{
    public int Id { get; set; }

    public string Nome { get; set; } = null!;

    public string Descricao { get; set; } = null!;

    public virtual ICollection<Usuarios> Usuarios { get; set; } = new List<Usuarios>();
}
