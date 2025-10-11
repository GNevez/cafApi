using cafApi.Models;
using cafApi.Models.DTOs;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace cafApi.Services
{
    public interface IProdutoService
    {
        Task<IEnumerable<ProdutoResponseDto>> GetAllAsync();
        Task<ProdutoResponseDto?> GetByIdAsync(int id);
        Task<ProdutoResponseDto?> GetBySlugAsync(string slug);
        Task<IEnumerable<ProdutoResponseDto>> GetByCategoriaAsync(int categoriaId);
        Task<Produtos> CreateAsync(Produtos produto);
        Task<List<Produtos>> CreateManyAsync(List<Produtos> produtos);
        Task<List<Produtos>> CreateManyFromDtoAsync(List<ProdutoCreateDto> produtosDto);
        Task<bool> UpdateAsync(int id, Produtos produto);
        Task<bool> DeleteAsync(int id);
    }
}
