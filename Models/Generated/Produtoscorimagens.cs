using System;
using System.Collections.Generic;

namespace cafApi.Models.Generated;

public partial class Produtoscorimagens
{
    public int Id { get; set; }

    public string Url { get; set; } = null!;

    public int Ordem { get; set; }

    public int ProdutosCorId { get; set; }

    public virtual Produtoscores ProdutosCor { get; set; } = null!;
}
