using cafApi.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace cafApi.Services
{
    public interface ICorService
    {
        Task<IEnumerable<Cor>> GetAllAsync();
        Task<Cor?> GetByIdAsync(int id);
        Task<Cor> CreateAsync(Cor cor);
        Task<bool> UpdateAsync(int id, Cor cor);
        Task<bool> DeleteAsync(int id);
    }
}
