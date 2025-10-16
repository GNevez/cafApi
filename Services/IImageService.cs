using Microsoft.AspNetCore.Http;

namespace cafApi.Services
{
    public interface IImageService
    {
        Task<string> SaveImageAsync(IFormFile file, string folder);
        Task<string> SaveImageWithProductIdAsync(IFormFile file, int produtoId, int imagemId);
        Task<string> SaveImageWithColorIdAsync(IFormFile file, string produtoSlug, int corId);
        Task<List<string>> SaveImagesAsync(List<IFormFile> files, string folder);
        Task<bool> DeleteImageAsync(string imagePath);
        string GetImageUrl(string imagePath);
    }
}
