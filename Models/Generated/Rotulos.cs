using System;
using System.Collections.Generic;

namespace cafApi.Models.Generated;

public partial class Rotulos
{
    public int Id { get; set; }

    public int? IdPedido { get; set; }

    public string IdRecibo { get; set; } = null!;

    public string? IdAtendimento { get; set; }

    public string NomeArquivo { get; set; } = null!;

    public string CaminhoArquivo { get; set; } = null!;

    public DateTime DataGeracao { get; set; }

    public int QuantidadeRotulos { get; set; }

    public string? CodigosObjeto { get; set; }

    public string? IdsPrePostagem { get; set; }

    public string TipoRotulo { get; set; } = null!;

    public string FormatoRotulo { get; set; } = null!;

    public long TamanhoBytes { get; set; }

    public string? Observacao { get; set; }

    public virtual ICollection<Filaimpressao> Filaimpressao { get; set; } = new List<Filaimpressao>();
}
