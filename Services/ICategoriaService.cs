using cafApi.Models;
using cafApi.Models.DTOs;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace cafApi.Services
{
    public interface ICategoriaService
    {
        Task<IEnumerable<Categoria>> GetAllAsync();
        Task<IEnumerable<Categoria>> GetInactiveAsync();
        Task<Categoria?> GetByIdAsync(int id);
        Task<Categoria> CreateAsync(Categoria categoria);
        Task<bool> UpdateAsync(int id, CategoriaUpdateDto categoriaDto);
        Task<bool> HasActiveProductsAsync(int categoriaId);
        Task<bool> DeleteAsync(int id);
        Task<bool> ReactivateAsync(int id);
    }
}
