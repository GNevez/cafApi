using cafApi.Contexts;
using cafApi.Models;
using System.Security.Cryptography;
using System.Text;

namespace cafApi.Services
{
    public class SeedService
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly ILogger<SeedService> _logger;

        public SeedService(
            ApplicationDbContext context,
            IConfiguration configuration,
            ILogger<SeedService> logger)
        {
            _context = context;
            _configuration = configuration;
            _logger = logger;
        }

        public async Task SeedAsync()
        {
            // Verificar se já existem dados
            if (_context.Roles.Any())
                return;

            // Criar roles
            var adminRole = new Role
            {
                Nome = "Administrador",
                Descricao = "Administrador do sistema"
            };

            var userRole = new Role
            {
                Nome = "Usuario",
                Descricao = "Usuário comum"
            };

            _context.Roles.AddRange(adminRole, userRole);
            await _context.SaveChangesAsync();

            // Criar usuário administrador padrão
            var adminEmail = _configuration["Seed:AdminEmail"];
            var adminPassword = _configuration["Seed:AdminPassword"];

            if (string.IsNullOrWhiteSpace(adminEmail) || string.IsNullOrWhiteSpace(adminPassword))
            {
                _logger.LogWarning(
                    "Administrador inicial nao criado. Configure Seed__AdminEmail e Seed__AdminPassword.");
                return;
            }

            var adminUser = new Usuario
            {
                Nome = "Administrador",
                Email = adminEmail,
                Senha = HashPassword(adminPassword),
                RoleId = adminRole.Id,
                DataCriacao = DateTime.UtcNow,
                Ativo = true
            };

            _context.Usuarios.Add(adminUser);
            await _context.SaveChangesAsync();
        }

        private string HashPassword(string password)
        {
            using var sha256 = SHA256.Create();
            var hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
            return Convert.ToBase64String(hashedBytes);
        }
    }
}
