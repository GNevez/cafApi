using System.Text.Json.Serialization;

namespace cafApi.Models;

public class Carrinho
{
    public int Id { get; set; }
    public string Token { get; set; } = null!; // Token único do carrinho
    public StatusCarrinho Status { get; set; } = StatusCarrinho.Ativo;
    public DateTime DataCriacao { get; set; } = DateTime.UtcNow;
    public DateTime? DataAtualizacao { get; set; }
    public bool Ativo { get; set; } = true; // Soft delete

    // Relacionamento com cliente (opcional - pode ser null para carrinhos anônimos)
    public int? ClienteId { get; set; }
    public Cliente? Cliente { get; set; }

    // Relacionamentos
    public ICollection<ItemCarrinho> Itens { get; set; } = new List<ItemCarrinho>();
    public ICollection<Pedido> Pedidos { get; set; } = new List<Pedido>();

    // Cupom aplicado ao carrinho
    public int? CupomId { get; set; }
    public Cupom? Cupom { get; set; }
}
