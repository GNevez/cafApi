using cafApi.Models;
using cafApi.Models.DTOs;
using cafApi.Services;
using Microsoft.AspNetCore.Mvc;

namespace cafApi.Controller
{
    [ApiController]
    [Route("api/[controller]")]
    public class TransacaoController : ControllerBase
    {
        private readonly ITransacaoService _transacaoService;

        public TransacaoController(ITransacaoService transacaoService)
        {
            _transacaoService = transacaoService;
        }

        [HttpGet]
        public async Task<ActionResult<object>> GetAll(
            [FromQuery] TipoTransacao? tipo = null,
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 10)
        {
            try
            {
                var (transacoes, totalCount) = await _transacaoService.GetAllAsync(tipo, pageNumber, pageSize);
                
                var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
                
                return Ok(new
                {
                    items = transacoes,
                    pageNumber,
                    pageSize,
                    totalPages,
                    totalCount
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Erro ao buscar transações", error = ex.Message });
            }
        }

        [HttpGet("{id:int}")]
        public async Task<ActionResult<TransacaoDto>> GetById(int id)
        {
            try
            {
                var transacao = await _transacaoService.GetByIdAsync(id);
                if (transacao == null)
                    return NotFound(new { message = "Transação não encontrada" });

                return Ok(transacao);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Erro ao buscar transação", error = ex.Message });
            }
        }

        [HttpPost]
        public async Task<ActionResult<TransacaoDto>> Create([FromBody] CriarTransacaoDto dto)
        {
            try
            {
                var transacao = await _transacaoService.CreateAsync(dto);
                return CreatedAtAction(nameof(GetById), new { id = transacao.Id }, transacao);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Erro ao criar transação", error = ex.Message });
            }
        }

        [HttpPut("{id:int}")]
        public async Task<ActionResult<TransacaoDto>> Update(int id, [FromBody] AtualizarTransacaoDto dto)
        {
            try
            {
                var transacao = await _transacaoService.UpdateAsync(id, dto);
                if (transacao == null)
                    return NotFound(new { message = "Transação não encontrada" });

                return Ok(transacao);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Erro ao atualizar transação", error = ex.Message });
            }
        }

        [HttpDelete("{id:int}")]
        public async Task<ActionResult> Delete(int id)
        {
            try
            {
                var result = await _transacaoService.DeleteAsync(id);
                if (!result)
                    return NotFound(new { message = "Transação não encontrada" });

                return NoContent();
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Erro ao deletar transação", error = ex.Message });
            }
        }

        [HttpGet("saldo")]
        public async Task<ActionResult<decimal>> GetSaldo()
        {
            try
            {
                var saldo = await _transacaoService.GetSaldoAsync();
                return Ok(new { saldo });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Erro ao calcular saldo", error = ex.Message });
            }
        }

        [HttpGet("total/{tipo:int}")]
        public async Task<ActionResult<decimal>> GetTotalByTipo(TipoTransacao tipo)
        {
            try
            {
                var total = await _transacaoService.GetTotalByTipoAsync(tipo);
                return Ok(new { tipo, total });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Erro ao calcular total", error = ex.Message });
            }
        }
    }
}
