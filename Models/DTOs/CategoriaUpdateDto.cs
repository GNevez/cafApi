namespace cafApi.Models.DTOs
{
    public class CategoriaUpdateDto
    {
        public string Nome { get; set; } = string.Empty;
        public string? Titulo { get; set; }
        public string? Mensagem { get; set; }
        public string? Banner { get; set; }
    }
}
