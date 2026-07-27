using System;
using System.Collections.Generic;

namespace cafApi.Models.Generated;

public partial class Produtoscores
{
    public int Id { get; set; }

    public string Nome { get; set; } = null!;

    public int QuantidadeEstoque { get; set; }

    public int ProdutosId { get; set; }

    public string? Hex1 { get; set; }

    public string? Hex2 { get; set; }

    public virtual ICollection<Itenscarrinho> Itenscarrinho { get; set; } = new List<Itenscarrinho>();

    public virtual Produtos Produtos { get; set; } = null!;

    public virtual ICollection<Produtoscorimagens> Produtoscorimagens { get; set; } = new List<Produtoscorimagens>();
}
