using System;
using System.Collections.Generic;

namespace cafApi.Models.Generated;

public partial class Videos
{
    public int Id { get; set; }

    public string Titulo { get; set; } = null!;

    public string Descricao { get; set; } = null!;

    public string Url { get; set; } = null!;

    public string? Thumbnail { get; set; }

    public int Duracao { get; set; }

    public int Ordem { get; set; }

    public bool Ativo { get; set; }

    public int CategoriaId { get; set; }

    public virtual Categorias Categoria { get; set; } = null!;
}
