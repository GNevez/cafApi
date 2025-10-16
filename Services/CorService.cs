using cafApi.Contexts;
using cafApi.Models;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace cafApi.Services
{
    public class CorService : ICorService
    {
        private readonly ApplicationDbContext _context;

        public CorService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<Cor>> GetAllAsync()
        {
            return await _context.Cor.ToListAsync();
        }

        public async Task<Cor?> GetByIdAsync(int id)
        {
            return await _context.Cor.FindAsync(id);
        }

        public async Task<Cor> CreateAsync(Cor cor)
        {
            _context.Cor.Add(cor);
            await _context.SaveChangesAsync();
            return cor;
        }

        public async Task<bool> UpdateAsync(int id, Cor cor)
        {
            var existingCor = await _context.Cor.FindAsync(id);
            if (existingCor == null) return false;

            existingCor.Nome = cor.Nome;
            existingCor.CodigoHex = cor.CodigoHex;
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var cor = await _context.Cor.FindAsync(id);
            if (cor == null) return false;

            _context.Cor.Remove(cor);
            await _context.SaveChangesAsync();
            return true;
        }
    }
}
