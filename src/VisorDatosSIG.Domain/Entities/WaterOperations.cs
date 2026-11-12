namespace VisorDatosSIG.Domain.Entities;

public static class WaterOperations
{
    public const string Cutter = "Cortador";
    public const string Reconnector = "Reconexion";
    public static readonly string[] States = ["Normal", "Para corte", "Cortado", "Baja parcial", "Baja total"];
    public static string Role(bool reconnect) => reconnect ? Reconnector : Cutter;
    public static byte InitialState(bool reconnect) => reconnect ? (byte)3 : (byte)2;
    public static byte FinalState(bool reconnect) => reconnect ? (byte)1 : (byte)3;
}

public sealed class WaterAssignment
{
    public int Id { get; set; }
    public int WorkerId { get; set; }
    public bool Reconnect { get; set; }
    public DateTime CreatedUtc { get; set; }
    public double StartLat { get; set; }
    public double StartLng { get; set; }
    public string RouteJson { get; set; } = "[]";
    public List<WaterTask> Tasks { get; set; } = [];
}

public sealed class WaterTask
{
    public int Id { get; set; }
    public int AssignmentId { get; set; }
    public WaterAssignment Assignment { get; set; } = null!;
    public int CodeId { get; set; }
    public CodigoFijo Code { get; set; } = null!;
    public int Sequence { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public DateTime? CompletedUtc { get; set; }
    public bool? Observed { get; set; }
    public string? Meter { get; set; }
    public string? Reading { get; set; }
    public string? Observation { get; set; }
    public List<WaterEvidence> Evidence { get; set; } = [];
    public double? ReportLat { get; set; }
    public double? ReportLng { get; set; }
    public double? Accuracy { get; set; }
    public byte[] Version { get; set; } = [];
}

public sealed class WaterEvidence
{
    public int Id { get; set; }
    public int WaterTaskId { get; set; }
    public byte[] Photo { get; set; } = [];
}

public sealed class WorkerPosition
{
    public int Id { get; set; }
    public int AssignmentId { get; set; }
    public WaterAssignment Assignment { get; set; } = null!;
    public DateTime RecordedUtc { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double Accuracy { get; set; }
}
