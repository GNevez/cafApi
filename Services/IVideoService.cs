using cafApi.Models.DTOs;

namespace cafApi.Services
{
    public interface IVideoService
    {
        Task<IEnumerable<VideoDto>> GetByCategoriaAsync(int categoriaId);
        Task<IEnumerable<VideoDto>> GetLifestyleAsync(int count);
        Task<VideoDto?> GetByIdAsync(int id);
        Task<VideoDto> CreateAsync(VideoDto videoDto);
        Task<bool> UpdateAsync(int id, VideoDto videoDto);
        Task<bool> DeleteAsync(int id);
    }
}
