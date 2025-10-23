namespace cafApi.Models
{
    public class Endereco
    {
        public int Id { get; set; }
        public int ClienteId { get; set; }
        public string Cep { get; set; } = null!;
        public string Logradouro { get; set; } = null!;
        public string Numero { get; set; } = null!;
        public string? Complemento { get; set; }
        public string Bairro { get; set; } = null!;
        public string Cidade { get; set; } = null!;
        public string Estado { get; set; } = null!;
        public bool IsPrincipal { get; set; } = false; // Endereço principal do cliente
        public DateTime DataCriacao { get; set; } = DateTime.UtcNow;
        public DateTime? DataAtualizacao { get; set; }

        // Propriedades de navegação
        public Cliente Cliente { get; set; } = null!;
        public ICollection<Pedido> Pedidos { get; set; } = new List<Pedido>();
    }
}
