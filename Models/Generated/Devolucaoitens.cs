using System;
using System.Collections.Generic;

namespace cafApi.Models.Generated;

public partial class Devolucaoitens
{
    public int Id { get; set; }

    public int DevolucaoId { get; set; }

    public int ItemCarrinhoId { get; set; }

    public int ProdutoId { get; set; }

    public string ProdutoNome { get; set; } = null!;

    public int CorId { get; set; }

    public string CorNome { get; set; } = null!;

    public int Quantidade { get; set; }

    public virtual Devolucoes Devolucao { get; set; } = null!;
}
