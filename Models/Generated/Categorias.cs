using System;
using System.Collections.Generic;

namespace cafApi.Models.Generated;

public partial class Categorias
{
    public int Id { get; set; }

    public string Nome { get; set; } = null!;

    public string Slug { get; set; } = null!;

    public string? Banner { get; set; }

    public string? Mensagem { get; set; }

    public string? Titulo { get; set; }

    public bool? Active { get; set; }

    public virtual ICollection<Produtos> Produtos { get; set; } = new List<Produtos>();

    public virtual ICollection<Videos> Videos { get; set; } = new List<Videos>();
}
