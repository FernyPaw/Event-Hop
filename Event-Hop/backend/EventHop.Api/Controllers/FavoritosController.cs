using System.Security.Claims;
using EventHop.Api.Data;
using EventHop.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MongoDB.Bson;

namespace EventHop.Api.Controllers;

[ApiController, Authorize(Roles = "usuario"), Route("api/favoritos")]
public class FavoritosController(EventHopContext baseDatos) : ControllerBase
{
    private ObjectId MiId => ObjectId.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet]
    public async Task<IActionResult> Consultar()
    {
        var propio = MiId;
        var favoritos = await baseDatos.Favoritos.AsNoTracking().Where(f => f.UsuarioId == propio).ToListAsync();
        var ids = favoritos.Select(f => f.EventoId).ToArray();
        var cafes = await baseDatos.Usuarios.AsNoTracking()
            .Where(u => u.TipoUsuario == "cafeteria" && u.Activo && u.EstadoSolicitud == "aprobada").ToListAsync();
        var nombres = cafes.ToDictionary(c => c.Id, c => c.Nombre);
        var eventos = await baseDatos.Eventos.AsNoTracking().Where(e => ids.Contains(e.Id)).ToListAsync();
        return Ok(eventos.Where(e => nombres.ContainsKey(e.CafeteriaId))
            .Select(e => EventosController.Respuesta(e, nombres[e.CafeteriaId])));
    }

    [HttpPut("{eventoId}")]
    public async Task<IActionResult> Guardar(string eventoId)
    {
        if (!ObjectId.TryParse(eventoId, out var id)) return BadRequest(new { mensaje = "Identificador inválido." });
        var evento = await baseDatos.Eventos.AsNoTracking().FirstOrDefaultAsync(e => e.Id == id);
        if (evento == null) return NotFound(new { mensaje = "Evento no encontrado." });
        var cafe = await baseDatos.Usuarios.AsNoTracking().FirstOrDefaultAsync(u => u.Id == evento.CafeteriaId);
        if (cafe == null || !cafe.Activo || cafe.EstadoSolicitud != "aprobada" || cafe.TipoUsuario != "cafeteria")
            return NotFound(new { mensaje = "Evento no disponible." });
        var clave = MiId + ":" + id;
        if (await baseDatos.Favoritos.AnyAsync(f => f.Id == clave)) return NoContent();
        baseDatos.Favoritos.Add(new Favorito { Id = clave, UsuarioId = MiId, EventoId = id });
        try { await baseDatos.SaveChangesAsync(); }
        catch (DbUpdateException error) when (error.ToString().Contains("E11000", StringComparison.Ordinal))
        { /* Dos clics simultáneos significan el mismo favorito, no dos documentos. */ }
        return NoContent();
    }

    [HttpDelete("{eventoId}")]
    public async Task<IActionResult> Quitar(string eventoId)
    {
        if (!ObjectId.TryParse(eventoId, out var id)) return BadRequest(new { mensaje = "Identificador inválido." });
        var clave = MiId + ":" + id;
        var favorito = await baseDatos.Favoritos.FirstOrDefaultAsync(f => f.Id == clave);
        if (favorito == null) return NoContent();
        baseDatos.Favoritos.Remove(favorito);
        try { await baseDatos.SaveChangesAsync(); }
        catch (DbUpdateConcurrencyException) { /* Ya se quitó en otra petición. */ }
        return NoContent();
    }
}
