using cafApi.Models;
using cafApi.Models.DTOs;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace cafApi.Services
{
    public interface IUsuarioService
    {
        Task<IEnumerable<UsuarioResponseDto>> GetAllActiveUsersAsync();
        Task<IEnumerable<UsuarioResponseDto>> GetAllInactiveUsersAsync();
        Task<UsuarioResponseDto?> GetUserByIdAsync(int id);
        Task<bool> BanUserAsync(int id);
        Task<bool> UnbanUserAsync(int id);
        Task<bool> UpdateUserRoleAsync(int id, int newRoleId);
    }
}
