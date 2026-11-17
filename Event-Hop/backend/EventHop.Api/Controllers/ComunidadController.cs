using System.Security.Claims;
using EventHop.Api.Data;
using EventHop.Api.Models;
using EventHop.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MongoDB.Bson;

namespace EventHop.Api.Controllers;

[ApiController, Authorize(Roles = "usuario,cafeteria"), Route("api/comunidad/{eventoId}")]
public class ComunidadController(EventHopContext db, ArchivoLog log) : ControllerBase
{
    private ObjectId MiId => ObjectId.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    private async Task<Evento> Cargar(string eventoId, bool propio = false)
    {
        if (!ObjectId.TryParse(eventoId, out var id))
            throw new OperacionException("Identificador inválido.", 400);
        var evento = await db.Eventos.AsNoTracking().FirstOrDefaultAsync(e => e.Id == id);
        if (evento == null || (propio && evento.CafeteriaId != MiId))
            throw new OperacionException("Evento no encontrado.", 404);
        var cafe = await db.Usuarios.AsNoTracking().FirstOrDefaultAsync(u => u.Id == evento.CafeteriaId);
        if (cafe == null || !cafe.Activo || cafe.EstadoSolicitud != "aprobada" || cafe.TipoUsuario != "cafeteria")
            throw new OperacionException("Evento no disponible.", 404);
        return evento;
    }

    public static bool PuedeResenar(Evento evento, ObjectId usuario)
    {
        var reserva = evento.Reservas.FirstOrDefault(r => r.UsuarioId == usuario && r.Estado == "confirmada");
        return reserva?.FechaEntrada != null && (string.IsNullOrEmpty(reserva.PagoId) ||
            evento.Pagos.Any(p => p.Identificador == reserva.PagoId && p.UsuarioId == usuario && p.Estado == "pagado"));
    }

    [HttpGet]
    public async Task<IActionResult> Consultar(string eventoId)
    {
        var evento = await Cargar(eventoId, User.IsInRole("cafeteria"));
        var propio = MiId;
        var esCafe = User.IsInRole("cafeteria");
        var puedeVerAvisos = esCafe || evento.Reservas.Any(r => r.UsuarioId == propio && r.Estado == "confirmada");
        var resenas = await db.Resenas.AsNoTracking().Where(r => r.EventoId == evento.Id)
            .OrderByDescending(r => r.FechaActualizacion).ToListAsync();
        var ids = resenas.Select(r => r.UsuarioId).ToList();
        var usuarios = await db.Usuarios.AsNoTracking().Where(u => ids.Contains(u.Id)).ToListAsync();
        var nombres = usuarios.ToDictionary(u => u.Id, u => u.Nombre);
        var avisos = puedeVerAvisos ? await db.Avisos.AsNoTracking().Where(a => a.EventoId == evento.Id)
            .OrderByDescending(a => a.FechaActualizacion).ToListAsync() : new List<Aviso>();
        return Ok(new {
            titulo = evento.Titulo, puedeVerAvisos, puedeAvisar = esCafe,
            puedeResenar = !esCafe && PuedeResenar(evento, propio),
            resenas = resenas.Select(r => new { r.Id, r.Calificacion, r.Comentario, r.FechaActualizacion,
                autor = nombres.GetValueOrDefault(r.UsuarioId, "Usuario"), esMia = r.UsuarioId == propio }),
            avisos = avisos.Select(a => new { a.Id, a.Titulo, a.Contenido, a.FechaActualizacion })
        });
    }

    [HttpPut("resena"), Authorize(Roles = "usuario")]
    public async Task<IActionResult> GuardarResena(string eventoId, ResenaSolicitud datos)
    {
        var evento = await Cargar(eventoId);
        if (!PuedeResenar(evento, MiId))
            return Conflict(new { mensaje = "Solo puedes reseñar un evento al que ya registraste tu entrada y con boleto válido." });
        if (datos.Calificacion < 1 || datos.Calificacion > 5 || string.IsNullOrWhiteSpace(datos.Comentario) ||
            datos.Comentario.Trim().Length < 3 || datos.Comentario.Length > 1000)
            return BadRequest(new { mensaje = "Califica de 1 a 5 y escribe un comentario de 3 a 1000 caracteres." });
        var clave = evento.Id + ":" + MiId;
        var resena = await db.Resenas.FirstOrDefaultAsync(r => r.Id == clave);
        var nueva = resena == null;
        resena ??= new Resena { Id = clave, EventoId = evento.Id, UsuarioId = MiId };
        resena.Calificacion = datos.Calificacion; resena.Comentario = datos.Comentario.Trim();
        resena.FechaActualizacion = DateTime.UtcNow;
        if (nueva) db.Resenas.Add(resena);
        await db.SaveChangesAsync(); log.Escribir($"Reseña guardada: evento {evento.Id}, usuario {MiId}");
        return Ok(new { mensaje = "Reseña guardada." });
    }

    [HttpDelete("resena"), Authorize(Roles = "usuario")]
    public async Task<IActionResult> EliminarResena(string eventoId)
    {
        var evento = await Cargar(eventoId);
        var clave = evento.Id + ":" + MiId;
        var resena = await db.Resenas.FirstOrDefaultAsync(r => r.Id == clave);
        if (resena == null) return NotFound(new { mensaje = "No tienes una reseña en este evento." });
        db.Resenas.Remove(resena); await db.SaveChangesAsync();
        log.Escribir($"Reseña eliminada: evento {evento.Id}, usuario {MiId}"); return NoContent();
    }

    [HttpPost("avisos"), Authorize(Roles = "cafeteria")]
    public async Task<IActionResult> CrearAviso(string eventoId, AvisoSolicitud datos)
    {
        var evento = await Cargar(eventoId, true);
        var error = ValidarAviso(datos); if (error != null) return BadRequest(new { mensaje = error });
        var aviso = new Aviso { EventoId = evento.Id, CafeteriaId = MiId,
            Titulo = datos.Titulo.Trim(), Contenido = datos.Contenido.Trim() };
        db.Avisos.Add(aviso); await db.SaveChangesAsync();
        log.Escribir($"Aviso creado: {aviso.Id}, evento {evento.Id}");
        return StatusCode(201, new { mensaje = "Aviso publicado.", id = aviso.Id });
    }

    [HttpPut("avisos/{id}"), Authorize(Roles = "cafeteria")]
    public async Task<IActionResult> EditarAviso(string eventoId, string id, AvisoSolicitud datos)
    {
        var evento = await Cargar(eventoId, true);
        var error = ValidarAviso(datos); if (error != null) return BadRequest(new { mensaje = error });
        var propio = MiId;
        var aviso = await db.Avisos.FirstOrDefaultAsync(a => a.Id == id && a.EventoId == evento.Id && a.CafeteriaId == propio);
        if (aviso == null) return NotFound(new { mensaje = "Aviso propio no encontrado." });
        aviso.Titulo = datos.Titulo.Trim(); aviso.Contenido = datos.Contenido.Trim(); aviso.FechaActualizacion = DateTime.UtcNow;
        await db.SaveChangesAsync(); log.Escribir($"Aviso editado: {id}");
        return Ok(new { mensaje = "Aviso actualizado." });
    }

    [HttpDelete("avisos/{id}"), Authorize(Roles = "cafeteria")]
    public async Task<IActionResult> EliminarAviso(string eventoId, string id)
    {
        var evento = await Cargar(eventoId, true); var propio = MiId;
        var aviso = await db.Avisos.FirstOrDefaultAsync(a => a.Id == id && a.EventoId == evento.Id && a.CafeteriaId == propio);
        if (aviso == null) return NotFound(new { mensaje = "Aviso propio no encontrado." });
        db.Avisos.Remove(aviso); await db.SaveChangesAsync(); log.Escribir($"Aviso eliminado: {id}");
        return NoContent();
    }

    public static string? ValidarAviso(AvisoSolicitud d) =>
        string.IsNullOrWhiteSpace(d.Titulo) || d.Titulo.Trim().Length < 3 || d.Titulo.Length > 100 ||
        string.IsNullOrWhiteSpace(d.Contenido) || d.Contenido.Trim().Length < 3 || d.Contenido.Length > 2000
            ? "Escribe título de 3 a 100 caracteres y contenido de 3 a 2000." : null;
}
