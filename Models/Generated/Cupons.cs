using System;
using System.Collections.Generic;

namespace cafApi.Models.Generated;

public partial class Cupons
{
    public int Id { get; set; }

    public string Codigo { get; set; } = null!;

    public string? Descricao { get; set; }

    public string TipoDesconto { get; set; } = null!;

    public decimal ValorDesconto { get; set; }

    public decimal? ValorMinimoCompra { get; set; }

    public decimal? ValorMaximoDesconto { get; set; }

    public int? QuantidadeMaximaUsos { get; set; }

    public int QuantidadeUsosAtual { get; set; }

    public int UsoPorUsuario { get; set; }

    public DateTime DataInicio { get; set; }

    public DateTime? DataExpiracao { get; set; }

    public bool Ativo { get; set; }

    public DateTime DataCriacao { get; set; }

    public DateTime? DataAtualizacao { get; set; }

    public virtual ICollection<Carrinhos> Carrinhos { get; set; } = new List<Carrinhos>();

    public virtual ICollection<CuponsUso> CuponsUso { get; set; } = new List<CuponsUso>();
}
