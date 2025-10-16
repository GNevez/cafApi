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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // 🔹 Oculos → Categoria
        modelBuilder.Entity<Produtos>()
            .HasOne(o => o.Categoria)
            .WithMany(c => c.Oculos)
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
    }
}
