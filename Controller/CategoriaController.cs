using cafApi.Models;
using cafApi.Models.DTOs;
using cafApi.Services;
using cafApi.Services.Extensions;
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
        private readonly IImageService _imageService;
        private readonly IVideoService _videoService;

        public CategoriaController(ICategoriaService service, IImageService imageService, IVideoService videoService)
        {
            _service = service;
            _imageService = imageService;
            _videoService = videoService;
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
        public async Task<ActionResult<Categoria>> Post([FromForm] IFormCollection form)
        {
            try
            {
                Console.WriteLine("📋 Recebendo dados da categoria...");
                
                if (form == null)
                {
                    Console.WriteLine("❌ Form é nulo");
                    return BadRequest("Dados da categoria não podem ser nulos.");
                }

                var nome = form["nome"].ToString();
                var titulo = form["titulo"].ToString();
                var mensagem = form["mensagem"].ToString();
                var bannerFile = form.Files["banner"];

                Console.WriteLine($"📝 Dados recebidos - Nome: {nome}, Título: {titulo}, Mensagem: {mensagem}, HasBanner: {bannerFile != null}");

                if (string.IsNullOrEmpty(nome))
                {
                    Console.WriteLine("❌ Nome é obrigatório");
                    return BadRequest("Nome da categoria é obrigatório.");
                }

                string? bannerPath = null;
                if (bannerFile != null && bannerFile.Length > 0)
                {
                    try
                    {
                        Console.WriteLine($"📸 Salvando banner: {bannerFile.FileName}, Tamanho: {bannerFile.Length}");
                        bannerPath = await _imageService.SaveImageAsync(bannerFile, "categorias");
                        Console.WriteLine($"✅ Banner salvo em: {bannerPath}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"❌ Erro ao salvar banner: {ex.Message}");
                        return BadRequest($"Erro ao salvar banner: {ex.Message}");
                    }
                }

                var categoria = new Categoria
                {
                    Nome = nome,
                    Slug = nome.GenerateSlug(),
                    Banner = bannerPath,
                    Titulo = string.IsNullOrEmpty(titulo) ? null : titulo,
                    Mensagem = string.IsNullOrEmpty(mensagem) ? null : mensagem
                };

                Console.WriteLine($"💾 Salvando categoria: {categoria.Nome}, Slug: {categoria.Slug}");
                var criada = await _service.CreateAsync(categoria);
                Console.WriteLine($"✅ Categoria criada com ID: {criada.Id}");

                // Processar vídeos
                await ProcessarVideosAsync(form, criada.Id);
                
                return CreatedAtAction(nameof(Get), new { id = criada.Id }, criada);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Erro interno: {ex.Message}");
                Console.WriteLine($"❌ Stack trace: {ex.StackTrace}");
                return StatusCode(500, $"Erro interno: {ex.Message}");
            }
        }

        [HttpPut("{id}")]
        [RequireAdmin]
        public async Task<IActionResult> Put(int id, [FromForm] IFormCollection form)
        {
            try
            {
                Console.WriteLine($"📋 Atualizando categoria ID: {id}");
                
                if (form == null)
                {
                    Console.WriteLine("❌ Form é nulo");
                    return BadRequest("Dados da categoria não podem ser nulos.");
                }

                var nome = form["nome"].ToString();
                var titulo = form["titulo"].ToString();
                var mensagem = form["mensagem"].ToString();
                var bannerFile = form.Files["banner"];

                Console.WriteLine($"📝 Dados recebidos - Nome: {nome}, Título: {titulo}, Mensagem: {mensagem}, HasBanner: {bannerFile != null}");

                if (string.IsNullOrEmpty(nome))
                {
                    Console.WriteLine("❌ Nome é obrigatório");
                    return BadRequest("Nome da categoria é obrigatório.");
                }

                // Buscar categoria existente
                var existingCategoria = await _service.GetByIdAsync(id);
                if (existingCategoria == null)
                {
                    Console.WriteLine($"❌ Categoria com ID {id} não encontrada");
                    return NotFound();
                }

                string? bannerPath = existingCategoria.Banner; // Manter banner atual por padrão
                
                // Se um novo banner foi enviado, salvá-lo
                if (bannerFile != null && bannerFile.Length > 0)
                {
                    try
                    {
                        Console.WriteLine($"📸 Salvando novo banner: {bannerFile.FileName}, Tamanho: {bannerFile.Length}");
                        bannerPath = await _imageService.SaveImageAsync(bannerFile, "categorias");
                        Console.WriteLine($"✅ Novo banner salvo em: {bannerPath}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"❌ Erro ao salvar banner: {ex.Message}");
                        return BadRequest($"Erro ao salvar banner: {ex.Message}");
                    }
                }

                var categoriaDto = new CategoriaUpdateDto
                {
                    Nome = nome,
                    Titulo = string.IsNullOrEmpty(titulo) ? null : titulo,
                    Mensagem = string.IsNullOrEmpty(mensagem) ? null : mensagem,
                    Banner = bannerPath
                };

                Console.WriteLine($"💾 Atualizando categoria: {categoriaDto.Nome}");
                var atualizada = await _service.UpdateAsync(id, categoriaDto);
                if (!atualizada)
                {
                    Console.WriteLine($"❌ Falha ao atualizar categoria ID: {id}");
                    return NotFound();
                }
                
                Console.WriteLine($"✅ Categoria ID: {id} atualizada com sucesso");
                return NoContent();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Erro interno: {ex.Message}");
                Console.WriteLine($"❌ Stack trace: {ex.StackTrace}");
                return StatusCode(500, $"Erro interno: {ex.Message}");
            }
        }

        [HttpDelete("{id}")]
        [RequireAdmin]
        public async Task<IActionResult> Delete(int id)
        {
            // Verificar se há produtos ativos com esta categoria
            var hasActiveProducts = await _service.HasActiveProductsAsync(id);
            if (hasActiveProducts)
            {
                return BadRequest(new { message = "Não é possível excluir esta categoria pois existem produtos ativos associados a ela." });
            }

            var deletada = await _service.DeleteAsync(id);
            if (!deletada) return NotFound();
            return NoContent();
        }

        [HttpGet("inactive")]
        [RequireAdmin]
        public async Task<IActionResult> GetInactive()
        {
            var categorias = await _service.GetInactiveAsync();
            return Ok(categorias);
        }

        [HttpPost("reactivate/{id}")]
        [RequireAdmin]
        public async Task<IActionResult> Reactivate(int id)
        {
            var reativada = await _service.ReactivateAsync(id);
            if (!reativada) return NotFound();
            return NoContent();
        }

        private async Task ProcessarVideosAsync(IFormCollection form, int categoriaId)
        {
            try
            {
                Console.WriteLine($"🎬 Processando vídeos para categoria ID: {categoriaId}");
                
                // Contar quantos vídeos foram enviados
                var videoCount = 0;
                for (int i = 0; i < 3; i++) // Máximo 3 vídeos
                {
                    var titulo = form[$"videos[{i}][titulo]"].ToString();
                    if (!string.IsNullOrEmpty(titulo))
                    {
                        videoCount++;
                    }
                }

                Console.WriteLine($"📊 Encontrados {videoCount} vídeos para processar");

                for (int i = 0; i < videoCount; i++)
                {
                    var titulo = form[$"videos[{i}][titulo]"].ToString();
                    var descricao = form[$"videos[{i}][descricao]"].ToString();
                    var duracaoStr = form[$"videos[{i}][duracao]"].ToString();
                    var ordemStr = form[$"videos[{i}][ordem]"].ToString();
                    var videoFile = form.Files[$"videos[{i}][file]"];
                    var thumbnailFile = form.Files[$"videos[{i}][thumbnail]"];

                    if (string.IsNullOrEmpty(titulo) || videoFile == null)
                    {
                        Console.WriteLine($"⚠️ Vídeo {i} ignorado - título ou arquivo ausente");
                        continue;
                    }

                    Console.WriteLine($"🎥 Processando vídeo {i}: {titulo}");

                    // Salvar arquivo de vídeo
                    string videoPath;
                    try
                    {
                        videoPath = await _imageService.SaveVideoAsync(videoFile, "categorias", categoriaId.ToString());
                        Console.WriteLine($"✅ Vídeo salvo em: {videoPath}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"❌ Erro ao salvar vídeo {i}: {ex.Message}");
                        continue;
                    }

                    // Salvar thumbnail se fornecido
                    string? thumbnailPath = null;
                    if (thumbnailFile != null && thumbnailFile.Length > 0)
                    {
                        try
                        {
                            thumbnailPath = await _imageService.SaveImageAsync(thumbnailFile, $"categorias/{categoriaId}/thumbnails");
                            Console.WriteLine($"✅ Thumbnail salvo em: {thumbnailPath}");
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"⚠️ Erro ao salvar thumbnail {i}: {ex.Message}");
                            // Continuar sem falhar o vídeo
                        }
                    }

                    // Criar DTO do vídeo para salvar no banco
                    var videoDto = new VideoDto
                    {
                        Titulo = titulo,
                        Descricao = string.IsNullOrEmpty(descricao) ? null : descricao,
                        Url = videoPath, // Caminho do arquivo salvo
                        Thumbnail = thumbnailPath ?? null,
                        Duracao = int.TryParse(duracaoStr, out int duracao) ? duracao : 0,
                        Ordem = int.TryParse(ordemStr, out int ordem) ? ordem : i + 1,
                        Ativo = true,
                        CategoriaId = categoriaId
                    };

                    // Salvar vídeo no banco de dados
                    try
                    {
                        await _videoService.CreateAsync(videoDto);
                        Console.WriteLine($"✅ Vídeo {i} salvo no banco de dados com ID: {videoDto.Id}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"❌ Erro ao salvar vídeo {i} no banco: {ex.Message}");
                        // Continuar processando outros vídeos
                    }
                    
                    Console.WriteLine($"✅ Vídeo {i} processado com sucesso");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Erro ao processar vídeos: {ex.Message}");
                // Não falhar a criação da categoria por causa dos vídeos
            }
        }
    }
}
