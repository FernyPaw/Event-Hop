using System.Globalization;
using System.Security.Claims;
using EventHop.Api.Data;
using EventHop.Api.Models;
using EventHop.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MongoDB.Bson;

namespace EventHop.Api.Controllers;

// Consultas de lectura: nunca confirman pagos, liberan cupos ni cambian reservas.
[ApiController, Authorize(Roles = "admin,cafeteria"), Route("api/reportes")]
public class ReportesController(EventHopContext db, ArchivoLog log) : ControllerBase
{
    [HttpGet("cafeterias"), Authorize(Roles = "admin")]
    public async Task<IActionResult> Cafeterias()
    {
        var cafes = await db.Usuarios.AsNoTracking().Where(u => u.TipoUsuario == "cafeteria")
            .Select(u => new { u.Id, u.Nombre }).ToListAsync();
        return Ok(cafes.OrderBy(c => c.Nombre).Select(c => new { id = c.Id.ToString(), c.Nombre }));
    }

    [HttpGet("resumen")]
    public async Task<IActionResult> Resumen()
    {
        if (User.IsInRole("admin"))
            return Ok(new {
                totalUsuarios = await db.Usuarios.CountAsync(u => u.TipoUsuario == "usuario"),
                cafeteriasAceptadas = await db.Usuarios.CountAsync(u => u.TipoUsuario == "cafeteria" && u.Activo && u.EstadoSolicitud == "aprobada"),
                totalSolicitudesCafeterias = await db.Usuarios.CountAsync(u => u.TipoUsuario == "cafeteria"),
                totalEventos = await db.Eventos.CountAsync()
            });
        if (!ObjectId.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var cafe)) return Unauthorized();
        var eventos = await db.Eventos.AsNoTracking().Where(e => e.CafeteriaId == cafe).ToListAsync();
        var ids = eventos.Select(e => e.Id).ToArray();
        return Ok(new {
            totalEventos = eventos.Count,
            favoritos = ids.Length == 0 ? 0 : await db.Favoritos.CountAsync(f => ids.Contains(f.EventoId)),
            reservas = eventos.Sum(e => e.Reservas.Count(r => r.Estado == "confirmada")),
            entradas = eventos.Sum(e => e.Reservas.Count(r => r.Estado == "confirmada" && r.FechaEntrada != null))
        });
    }

    [HttpGet("{tipo}")]
    public async Task<IActionResult> Consultar(string tipo, [FromQuery] string? desde = null,
        [FromQuery] string? hasta = null, [FromQuery] string? cafeteriaId = null)
    {
        if (!new[] { "eventos", "asistencia", "pagos", "resenas" }.Contains(tipo))
            return BadRequest(new { mensaje = "Reporte no válido." });
        if (!FechaValida(desde) || !FechaValida(hasta) ||
            (!string.IsNullOrEmpty(desde) && !string.IsNullOrEmpty(hasta) && string.CompareOrdinal(desde, hasta) > 0))
            return BadRequest(new { mensaje = "Utiliza fechas válidas y un rango de inicio menor o igual al final." });
        ObjectId? filtroCafe = null;
        if (!User.IsInRole("admin")) {
            if (!ObjectId.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var propia)) return Unauthorized();
            // Un parámetro enviado por una cafetería nunca amplía su alcance.
            if (!string.IsNullOrEmpty(cafeteriaId) && cafeteriaId != propia.ToString()) return Forbid();
            filtroCafe = propia;
        } else if (!string.IsNullOrEmpty(cafeteriaId)) {
            if (!ObjectId.TryParse(cafeteriaId, out var id)) return BadRequest(new { mensaje = "Identificador de cafetería inválido." });
            filtroCafe = id;
        }
        log.Escribir($"Inicio de reporte: {tipo}, rol: {(User.IsInRole("admin") ? "admin" : "cafeteria")}");
        var consulta = db.Eventos.AsNoTracking();
        if (filtroCafe.HasValue) consulta = consulta.Where(e => e.CafeteriaId == filtroCafe.Value);
        // La combinación de colecciones se realiza tras leerlas con EF, sin usar el driver directamente.
        // Límite explícito para este proyecto académico: nunca se devuelve un reporte truncado.
        var eventos = await consulta.Take(5001).ToListAsync();
        if (eventos.Count > 5000) return StatusCode(413, new { mensaje = "El alcance supera 5000 eventos. Selecciona una cafetería." });
        eventos = eventos.Where(e => (string.IsNullOrEmpty(desde) || string.CompareOrdinal(e.Fecha, desde) >= 0) &&
            (string.IsNullOrEmpty(hasta) || string.CompareOrdinal(e.Fecha, hasta) <= 0)).OrderBy(e => e.Fecha).ThenBy(e => e.Hora).ThenBy(e => e.Id).ToList();
        var cafes = await db.Usuarios.AsNoTracking().Where(u => u.TipoUsuario == "cafeteria")
            .Select(u => new { u.Id, u.Nombre }).ToListAsync();
        var nombres = cafes.ToDictionary(c => c.Id, c => c.Nombre);
        var ids = eventos.Select(e => e.Id).ToArray();
        var favoritos = tipo == "eventos" && ids.Length > 0
            ? await db.Favoritos.AsNoTracking().Where(f => ids.Contains(f.EventoId)).ToListAsync() : [];
        var resenas = tipo == "resenas" && ids.Length > 0
            ? await db.Resenas.AsNoTracking().Where(r => ids.Contains(r.EventoId)).ToListAsync() : [];
        var reporte = Calcular(tipo, eventos, nombres, favoritos, resenas, DateTime.UtcNow);
        log.Escribir($"Fin de reporte: {tipo}, filas: {reporte.Filas.Count}");
        return Ok(reporte with { Desde = desde ?? "", Hasta = hasta ?? "" });
    }

    public static bool FechaValida(string? fecha) => string.IsNullOrEmpty(fecha) ||
        DateOnly.TryParseExact(fecha, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _);

    public static ReporteRespuesta Calcular(string tipo, List<Evento> eventos, Dictionary<ObjectId, string> cafes,
        List<Favorito> favoritos, List<Resena> resenas, DateTime ahoraUtc)
    {
        string N(long valor) => valor.ToString(CultureInfo.InvariantCulture);
        string M(long centavos) => (centavos / 100m).ToString("0.00", CultureInfo.InvariantCulture);
        string Porcentaje(int numerador, int denominador) => denominador == 0 ? "0.0" :
            (100m * numerador / denominador).ToString("0.0", CultureInfo.InvariantCulture);
        var filas = new List<string[]>();
        var indicadores = new Dictionary<string, string>();
        var columnas = tipo switch {
            "eventos" => new[] { "Evento", "Cafetería", "Fecha", "Categoría", "Cupo", "Reservas confirmadas", "Retenciones", "Disponibles", "Ocupación %", "Guardados" },
            "asistencia" => new[] { "Evento", "Cafetería", "Fecha", "Confirmadas", "Canceladas", "Entradas registradas", "Sin entrada", "Asistencia %" },
            "pagos" => new[] { "Evento", "Cafetería", "Fecha", "Pagados", "Reembolsados", "En reembolso", "Sin resolver", "Cobrado MXN", "Reembolsado MXN", "Neto MXN" },
            "resenas" => new[] { "Evento", "Cafetería", "Fecha", "Reseñas", "Promedio / 5", "1 estrella", "2 estrellas", "3 estrellas", "4 estrellas", "5 estrellas" },
            _ => throw new ArgumentException("Reporte no válido.")
        };
        foreach (var e in eventos) {
            var comunes = new[] { e.Titulo, cafes.GetValueOrDefault(e.CafeteriaId, "Cafetería no disponible"), e.Fecha };
            var confirmadas = e.Reservas.Count(r => r.Estado == "confirmada");
            var entradas = e.Reservas.Count(r => r.Estado == "confirmada" && r.FechaEntrada != null);
            string[] valores;
            if (tipo == "eventos") {
                var retenidas = e.Pagos.Count(p => ReglasReservas.Retiene(p, ahoraUtc));
                valores = [e.Categoria, N(e.Cupo), N(confirmadas), N(retenidas), N(Math.Max(0, e.Cupo - confirmadas - retenidas)),
                    Porcentaje(confirmadas + retenidas, e.Cupo), N(favoritos.Count(f => f.EventoId == e.Id))];
            } else if (tipo == "asistencia") {
                valores = [N(confirmadas), N(e.Reservas.Count(r => r.Estado == "cancelada")), N(entradas),
                    N(confirmadas - entradas), Porcentaje(entradas, confirmadas)];
            } else if (tipo == "pagos") {
                var cobrados = e.Pagos.Where(p => p.Estado is "pagado" or "reembolsando" or "reembolsado").Sum(p => (long)p.Centavos);
                var devueltos = e.Pagos.Where(p => p.Estado == "reembolsado").Sum(p => (long)p.Centavos);
                valores = [N(e.Pagos.Count(p => p.Estado == "pagado")), N(e.Pagos.Count(p => p.Estado == "reembolsado")),
                    N(e.Pagos.Count(p => p.Estado == "reembolsando")), N(e.Pagos.Count(p => p.Estado is "pendiente" or "capturando")),
                    M(cobrados), M(devueltos), M(cobrados - devueltos)];
            } else {
                var opiniones = resenas.Where(r => r.EventoId == e.Id).ToList();
                valores = [N(opiniones.Count), opiniones.Count == 0 ? "Sin reseñas" : opiniones.Average(r => r.Calificacion).ToString("0.00", CultureInfo.InvariantCulture),
                    .. Enumerable.Range(1, 5).Select(n => N(opiniones.Count(r => r.Calificacion == n)))];
            }
            filas.Add([.. comunes, .. valores]);
        }
        indicadores["Eventos"] = N(eventos.Count);
        if (tipo == "eventos") {
            indicadores["Cupo total"] = N(eventos.Sum(e => (long)e.Cupo));
            indicadores["Reservas confirmadas"] = N(eventos.Sum(e => e.Reservas.Count(r => r.Estado == "confirmada")));
            indicadores["Veces guardados"] = N(favoritos.Count);
        } else if (tipo == "asistencia") {
            var confirmadas = eventos.Sum(e => e.Reservas.Count(r => r.Estado == "confirmada"));
            var entradas = eventos.Sum(e => e.Reservas.Count(r => r.Estado == "confirmada" && r.FechaEntrada != null));
            indicadores["Reservas confirmadas"] = N(confirmadas);
            indicadores["Entradas registradas"] = N(entradas);
            indicadores["Asistencia %"] = Porcentaje(entradas, confirmadas);
        } else if (tipo == "pagos") {
            var pagos = eventos.SelectMany(e => e.Pagos).ToList();
            var cobrado = pagos.Where(p => p.Estado is "pagado" or "reembolsando" or "reembolsado").Sum(p => (long)p.Centavos);
            var reembolsado = pagos.Where(p => p.Estado == "reembolsado").Sum(p => (long)p.Centavos);
            indicadores["Cobrado MXN (Sandbox)"] = M(cobrado);
            indicadores["Reembolsado MXN (Sandbox)"] = M(reembolsado);
            indicadores["Neto MXN (Sandbox)"] = M(cobrado - reembolsado);
        } else {
            indicadores["Reseñas"] = N(resenas.Count);
            indicadores["Promedio / 5"] = resenas.Count == 0 ? "Sin reseñas" : resenas.Average(r => r.Calificacion).ToString("0.00", CultureInfo.InvariantCulture);
        }
        return new ReporteRespuesta(tipo, DateTime.UtcNow, "", "", columnas, filas, indicadores);
    }
}

public record ReporteRespuesta(string Tipo, DateTime GeneradoUtc, string Desde, string Hasta,
    string[] Columnas, List<string[]> Filas, Dictionary<string, string> Indicadores);
