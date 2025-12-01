namespace cafApi.Contexts;

using Microsoft.EntityFrameworkCore;
using cafApi.Models;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

    public DbSet<Produtos> Produtos { get; set; }
    public DbSet<ProdutosCor> ProdutosCores { get; set; }
    public DbSet<ProdutosCorImagem> ProdutosCorImagens { get; set; }
    public DbSet<Categoria> Categorias { get; set; }
    public DbSet<Cor> Cor { get; set; }
    public DbSet<Usuario> Usuarios { get; set; }
    public DbSet<Role> Roles { get; set; }
    public DbSet<Video> Videos { get; set; }
    public DbSet<Cliente> Clientes { get; set; }
    public DbSet<Carrinho> Carrinhos { get; set; }
    public DbSet<ItemCarrinho> ItensCarrinho { get; set; }
    public DbSet<Pedido> Pedidos { get; set; }
    public DbSet<Endereco> Enderecos { get; set; }
    public DbSet<DescontoQuantidade> DescontosQuantidade { get; set; }
    public DbSet<Cupom> Cupons { get; set; }
    public DbSet<CupomUso> CuponsUso { get; set; }
    public DbSet<Transacao> Transacoes { get; set; }
    public DbSet<Devolucao> Devolucoes { get; set; }
    public DbSet<DevolucaoItem> DevolucaoItens { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // 🔹 Produtos → Categoria
        modelBuilder.Entity<Produtos>()
            .HasOne(o => o.Categoria)
            .WithMany()
            .HasForeignKey(o => o.CategoriaId)
            .OnDelete(DeleteBehavior.Restrict);

        // 🔹 Produtos → Cores
        modelBuilder.Entity<ProdutosCor>()
            .HasOne(oc => oc.Produtos)
            .WithMany(o => o.CoresDisponiveis)
            .HasForeignKey(oc => oc.ProdutosId)
            .OnDelete(DeleteBehavior.Cascade);

        // 🔹 ProdutosCor → Imagens
        modelBuilder.Entity<ProdutosCorImagem>()
            .HasOne(i => i.ProdutosCor)
            .WithMany(oc => oc.Imagens)
            .HasForeignKey(i => i.ProdutosCorId)
            .OnDelete(DeleteBehavior.Cascade);

        // 🔹 Usuario → Role
        modelBuilder.Entity<Usuario>()
            .HasOne(u => u.Role)
            .WithMany(r => r.Usuarios)
            .HasForeignKey(u => u.RoleId)
            .OnDelete(DeleteBehavior.Restrict);

        // 🔹 Video → Categoria
        modelBuilder.Entity<Video>()
            .HasOne(v => v.Categoria)
            .WithMany()
            .HasForeignKey(v => v.CategoriaId)
            .OnDelete(DeleteBehavior.Restrict);

        // 🔹 Configurações de índices únicos
        modelBuilder.Entity<Usuario>()
            .HasIndex(u => u.Email)
            .IsUnique();

        modelBuilder.Entity<Role>()
            .HasIndex(r => r.Nome)
            .IsUnique();

        // Configuração de precisão para campos decimal
        modelBuilder.Entity<Produtos>()
            .Property(p => p.Preco)
            .HasPrecision(10, 2);

        modelBuilder.Entity<Produtos>()
            .Property(p => p.PrecoOriginal)
            .HasPrecision(10, 2);

        modelBuilder.Entity<Produtos>()
            .Property(p => p.TaxaJuros)
            .HasPrecision(5, 4); // Ex: 0.1234 = 12.34%

        // 🔹 Cliente → Carrinhos
        modelBuilder.Entity<Cliente>()
            .HasMany(c => c.Carrinhos)
            .WithOne(c => c.Cliente)
            .HasForeignKey(c => c.ClienteId)
            .OnDelete(DeleteBehavior.SetNull);

        // 🔹 Carrinho → ItensCarrinho
        modelBuilder.Entity<Carrinho>()
            .HasMany(c => c.Itens)
            .WithOne(i => i.Carrinho)
            .HasForeignKey(i => i.CarrinhoId)
            .OnDelete(DeleteBehavior.Cascade);

        // 🔹 ItemCarrinho → Produto
        modelBuilder.Entity<ItemCarrinho>()
            .HasOne(i => i.Produto)
            .WithMany()
            .HasForeignKey(i => i.ProdutoId)
            .OnDelete(DeleteBehavior.Restrict);

        // 🔹 ItemCarrinho → Cor
        modelBuilder.Entity<ItemCarrinho>()
            .HasOne(i => i.Cor)
            .WithMany()
            .HasForeignKey(i => i.CorId)
            .OnDelete(DeleteBehavior.Restrict);

        // 🔹 Índice único para token do carrinho
        modelBuilder.Entity<Carrinho>()
            .HasIndex(c => c.Token)
            .IsUnique();

        // 🔹 Cliente → Enderecos
        modelBuilder.Entity<Cliente>()
            .HasMany(c => c.Enderecos)
            .WithOne(e => e.Cliente)
            .HasForeignKey(e => e.ClienteId)
            .OnDelete(DeleteBehavior.Cascade);

        // 🔹 Cliente → Pedidos
        modelBuilder.Entity<Cliente>()
            .HasMany(c => c.Pedidos)
            .WithOne(p => p.Cliente)
            .HasForeignKey(p => p.ClienteId)
            .OnDelete(DeleteBehavior.Restrict);

        // 🔹 Carrinho → Pedidos
        modelBuilder.Entity<Carrinho>()
            .HasMany(c => c.Pedidos)
            .WithOne(p => p.Carrinho)
            .HasForeignKey(p => p.CarrinhoId)
            .OnDelete(DeleteBehavior.Restrict);

        // 🔹 Endereco → Pedidos
        modelBuilder.Entity<Endereco>()
            .HasMany(e => e.Pedidos)
            .WithOne(p => p.EnderecoEntrega)
            .HasForeignKey(p => p.EnderecoEntregaId)
            .OnDelete(DeleteBehavior.Restrict);

        // 🔹 Carrinho → Cupom
        modelBuilder.Entity<Carrinho>()
            .HasOne(c => c.Cupom)
            .WithMany()
            .HasForeignKey(c => c.CupomId)
            .OnDelete(DeleteBehavior.SetNull);

        // 🔹 Devolução → Pedido
        modelBuilder.Entity<Devolucao>()
            .HasOne(d => d.Pedido)
            .WithMany()
            .HasForeignKey(d => d.PedidoId)
            .OnDelete(DeleteBehavior.Restrict);

        // 🔹 DevolucaoItem → Devolucao
        modelBuilder.Entity<DevolucaoItem>()
            .HasOne(di => di.Devolucao)
            .WithMany(d => d.Itens)
            .HasForeignKey(di => di.DevolucaoId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
