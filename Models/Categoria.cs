namespace cafApi.Models;

public class Categoria
{
    public int Id { get; set; }
    public string Nome { get; set; } = null!;
    public string Slug { get; set; } = null!;
    public string? Banner { get; set; }
    public string? Titulo { get; set; }
    public string? Mensagem { get; set; }
    public bool Active { get; set; } = true;
}
