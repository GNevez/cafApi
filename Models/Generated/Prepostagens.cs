using System;
using System.Collections.Generic;

namespace cafApi.Models.Generated;

public partial class Prepostagens
{
    public int Id { get; set; }

    public int PedidoId { get; set; }

    public string? CodigoRastreamento { get; set; }

    public string? IdPrePostagem { get; set; }

    public string? NumeroEtiqueta { get; set; }

    public string CodigoServico { get; set; } = null!;

    public string NomeServico { get; set; } = null!;

    public decimal Peso { get; set; }

    public int Altura { get; set; }

    public int Largura { get; set; }

    public int Comprimento { get; set; }

    public decimal? ValorDeclarado { get; set; }

    public int Status { get; set; }

    public DateTime DataCriacao { get; set; }

    public DateTime? DataPostagem { get; set; }

    public DateTime? DataEntrega { get; set; }

    public string? Observacoes { get; set; }

    public string? MensagemErro { get; set; }

    public string? RespostaCorreiosJson { get; set; }

    public virtual Pedidos Pedido { get; set; } = null!;
}
