using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using cafApi.Contexts;
using cafApi.Models;
using cafApi.Attributes;

namespace cafApi.Controller
{
    [ApiController]
    [Route("api/[controller]")]
    public class DescontoQuantidadeController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public DescontoQuantidadeController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: api/descontoquantidade
        [HttpGet]
        public async Task<ActionResult<IEnumerable<DescontoQuantidade>>> GetDescontos()
        {
            return await _context.DescontosQuantidade
                .Where(d => d.Ativo)
                .OrderBy(d => d.QuantidadeMinima)
                .ToListAsync();
        }

        // GET: api/descontoquantidade/all (incluindo inativos)
        [HttpGet("all")]
        [RequireAdmin]
        public async Task<ActionResult<IEnumerable<DescontoQuantidade>>> GetAllDescontos()
        {
            return await _context.DescontosQuantidade
                .OrderBy(d => d.QuantidadeMinima)
                .ToListAsync();
        }

        // GET: api/descontoquantidade/{id}
        [HttpGet("{id}")]
        public async Task<ActionResult<DescontoQuantidade>> GetDesconto(int id)
        {
            var desconto = await _context.DescontosQuantidade.FindAsync(id);

            if (desconto == null)
            {
                return NotFound(new { message = "Desconto não encontrado" });
            }

            return desconto;
        }

        // GET: api/descontoquantidade/calcular/{quantidade}
        [HttpGet("calcular/{quantidade}")]
        public async Task<ActionResult<object>> CalcularDesconto(int quantidade, [FromQuery] decimal valorOriginal)
        {
            var desconto = await _context.DescontosQuantidade
                .Where(d => d.Ativo && 
                           d.QuantidadeMinima <= quantidade && 
                           (d.QuantidadeMaxima == null || d.QuantidadeMaxima >= quantidade))
                .OrderByDescending(d => d.QuantidadeMinima)
                .FirstOrDefaultAsync();

            if (desconto == null)
            {
                return Ok(new
                {
                    aplicado = false,
                    valorOriginal,
                    valorFinal = valorOriginal,
                    desconto = 0m,
                    descricao = "Nenhum desconto disponível"
                });
            }

            var valorFinal = desconto.ValorPromocional;
            var valorDesconto = valorOriginal - valorFinal;

            return Ok(new
            {
                aplicado = true,
                valorOriginal,
                valorFinal,
                desconto = valorDesconto,
                descricao = desconto.Descricao,
                quantidadeMinima = desconto.QuantidadeMinima,
                quantidadeMaxima = desconto.QuantidadeMaxima
            });
        }

        // POST: api/descontoquantidade
        [HttpPost]
        [RequireAdmin]
        public async Task<ActionResult<DescontoQuantidade>> CreateDesconto(DescontoQuantidade desconto)
        {
            // Validar se não existe conflito de quantidade
            var conflito = await _context.DescontosQuantidade
                .Where(d => d.Ativo && d.Id != desconto.Id)
                .AnyAsync(d => 
                    (d.QuantidadeMinima <= desconto.QuantidadeMinima && 
                     (d.QuantidadeMaxima == null || d.QuantidadeMaxima >= desconto.QuantidadeMinima)) ||
                    (desconto.QuantidadeMaxima != null && 
                     d.QuantidadeMinima <= desconto.QuantidadeMaxima && 
                     (d.QuantidadeMaxima == null || d.QuantidadeMaxima >= desconto.QuantidadeMaxima))
                );

            if (conflito)
            {
                return BadRequest(new { message = "Já existe um desconto ativo para esta faixa de quantidade" });
            }

            desconto.DataCriacao = DateTime.UtcNow;
            _context.DescontosQuantidade.Add(desconto);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetDesconto), new { id = desconto.Id }, desconto);
        }

        // PUT: api/descontoquantidade/{id}
        [HttpPut("{id}")]
        [RequireAdmin]
        public async Task<IActionResult> UpdateDesconto(int id, DescontoQuantidade desconto)
        {
            if (id != desconto.Id)
            {
                return BadRequest(new { message = "ID não corresponde" });
            }

            var descontoExistente = await _context.DescontosQuantidade.FindAsync(id);
            if (descontoExistente == null)
            {
                return NotFound(new { message = "Desconto não encontrado" });
            }

            // Validar conflito
            var conflito = await _context.DescontosQuantidade
                .Where(d => d.Ativo && d.Id != id)
                .AnyAsync(d => 
                    (d.QuantidadeMinima <= desconto.QuantidadeMinima && 
                     (d.QuantidadeMaxima == null || d.QuantidadeMaxima >= desconto.QuantidadeMinima)) ||
                    (desconto.QuantidadeMaxima != null && 
                     d.QuantidadeMinima <= desconto.QuantidadeMaxima && 
                     (d.QuantidadeMaxima == null || d.QuantidadeMaxima >= desconto.QuantidadeMaxima))
                );

            if (conflito)
            {
                return BadRequest(new { message = "Já existe um desconto ativo para esta faixa de quantidade" });
            }

            descontoExistente.QuantidadeMinima = desconto.QuantidadeMinima;
            descontoExistente.QuantidadeMaxima = desconto.QuantidadeMaxima;
            descontoExistente.ValorPromocional = desconto.ValorPromocional;
            descontoExistente.Descricao = desconto.Descricao;
            descontoExistente.Ativo = desconto.Ativo;
            descontoExistente.DataAtualizacao = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return NoContent();
        }

        // DELETE: api/descontoquantidade/{id}
        [HttpDelete("{id}")]
        [RequireAdmin]
        public async Task<IActionResult> DeleteDesconto(int id)
        {
            var desconto = await _context.DescontosQuantidade.FindAsync(id);
            if (desconto == null)
            {
                return NotFound(new { message = "Desconto não encontrado" });
            }

            _context.DescontosQuantidade.Remove(desconto);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        // PATCH: api/descontoquantidade/{id}/toggle
        [HttpPatch("{id}/toggle")]
        [RequireAdmin]
        public async Task<IActionResult> ToggleAtivo(int id)
        {
            var desconto = await _context.DescontosQuantidade.FindAsync(id);
            if (desconto == null)
            {
                return NotFound(new { message = "Desconto não encontrado" });
            }

            desconto.Ativo = !desconto.Ativo;
            desconto.DataAtualizacao = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return Ok(new { ativo = desconto.Ativo });
        }
    }
}
