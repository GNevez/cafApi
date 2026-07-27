using System;
using System.Collections.Generic;

namespace cafApi.Models.Generated;

public partial class Filaimpressao
{
    public int Id { get; set; }

    public int? RotuloId { get; set; }

    public int? PedidoId { get; set; }

    public string? CodigoPedido { get; set; }

    public string NomeArquivo { get; set; } = null!;

    public string CaminhoArquivo { get; set; } = null!;

    public int Status { get; set; }

    public int Tentativas { get; set; }

    public int MaxTentativas { get; set; }

    public string? MensagemErro { get; set; }

    public string? ImpressoraDestino { get; set; }

    public int Copias { get; set; }

    public DateTime DataCriacao { get; set; }

    public DateTime? DataProcessamento { get; set; }

    public DateTime? DataImpressao { get; set; }

    public string? ClienteId { get; set; }

    public virtual Rotulos? Rotulo { get; set; }
}
