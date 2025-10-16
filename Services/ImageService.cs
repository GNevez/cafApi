using Microsoft.AspNetCore.Http;
using System.IO;

namespace cafApi.Services
{
    public class ImageService : IImageService
    {
        private readonly IWebHostEnvironment _environment;
        private readonly IConfiguration _configuration;

        public ImageService(IWebHostEnvironment environment, IConfiguration configuration)
        {
            _environment = environment;
            _configuration = configuration;
        }

        public async Task<string> SaveImageAsync(IFormFile file, string folder)
        {
            if (file == null || file.Length == 0)
                throw new ArgumentException("Arquivo inválido");

            if (string.IsNullOrEmpty(file.FileName))
                throw new ArgumentException("Nome do arquivo não pode ser vazio");

            // Validar tipo de arquivo
            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
            var fileExtension = Path.GetExtension(file.FileName).ToLowerInvariant();
            
            if (!allowedExtensions.Contains(fileExtension))
                throw new ArgumentException("Tipo de arquivo não permitido");

            // Validar tamanho (máximo 5MB)
            if (file.Length > 5 * 1024 * 1024)
                throw new ArgumentException("Arquivo muito grande. Máximo 5MB");

            // Determinar o caminho base para uploads
            string basePath;
            if (!string.IsNullOrEmpty(_environment.WebRootPath))
            {
                basePath = _environment.WebRootPath;
            }
            else
            {
                // Fallback: usar o diretório atual + wwwroot
                basePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
            }

            // Criar diretório se não existir
            var uploadsPath = Path.Combine(basePath, "uploads", folder);
            Directory.CreateDirectory(uploadsPath);

            // Gerar nome único para o arquivo
            var fileName = $"{Guid.NewGuid()}{fileExtension}";
            var filePath = Path.Combine(uploadsPath, fileName);

            // Salvar arquivo
            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            // Retornar URL relativa
            return $"/uploads/{folder}/{fileName}";
        }

        public async Task<string> SaveImageWithProductIdAsync(IFormFile file, int produtoId, int imagemId)
        {
            if (file == null || file.Length == 0)
                throw new ArgumentException("Arquivo inválido");

            if (string.IsNullOrEmpty(file.FileName))
                throw new ArgumentException("Nome do arquivo não pode ser vazio");

            // Validar tipo de arquivo
            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
            var fileExtension = Path.GetExtension(file.FileName).ToLowerInvariant();
            
            if (!allowedExtensions.Contains(fileExtension))
                throw new ArgumentException("Tipo de arquivo não permitido");

            // Validar tamanho (máximo 5MB)
            if (file.Length > 5 * 1024 * 1024)
                throw new ArgumentException("Arquivo muito grande. Máximo 5MB");

            // Determinar o caminho base para uploads
            string basePath;
            if (!string.IsNullOrEmpty(_environment.WebRootPath))
            {
                basePath = _environment.WebRootPath;
            }
            else
            {
                // Fallback: usar o diretório atual + wwwroot
                basePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
            }

            // Criar estrutura de pastas: uploads/produtos/cores/{produtoId}/{imagemId}
            var uploadsPath = Path.Combine(basePath, "uploads", "produtos", "cores", produtoId.ToString(), imagemId.ToString());
            Directory.CreateDirectory(uploadsPath);

            // Gerar nome único para o arquivo
            var fileName = $"{Guid.NewGuid()}{fileExtension}";
            var filePath = Path.Combine(uploadsPath, fileName);

            // Salvar arquivo
            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            // Retornar URL relativa
            return $"/uploads/produtos/cores/{produtoId}/{imagemId}/{fileName}";
        }

        public async Task<string> SaveImageWithColorIdAsync(IFormFile file, string produtoSlug, int corId)
        {
            if (file == null || file.Length == 0)
                throw new ArgumentException("Arquivo inválido");

            if (string.IsNullOrEmpty(file.FileName))
                throw new ArgumentException("Nome do arquivo não pode ser vazio");

            // Validar tipo de arquivo
            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
            var fileExtension = Path.GetExtension(file.FileName).ToLowerInvariant();
            
            if (!allowedExtensions.Contains(fileExtension))
                throw new ArgumentException("Tipo de arquivo não permitido");

            // Validar tamanho (máximo 5MB)
            if (file.Length > 5 * 1024 * 1024)
                throw new ArgumentException("Arquivo muito grande. Máximo 5MB");

            // Determinar o caminho base para uploads
            string basePath;
            if (!string.IsNullOrEmpty(_environment.WebRootPath))
            {
                basePath = _environment.WebRootPath;
            }
            else
            {
                // Fallback: usar o diretório atual + wwwroot
                basePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
            }

            // Criar estrutura de pastas: uploads/produtos/cores/{slug}/{idCor}
            var uploadsPath = Path.Combine(basePath, "uploads", "produtos", "cores", produtoSlug, corId.ToString());
            Directory.CreateDirectory(uploadsPath);

            // Gerar nome único para o arquivo
            var fileName = $"{Guid.NewGuid()}{fileExtension}";
            var filePath = Path.Combine(uploadsPath, fileName);

            // Salvar arquivo
            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            // Retornar URL relativa
            return $"/uploads/produtos/cores/{produtoSlug}/{corId}/{fileName}";
        }

        public async Task<List<string>> SaveImagesAsync(List<IFormFile> files, string folder)
        {
            var savedPaths = new List<string>();

            foreach (var file in files)
            {
                if (file != null && file.Length > 0)
                {
                    var path = await SaveImageAsync(file, folder);
                    savedPaths.Add(path);
                }
            }

            return savedPaths;
        }

        public Task<bool> DeleteImageAsync(string imagePath)
        {
            try
            {
                // Determinar o caminho base para uploads
                string basePath;
                if (!string.IsNullOrEmpty(_environment.WebRootPath))
                {
                    basePath = _environment.WebRootPath;
                }
                else
                {
                    // Fallback: usar o diretório atual + wwwroot
                    basePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
                }

                var fullPath = Path.Combine(basePath, imagePath.TrimStart('/'));
                
                if (File.Exists(fullPath))
                {
                    File.Delete(fullPath);
                    return Task.FromResult(true);
                }
                
                return Task.FromResult(false);
            }
            catch
            {
                return Task.FromResult(false);
            }
        }

        public string GetImageUrl(string imagePath)
        {
            if (string.IsNullOrEmpty(imagePath))
                return string.Empty;

            var baseUrl = _configuration["BaseUrl"] ?? "https://localhost:5006";
            return $"{baseUrl}{imagePath}";
        }
    }
}
