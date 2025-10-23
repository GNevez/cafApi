using cafApi.Contexts;
using cafApi.Models;
using cafApi.Models.DTOs;
using Microsoft.EntityFrameworkCore;

namespace cafApi.Services
{
    public class VideoService : IVideoService
    {
        private readonly ApplicationDbContext _context;

        public VideoService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<VideoDto>> GetByCategoriaAsync(int categoriaId)
        {
            var videos = await _context.Videos
                .Where(v => v.Ativo == true && v.CategoriaId == categoriaId)
                .Include(v => v.Categoria)
                .OrderBy(v => v.Ordem)
                .ToListAsync();

            return videos.Select(v => new VideoDto
            {
                Id = v.Id,
                Titulo = v.Titulo,
                Descricao = v.Descricao,
                Url = v.Url,
                Thumbnail = v.Thumbnail,
                Duracao = v.Duracao,
                Ordem = v.Ordem,
                Ativo = v.Ativo,
                CategoriaId = v.CategoriaId,
                CategoriaNome = v.Categoria.Nome
            });
        }

        public async Task<VideoDto?> GetByIdAsync(int id)
        {
            var video = await _context.Videos
                .Where(v => v.Ativo == true)
                .Include(v => v.Categoria)
                .FirstOrDefaultAsync(v => v.Id == id);

            if (video == null) return null;

            return new VideoDto
            {
                Id = video.Id,
                Titulo = video.Titulo,
                Descricao = video.Descricao,
                Url = video.Url,
                Thumbnail = video.Thumbnail,
                Duracao = video.Duracao,
                Ordem = video.Ordem,
                Ativo = video.Ativo,
                CategoriaId = video.CategoriaId,
                CategoriaNome = video.Categoria.Nome
            };
        }

        public async Task<VideoDto> CreateAsync(VideoDto videoDto)
        {
            var video = new Video
            {
                Titulo = videoDto.Titulo,
                Descricao = videoDto.Descricao,
                Url = videoDto.Url,
                Thumbnail = videoDto.Thumbnail,
                Duracao = videoDto.Duracao,
                Ordem = videoDto.Ordem,
                Ativo = videoDto.Ativo,
                CategoriaId = videoDto.CategoriaId
            };

            _context.Videos.Add(video);
            await _context.SaveChangesAsync();

            videoDto.Id = video.Id;
            return videoDto;
        }

        public async Task<bool> UpdateAsync(int id, VideoDto videoDto)
        {
            var video = await _context.Videos.FindAsync(id);
            if (video == null) return false;

            video.Titulo = videoDto.Titulo;
            video.Descricao = videoDto.Descricao;
            video.Url = videoDto.Url;
            video.Thumbnail = videoDto.Thumbnail;
            video.Duracao = videoDto.Duracao;
            video.Ordem = videoDto.Ordem;
            video.Ativo = videoDto.Ativo;
            video.CategoriaId = videoDto.CategoriaId;

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var video = await _context.Videos.FindAsync(id);
            if (video == null) return false;

            video.Ativo = false; // Soft delete
            await _context.SaveChangesAsync();
            return true;
        }
    }
}
