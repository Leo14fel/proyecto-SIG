using NetTopologySuite.Geometries;

namespace VisorDatosSIG.Domain.Entities;

public sealed class Manzana
{
    public int IdManzana { get; set; }
    public int? IdOrigen { get; set; }
    public string? UvMza { get; set; }
    public string? Uv { get; set; }
    public string? Mza { get; set; }
    public Geometry? Geom { get; set; }
}

public sealed class Lote
{
    public int IdLote { get; set; }
    public int? IdOrigen { get; set; }
    public string? NroLote { get; set; }
    public int? IdManzana { get; set; }
    public Geometry? Geom { get; set; }
}

public sealed class CodigoFijo
{
    public int IdCodigo { get; set; }
    public int? CodFSql { get; set; }
    public string? CodFSig { get; set; }
    public int? CodFijoValor { get; set; }
    public string? Nombre { get; set; }
    public byte Estado { get; set; }
    public DateTime FechaCambioEstado { get; set; }
    public int? IdLote { get; set; }
    public double? Longitud { get; set; }
    public double? Latitud { get; set; }
    public Geometry? Geom { get; set; }
}

public sealed class Via
{
    public int IdVia { get; set; }
    public int? ObjectId { get; set; }
    public string? Nombre { get; set; }
    public string? TipoVia { get; set; }
    public string? OsmId { get; set; }
    public Geometry? Geom { get; set; }
}

public sealed class Usuario
{
    public int IdUsuario { get; set; }
    public string Login { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public byte[] PasswordHash { get; set; } = [];
    public byte[] PasswordSalt { get; set; } = [];
    public int Iteraciones { get; set; }
    public bool Activo { get; set; }
    public DateTime FechaRegistro { get; set; }
}

public sealed class Rol
{
    public int IdRol { get; set; }
    public string NombreRol { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public bool Estado { get; set; }
}

public sealed class UsuarioRol
{
    public int IdUsuarioRol { get; set; }
    public int IdUsuario { get; set; }
    public int IdRol { get; set; }
}
