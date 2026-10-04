using Microsoft.EntityFrameworkCore;
using VisorDatosSIG.Domain.Entities;

namespace VisorDatosSIG.Infrastructure.Persistence;

public sealed class VisorDatosSigDbContext(DbContextOptions<VisorDatosSigDbContext> options) : DbContext(options)
{
    public DbSet<Manzana> Manzanas => Set<Manzana>();
    public DbSet<Lote> Lotes => Set<Lote>();
    public DbSet<CodigoFijo> CodigosFijos => Set<CodigoFijo>();
    public DbSet<Via> Vias => Set<Via>();
    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<Rol> Roles => Set<Rol>();
    public DbSet<UsuarioRol> UsuariosRoles => Set<UsuarioRol>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Manzana>(entity => { entity.ToTable("Manzanas"); entity.HasKey(x => x.IdManzana); entity.Property(x => x.UvMza).HasColumnName("UV_MZA").HasMaxLength(20); entity.Property(x => x.Uv).HasColumnName("UV").HasMaxLength(15); entity.Property(x => x.Mza).HasColumnName("MZA").HasMaxLength(10); entity.Property(x => x.Geom).HasColumnType("geometry"); });
        modelBuilder.Entity<Lote>(entity => { entity.ToTable("Lotes"); entity.HasKey(x => x.IdLote); entity.Property(x => x.NroLote).HasMaxLength(15); entity.Property(x => x.Geom).HasColumnType("geometry"); });
        modelBuilder.Entity<CodigoFijo>(entity => { entity.ToTable("CodigosFijos"); entity.HasKey(x => x.IdCodigo); entity.Property(x => x.CodFSql).HasColumnName("CodF_SQL"); entity.Property(x => x.CodFSig).HasColumnName("CodF_SIG").HasMaxLength(25); entity.Property(x => x.CodFijoValor).HasColumnName("CodFijo"); entity.Property(x => x.Nombre).HasMaxLength(120); entity.Property(x => x.FechaCambioEstado).HasColumnType("datetime2"); entity.Property(x => x.Geom).HasColumnType("geometry"); });
        modelBuilder.Entity<Via>(entity => { entity.ToTable("Vias"); entity.HasKey(x => x.IdVia); entity.Property(x => x.ObjectId).HasColumnName("OBJECTID"); entity.Property(x => x.Nombre).HasMaxLength(40); entity.Property(x => x.TipoVia).HasMaxLength(30); entity.Property(x => x.OsmId).HasColumnName("OSMID").HasMaxLength(20); entity.Property(x => x.Geom).HasColumnType("geometry"); });
        modelBuilder.Entity<Usuario>(entity => { entity.ToTable("Usuarios"); entity.HasKey(x => x.IdUsuario); entity.Property(x => x.Login).HasMaxLength(50); entity.Property(x => x.Nombre).HasMaxLength(120); entity.Property(x => x.PasswordHash).HasMaxLength(32); entity.Property(x => x.PasswordSalt).HasMaxLength(32); entity.Property(x => x.FechaRegistro).HasColumnType("datetime2"); });
        modelBuilder.Entity<Rol>(entity => { entity.ToTable("Roles"); entity.HasKey(x => x.IdRol); entity.Property(x => x.NombreRol).HasMaxLength(50).IsUnicode(false); entity.Property(x => x.Descripcion).HasMaxLength(200).IsUnicode(false); });
        modelBuilder.Entity<UsuarioRol>(entity => { entity.ToTable("UsuariosRoles"); entity.HasKey(x => x.IdUsuarioRol); });
    }
}
