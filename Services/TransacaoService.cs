using cafApi.Contexts;
using cafApi.Models;
using cafApi.Models.DTOs;
using Microsoft.EntityFrameworkCore;

namespace cafApi.Services
{
    public class TransacaoService : ITransacaoService
    {
        private readonly ApplicationDbContext _context;

        public TransacaoService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<(List<TransacaoDto> transacoes, int totalCount)> GetAllAsync(TipoTransacao? tipo = null, int pageNumber = 1, int pageSize = 10)
        {
            var query = _context.Transacoes.AsQueryable();

            if (tipo.HasValue)
            {
                query = query.Where(t => t.Tipo == tipo.Value);
            }

            var totalCount = await query.CountAsync();

            var transacoes = await query
                .OrderByDescending(t => t.DataTransacao)
                .ThenByDescending(t => t.Id)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (transacoes.Select(MapToDto).ToList(), totalCount);
        }

        public async Task<TransacaoDto?> GetByIdAsync(int id)
        {
            var transacao = await _context.Transacoes.FindAsync(id);
            return transacao == null ? null : MapToDto(transacao);
        }

        public async Task<TransacaoDto> CreateAsync(CriarTransacaoDto dto)
        {
            var transacao = new Transacao
            {
                Tipo = dto.Tipo,
                Valor = dto.Valor,
                Descricao = dto.Descricao,
                MetodoPagamento = dto.MetodoPagamento,
                DataTransacao = dto.DataTransacao ?? DateTime.UtcNow,
                DataCriacao = DateTime.UtcNow
            };

            _context.Transacoes.Add(transacao);
            await _context.SaveChangesAsync();

            return MapToDto(transacao);
        }

        public async Task<TransacaoDto?> UpdateAsync(int id, AtualizarTransacaoDto dto)
        {
            var transacao = await _context.Transacoes.FindAsync(id);
            if (transacao == null) return null;

            if (dto.Valor.HasValue)
                transacao.Valor = dto.Valor.Value;

            if (!string.IsNullOrWhiteSpace(dto.Descricao))
                transacao.Descricao = dto.Descricao;

            if (!string.IsNullOrWhiteSpace(dto.MetodoPagamento))
                transacao.MetodoPagamento = dto.MetodoPagamento;

            if (dto.DataTransacao.HasValue)
                transacao.DataTransacao = dto.DataTransacao.Value;

            await _context.SaveChangesAsync();

            return MapToDto(transacao);
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var transacao = await _context.Transacoes.FindAsync(id);
            if (transacao == null) return false;

            _context.Transacoes.Remove(transacao);
            await _context.SaveChangesAsync();

            return true;
        }

        public async Task<decimal> GetSaldoAsync()
        {
            var entradas = await _context.Transacoes
                .Where(t => t.Tipo == TipoTransacao.Entrada)
                .SumAsync(t => t.Valor);

            var saidas = await _context.Transacoes
                .Where(t => t.Tipo == TipoTransacao.Saida)
                .SumAsync(t => t.Valor);

            return entradas - saidas;
        }

        public async Task<decimal> GetTotalByTipoAsync(TipoTransacao tipo)
        {
            return await _context.Transacoes
                .Where(t => t.Tipo == tipo)
                .SumAsync(t => t.Valor);
        }

        private static TransacaoDto MapToDto(Transacao transacao)
        {
            return new TransacaoDto
            {
                Id = transacao.Id,
                Tipo = transacao.Tipo,
                Valor = transacao.Valor,
                Descricao = transacao.Descricao,
                MetodoPagamento = transacao.MetodoPagamento,
                PedidoId = transacao.PedidoId,
                DataTransacao = transacao.DataTransacao,
                DataCriacao = transacao.DataCriacao
            };
        }
    }
}
