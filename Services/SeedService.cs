using cafApi.Contexts;
using cafApi.Models;
using System.Security.Cryptography;
using System.Text;

namespace cafApi.Services
{
    public class SeedService
    {
        private readonly ApplicationDbContext _context;

        public SeedService(ApplicationDbContext context)
        {
            _context = context;
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
            var adminUser = new Usuario
            {
                Nome = "Administrador",
                Email = "admin@chaseaflare.com.br",
                Senha = HashPassword("REMOVED_DEFAULT_PASSWORD"),
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
