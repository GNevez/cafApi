using cafApi.Models;
using Microsoft.AspNetCore.Http;

namespace cafApi.Services
{
    public class ProdutoUploadService : IProdutoUploadService
    {
        private readonly IProdutoService _produtoService;
        private readonly IImageService _imageService;

        public ProdutoUploadService(IProdutoService produtoService, IImageService imageService)
        {
            _produtoService = produtoService;
            _imageService = imageService;
        }

        public async Task<Produtos> UploadProdutoComImagensAsync(IFormCollection form)
        {
            // Verificar se é modo de adição de cor
            var modoAdicaoCorStr = form["modoAdicaoCor"].FirstOrDefault();
            var produtoExistenteIdStr = form["produtoExistenteId"].FirstOrDefault();
            
            if (bool.TryParse(modoAdicaoCorStr, out bool modoAdicaoCor) && modoAdicaoCor)
            {
                return await AdicionarCorAProdutoExistenteAsync(form, produtoExistenteIdStr);
            }
            
            // Verificar se é modo de edição
            var editingProductIdStr = form["editingProductId"].FirstOrDefault();
            var isEditModeStr = form["isEditMode"].FirstOrDefault();
            
            if (bool.TryParse(isEditModeStr, out bool isEditMode) && isEditMode)
            {
                return await EditarProdutoAsync(form, editingProductIdStr);
            }
            
            // Modo de criação de novo produto (código original)
            return await CriarNovoProdutoAsync(form);
        }

        private async Task<Produtos> EditarProdutoAsync(IFormCollection form, string editingProductIdStr)
        {
            // Validar ID do produto
            if (!int.TryParse(editingProductIdStr, out int editingProductId))
                throw new ArgumentException("ID do produto inválido.");

            // Validar dados básicos
            var nome = form["nome"].FirstOrDefault();
            var sku = form["sku"].FirstOrDefault();
            var codigoExterno = form["codigoExterno"].FirstOrDefault();

            if (string.IsNullOrEmpty(sku))
                throw new ArgumentException("SKU do produto é obrigatório.");

            // Buscar produto existente pelo SKU (já que GetByIdAsync retorna DTO)
            var produtoExistente = await _produtoService.GetBySKUAsync(sku);
            if (produtoExistente == null)
                throw new ArgumentException("Produto não encontrado.");
            var fabricante = form["fabricante"].FirstOrDefault();
            var categoriaIdStr = form["categoriaId"].FirstOrDefault();
            var precoStr = form["preco"].FirstOrDefault();
            var precoOriginalStr = form["precoOriginal"].FirstOrDefault();
            var isSaleStr = form["isSale"].FirstOrDefault();
            var isNewStr = form["isNew"].FirstOrDefault();

            if (string.IsNullOrEmpty(nome))
                throw new ArgumentException("Nome do produto é obrigatório.");

            // NÃO validar SKU e Código Externo no modo de edição (já existem)

            if (!int.TryParse(categoriaIdStr, out int categoriaId))
                throw new ArgumentException("CategoriaId inválido.");

            if (!decimal.TryParse(precoStr, out decimal preco))
                throw new ArgumentException("Preço inválido.");

            decimal? precoOriginal = null;
            if (!string.IsNullOrEmpty(precoOriginalStr) && decimal.TryParse(precoOriginalStr, out decimal precoOriginalValue))
            {
                precoOriginal = precoOriginalValue;
            }

            bool isSale = bool.TryParse(isSaleStr, out bool isSaleValue) && isSaleValue;
            bool isNew = bool.TryParse(isNewStr, out bool isNewValue) && isNewValue;

            // Atualizar dados do produto
            produtoExistente.Nome = nome;
            produtoExistente.SKU = sku; // Manter o mesmo SKU
            produtoExistente.CodigoExterno = string.IsNullOrEmpty(codigoExterno) ? null : codigoExterno;
            produtoExistente.Fabricante = string.IsNullOrEmpty(fabricante) ? null : fabricante;
            produtoExistente.Slug = nome.ToLower().Replace(" ", "-").Replace("_", "-");
            produtoExistente.Preco = preco;
            produtoExistente.PrecoOriginal = precoOriginal;
            produtoExistente.IsSale = isSale;
            produtoExistente.IsNew = isNew;
            produtoExistente.CategoriaId = categoriaId;

            // Processar imagens principais (apenas se fornecidas)
            var imagemPrincipal = form.Files["imagemPrincipal"];
            var imagemHover = form.Files["imagemHover"];

            if (imagemPrincipal != null)
            {
                var imagemPrincipalPath = await _imageService.SaveImageAsync(imagemPrincipal, "produtos");
                produtoExistente.ImagemPrincipal = imagemPrincipalPath;
            }

            if (imagemHover != null)
            {
                var imagemHoverPath = await _imageService.SaveImageAsync(imagemHover, "produtos");
                produtoExistente.ImagemHover = imagemHoverPath;
            }

            // Atualizar produto no banco
            var atualizado = await _produtoService.UpdateAsync(editingProductId, produtoExistente);
            if (!atualizado)
                throw new ArgumentException("Erro ao atualizar produto.");

            // Processar cores (novas cores + atualizações nas existentes)
            await ProcessarCoresEdicaoAsync(form, produtoExistente);

            return produtoExistente;
        }

        private async Task ProcessarCoresEdicaoAsync(IFormCollection form, Produtos produto)
        {
            // Processar cores existentes (atualizações de estoque e deleções)
            var coresExistentes = form["coresExistentes"].FirstOrDefault();
            if (!string.IsNullOrEmpty(coresExistentes))
            {
                // TODO: Implementar lógica para atualizar/deletar cores existentes
                // Por enquanto, vamos processar apenas as novas cores
            }

            // Processar novas cores (se houver)
            await ProcessarCoresAsync(form, produto);
        }

        private async Task<Produtos> CriarNovoProdutoAsync(IFormCollection form)
        {
            // Validar dados básicos
            var nome = form["nome"].FirstOrDefault();
            var sku = form["sku"].FirstOrDefault();
            var codigoExterno = form["codigoExterno"].FirstOrDefault();
            var fabricante = form["fabricante"].FirstOrDefault();
            var categoriaIdStr = form["categoriaId"].FirstOrDefault();
            var precoStr = form["preco"].FirstOrDefault();
            var precoOriginalStr = form["precoOriginal"].FirstOrDefault();
            var isSaleStr = form["isSale"].FirstOrDefault();
            var isNewStr = form["isNew"].FirstOrDefault();

            if (string.IsNullOrEmpty(nome))
                throw new ArgumentException("Nome do produto é obrigatório.");

            if (string.IsNullOrEmpty(sku))
                throw new ArgumentException("SKU do produto é obrigatório.");

            // Validar se SKU já existe
            var produtoComSKU = await _produtoService.GetBySKUAsync(sku);
            if (produtoComSKU != null)
                throw new ArgumentException($"SKU '{sku}' já está cadastrado no produto: '{produtoComSKU.Nome}'");

            // Validar se código externo já existe (apenas se fornecido)
            if (!string.IsNullOrEmpty(codigoExterno))
            {
                var produtoComCodigoExterno = await _produtoService.GetByCodigoExternoAsync(codigoExterno);
                if (produtoComCodigoExterno != null)
                    throw new ArgumentException($"Código externo '{codigoExterno}' já está cadastrado no produto: '{produtoComCodigoExterno.Nome}'");
            }

            if (!int.TryParse(categoriaIdStr, out int categoriaId))
                throw new ArgumentException("CategoriaId inválido.");

            if (!decimal.TryParse(precoStr, out decimal preco))
                throw new ArgumentException("Preço inválido.");

            decimal? precoOriginal = null;
            if (!string.IsNullOrEmpty(precoOriginalStr) && decimal.TryParse(precoOriginalStr, out decimal precoOriginalValue))
                precoOriginal = precoOriginalValue;

            bool isSale = bool.TryParse(isSaleStr, out bool isSaleValue) && isSaleValue;
            bool isNew = bool.TryParse(isNewStr, out bool isNewValue) && isNewValue;

            // Validar imagens principais
            var imagemPrincipal = form.Files.GetFile("imagemPrincipal");
            var imagemHover = form.Files.GetFile("imagemHover");

            if (imagemPrincipal == null || imagemHover == null)
                throw new ArgumentException("Imagem principal e hover são obrigatórias.");

            // Salvar imagens principais
            var imagemPrincipalPath = await _imageService.SaveImageAsync(imagemPrincipal, "produtos");
            var imagemHoverPath = await _imageService.SaveImageAsync(imagemHover, "produtos");

            // Criar produto
            var produto = new Produtos
            {
                Nome = nome,
                SKU = sku,
                CodigoExterno = string.IsNullOrEmpty(codigoExterno) ? null : codigoExterno,
                Fabricante = string.IsNullOrEmpty(fabricante) ? null : fabricante,
                Slug = nome.ToLower().Replace(" ", "-").Replace("_", "-"),
                Preco = preco,
                PrecoOriginal = precoOriginal,
                IsSale = isSale,
                IsNew = isNew,
                ImagemPrincipal = imagemPrincipalPath,
                ImagemHover = imagemHoverPath,
                CategoriaId = categoriaId
            };

            // Salvar produto no banco
            var produtoCriado = await _produtoService.CreateAsync(produto);

            // Processar cores
            await ProcessarCoresAsync(form, produtoCriado);
            
            // Fallback: tentar processar cores de forma mais simples
            await ProcessarCoresFallbackAsync(form, produtoCriado);

            // Verificar se as cores foram inseridas corretamente
            await VerificarCoresInseridasAsync(produtoCriado.Id);

            return produtoCriado;
        }

        private async Task<Produtos> AdicionarCorAProdutoExistenteAsync(IFormCollection form, string? produtoExistenteIdStr)
        {
            if (string.IsNullOrEmpty(produtoExistenteIdStr) || !int.TryParse(produtoExistenteIdStr, out int produtoExistenteId))
                throw new ArgumentException("ID do produto existente é obrigatório.");

            // Buscar produto existente
            var produtoExistenteDto = await _produtoService.GetByIdAsync(produtoExistenteId);
            if (produtoExistenteDto == null)
                throw new ArgumentException("Produto não encontrado.");

            // Converter DTO para entidade Produtos
            var produtoExistente = new Produtos
            {
                Id = produtoExistenteDto.Id,
                Nome = produtoExistenteDto.Nome,
                Slug = produtoExistenteDto.Slug,
                Preco = produtoExistenteDto.Preco,
                PrecoOriginal = produtoExistenteDto.PrecoOriginal,
                IsSale = produtoExistenteDto.IsSale,
                IsNew = produtoExistenteDto.IsNew,
                ImagemPrincipal = produtoExistenteDto.ImagemPrincipal,
                ImagemHover = produtoExistenteDto.ImagemHover,
                CategoriaId = produtoExistenteDto.CategoriaId
            };

            // Processar apenas as cores (sem criar novo produto)
            await ProcessarCoresAsync(form, produtoExistente);
            
            // Fallback: tentar processar cores de forma mais simples
            await ProcessarCoresFallbackAsync(form, produtoExistente);

            // Verificar se as cores foram inseridas corretamente
            await VerificarCoresInseridasAsync(produtoExistente.Id);

            return produtoExistente;
        }

        private async Task ProcessarCoresAsync(IFormCollection form, Produtos produto)
        {
            // Debug: listar todas as chaves disponíveis no form
            Console.WriteLine("=== DEBUG: Chaves disponíveis no FormData ===");
            foreach (var key in form.Keys)
            {
                Console.WriteLine($"Chave: {key}, Valor: {form[key]}");
            }
            Console.WriteLine("=== Fim do debug ===");

            var cores = new List<Dictionary<string, string>>();
            var corIndex = 0;
            
            // Tentar diferentes formatos de chaves
            while (form.ContainsKey($"cores[{corIndex}][nome]") || 
                   form.ContainsKey($"cores[{corIndex}][hex1]"))
            {
                var cor = new Dictionary<string, string>();
                
                // Usar as chaves corretas do frontend
                var nomeKey = $"cores[{corIndex}][nome]";
                var hex1Key = $"cores[{corIndex}][hex1]";
                var hex2Key = $"cores[{corIndex}][hex2]";
                var estoqueKey = $"cores[{corIndex}][estoque]";
                
                cor["nome"] = form[nomeKey].FirstOrDefault() ?? "";
                cor["hex1"] = form[hex1Key].FirstOrDefault() ?? "";
                cor["hex2"] = form[hex2Key].FirstOrDefault() ?? "";
                cor["estoque"] = form[estoqueKey].FirstOrDefault() ?? "0";
                
                cores.Add(cor);
                corIndex++;
            }

            Console.WriteLine($"Encontradas {cores.Count} cores para processar");

            // Processar cada cor
            for (int i = 0; i < cores.Count; i++)
            {
                var corData = cores[i];
                
                if (!int.TryParse(corData["estoque"], out int estoque))
                    estoque = 0;

                var cor = new ProdutosCor
                {
                    Nome = corData["nome"] ?? "Cor sem nome",
                    Hex1 = corData.ContainsKey("hex1") ? corData["hex1"] : null,
                    Hex2 = corData.ContainsKey("hex2") ? corData["hex2"] : null,
                    QuantidadeEstoque = estoque,
                    ProdutosId = produto.Id
                };

                // Salvar cor no banco primeiro para obter o ID
                _produtoService.AddCor(cor);
                await _produtoService.SaveChangesAsync();
                
                // Debug: verificar se o ID foi atribuído
                Console.WriteLine($"Cor salva com ID: {cor.Id}");

                // Processar imagens da cor
                await ProcessarImagensCorAsync(form, produto, cor, i);
            }
        }

        private async Task ProcessarImagensCorAsync(IFormCollection form, Produtos produto, ProdutosCor cor, int corIndex)
        {
            var imagemIndex = 0;
            var imagensProcessadas = 0;
            
            Console.WriteLine($"Processando imagens da cor {corIndex} (ID: {cor.Id})");
            Console.WriteLine($"Produto Slug: {produto.Slug}");
            
            // Debug: listar todas as chaves de arquivo disponíveis
            Console.WriteLine("Chaves de arquivo disponíveis:");
            foreach (var file in form.Files)
            {
                Console.WriteLine($"  - {file.Name}: {file.FileName}");
            }
            
            // Tentar diferentes formatos de chaves para imagens
            var imagemKey1 = $"cores[{corIndex}].imagens[{imagemIndex}]";
            var imagemKey2 = $"cores[{corIndex}][imagens][{imagemIndex}]";
            var imagemKey3 = $"cores[{corIndex}].imagens[{imagemIndex}]";
            var imagemKey4 = $"cores[{corIndex}][imagens][{imagemIndex}]";
            
            // Processar todas as imagens desta cor
            while (form.Files.GetFile(imagemKey1) != null || 
                   form.Files.GetFile(imagemKey2) != null ||
                   form.Files.GetFile(imagemKey3) != null ||
                   form.Files.GetFile(imagemKey4) != null)
            {
                var imagemFile = form.Files.GetFile(imagemKey1) ?? 
                               form.Files.GetFile(imagemKey2) ?? 
                               form.Files.GetFile(imagemKey3) ?? 
                               form.Files.GetFile(imagemKey4);
                               
                Console.WriteLine($"Encontrada imagem {imagemIndex} da cor {corIndex}: {imagemFile?.FileName} (tamanho: {imagemFile?.Length})");
                
                if (imagemFile != null && imagemFile.Length > 0)
                {
                    try
                    {
                        // Usar o novo método que organiza por slug e idCor
                        var imagemPath = await _imageService.SaveImageWithColorIdAsync(imagemFile, produto.Slug, cor.Id);
                        Console.WriteLine($"Imagem salva em: {imagemPath}");
                        
                        var imagemCor = new ProdutosCorImagem
                        {
                            Url = imagemPath,
                            Ordem = imagemIndex + 1,
                            ProdutosCorId = cor.Id
                        };

                        Console.WriteLine($"Criando ProdutosCorImagem com ProdutosCorId: {cor.Id}");
                        _produtoService.AddImagemCor(imagemCor);
                        imagensProcessadas++;
                    }
                    catch (Exception ex)
                    {
                        // Log do erro mas continua processando outras imagens
                        Console.WriteLine($"Erro ao processar imagem {imagemIndex} da cor {corIndex}: {ex.Message}");
                        Console.WriteLine($"Stack trace: {ex.StackTrace}");
                    }
                }
                imagemIndex++;
                
                // Atualizar as chaves para a próxima iteração
                imagemKey1 = $"cores[{corIndex}].imagens[{imagemIndex}]";
                imagemKey2 = $"cores[{corIndex}][imagens][{imagemIndex}]";
                imagemKey3 = $"cores[{corIndex}].imagens[{imagemIndex}]";
                imagemKey4 = $"cores[{corIndex}][imagens][{imagemIndex}]";
            }
            
            Console.WriteLine($"Processadas {imagensProcessadas} imagens para a cor {corIndex}");
            
            // Salvar as imagens desta cor no banco
            if (imagensProcessadas > 0)
            {
                await _produtoService.SaveChangesAsync();
                Console.WriteLine($"Imagens da cor {corIndex} salvas no banco");
            }
            else
            {
                Console.WriteLine($"Nenhuma imagem encontrada para a cor {corIndex}");
            }
        }

        private async Task ProcessarCoresFallbackAsync(IFormCollection form, Produtos produto)
        {
            Console.WriteLine("=== FALLBACK: Tentando processar cores de forma alternativa ===");
            
            // Tentar processar cores de forma mais direta
            var coresCount = 0;
            
            // Contar quantas cores existem
            foreach (var key in form.Keys)
            {
                if (key.StartsWith("cores[") && key.Contains("].cor1"))
                {
                    coresCount++;
                }
            }
            
            Console.WriteLine($"Fallback encontrou {coresCount} cores");
            
            for (int i = 0; i < coresCount; i++)
            {
                try
                {
                    var cor1 = form[$"cores[{i}].cor1"].FirstOrDefault() ?? "";
                    var cor2 = form[$"cores[{i}].cor2"].FirstOrDefault() ?? "";
                    var estoqueStr = form[$"cores[{i}].estoque"].FirstOrDefault() ?? "0";
                    
                    if (!int.TryParse(estoqueStr, out int estoque))
                        estoque = 0;

                    var cor = new ProdutosCor
                    {
                        Nome = $"{cor1} / {cor2}",
                        QuantidadeEstoque = estoque,
                        ProdutosId = produto.Id
                    };

                    _produtoService.AddCor(cor);
                    await _produtoService.SaveChangesAsync();
                    
                    Console.WriteLine($"Fallback: Cor {i} salva com ID: {cor.Id}");
                    
                    // Tentar processar imagens desta cor
                    await ProcessarImagensCorFallbackAsync(form, produto, cor, i);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Erro no fallback para cor {i}: {ex.Message}");
                }
            }
        }

        private async Task ProcessarImagensCorFallbackAsync(IFormCollection form, Produtos produto, ProdutosCor cor, int corIndex)
        {
            Console.WriteLine($"Fallback: Processando imagens da cor {corIndex} (ID: {cor.Id})");
            
            var imagensProcessadas = 0;
            
            // Tentar diferentes formatos de chaves para imagens
            for (int imgIndex = 0; imgIndex < 10; imgIndex++) // Máximo 10 imagens por cor
            {
                var imagemFile = form.Files.GetFile($"cores[{corIndex}].imagens[{imgIndex}]");
                
                if (imagemFile == null)
                {
                    // Tentar formato alternativo
                    imagemFile = form.Files.GetFile($"cores[{corIndex}][imagens][{imgIndex}]");
                }
                
                if (imagemFile != null && imagemFile.Length > 0)
                {
                    try
                    {
                        var imagemPath = await _imageService.SaveImageWithColorIdAsync(imagemFile, produto.Slug, cor.Id);
                        Console.WriteLine($"Fallback: Imagem {imgIndex} salva em: {imagemPath}");
                        
                        var imagemCor = new ProdutosCorImagem
                        {
                            Url = imagemPath,
                            Ordem = imgIndex + 1,
                            ProdutosCorId = cor.Id
                        };

                        _produtoService.AddImagemCor(imagemCor);
                        imagensProcessadas++;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Erro no fallback ao processar imagem {imgIndex}: {ex.Message}");
                    }
                }
                else
                {
                    // Se não encontrou imagem, para de procurar
                    break;
                }
            }
            
            if (imagensProcessadas > 0)
            {
                await _produtoService.SaveChangesAsync();
                Console.WriteLine($"Fallback: {imagensProcessadas} imagens salvas para cor {corIndex}");
            }
        }

        private async Task VerificarCoresInseridasAsync(int produtoId)
        {
            Console.WriteLine($"=== VERIFICAÇÃO: Verificando cores inseridas para produto {produtoId} ===");
            
            try
            {
                var cores = await _produtoService.GetCoresByProdutoIdAsync(produtoId);
                Console.WriteLine($"Encontradas {cores.Count} cores no banco para o produto {produtoId}");
                
                foreach (var cor in cores)
                {
                    Console.WriteLine($"  - Cor ID: {cor.Id}, Nome: {cor.Nome}, Estoque: {cor.QuantidadeEstoque}");
                    
                    var imagens = await _produtoService.GetImagensByCorIdAsync(cor.Id);
                    Console.WriteLine($"    Imagens: {imagens.Count}");
                    
                    foreach (var imagem in imagens)
                    {
                        Console.WriteLine($"      - Imagem ID: {imagem.Id}, URL: {imagem.Url}, Ordem: {imagem.Ordem}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao verificar cores: {ex.Message}");
            }
        }
    }
}
