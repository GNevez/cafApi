using System;
using System.Collections.Generic;

namespace cafApi.Models.Generated;

public partial class Usuarios
{
    public int Id { get; set; }

    public string Nome { get; set; } = null!;

    public string Email { get; set; } = null!;

    public string Senha { get; set; } = null!;

    public int RoleId { get; set; }

    public DateTime DataCriacao { get; set; }

    public bool Ativo { get; set; }

    public virtual ICollection<CuponsUso> CuponsUso { get; set; } = new List<CuponsUso>();

    public virtual Roles Role { get; set; } = null!;
}
