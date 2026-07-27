using System;
using System.Collections.Generic;

namespace cafApi.Models.Generated;

public partial class Itenscarrinho
{
    public int Id { get; set; }

    public int CarrinhoId { get; set; }

    public int ProdutoId { get; set; }

    public int CorId { get; set; }

    public int Quantidade { get; set; }

    public DateTime DataAdicao { get; set; }

    public DateTime? DataAtualizacao { get; set; }

    public virtual Carrinhos Carrinho { get; set; } = null!;

    public virtual Produtoscores Cor { get; set; } = null!;

    public virtual Produtos Produto { get; set; } = null!;
}
