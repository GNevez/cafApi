using System;
using System.Collections.Generic;

namespace cafApi.Models.Generated;

public partial class Clientes
{
    public int Id { get; set; }

    public string Nome { get; set; } = null!;

    public string Email { get; set; } = null!;

    public string? Telefone { get; set; }

    public string? Cpf { get; set; }

    public DateTime DataCriacao { get; set; }

    public DateTime? DataAtualizacao { get; set; }

    public bool Ativo { get; set; }

    public virtual ICollection<Carrinhos> Carrinhos { get; set; } = new List<Carrinhos>();

    public virtual ICollection<Enderecos> Enderecos { get; set; } = new List<Enderecos>();

    public virtual ICollection<Pedidos> Pedidos { get; set; } = new List<Pedidos>();
}
