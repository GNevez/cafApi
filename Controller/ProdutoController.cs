using cafApi.Models;
using cafApi.Models.DTOs;
using cafApi.Services;
using cafApi.Attributes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace cafApi.Controller
{
    [ApiController]
    [Route("api/[controller]")]
    public class ProdutoController : ControllerBase
    {
        private readonly IProdutoService _service;

        public ProdutoController(IProdutoService service)
        {
            _service = service;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<ProdutoResponseDto>>> Get()
        {
            var produtos = await _service.GetAllAsync();
            return Ok(produtos);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<ProdutoResponseDto>> Get(int id)
        {
            var produto = await _service.GetByIdAsync(id);
            if (produto == null) return NotFound();
            return Ok(produto);
        }

        [HttpGet("slug/{slug}")]
        public async Task<ActionResult<ProdutoResponseDto>> GetBySlug(string slug)
        {
            var produto = await _service.GetBySlugAsync(slug);
            if (produto == null) return NotFound();
            return Ok(produto);
        }

        [HttpGet("categoria/{categoriaId}")]
        public async Task<ActionResult<IEnumerable<ProdutoResponseDto>>> GetByCategoria(int categoriaId)
        {
            var produtos = await _service.GetByCategoriaAsync(categoriaId);
            return Ok(produtos);
        }

        [HttpPost]
        [RequireAdmin]
        public async Task<ActionResult<IEnumerable<Produtos>>> Post([FromBody] List<ProdutoCreateDto> produtosDto)
        {
            if (produtosDto == null || produtosDto.Count == 0)
                return BadRequest("Nenhum produto enviado.");

            var criados = await _service.CreateManyFromDtoAsync(produtosDto);

            return CreatedAtAction(nameof(Get), criados);
        }
        
        [HttpPut("{id}")]
        [RequireAdmin]
        public async Task<IActionResult> Put(int id, [FromBody] Produtos produto)
        {
            var atualizado = await _service.UpdateAsync(id, produto);
            if (!atualizado) return NotFound();
            return NoContent();
        }

        [HttpDelete("{id}")]
        [RequireAdmin]
        public async Task<IActionResult> Delete(int id)
        {
            var deletado = await _service.DeleteAsync(id);
            if (!deletado) return NotFound();
            return NoContent();
        }
    }
}
