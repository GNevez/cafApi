using System;
using System.Collections.Generic;

namespace cafApi.Models.Generated;

public partial class Produtos
{
    public int Id { get; set; }

    public string Nome { get; set; } = null!;

    public string Slug { get; set; } = null!;

    public decimal Preco { get; set; }

    public decimal? PrecoOriginal { get; set; }

    public bool IsSale { get; set; }

    public bool IsNew { get; set; }

    public string ImagemPrincipal { get; set; } = null!;

    public string ImagemHover { get; set; } = null!;

    public int CategoriaId { get; set; }

    public string? CodigoExterno { get; set; }

    public string? Fabricante { get; set; }

    public string Sku { get; set; } = null!;

    public bool? Active { get; set; }

    public string? Descricao { get; set; }

    public int MaxParcelas { get; set; }

    public decimal TaxaJuros { get; set; }

    public virtual Categorias Categoria { get; set; } = null!;

    public virtual ICollection<Itenscarrinho> Itenscarrinho { get; set; } = new List<Itenscarrinho>();

    public virtual ICollection<Produtoscores> Produtoscores { get; set; } = new List<Produtoscores>();
}
