using cafApi.Models;

namespace cafApi.Services
{
    public interface IProdutoUploadService
    {
        Task<Produtos> UploadProdutoComImagensAsync(IFormCollection form);
    }
}
