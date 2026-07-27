using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using cafApi.Models.Generated;

namespace cafApi.Contexts;

public partial class ChaseAFlareContext : DbContext
{
    public ChaseAFlareContext(DbContextOptions<ChaseAFlareContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Carrinhos> Carrinhos { get; set; }

    public virtual DbSet<Categorias> Categorias { get; set; }

    public virtual DbSet<Clientes> Clientes { get; set; }

    public virtual DbSet<Cor> Cor { get; set; }

    public virtual DbSet<Cupons> Cupons { get; set; }

    public virtual DbSet<CuponsUso> CuponsUso { get; set; }

    public virtual DbSet<DescontosQuantidade> DescontosQuantidade { get; set; }

    public virtual DbSet<Devolucaoitens> Devolucaoitens { get; set; }

    public virtual DbSet<Devolucoes> Devolucoes { get; set; }

    public virtual DbSet<Efmigrationshistory> Efmigrationshistory { get; set; }

    public virtual DbSet<Enderecos> Enderecos { get; set; }

    public virtual DbSet<Filaimpressao> Filaimpressao { get; set; }

    public virtual DbSet<Itenscarrinho> Itenscarrinho { get; set; }

    public virtual DbSet<Pedidos> Pedidos { get; set; }

    public virtual DbSet<Prepostagens> Prepostagens { get; set; }

    public virtual DbSet<Produtos> Produtos { get; set; }

    public virtual DbSet<Produtoscores> Produtoscores { get; set; }

    public virtual DbSet<Produtoscorimagens> Produtoscorimagens { get; set; }

    public virtual DbSet<Roles> Roles { get; set; }

    public virtual DbSet<Rotulos> Rotulos { get; set; }

    public virtual DbSet<Transacoes> Transacoes { get; set; }

    public virtual DbSet<Usuarios> Usuarios { get; set; }

    public virtual DbSet<Videos> Videos { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder
            .UseCollation("utf8mb4_general_ci")
            .HasCharSet("utf8mb4");

        modelBuilder.Entity<Carrinhos>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("carrinhos");

            entity.HasIndex(e => e.ClienteId, "IX_Carrinhos_ClienteId");

            entity.HasIndex(e => e.CupomId, "IX_Carrinhos_CupomId");

            entity.HasIndex(e => e.Token, "IX_Carrinhos_Token").IsUnique();

            entity.Property(e => e.Id).HasColumnType("int(11)");
            entity.Property(e => e.ClienteId).HasColumnType("int(11)");
            entity.Property(e => e.CupomId).HasColumnType("int(11)");
            entity.Property(e => e.DataAtualizacao).HasMaxLength(6);
            entity.Property(e => e.DataCriacao).HasMaxLength(6);
            entity.Property(e => e.Status).HasColumnType("int(11)");

            entity.HasOne(d => d.Cliente).WithMany(p => p.Carrinhos)
                .HasForeignKey(d => d.ClienteId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("FK_Carrinhos_Clientes_ClienteId");

            entity.HasOne(d => d.Cupom).WithMany(p => p.Carrinhos)
                .HasForeignKey(d => d.CupomId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("FK_Carrinhos_cupons_CupomId");
        });

        modelBuilder.Entity<Categorias>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("categorias");

            entity.Property(e => e.Id).HasColumnType("int(11)");
            entity.Property(e => e.Active)
                .IsRequired()
                .HasDefaultValueSql("b'0'");
        });

        modelBuilder.Entity<Clientes>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("clientes");

            entity.Property(e => e.Id).HasColumnType("int(11)");
            entity.Property(e => e.DataAtualizacao).HasMaxLength(6);
            entity.Property(e => e.DataCriacao).HasMaxLength(6);
        });

        modelBuilder.Entity<Cor>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("cor");

            entity.Property(e => e.Id).HasColumnType("int(11)");
        });

        modelBuilder.Entity<Cupons>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("cupons");

            entity.Property(e => e.Id)
                .HasColumnType("int(11)")
                .HasColumnName("id");
            entity.Property(e => e.Ativo).HasColumnName("ativo");
            entity.Property(e => e.Codigo)
                .HasMaxLength(50)
                .HasColumnName("codigo");
            entity.Property(e => e.DataAtualizacao)
                .HasMaxLength(6)
                .HasColumnName("data_atualizacao");
            entity.Property(e => e.DataCriacao)
                .HasMaxLength(6)
                .HasColumnName("data_criacao");
            entity.Property(e => e.DataExpiracao)
                .HasMaxLength(6)
                .HasColumnName("data_expiracao");
            entity.Property(e => e.DataInicio)
                .HasMaxLength(6)
                .HasColumnName("data_inicio");
            entity.Property(e => e.Descricao)
                .HasMaxLength(255)
                .HasColumnName("descricao");
            entity.Property(e => e.QuantidadeMaximaUsos)
                .HasColumnType("int(11)")
                .HasColumnName("quantidade_maxima_usos");
            entity.Property(e => e.QuantidadeUsosAtual)
                .HasColumnType("int(11)")
                .HasColumnName("quantidade_usos_atual");
            entity.Property(e => e.TipoDesconto)
                .HasMaxLength(20)
                .HasColumnName("tipo_desconto");
            entity.Property(e => e.UsoPorUsuario)
                .HasColumnType("int(11)")
                .HasColumnName("uso_por_usuario");
            entity.Property(e => e.ValorDesconto)
                .HasPrecision(10, 2)
                .HasColumnName("valor_desconto");
            entity.Property(e => e.ValorMaximoDesconto)
                .HasPrecision(10, 2)
                .HasColumnName("valor_maximo_desconto");
            entity.Property(e => e.ValorMinimoCompra)
                .HasPrecision(10, 2)
                .HasColumnName("valor_minimo_compra");
        });

        modelBuilder.Entity<CuponsUso>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("cupons_uso");

            entity.HasIndex(e => e.CupomId, "IX_cupons_uso_cupom_id");

            entity.HasIndex(e => e.UsuarioId, "IX_cupons_uso_usuario_id");

            entity.Property(e => e.Id)
                .HasColumnType("int(11)")
                .HasColumnName("id");
            entity.Property(e => e.CupomId)
                .HasColumnType("int(11)")
                .HasColumnName("cupom_id");
            entity.Property(e => e.DataUso)
                .HasMaxLength(6)
                .HasColumnName("data_uso");
            entity.Property(e => e.PedidoId)
                .HasColumnType("int(11)")
                .HasColumnName("pedido_id");
            entity.Property(e => e.UsuarioId)
                .HasColumnType("int(11)")
                .HasColumnName("usuario_id");
            entity.Property(e => e.ValorDescontoAplicado)
                .HasPrecision(10, 2)
                .HasColumnName("valor_desconto_aplicado");

            entity.HasOne(d => d.Cupom).WithMany(p => p.CuponsUso).HasForeignKey(d => d.CupomId);

            entity.HasOne(d => d.Usuario).WithMany(p => p.CuponsUso)
                .HasForeignKey(d => d.UsuarioId)
                .HasConstraintName("FK_cupons_uso_Usuarios_usuario_id");
        });

        modelBuilder.Entity<DescontosQuantidade>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("descontos_quantidade");

            entity.Property(e => e.Id)
                .HasColumnType("int(11)")
                .HasColumnName("id");
            entity.Property(e => e.Ativo).HasColumnName("ativo");
            entity.Property(e => e.DataAtualizacao)
                .HasMaxLength(6)
                .HasColumnName("data_atualizacao");
            entity.Property(e => e.DataCriacao)
                .HasMaxLength(6)
                .HasColumnName("data_criacao");
            entity.Property(e => e.Descricao)
                .HasMaxLength(255)
                .HasColumnName("descricao");
            entity.Property(e => e.QuantidadeMaxima)
                .HasColumnType("int(11)")
                .HasColumnName("quantidade_maxima");
            entity.Property(e => e.QuantidadeMinima)
                .HasColumnType("int(11)")
                .HasColumnName("quantidade_minima");
            entity.Property(e => e.ValorPromocional)
                .HasPrecision(10, 2)
                .HasColumnName("valor_promocional");
        });

        modelBuilder.Entity<Devolucaoitens>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("devolucaoitens");

            entity.HasIndex(e => e.DevolucaoId, "IX_DevolucaoItens_DevolucaoId");

            entity.Property(e => e.Id).HasColumnType("int(11)");
            entity.Property(e => e.CorId).HasColumnType("int(11)");
            entity.Property(e => e.DevolucaoId).HasColumnType("int(11)");
            entity.Property(e => e.ItemCarrinhoId).HasColumnType("int(11)");
            entity.Property(e => e.ProdutoId).HasColumnType("int(11)");
            entity.Property(e => e.Quantidade).HasColumnType("int(11)");

            entity.HasOne(d => d.Devolucao).WithMany(p => p.Devolucaoitens)
                .HasForeignKey(d => d.DevolucaoId)
                .HasConstraintName("FK_DevolucaoItens_Devolucoes_DevolucaoId");
        });

        modelBuilder.Entity<Devolucoes>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("devolucoes");

            entity.HasIndex(e => e.PedidoId, "IX_Devolucoes_PedidoId");

            entity.Property(e => e.Id).HasColumnType("int(11)");
            entity.Property(e => e.DataAtualizacao).HasMaxLength(6);
            entity.Property(e => e.DataCriacao).HasMaxLength(6);
            entity.Property(e => e.PedidoId).HasColumnType("int(11)");
            entity.Property(e => e.Status).HasColumnType("int(11)");

            entity.HasOne(d => d.Pedido).WithMany(p => p.Devolucoes)
                .HasForeignKey(d => d.PedidoId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Devolucoes_Pedidos_PedidoId");
        });

        modelBuilder.Entity<Efmigrationshistory>(entity =>
        {
            entity.HasKey(e => e.MigrationId).HasName("PRIMARY");

            entity.ToTable("__efmigrationshistory");

            entity.Property(e => e.MigrationId).HasMaxLength(150);
            entity.Property(e => e.ProductVersion).HasMaxLength(32);
        });

        modelBuilder.Entity<Enderecos>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("enderecos");

            entity.HasIndex(e => e.ClienteId, "IX_Enderecos_ClienteId");

            entity.Property(e => e.Id).HasColumnType("int(11)");
            entity.Property(e => e.ClienteId).HasColumnType("int(11)");
            entity.Property(e => e.DataAtualizacao).HasMaxLength(6);
            entity.Property(e => e.DataCriacao).HasMaxLength(6);

            entity.HasOne(d => d.Cliente).WithMany(p => p.Enderecos)
                .HasForeignKey(d => d.ClienteId)
                .HasConstraintName("FK_Enderecos_Clientes_ClienteId");
        });

        modelBuilder.Entity<Filaimpressao>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("filaimpressao");

            entity.HasIndex(e => e.DataCriacao, "IX_FilaImpressao_DataCriacao");

            entity.HasIndex(e => e.PedidoId, "IX_FilaImpressao_PedidoId");

            entity.HasIndex(e => e.RotuloId, "IX_FilaImpressao_RotuloId");

            entity.HasIndex(e => e.Status, "IX_FilaImpressao_Status");

            entity.Property(e => e.Id).HasColumnType("int(11)");
            entity.Property(e => e.CaminhoArquivo).HasMaxLength(500);
            entity.Property(e => e.ClienteId).HasMaxLength(100);
            entity.Property(e => e.CodigoPedido).HasMaxLength(50);
            entity.Property(e => e.Copias)
                .HasDefaultValueSql("'1'")
                .HasColumnType("int(11)");
            entity.Property(e => e.DataCriacao).HasMaxLength(6);
            entity.Property(e => e.DataImpressao).HasMaxLength(6);
            entity.Property(e => e.DataProcessamento).HasMaxLength(6);
            entity.Property(e => e.ImpressoraDestino).HasMaxLength(255);
            entity.Property(e => e.MaxTentativas)
                .HasDefaultValueSql("'3'")
                .HasColumnType("int(11)");
            entity.Property(e => e.NomeArquivo).HasMaxLength(500);
            entity.Property(e => e.PedidoId).HasColumnType("int(11)");
            entity.Property(e => e.RotuloId).HasColumnType("int(11)");
            entity.Property(e => e.Status).HasColumnType("int(11)");
            entity.Property(e => e.Tentativas).HasColumnType("int(11)");

            entity.HasOne(d => d.Rotulo).WithMany(p => p.Filaimpressao)
                .HasForeignKey(d => d.RotuloId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("FK_FilaImpressao_Rotulos_RotuloId");
        });

        modelBuilder.Entity<Itenscarrinho>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("itenscarrinho");

            entity.HasIndex(e => e.CarrinhoId, "IX_ItensCarrinho_CarrinhoId");

            entity.HasIndex(e => e.CorId, "IX_ItensCarrinho_CorId");

            entity.HasIndex(e => e.ProdutoId, "IX_ItensCarrinho_ProdutoId");

            entity.Property(e => e.Id).HasColumnType("int(11)");
            entity.Property(e => e.CarrinhoId).HasColumnType("int(11)");
            entity.Property(e => e.CorId).HasColumnType("int(11)");
            entity.Property(e => e.DataAdicao).HasMaxLength(6);
            entity.Property(e => e.DataAtualizacao).HasMaxLength(6);
            entity.Property(e => e.ProdutoId).HasColumnType("int(11)");
            entity.Property(e => e.Quantidade).HasColumnType("int(11)");

            entity.HasOne(d => d.Carrinho).WithMany(p => p.Itenscarrinho)
                .HasForeignKey(d => d.CarrinhoId)
                .HasConstraintName("FK_ItensCarrinho_Carrinhos_CarrinhoId");

            entity.HasOne(d => d.Cor).WithMany(p => p.Itenscarrinho)
                .HasForeignKey(d => d.CorId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ItensCarrinho_ProdutosCores_CorId");

            entity.HasOne(d => d.Produto).WithMany(p => p.Itenscarrinho)
                .HasForeignKey(d => d.ProdutoId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ItensCarrinho_Produtos_ProdutoId");
        });

        modelBuilder.Entity<Pedidos>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("pedidos");

            entity.HasIndex(e => e.CarrinhoId, "IX_Pedidos_CarrinhoId");

            entity.HasIndex(e => e.ClienteId, "IX_Pedidos_ClienteId");

            entity.HasIndex(e => e.EnderecoEntregaId, "IX_Pedidos_EnderecoEntregaId");

            entity.Property(e => e.Id).HasColumnType("int(11)");
            entity.Property(e => e.CarrinhoId).HasColumnType("int(11)");
            entity.Property(e => e.ClienteId).HasColumnType("int(11)");
            entity.Property(e => e.DataAtualizacao).HasMaxLength(6);
            entity.Property(e => e.DataPedido).HasMaxLength(6);
            entity.Property(e => e.EnderecoEntregaId).HasColumnType("int(11)");
            entity.Property(e => e.NomeCliente).HasDefaultValueSql("''");
            entity.Property(e => e.Status).HasColumnType("int(11)");

            entity.HasOne(d => d.Carrinho).WithMany(p => p.Pedidos)
                .HasForeignKey(d => d.CarrinhoId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Pedidos_Carrinhos_CarrinhoId");

            entity.HasOne(d => d.Cliente).WithMany(p => p.Pedidos)
                .HasForeignKey(d => d.ClienteId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Pedidos_Clientes_ClienteId");

            entity.HasOne(d => d.EnderecoEntrega).WithMany(p => p.Pedidos)
                .HasForeignKey(d => d.EnderecoEntregaId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Pedidos_Enderecos_EnderecoEntregaId");
        });

        modelBuilder.Entity<Prepostagens>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("prepostagens");

            entity.HasIndex(e => e.PedidoId, "IX_PrePostagens_PedidoId");

            entity.Property(e => e.Id).HasColumnType("int(11)");
            entity.Property(e => e.Altura).HasColumnType("int(11)");
            entity.Property(e => e.Comprimento).HasColumnType("int(11)");
            entity.Property(e => e.DataCriacao).HasMaxLength(6);
            entity.Property(e => e.DataEntrega).HasMaxLength(6);
            entity.Property(e => e.DataPostagem).HasMaxLength(6);
            entity.Property(e => e.Largura).HasColumnType("int(11)");
            entity.Property(e => e.PedidoId).HasColumnType("int(11)");
            entity.Property(e => e.Peso).HasPrecision(10, 3);
            entity.Property(e => e.Status).HasColumnType("int(11)");
            entity.Property(e => e.ValorDeclarado).HasPrecision(10, 2);

            entity.HasOne(d => d.Pedido).WithMany(p => p.Prepostagens)
                .HasForeignKey(d => d.PedidoId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_PrePostagens_Pedidos_PedidoId");
        });

        modelBuilder.Entity<Produtos>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("produtos");

            entity.HasIndex(e => e.CategoriaId, "IX_Produtos_CategoriaId");

            entity.Property(e => e.Id).HasColumnType("int(11)");
            entity.Property(e => e.Active)
                .IsRequired()
                .HasDefaultValueSql("b'1'");
            entity.Property(e => e.CategoriaId).HasColumnType("int(11)");
            entity.Property(e => e.MaxParcelas).HasColumnType("int(11)");
            entity.Property(e => e.Preco).HasPrecision(10, 2);
            entity.Property(e => e.PrecoOriginal).HasPrecision(10, 2);
            entity.Property(e => e.Sku).HasColumnName("SKU");
            entity.Property(e => e.TaxaJuros).HasPrecision(5, 4);

            entity.HasOne(d => d.Categoria).WithMany(p => p.Produtos)
                .HasForeignKey(d => d.CategoriaId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Produtos_Categorias_CategoriaId");
        });

        modelBuilder.Entity<Produtoscores>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("produtoscores");

            entity.HasIndex(e => e.ProdutosId, "IX_ProdutosCores_ProdutosId");

            entity.Property(e => e.Id).HasColumnType("int(11)");
            entity.Property(e => e.ProdutosId).HasColumnType("int(11)");
            entity.Property(e => e.QuantidadeEstoque).HasColumnType("int(11)");

            entity.HasOne(d => d.Produtos).WithMany(p => p.Produtoscores)
                .HasForeignKey(d => d.ProdutosId)
                .HasConstraintName("FK_ProdutosCores_Produtos_ProdutosId");
        });

        modelBuilder.Entity<Produtoscorimagens>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("produtoscorimagens");

            entity.HasIndex(e => e.ProdutosCorId, "IX_ProdutosCorImagens_ProdutosCorId");

            entity.Property(e => e.Id).HasColumnType("int(11)");
            entity.Property(e => e.Ordem).HasColumnType("int(11)");
            entity.Property(e => e.ProdutosCorId).HasColumnType("int(11)");

            entity.HasOne(d => d.ProdutosCor).WithMany(p => p.Produtoscorimagens)
                .HasForeignKey(d => d.ProdutosCorId)
                .HasConstraintName("FK_ProdutosCorImagens_ProdutosCores_ProdutosCorId");
        });

        modelBuilder.Entity<Roles>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("roles");

            entity.HasIndex(e => e.Nome, "IX_Roles_Nome").IsUnique();

            entity.Property(e => e.Id).HasColumnType("int(11)");
        });

        modelBuilder.Entity<Rotulos>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("rotulos");

            entity.HasIndex(e => e.DataGeracao, "IX_Rotulos_DataGeracao");

            entity.HasIndex(e => e.IdPedido, "IX_Rotulos_IdPedido");

            entity.HasIndex(e => e.IdRecibo, "IX_Rotulos_IdRecibo");

            entity.Property(e => e.Id).HasColumnType("int(11)");
            entity.Property(e => e.CaminhoArquivo).HasMaxLength(500);
            entity.Property(e => e.DataGeracao).HasMaxLength(6);
            entity.Property(e => e.FormatoRotulo)
                .HasMaxLength(10)
                .HasDefaultValueSql("'ET'");
            entity.Property(e => e.IdAtendimento).HasMaxLength(255);
            entity.Property(e => e.IdPedido).HasColumnType("int(11)");
            entity.Property(e => e.NomeArquivo).HasMaxLength(500);
            entity.Property(e => e.QuantidadeRotulos).HasColumnType("int(11)");
            entity.Property(e => e.TamanhoBytes).HasColumnType("bigint(20)");
            entity.Property(e => e.TipoRotulo)
                .HasMaxLength(10)
                .HasDefaultValueSql("'P'");
        });

        modelBuilder.Entity<Transacoes>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("transacoes");

            entity.HasIndex(e => e.PedidoId, "IX_Transacoes_PedidoId");

            entity.Property(e => e.Id).HasColumnType("int(11)");
            entity.Property(e => e.DataCriacao).HasMaxLength(6);
            entity.Property(e => e.DataTransacao).HasMaxLength(6);
            entity.Property(e => e.PedidoId).HasColumnType("int(11)");
            entity.Property(e => e.Tipo).HasColumnType("int(11)");

            entity.HasOne(d => d.Pedido).WithMany(p => p.Transacoes)
                .HasForeignKey(d => d.PedidoId)
                .HasConstraintName("FK_Transacoes_Pedidos_PedidoId");
        });

        modelBuilder.Entity<Usuarios>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("usuarios");

            entity.HasIndex(e => e.Email, "IX_Usuarios_Email").IsUnique();

            entity.HasIndex(e => e.RoleId, "IX_Usuarios_RoleId");

            entity.Property(e => e.Id).HasColumnType("int(11)");
            entity.Property(e => e.DataCriacao).HasMaxLength(6);
            entity.Property(e => e.RoleId).HasColumnType("int(11)");

            entity.HasOne(d => d.Role).WithMany(p => p.Usuarios)
                .HasForeignKey(d => d.RoleId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Usuarios_Roles_RoleId");
        });

        modelBuilder.Entity<Videos>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("videos");

            entity.HasIndex(e => e.CategoriaId, "IX_Videos_CategoriaId");

            entity.Property(e => e.Id).HasColumnType("int(11)");
            entity.Property(e => e.CategoriaId).HasColumnType("int(11)");
            entity.Property(e => e.Duracao).HasColumnType("int(11)");
            entity.Property(e => e.Ordem).HasColumnType("int(11)");

            entity.HasOne(d => d.Categoria).WithMany(p => p.Videos)
                .HasForeignKey(d => d.CategoriaId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Videos_Categorias_CategoriaId");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
