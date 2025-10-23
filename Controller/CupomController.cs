using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using cafApi.Contexts;
using cafApi.Models;
using cafApi.Attributes;

namespace cafApi.Controller
{
    [ApiController]
    [Route("api/[controller]")]
    public class CupomController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public CupomController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: api/cupom
        [HttpGet]
        [RequireAdmin]
        public async Task<ActionResult<IEnumerable<object>>> GetCupons()
        {
            var cupons = await _context.Cupons
                .OrderByDescending(c => c.DataCriacao)
                .Select(c => new
                {
                    c.Id,
                    c.Codigo,
                    c.Descricao,
                    c.TipoDesconto,
                    c.ValorDesconto,
                    c.ValorMinimoCompra,
                    c.ValorMaximoDesconto,
                    c.QuantidadeMaximaUsos,
                    c.QuantidadeUsosAtual,
                    c.UsosPorUsuario,
                    c.DataInicio,
                    c.DataExpiracao,
                    c.Ativo,
                    c.DataCriacao,
                    UsosRestantes = c.QuantidadeMaximaUsos.HasValue 
                        ? c.QuantidadeMaximaUsos.Value - c.QuantidadeUsosAtual 
                        : (int?)null,
                    Expirado = c.DataExpiracao.HasValue && c.DataExpiracao.Value < DateTime.UtcNow,
                    Valido = c.Ativo && 
                             (!c.DataExpiracao.HasValue || c.DataExpiracao.Value >= DateTime.UtcNow) &&
                             (!c.QuantidadeMaximaUsos.HasValue || c.QuantidadeUsosAtual < c.QuantidadeMaximaUsos.Value)
                })
                .ToListAsync();

            return Ok(cupons);
        }

        // GET: api/cupom/{id}
        [HttpGet("{id}")]
        [RequireAdmin]
        public async Task<ActionResult<Cupom>> GetCupom(int id)
        {
            var cupom = await _context.Cupons
                .Include(c => c.CupomUsos)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (cupom == null)
            {
                return NotFound(new { message = "Cupom não encontrado" });
            }

            return cupom;
        }

        // POST: api/cupom/validar
        [HttpPost("validar")]
        public async Task<ActionResult<object>> ValidarCupom([FromBody] ValidarCupomRequest request)
        {
            var cupom = await _context.Cupons
                .FirstOrDefaultAsync(c => c.Codigo.ToUpper() == request.Codigo.ToUpper());

            if (cupom == null)
            {
                return Accepted(new { valido = false, message = "Cupom não encontrado" });
            }

            // Verificar se está ativo
            if (!cupom.Ativo)
            {
                return Ok(new { valido = false, message = "Cupom inativo" });
            }

            // Verificar data de início
            if (cupom.DataInicio > DateTime.UtcNow)
            {
                return Ok(new { valido = false, message = "Cupom ainda não está disponível" });
            }

            // Verificar data de expiração
            if (cupom.DataExpiracao.HasValue && cupom.DataExpiracao.Value < DateTime.UtcNow)
            {
                return Ok(new { valido = false, message = "Cupom expirado" });
            }

            // Verificar limite de usos
            if (cupom.QuantidadeMaximaUsos.HasValue && cupom.QuantidadeUsosAtual >= cupom.QuantidadeMaximaUsos.Value)
            {
                return Ok(new { valido = false, message = "Cupom esgotado" });
            }

            // Verificar valor mínimo de compra
            if (cupom.ValorMinimoCompra.HasValue && request.ValorCarrinho < cupom.ValorMinimoCompra.Value)
            {
                return Ok(new 
                { 
                    valido = false, 
                    message = $"Valor mínimo de compra de R$ {cupom.ValorMinimoCompra.Value:F2} não atingido" 
                });
            }

            // Verificar uso por usuário (se houver usuário logado)
            if (request.UsuarioId.HasValue)
            {
                var usosUsuario = await _context.CuponsUso
                    .CountAsync(cu => cu.CupomId == cupom.Id && cu.UsuarioId == request.UsuarioId.Value);

                if (usosUsuario >= cupom.UsosPorUsuario)
                {
                    return Ok(new { valido = false, message = "Você já utilizou este cupom o máximo de vezes permitido" });
                }
            }

            // Calcular desconto
            decimal valorDesconto = 0;
            if (cupom.TipoDesconto == "percentual")
            {
                valorDesconto = request.ValorCarrinho * (cupom.ValorDesconto / 100);
                
                // Aplicar limite máximo de desconto se houver
                if (cupom.ValorMaximoDesconto.HasValue && valorDesconto > cupom.ValorMaximoDesconto.Value)
                {
                    valorDesconto = cupom.ValorMaximoDesconto.Value;
                }
            }
            else // fixo
            {
                valorDesconto = cupom.ValorDesconto;
            }

            // Garantir que o desconto não seja maior que o valor do carrinho
            if (valorDesconto > request.ValorCarrinho)
            {
                valorDesconto = request.ValorCarrinho;
            }

            return Ok(new
            {
                valido = true,
                cupomId = cupom.Id,
                codigo = cupom.Codigo,
                descricao = cupom.Descricao,
                tipoDesconto = cupom.TipoDesconto,
                valorDesconto,
                valorFinal = request.ValorCarrinho - valorDesconto
            });
        }

        // POST: api/cupom
        [HttpPost]
        [RequireAdmin]
        public async Task<ActionResult<Cupom>> CreateCupom(Cupom cupom)
        {
            // Verificar se código já existe
            var codigoExiste = await _context.Cupons
                .AnyAsync(c => c.Codigo.ToUpper() == cupom.Codigo.ToUpper());

            if (codigoExiste)
            {
                return BadRequest(new { message = "Código de cupom já existe" });
            }

            cupom.Codigo = cupom.Codigo.ToUpper();
            cupom.DataCriacao = DateTime.UtcNow;
            
            _context.Cupons.Add(cupom);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetCupom), new { id = cupom.Id }, cupom);
        }

        // PUT: api/cupom/{id}
        [HttpPut("{id}")]
        [RequireAdmin]
        public async Task<IActionResult> UpdateCupom(int id, Cupom cupom)
        {
            if (id != cupom.Id)
            {
                return BadRequest(new { message = "ID não corresponde" });
            }

            var cupomExistente = await _context.Cupons.FindAsync(id);
            if (cupomExistente == null)
            {
                return NotFound(new { message = "Cupom não encontrado" });
            }

            // Verificar se código já existe (exceto o próprio cupom)
            var codigoExiste = await _context.Cupons
                .AnyAsync(c => c.Id != id && c.Codigo.ToUpper() == cupom.Codigo.ToUpper());

            if (codigoExiste)
            {
                return BadRequest(new { message = "Código de cupom já existe" });
            }

            cupomExistente.Codigo = cupom.Codigo.ToUpper();
            cupomExistente.Descricao = cupom.Descricao;
            cupomExistente.TipoDesconto = cupom.TipoDesconto;
            cupomExistente.ValorDesconto = cupom.ValorDesconto;
            cupomExistente.ValorMinimoCompra = cupom.ValorMinimoCompra;
            cupomExistente.ValorMaximoDesconto = cupom.ValorMaximoDesconto;
            cupomExistente.QuantidadeMaximaUsos = cupom.QuantidadeMaximaUsos;
            cupomExistente.UsosPorUsuario = cupom.UsosPorUsuario;
            cupomExistente.DataInicio = cupom.DataInicio;
            cupomExistente.DataExpiracao = cupom.DataExpiracao;
            cupomExistente.Ativo = cupom.Ativo;
            cupomExistente.DataAtualizacao = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return NoContent();
        }

        // DELETE: api/cupom/{id}
        [HttpDelete("{id}")]
        [RequireAdmin]
        public async Task<IActionResult> DeleteCupom(int id)
        {
            var cupom = await _context.Cupons.FindAsync(id);
            if (cupom == null)
            {
                return NotFound(new { message = "Cupom não encontrado" });
            }

            // Verificar se cupom já foi usado
            var foiUsado = await _context.CuponsUso.AnyAsync(cu => cu.CupomId == id);
            if (foiUsado)
            {
                return BadRequest(new { message = "Não é possível excluir um cupom que já foi utilizado" });
            }

            _context.Cupons.Remove(cupom);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        // PATCH: api/cupom/{id}/toggle
        [HttpPatch("{id}/toggle")]
        [RequireAdmin]
        public async Task<IActionResult> ToggleAtivo(int id)
        {
            var cupom = await _context.Cupons.FindAsync(id);
            if (cupom == null)
            {
                return NotFound(new { message = "Cupom não encontrado" });
            }

            cupom.Ativo = !cupom.Ativo;
            cupom.DataAtualizacao = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return Ok(new { ativo = cupom.Ativo });
        }

        // GET: api/cupom/{id}/estatisticas
        [HttpGet("{id}/estatisticas")]
        [RequireAdmin]
        public async Task<ActionResult<object>> GetEstatisticas(int id)
        {
            var cupom = await _context.Cupons.FindAsync(id);
            if (cupom == null)
            {
                return NotFound(new { message = "Cupom não encontrado" });
            }

            var usos = await _context.CuponsUso
                .Where(cu => cu.CupomId == id)
                .ToListAsync();

            var totalDescontoAplicado = usos.Sum(u => u.ValorDescontoAplicado);
            var usuariosUnicos = usos.Where(u => u.UsuarioId.HasValue)
                .Select(u => u.UsuarioId).Distinct().Count();

            return Ok(new
            {
                cupomId = id,
                codigo = cupom.Codigo,
                totalUsos = usos.Count,
                usuariosUnicos,
                totalDescontoAplicado,
                usosRestantes = cupom.QuantidadeMaximaUsos.HasValue 
                    ? cupom.QuantidadeMaximaUsos.Value - cupom.QuantidadeUsosAtual 
                    : (int?)null
            });
        }
    }

    public class ValidarCupomRequest
    {
        public string Codigo { get; set; } = string.Empty;
        public decimal ValorCarrinho { get; set; }
        public int? UsuarioId { get; set; }
    }
}
