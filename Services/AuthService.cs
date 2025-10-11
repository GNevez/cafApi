using cafApi.Contexts;
using cafApi.Models;
using cafApi.Models.DTOs;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace cafApi.Services
{
    public class AuthService : IAuthService
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;

        public AuthService(ApplicationDbContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        public async Task<AuthResponseDto?> LoginAsync(LoginDto loginDto, HttpContext httpContext)
        {
            var usuario = await _context.Usuarios
                .Include(u => u.Role)
                .FirstOrDefaultAsync(u => u.Email == loginDto.Email && u.Ativo);

            if (usuario == null || !VerifyPassword(loginDto.Senha, usuario.Senha))
                return null;

            var token = GenerateJwtToken(usuario);
            var expiresAt = DateTime.UtcNow.AddHours(24);

            // 🔐 Definir cookie com o token
            var cookieOptions = new CookieOptions
            {
                HttpOnly = true, // Não acessível via JavaScript (mais seguro)
                Secure = true,   // Apenas HTTPS
                SameSite = SameSiteMode.Strict, // Proteção CSRF
                Expires = expiresAt
            };

            httpContext.Response.Cookies.Append("auth_token", token, cookieOptions);

            return new AuthResponseDto
            {
                Token = token, // Ainda retorna para compatibilidade
                Nome = usuario.Nome,
                Email = usuario.Email,
                Role = usuario.Role.Nome,
                ExpiresAt = expiresAt
            };
        }

        public async Task<AuthResponseDto?> RegisterAsync(RegisterDto registerDto)
        {
            // Verificar se email já existe
            if (await _context.Usuarios.AnyAsync(u => u.Email == registerDto.Email))
                return null;

            // Buscar role de usuário padrão (assumindo que existe)
            var userRole = await _context.Roles.FirstOrDefaultAsync(r => r.Nome == "Usuario");
            if (userRole == null)
            {
                // Criar role de usuário se não existir
                userRole = new Role { Nome = "Usuario", Descricao = "Usuário comum" };
                _context.Roles.Add(userRole);
                await _context.SaveChangesAsync();
            }

            var usuario = new Usuario
            {
                Nome = registerDto.Nome,
                Email = registerDto.Email,
                Senha = HashPassword(registerDto.Senha),
                RoleId = userRole.Id,
                DataCriacao = DateTime.UtcNow,
                Ativo = true
            };

            _context.Usuarios.Add(usuario);
            await _context.SaveChangesAsync();

            var token = GenerateJwtToken(usuario);
            var expiresAt = DateTime.UtcNow.AddHours(24);

            return new AuthResponseDto
            {
                Token = token,
                Nome = usuario.Nome,
                Email = usuario.Email,
                Role = userRole.Nome,
                ExpiresAt = expiresAt
            };
        }

        public async Task<UserValidationDto?> ValidateAndGetDataAsync(string token)
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.ASCII.GetBytes(_configuration["Jwt:Key"]!);

            try
            {
                var principal = tokenHandler.ValidateToken(token, new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(key),
                    ValidateIssuer = true,
                    ValidIssuer = _configuration["Jwt:Issuer"],
                    ValidateAudience = true,
                    ValidAudience = _configuration["Jwt:Audience"],
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero
                }, out SecurityToken validatedToken);

                if (validatedToken == null) return null;

                return new UserValidationDto
                {
                    Nome = principal.FindFirst(ClaimTypes.Name)?.Value,
                    Email = principal.FindFirst(ClaimTypes.Email)?.Value,
                    Role = principal.FindFirst(ClaimTypes.Role)?.Value
                };
            }
            catch
            {
                return null;
            }
        }

        public async Task<string?> GetUserRoleAsync(string token)
        {
            try
            {
                var tokenHandler = new JwtSecurityTokenHandler();
                var jwtToken = tokenHandler.ReadJwtToken(token);
                var roleClaim = jwtToken.Claims.FirstOrDefault(x => x.Type == ClaimTypes.Role);
                return roleClaim?.Value;
            }
            catch
            {
                return null;
            }
        }

        private string GenerateJwtToken(Usuario usuario)
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.ASCII.GetBytes(_configuration["Jwt:Key"]!);

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, usuario.Id.ToString()),
                    new Claim(ClaimTypes.Name, usuario.Nome),
                    new Claim(ClaimTypes.Email, usuario.Email),
                    new Claim(ClaimTypes.Role, usuario.Role.Nome)
                }),
                Expires = DateTime.UtcNow.AddHours(24),
                Issuer = _configuration["Jwt:Issuer"],
                Audience = _configuration["Jwt:Audience"],
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
            };

            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }

        private string HashPassword(string password)
        {
            using var sha256 = SHA256.Create();
            var hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
            return Convert.ToBase64String(hashedBytes);
        }

        private bool VerifyPassword(string password, string hashedPassword)
        {
            var hashedInput = HashPassword(password);
            return hashedInput == hashedPassword;
        }
    }
}
