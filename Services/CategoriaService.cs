using cafApi.Contexts;
using cafApi.Models;
using cafApi.Models.DTOs;
using cafApi.Services.Extensions;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace cafApi.Services
{
    public class CategoriaService : ICategoriaService
    {
        private readonly ApplicationDbContext _context;

        public CategoriaService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<Categoria>> GetAllAsync()
        {
            return await _context.Categorias.Where(c => c.Active == true).ToListAsync();
        }

        public async Task<IEnumerable<Categoria>> GetInactiveAsync()
        {
            return await _context.Categorias.Where(c => c.Active == false).ToListAsync();
        }

        public async Task<Categoria?> GetByIdAsync(int id)
        {
            return await _context.Categorias.FindAsync(id);
        }

        public async Task<Categoria> CreateAsync(Categoria categoria)
        {
            _context.Categorias.Add(categoria);
            await _context.SaveChangesAsync();
            return categoria;
        }

        public async Task<bool> UpdateAsync(int id, CategoriaUpdateDto categoriaDto)
        {
            var existingCategoria = await _context.Categorias.FindAsync(id);
            if (existingCategoria == null) return false;

            existingCategoria.Nome = categoriaDto.Nome;
            existingCategoria.Slug = categoriaDto.Nome.GenerateSlug(); // Gerar slug automaticamente
            existingCategoria.Banner = categoriaDto.Banner;
            existingCategoria.Titulo = categoriaDto.Titulo;
            existingCategoria.Mensagem = categoriaDto.Mensagem;

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> HasActiveProductsAsync(int categoriaId)
        {
            return await _context.Produtos
                .AnyAsync(p => p.CategoriaId == categoriaId && p.Active == true);
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var categoria = await _context.Categorias.FindAsync(id);
            if (categoria == null) return false;

            // Soft delete - apenas desativar
            categoria.Active = false;
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> ReactivateAsync(int id)
        {
            var categoria = await _context.Categorias.FindAsync(id);
            if (categoria == null) return false;

            categoria.Active = true;
            await _context.SaveChangesAsync();
            return true;
        }
    }
}
