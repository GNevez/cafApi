using cafApi.Models;
using cafApi.Models.DTOs;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace cafApi.Services
{
    public interface IProdutoService
    {
        Task<IEnumerable<ProdutoResponseDto>> GetAllAsync();
        Task<(List<ProdutoResponseDto> produtos, int totalCount)> GetPaginatedAsync(int pageNumber, int pageSize, int? categoriaId = null, int? corId = null, decimal? precoMin = null, decimal? precoMax = null, string? ordenacao = null);
        Task<IEnumerable<ProdutoResponseDto>> GetInactiveAsync(); // 🔹 Listar produtos desativados
        Task<ProdutoResponseDto?> GetByIdAsync(int id);
        Task<ProdutoResponseDto?> GetBySlugAsync(string slug);
        Task<ProdutoDetalhadoDto?> GetBySlugDetalhadoAsync(string slug);
        Task<IEnumerable<ProdutoResponseDto>> GetByCategoriaAsync(int categoriaId);
        Task<IEnumerable<ProdutoSearchDto>> SearchAsync(string query, int limit = 8);
        Task<Produtos> CreateAsync(Produtos produto);
        Task<List<Produtos>> CreateManyAsync(List<Produtos> produtos);
        Task<List<Produtos>> CreateManyFromDtoAsync(List<ProdutoCreateDto> produtosDto);
        Task<bool> UpdateAsync(int id, Produtos produto);
        Task<bool> DeleteAsync(int id);
        Task<bool> DeactivateAsync(int id); // 🔹 Soft delete - desativar produto
        Task<bool> ReactivateAsync(int id); // 🔹 Reativar produto
        Task<bool> DeleteCorAsync(int corId);
        void AddCor(ProdutosCor cor);
        void AddImagemCor(ProdutosCorImagem imagemCor);
        Task SaveChangesAsync();
        Task<List<ProdutosCor>> GetCoresByProdutoIdAsync(int produtoId);
        Task<List<ProdutosCorImagem>> GetImagensByCorIdAsync(int corId);
        Task<Produtos?> GetBySKUAsync(string sku);
        Task<Produtos?> GetBySKUIncludingInactiveAsync(string sku); // 🔹 Inclui produtos desativados
        Task<Produtos?> GetByCodigoExternoAsync(string codigoExterno);
        Task<Produtos?> GetByCodigoExternoIncludingInactiveAsync(string codigoExterno); // 🔹 Inclui produtos desativados
        Task<IEnumerable<object>> GetCoresDisponiveisAsync();
    }
}
