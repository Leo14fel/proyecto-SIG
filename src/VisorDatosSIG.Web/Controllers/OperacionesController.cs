using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Densify;
using NetTopologySuite.Geometries;
using NetTopologySuite.Operation.Union;
using VisorDatosSIG.Application;
using VisorDatosSIG.Domain.Entities;
using VisorDatosSIG.Infrastructure.Persistence;

namespace VisorDatosSIG.Web.Controllers;

[Authorize]
[AutoValidateAntiforgeryToken]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[Route("Operaciones")]
public sealed class OperacionesController(VisorDatosSigDbContext db, IDataProtectionProvider protection) : Controller
{
    private const string Workers = WaterOperations.Cutter + "," + WaterOperations.Reconnector;
    private readonly IDataProtector preview = protection.CreateProtector("WaterAssignmentPreview.v1");
    private int UserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [Authorize(Roles = "Administrador")]
    [HttpGet("Asignaciones")]
    public IActionResult Asignaciones() => View();

    [Authorize(Roles = "Administrador")]
    [HttpGet("Monitoreo")]
    public IActionResult Monitoreo() => View();

    [Authorize(Roles = Workers)]
    [HttpGet("Campo")]
    public IActionResult Campo() => View();

    private IQueryable<Usuario> Staff(string role) =>
        from user in db.Usuarios.AsNoTracking()
        where user.Activo && (from membership in db.UsuariosRoles
                              join r in db.Roles on membership.IdRol equals r.IdRol
                              where membership.IdUsuario == user.IdUsuario && r.Estado && r.NombreRol == role
                              select r.IdRol).Any()
        select user;

    [Authorize(Roles = "Administrador")]
    [HttpGet("Trabajadores")]
    public async Task<IActionResult> Trabajadores(bool reconnect, CancellationToken ct) =>
        Json(await Staff(WaterOperations.Role(reconnect)).OrderBy(u => u.Nombre)
            .Select(u => new { id = u.IdUsuario, nombre = u.Nombre }).ToListAsync(ct));

    [Authorize(Roles = "Administrador")]
    [HttpPost("PrepararCorte/{id:int}")]
    public async Task<IActionResult> PrepararCorte(int id, CancellationToken ct)
    {
        var changed = await db.CodigosFijos.Where(c => c.IdCodigo == id && c.Estado == 1
                && !db.WaterTasks.Any(t => t.CodeId == id && t.CompletedUtc == null))
            .ExecuteUpdateAsync(set => set.SetProperty(c => c.Estado, (byte)2)
                .SetProperty(c => c.FechaCambioEstado, DateTime.UtcNow), ct);
        TempData["OperationMessage"] = changed == 1 ? "La conexión está lista para asignar a corte." : "La conexión ya no está disponible para marcar.";
        return RedirectToAction("Detalle", "Registros", new { capa = "codigos-fijos", id });
    }

    [Authorize(Roles = "Administrador")]
    [HttpPost("Ruta")]
    public async Task<IActionResult> Ruta([FromBody] RouteInput? input, CancellationToken ct)
    {
        if (!ModelState.IsValid || input is null || input.Count is < 1 or > 50 || !input.Start.IsValid)
            return BadRequest(new { message = "Selecciona un punto de partida y entre 1 y 50 tareas." });
        if (!await Staff(WaterOperations.Role(input.Reconnect)).AnyAsync(u => u.IdUsuario == input.WorkerId, ct))
            return BadRequest(new { message = "El trabajador no está activo o no tiene el rol requerido." });

        var candidates = await db.CodigosFijos.AsNoTracking()
            .Where(x => x.Estado == WaterOperations.InitialState(input.Reconnect) && x.Geom != null
                && !db.WaterTasks.Any(t => t.CodeId == x.IdCodigo && t.CompletedUtc == null))
            .OrderBy(x => x.IdCodigo).Select(x => new { x.IdCodigo, x.CodFijoValor, x.Nombre, x.Geom }).ToListAsync(ct);
        var points = candidates.Where(x => x.Geom is Point { IsEmpty: false, SRID: 4326 })
            .ToDictionary(x => x.IdCodigo, x => new MapPoint(x.Geom!.Coordinate.Y, x.Geom.Coordinate.X));
        var roads = await db.Vias.AsNoTracking().Where(x => x.Geom != null).OrderBy(x => x.IdVia).Select(x => x.Geom!).ToListAsync(ct);
        var lines = roads.Where(x => x.SRID == 4326).SelectMany(Lines).Where(x => !x.IsEmpty).ToList();
        if (lines.Count == 0) return Conflict(new { message = "No hay vías en WGS 84 para planificar." });
        // Node intersecting street segments before building the graph.
        var network = UnaryUnionOp.Union(lines.Cast<Geometry>().ToArray());
        var router = new StreetRouter(Lines(network).Select(line =>
            Densifier.Densify(line, 0.00015).Coordinates.Select(p => new MapPoint(p.Y, p.X)).ToArray()));
        StreetPlan plan;
        try { plan = router.Plan(input.Start, points.Select(x => (x.Key, x.Value)), input.Count, ct); }
        catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
        if (plan.Stops.Length != input.Count)
            return Conflict(new { message = $"Solo hay {plan.Stops.Length} conexiones elegibles alcanzables desde ese punto." });

        var signature = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(
            new { input.WorkerId, input.Reconnect, input.Start, plan })));
        if (input.Preview is null)
        {
            var byId = candidates.ToDictionary(c => c.IdCodigo);
            return Json(new { plan.Path, plan.Meters, preview = preview.Protect(signature),
                stops = plan.Stops.Select(id => new { id, code = byId[id].CodFijoValor, byId[id].Nombre, point = points[id] }) });
        }

        try
        {
            if (preview.Unprotect(input.Preview) != signature)
                return Conflict(new { message = "La ruta cambió. Vuelve a calcularla antes de guardar." });
        }
        catch (CryptographicException) { return BadRequest(new { message = "Vista previa inválida." }); }

        var assignment = new WaterAssignment
        {
            WorkerId = input.WorkerId, Reconnect = input.Reconnect, CreatedUtc = DateTime.UtcNow,
            StartLat = input.Start.Lat, StartLng = input.Start.Lng, RouteJson = JsonSerializer.Serialize(plan.Path),
            Tasks = plan.Stops.Select((id, i) => new WaterTask
            {
                CodeId = id, Sequence = i + 1, Latitude = points[id].Lat, Longitude = points[id].Lng
            }).ToList()
        };
        try
        {
            await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
            var stillAvailable = await db.CodigosFijos.CountAsync(x => plan.Stops.Contains(x.IdCodigo)
                && x.Estado == WaterOperations.InitialState(input.Reconnect)
                && !db.WaterTasks.Any(t => t.CodeId == x.IdCodigo && t.CompletedUtc == null), ct);
            if (stillAvailable != plan.Stops.Length
                || !await Staff(WaterOperations.Role(input.Reconnect)).AnyAsync(u => u.IdUsuario == input.WorkerId, ct))
                return Conflict(new { message = "Cambió la disponibilidad de la ruta. Vuelve a calcularla." });
            db.WaterAssignments.Add(assignment);
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 1205 or 2601 or 2627 })
        { return Conflict(new { message = "Otra asignación tomó una de estas conexiones. Calcula nuevamente." }); }
        catch (SqlException ex) when (ex.Number == 1205)
        { return Conflict(new { message = "La ruta coincidió con otra actualización. Calcula nuevamente." }); }
        return Json(new { assignment.Id });
    }

    private static IEnumerable<LineString> Lines(Geometry geom) => geom switch
    {
        LineString line => [line],
        GeometryCollection collection => Enumerable.Range(0, collection.NumGeometries).SelectMany(i => Lines(collection.GetGeometryN(i))),
        _ => []
    };

    private IQueryable<WaterAssignment> MyAssignments()
    {
        var cutter = User.IsInRole(WaterOperations.Cutter);
        var reconnect = User.IsInRole(WaterOperations.Reconnector);
        return db.WaterAssignments.Where(a => a.WorkerId == UserId
            && (a.Reconnect ? reconnect : cutter) && db.Usuarios.Any(u => u.IdUsuario == a.WorkerId && u.Activo)
            && (from membership in db.UsuariosRoles
                join role in db.Roles on membership.IdRol equals role.IdRol
                where membership.IdUsuario == a.WorkerId && role.Estado
                    && role.NombreRol == (a.Reconnect ? WaterOperations.Reconnector : WaterOperations.Cutter)
                select role.IdRol).Any());
    }

    [Authorize(Roles = Workers)]
    [HttpGet("Tareas")]
    public async Task<IActionResult> Tareas(CancellationToken ct) =>
        Json(await MyAssignments().AsNoTracking().OrderByDescending(a => a.Id).Take(30)
            .Select(a => new { a.Id, a.Reconnect, tasks = a.Tasks.OrderBy(t => t.Sequence)
                .Select(t => new { t.Id, t.Sequence, t.Code.CodFijoValor, t.Code.Nombre, t.Latitude, t.Longitude, t.CompletedUtc, t.Observed }) })
            .ToListAsync(ct));

    [Authorize(Roles = Workers)]
    [HttpPost("Posicion/{id:int}")]
    public async Task<IActionResult> Posicion(int id, [FromBody] GpsInput? gps, CancellationToken ct)
    {
        if (gps is null || !gps.IsValid) return BadRequest();
        if (!await MyAssignments().AnyAsync(x => x.Id == id && x.Tasks.Any(t => t.CompletedUtc == null), ct)) return NotFound();
        var now = DateTime.UtcNow;
        if (await db.WorkerPositions.AnyAsync(p => p.AssignmentId == id && p.RecordedUtc > now.AddSeconds(-8), ct))
            return NoContent();
        db.WorkerPositions.Add(new WorkerPosition { AssignmentId = id, RecordedUtc = now, Latitude = gps.Lat, Longitude = gps.Lng, Accuracy = gps.Accuracy });
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    [Authorize(Roles = Workers)]
    [HttpPost("Resultado/{id:int}")]
    [RequestSizeLimit(16_000_000)]
    public async Task<IActionResult> Resultado(int id, [FromForm] ResultInput input, CancellationToken ct)
    {
        var gps = new GpsInput(input.Lat, input.Lng, input.Accuracy);
        if (!ModelState.IsValid || !gps.IsValid || (input.Observed
            ? string.IsNullOrWhiteSpace(input.Observation)
            : string.IsNullOrWhiteSpace(input.Meter) || string.IsNullOrWhiteSpace(input.Reading) || input.Photos.Count == 0))
            return BadRequest(new { message = "Se requiere GPS y una observación, o medidor, lectura y foto para un trabajo realizado." });
        if (!input.Observed && (!decimal.TryParse(input.Reading, System.Globalization.NumberStyles.AllowDecimalPoint,
            System.Globalization.CultureInfo.InvariantCulture, out var reading) || reading < 0))
            return BadRequest(new { message = "Introduce una lectura no negativa. Usa punto decimal." });

        var task = await db.WaterTasks.Include(t => t.Assignment).Where(t => MyAssignments().Any(a => a.Id == t.AssignmentId))
            .SingleOrDefaultAsync(t => t.Id == id, ct);
        if (task is null) return NotFound();
        if (task.CompletedUtc.HasValue) return Conflict(new { message = "La tarea ya fue registrada." });
        if (gps.Point.DistanceTo(new MapPoint(task.Latitude, task.Longitude)) > 150)
            return BadRequest(new { message = "Acércate a menos de 150 m de la conexión para registrar el resultado." });

        if (input.Photos.Count > 3) return BadRequest(new { message = "Puedes adjuntar hasta tres fotos." });
        foreach (var photo in input.Photos)
        {
            if (photo.Length is < 8 or > 5_000_000) return BadRequest(new { message = "Cada foto debe pesar hasta 5 MB." });
            using var buffer = new MemoryStream();
            await photo.CopyToAsync(buffer, ct);
            var bytes = buffer.ToArray();
            if (!IsPhoto(bytes)) return BadRequest(new { message = "La foto debe ser JPEG o PNG." });
            task.Evidence.Add(new WaterEvidence { Photo = bytes });
        }
        task.CompletedUtc = DateTime.UtcNow;
        task.Observed = input.Observed;
        task.Meter = input.Meter?.Trim();
        task.Reading = input.Reading?.Trim();
        task.Observation = input.Observation?.Trim();
        task.ReportLat = gps.Lat;
        task.ReportLng = gps.Lng;
        task.Accuracy = gps.Accuracy;
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        if (!input.Observed)
        {
            var changed = await db.CodigosFijos.Where(x => x.IdCodigo == task.CodeId && x.Estado == WaterOperations.InitialState(task.Assignment.Reconnect))
                .ExecuteUpdateAsync(set => set.SetProperty(x => x.Estado, WaterOperations.FinalState(task.Assignment.Reconnect))
                    .SetProperty(x => x.FechaCambioEstado, task.CompletedUtc.Value), ct);
            if (changed != 1) return Conflict(new { message = "El estado de la conexión cambió. Consulta al administrador." });
        }
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { return Conflict(new { message = "La tarea ya fue registrada." }); }
        await tx.CommitAsync(ct);
        return NoContent();
    }

    private static bool IsPhoto(byte[] bytes) => bytes.AsSpan().StartsWith(new byte[] { 255, 216, 255 })
        || bytes.AsSpan().StartsWith(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });

    [Authorize(Roles = "Administrador")]
    [HttpGet("Evidencia/{id:int}")]
    public async Task<IActionResult> Evidencia(int id, CancellationToken ct)
    {
        var photo = await db.WaterEvidence.Where(e => e.Id == id).Select(e => e.Photo).SingleOrDefaultAsync(ct);
        if (photo is null) return NotFound();
        Response.Headers.XContentTypeOptions = "nosniff";
        return File(photo, "application/octet-stream", photo[0] == 255 ? "medidor.jpg" : "medidor.png");
    }

    [Authorize(Roles = "Administrador")]
    [HttpGet("Recorridos")]
    public async Task<IActionResult> Recorridos(DateOnly? day, int? worker, bool? reconnect, CancellationToken ct)
    {
        // La fecha de la interfaz es Bolivia (UTC-04).
        var localDay = day ?? DateOnly.FromDateTime(DateTime.UtcNow.AddHours(-4));
        var from = localDay.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc).AddHours(4);
        var until = from.AddDays(1);
        var scope = db.WaterAssignments.AsNoTracking()
            .Where(a => (!worker.HasValue || a.WorkerId == worker) && (!reconnect.HasValue || a.Reconnect == reconnect));
        var assignments = await scope.Where(a => a.CreatedUtc >= from && a.CreatedUtc < until
                || db.WorkerPositions.Any(p => p.AssignmentId == a.Id && p.RecordedUtc >= from && p.RecordedUtc < until)
                || a.Tasks.Any(t => t.CompletedUtc >= from && t.CompletedUtc < until))
            .OrderByDescending(a => a.Id).Take(50)
            .Select(a => new { a.Id, a.WorkerId, a.Reconnect, a.RouteJson, name = db.Usuarios.Where(u => u.IdUsuario == a.WorkerId).Select(u => u.Nombre).FirstOrDefault(),
                tasks = a.Tasks.OrderBy(t => t.Sequence).Select(t => new { t.Id, t.Sequence, t.CompletedUtc, t.Observed, t.Observation, t.Meter, t.Reading, t.ReportLat, t.ReportLng,
                    evidence = t.Evidence.OrderBy(e => e.Id).Select(e => e.Id) }) })
            .ToListAsync(ct);
        var ids = assignments.Select(a => a.Id).ToArray();
        var positions = await db.WorkerPositions.AsNoTracking().Where(p => ids.Contains(p.AssignmentId) && p.RecordedUtc >= from && p.RecordedUtc < until)
            .OrderByDescending(p => p.RecordedUtc).Take(5000).Select(p => new { p.AssignmentId, p.Latitude, p.Longitude, p.RecordedUtc }).ToListAsync(ct);
        return Json(new { assignments, positions });
    }

    public sealed record RouteInput(int WorkerId, bool Reconnect, int Count, MapPoint Start, string? Preview);
    public sealed record GpsInput(double Lat, double Lng, double Accuracy)
    {
        public MapPoint Point => new(Lat, Lng);
        public bool IsValid => Point.IsValid && double.IsFinite(Accuracy) && Accuracy is > 0 and <= 100;
    }
    public sealed class ResultInput
    {
        [Microsoft.AspNetCore.Mvc.ModelBinding.BindRequired] public double Lat { get; set; }
        [Microsoft.AspNetCore.Mvc.ModelBinding.BindRequired] public double Lng { get; set; }
        [Microsoft.AspNetCore.Mvc.ModelBinding.BindRequired] public double Accuracy { get; set; }
        public bool Observed { get; set; }
        [StringLength(50)] public string? Meter { get; set; }
        [StringLength(32)] public string? Reading { get; set; }
        [StringLength(500)] public string? Observation { get; set; }
        public List<IFormFile> Photos { get; set; } = [];
    }
}
