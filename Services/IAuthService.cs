using cafApi.Models.DTOs;

namespace cafApi.Services
{
    public interface IAuthService
    {
        Task<AuthResponseDto?> LoginAsync(LoginDto loginDto, HttpContext httpContext);
        Task<AuthResponseDto?> RegisterAsync(RegisterDto registerDto);
        Task<UserValidationDto?> ValidateAndGetDataAsync(string token);
        Task<string?> GetUserRoleAsync(string token);
    }
}
