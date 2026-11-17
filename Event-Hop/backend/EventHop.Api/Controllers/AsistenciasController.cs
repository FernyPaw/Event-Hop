using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using EventHop.Api.Data;
using EventHop.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MongoDB.Bson;

namespace EventHop.Api.Controllers;

[ApiController, Authorize(Roles = "cafeteria"), Route("api/asistencias")]
public class AsistenciasController(EventHopContext db, AsistenciasService asistencias) : ControllerBase
{
    private ObjectId MiId => ObjectId.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet("{eventoId}")]
    public async Task<IActionResult> Consultar(string eventoId)
    {
        if (!ObjectId.TryParse(eventoId, out var id)) return BadRequest(new { mensaje = "Identificador inválido." });
        var evento = await asistencias.Cargar(id, MiId);
        var ids = evento.Reservas.Select(r => r.UsuarioId).Distinct().ToList();
        var usuarios = await db.Usuarios.AsNoTracking().Where(u => ids.Contains(u.Id)).ToListAsync();
        var nombres = usuarios.ToDictionary(u => u.Id, u => u.Nombre);
        return Ok(new {
            eventoId, evento.Titulo, evento.Fecha, evento.Hora,
            confirmadas = evento.Reservas.Count(r => r.Estado == "confirmada"),
            entradas = evento.Reservas.Count(r => r.FechaEntrada != null),
            asistentes = evento.Reservas.OrderBy(r => r.FechaRegistro).Select(r => new {
                nombre = nombres.GetValueOrDefault(r.UsuarioId, "Usuario no disponible"),
                r.Codigo, r.Estado, r.FechaEntrada,
                pago = r.PagoId == "" ? "gratuito" : evento.Pagos.FirstOrDefault(p =>
                    p.Identificador == r.PagoId && p.UsuarioId == r.UsuarioId)?.Estado ?? "sin confirmar"
            })
        });
    }

    [HttpPost("{eventoId}")]
    public async Task<IActionResult> Registrar(string eventoId, EntradaSolicitud solicitud)
    {
        if (!ObjectId.TryParse(eventoId, out var id)) return BadRequest(new { mensaje = "Identificador inválido." });
        return Ok(await asistencias.Registrar(id, MiId, solicitud.Codigo.Trim().ToUpperInvariant()));
    }
}

public class EntradaSolicitud
{
    [Required, StringLength(50), RegularExpression("^\\s*[Ee][Hh]-[a-fA-F0-9]{32}\\s*$")]
    public string Codigo { get; set; } = "";
}
