using cafApi.Models.DTOs;
using cafApi.Services;
using Microsoft.AspNetCore.Mvc;

namespace cafApi.Controller
{
    [ApiController]
    [Route("api/[controller]")]
    public class VideoController : ControllerBase
    {
        private readonly IVideoService _videoService;

        public VideoController(IVideoService videoService)
        {
            _videoService = videoService;
        }

        [HttpGet("categoria/{categoriaId}")]
        public async Task<ActionResult<IEnumerable<VideoDto>>> GetByCategoria(int categoriaId)
        {
            var videos = await _videoService.GetByCategoriaAsync(categoriaId);
            return Ok(videos);
        }

        [HttpGet("lifestyle")]
        public async Task<ActionResult<IEnumerable<VideoDto>>> GetLifestyle([FromQuery] int count = 5)
        {
            if (count <= 0) count = 5;
            var videos = await _videoService.GetLifestyleAsync(count);
            return Ok(videos);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<VideoDto>> GetById(int id)
        {
            var video = await _videoService.GetByIdAsync(id);
            if (video == null) return NotFound();
            return Ok(video);
        }
    }
}
