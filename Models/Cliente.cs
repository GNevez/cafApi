using System.Text.Json.Serialization;

namespace cafApi.Models;

public class Cliente
{
    public int Id { get; set; }
    public string Nome { get; set; } = null!;
    public string Email { get; set; } = null!;
    public string? Telefone { get; set; }
    public string? Cpf { get; set; }
    public DateTime DataCriacao { get; set; } = DateTime.UtcNow;
    public DateTime? DataAtualizacao { get; set; }
    public bool Ativo { get; set; } = true; // Soft delete

    // Relacionamentos
    public ICollection<Carrinho> Carrinhos { get; set; } = new List<Carrinho>();
    public ICollection<Endereco> Enderecos { get; set; } = new List<Endereco>();
    public ICollection<Pedido> Pedidos { get; set; } = new List<Pedido>();
}
