namespace cafApi.Models;

public class Categoria
{
    public int Id { get; set; }
    public string Nome { get; set; } = null!;
    public string Slug { get; set; } = null!;

    public ICollection<Produtos> Oculos { get; set; } = new List<Produtos>();
}
