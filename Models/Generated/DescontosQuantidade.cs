using System;
using System.Collections.Generic;

namespace cafApi.Models.Generated;

public partial class DescontosQuantidade
{
    public int Id { get; set; }

    public int QuantidadeMinima { get; set; }

    public int QuantidadeMaxima { get; set; }

    public decimal ValorPromocional { get; set; }

    public string? Descricao { get; set; }

    public bool Ativo { get; set; }

    public DateTime DataCriacao { get; set; }

    public DateTime? DataAtualizacao { get; set; }
}
