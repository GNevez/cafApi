using cafApi.Models;
using cafApi.Models.DTOs;
using cafApi.Services;
using cafApi.Attributes;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace cafApi.Controller
{
    [ApiController]
    [Route("api/[controller]")]
    public class CategoriaController : ControllerBase
    {
        private readonly ICategoriaService _service;

        public CategoriaController(ICategoriaService service)
        {
            _service = service;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<Categoria>>> Get()
        {
            var categorias = await _service.GetAllAsync();
            return Ok(categorias);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<Categoria>> Get(int id)
        {
            var categoria = await _service.GetByIdAsync(id);
            if (categoria == null) return NotFound();
            return Ok(categoria);
        }

        [HttpPost]
        [RequireAdmin]
        public async Task<ActionResult<Categoria>> Post([FromBody] CategoriaCreateDto categoriaDto)
        {
            if (categoriaDto == null)
                return BadRequest("Categoria não pode ser nula.");

            var categoria = new Categoria
            {
                Nome = categoriaDto.Nome,
                Slug = categoriaDto.Slug
            };

            var criada = await _service.CreateAsync(categoria);
            return CreatedAtAction(nameof(Get), new { id = criada.Id }, criada);
        }

        [HttpPut("{id}")]
        [RequireAdmin]
        public async Task<IActionResult> Put(int id, [FromBody] Categoria categoria)
        {
            var atualizada = await _service.UpdateAsync(id, categoria);
            if (!atualizada) return NotFound();
            return NoContent();
        }

        [HttpDelete("{id}")]
        [RequireAdmin]
        public async Task<IActionResult> Delete(int id)
        {
            var deletada = await _service.DeleteAsync(id);
            if (!deletada) return NotFound();
            return NoContent();
        }
    }
}
