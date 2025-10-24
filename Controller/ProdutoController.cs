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
        private readonly IImageService _imageService;
        private readonly IProdutoUploadService _uploadService;

        public ProdutoController(IProdutoService service, IImageService imageService, IProdutoUploadService uploadService)
        {
            _service = service;
            _imageService = imageService;
            _uploadService = uploadService;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<ProdutoResponseDto>>> Get()
        {
            var produtos = await _service.GetAllAsync();
            return Ok(produtos);
        }

        [HttpGet("{id:int}")]
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

        [HttpGet("detalhado/{slug}")]
        public async Task<ActionResult<ProdutoDetalhadoDto>> GetDetalhadoBySlug(string slug)
        {
            var produto = await _service.GetBySlugDetalhadoAsync(slug);
            if (produto == null) return NotFound();
            return Ok(produto);
        }

        [HttpGet("categoria/{categoriaId:int}")]
        public async Task<ActionResult<IEnumerable<ProdutoResponseDto>>> GetByCategoria(int categoriaId)
        {
            var produtos = await _service.GetByCategoriaAsync(categoriaId);
            return Ok(produtos);
        }

        [HttpGet("search")]
        public async Task<ActionResult<IEnumerable<ProdutoSearchDto>>> Search([FromQuery] string q, [FromQuery] int limit = 8)
        {
            if (string.IsNullOrWhiteSpace(q) || q.Trim().Length < 2)
            {
                return Ok(Enumerable.Empty<ProdutoSearchDto>());
            }

            limit = Math.Clamp(limit, 1, 20);
            var resultados = await _service.SearchAsync(q, limit);
            return Ok(resultados);
        }

        [HttpGet("validate/sku/{sku}")]
        public async Task<ActionResult<object>> ValidateSKU(string sku)
        {
            // Primeiro verifica se existe um produto ativo com esse SKU
            var produtoAtivo = await _service.GetBySKUAsync(sku);
            if (produtoAtivo != null)
            {
                return Ok(new { 
                    exists = true, 
                    isActive = true,
                    message = $"SKU '{sku}' já está cadastrado no produto: '{produtoAtivo.Nome}'",
                    produtoNome = produtoAtivo.Nome
                });
            }

            // Se não há produto ativo, verifica se existe um produto desativado
            var produtoDesativado = await _service.GetBySKUIncludingInactiveAsync(sku);
            if (produtoDesativado != null && !produtoDesativado.Active)
            {
                return Ok(new { 
                    exists = true, 
                    isActive = false,
                    message = $"Esse SKU já está cadastrado em um produto desativado ('{produtoDesativado.Nome}'). Ative-o novamente para editar esse produto.",
                    produtoNome = produtoDesativado.Nome
                });
            }

            return Ok(new { exists = false, message = "SKU disponível" });
        }

        [HttpGet("validate/codigo-externo/{codigoExterno}")]
        public async Task<ActionResult<object>> ValidateCodigoExterno(string codigoExterno)
        {
            // Primeiro verifica se existe um produto ativo com esse código externo
            var produtoAtivo = await _service.GetByCodigoExternoAsync(codigoExterno);
            if (produtoAtivo != null)
            {
                return Ok(new { 
                    exists = true, 
                    isActive = true,
                    message = $"Código externo '{codigoExterno}' já está cadastrado no produto: '{produtoAtivo.Nome}'",
                    produtoNome = produtoAtivo.Nome
                });
            }

            // Se não há produto ativo, verifica se existe um produto desativado
            var produtoDesativado = await _service.GetByCodigoExternoIncludingInactiveAsync(codigoExterno);
            if (produtoDesativado != null && !produtoDesativado.Active)
            {
                return Ok(new { 
                    exists = true, 
                    isActive = false,
                    message = $"Esse código externo já está cadastrado em um produto desativado ('{produtoDesativado.Nome}'). Ative-o novamente para editar esse produto.",
                    produtoNome = produtoDesativado.Nome
                });
            }

            return Ok(new { exists = false, message = "Código externo disponível" });
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

        [HttpPost("upload")]
        [RequireAdmin]
        public async Task<ActionResult<Produtos>> PostWithImages()
        {
            try
            {
                // Obter dados do FormData manualmente
                var form = await Request.ReadFormAsync();
                
                if (form == null || form.Count == 0)
                    return BadRequest("Dados do produto não fornecidos.");

                // Usar o service para processar o upload
                var produtoCriado = await _uploadService.UploadProdutoComImagensAsync(form);

                return CreatedAtAction(nameof(Get), new { id = produtoCriado.Id }, produtoCriado);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Erro interno do servidor: {ex.Message}");
            }
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
            var desativado = await _service.DeactivateAsync(id); // 🔹 Soft delete - desativar produto
            if (!desativado) return NotFound();
            return NoContent();
        }

        [HttpGet("inactive")]
        public async Task<ActionResult<IEnumerable<ProdutoResponseDto>>> GetInactive()
        {
            var produtos = await _service.GetInactiveAsync();
            return Ok(produtos);
        }

        [HttpPost("reactivate/{id}")]
        [RequireAdmin]
        public async Task<IActionResult> Reactivate(int id)
        {
            var reativado = await _service.ReactivateAsync(id);
            if (!reativado) return NotFound();
            return Ok(new { message = $"Produto {id} reativado com sucesso" });
        }
    }
}
