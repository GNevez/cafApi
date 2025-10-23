using cafApi.Contexts;
using cafApi.Models;
using cafApi.Models.DTOs;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace cafApi.Services
{
    public class UsuarioService : IUsuarioService
    {
        private readonly ApplicationDbContext _context;

        public UsuarioService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<UsuarioResponseDto>> GetAllActiveUsersAsync()
        {
            try
            {
                Console.WriteLine("🔍 Buscando usuários ativos...");
                var usuarios = await _context.Usuarios
                    .Include(u => u.Role)
                    .Where(u => u.Ativo == true)
                    .Select(u => new UsuarioResponseDto
                    {
                        Id = u.Id,
                        Nome = u.Nome,
                        Email = u.Email,
                        DataCriacao = u.DataCriacao,
                        Ativo = u.Ativo,
                        Role = new RoleDto
                        {
                            Id = u.Role.Id,
                            Nome = u.Role.Nome,
                            Descricao = u.Role.Descricao
                        }
                    })
                    .ToListAsync();
                Console.WriteLine($"✅ Encontrados {usuarios.Count} usuários ativos");
                return usuarios;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Erro ao buscar usuários: {ex.Message}");
                throw;
            }
        }

        public async Task<IEnumerable<UsuarioResponseDto>> GetAllInactiveUsersAsync()
        {
            return await _context.Usuarios
                .Include(u => u.Role)
                .Where(u => u.Ativo == false)
                .Select(u => new UsuarioResponseDto
                {
                    Id = u.Id,
                    Nome = u.Nome,
                    Email = u.Email,
                    DataCriacao = u.DataCriacao,
                    Ativo = u.Ativo,
                    Role = new RoleDto
                    {
                        Id = u.Role.Id,
                        Nome = u.Role.Nome,
                        Descricao = u.Role.Descricao
                    }
                })
                .ToListAsync();
        }

        public async Task<UsuarioResponseDto?> GetUserByIdAsync(int id)
        {
            return await _context.Usuarios
                .Include(u => u.Role)
                .Where(u => u.Id == id)
                .Select(u => new UsuarioResponseDto
                {
                    Id = u.Id,
                    Nome = u.Nome,
                    Email = u.Email,
                    DataCriacao = u.DataCriacao,
                    Ativo = u.Ativo,
                    Role = new RoleDto
                    {
                        Id = u.Role.Id,
                        Nome = u.Role.Nome,
                        Descricao = u.Role.Descricao
                    }
                })
                .FirstOrDefaultAsync();
        }

        public async Task<bool> BanUserAsync(int id)
        {
            var usuario = await _context.Usuarios.FindAsync(id);
            if (usuario == null) return false;

            usuario.Ativo = false;
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> UnbanUserAsync(int id)
        {
            var usuario = await _context.Usuarios.FindAsync(id);
            if (usuario == null) return false;

            usuario.Ativo = true;
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> UpdateUserRoleAsync(int id, int newRoleId)
        {
            var usuario = await _context.Usuarios.FindAsync(id);
            if (usuario == null) return false;

            var roleExists = await _context.Roles.AnyAsync(r => r.Id == newRoleId);
            if (!roleExists) return false;

            usuario.RoleId = newRoleId;
            await _context.SaveChangesAsync();
            return true;
        }
    }
}
