using System.Security.Claims;
using EventHop.Api.Data;
using EventHop.Api.Models;
using EventHop.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MongoDB.Bson;

namespace EventHop.Api.Controllers;

[ApiController, Authorize(Roles = "usuario"), Route("api/reservas")]
public class ReservasController(EventHopContext baseDatos, ArchivoLog log, IConfiguration configuracion) : ControllerBase
{
    private ObjectId MiId => ObjectId.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private DateTime AhoraLocal => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow,
        TimeZoneInfo.FindSystemTimeZoneById(configuracion["Eventos:ZonaHoraria"] ?? "America/Monterrey"));

    [HttpGet]
    public async Task<IActionResult> Consultar()
    {
        var propio = MiId;
        var eventos = await baseDatos.Eventos.AsNoTracking()
            .Where(e => e.Reservas.Any(r => r.UsuarioId == propio)).ToListAsync();
        var cafes = await baseDatos.Usuarios.AsNoTracking().Where(u => u.TipoUsuario == "cafeteria").ToListAsync();
        var nombres = cafes.ToDictionary(c => c.Id, c => c.Nombre);
        return Ok(eventos.OrderBy(e => e.Fecha).ThenBy(e => e.Hora).Select(e => new {
            eventoId = e.Id.ToString(), e.Titulo, cafeteria = nombres.GetValueOrDefault(e.CafeteriaId, "Cafetería"),
            e.Fecha, e.Hora, e.Direccion, e.ImagenUrl,
            codigo = e.Reservas.First(r => r.UsuarioId == propio).Codigo,
            estado = e.Reservas.First(r => r.UsuarioId == propio).Estado,
            fechaEntrada = e.Reservas.First(r => r.UsuarioId == propio).FechaEntrada,
            pagoId = e.Reservas.First(r => r.UsuarioId == propio).PagoId,
            esGratis = e.Reservas.First(r => r.UsuarioId == propio).PagoId == ""
        }));
    }

    // No recibimos usuarioId ni cupo del navegador: ambos se resuelven en el servidor.
    [HttpPut("{eventoId}")]
    public async Task<IActionResult> Reservar(string eventoId)
    {
        if (!ObjectId.TryParse(eventoId, out var id)) return BadRequest(new { mensaje = "Identificador inválido." });
        var propio = MiId;
        for (var intento = 0; intento < 3; intento++)
        {
            baseDatos.ChangeTracker.Clear();
            var evento = await baseDatos.Eventos.FirstOrDefaultAsync(e => e.Id == id);
            if (evento == null) return NotFound(new { mensaje = "Evento no encontrado." });
            var cafe = await baseDatos.Usuarios.AsNoTracking().FirstOrDefaultAsync(u => u.Id == evento.CafeteriaId);
            if (cafe == null || !cafe.Activo || cafe.TipoUsuario != "cafeteria" || cafe.EstadoSolicitud != "aprobada")
                return NotFound(new { mensaje = "Evento no disponible." });
            var error = ReglasReservas.Validar(evento, propio, AhoraLocal);
            if (error != null) return Conflict(new { mensaje = error });
            var reserva = evento.Reservas.FirstOrDefault(r => r.UsuarioId == propio);
            if (reserva?.Estado == "confirmada") return Ok(new { mensaje = "Ya tienes un lugar reservado.", reserva.Codigo });
            if (reserva == null) { reserva = new Reserva { UsuarioId = propio }; evento.Reservas.Add(reserva); }
            else
            {
                reserva.Estado = "confirmada";
                reserva.Codigo = "EH-" + Guid.NewGuid().ToString("N").ToUpperInvariant();
                reserva.FechaRegistro = DateTime.UtcNow;
                reserva.FechaCancelacion = null;
            }
            evento.Version++;
            reserva.PagoId = "";
            reserva.FechaEntrada = null;
            try
            {
                await baseDatos.SaveChangesAsync();
                log.Escribir($"Reserva confirmada: evento {id}, usuario {propio}");
                return Ok(new { mensaje = "Lugar reservado. Consulta Mis boletos.", reserva.Codigo });
            }
            catch (DbUpdateConcurrencyException) { /* Releer el cupo actualizado antes de reintentar. */ }
        }
        return Conflict(new { mensaje = "El cupo está cambiando. Vuelve a intentarlo." });
    }

    [HttpDelete("{eventoId}")]
    public async Task<IActionResult> Cancelar(string eventoId)
    {
        if (!ObjectId.TryParse(eventoId, out var id)) return BadRequest(new { mensaje = "Identificador inválido." });
        var propio = MiId;
        for (var intento = 0; intento < 3; intento++)
        {
            baseDatos.ChangeTracker.Clear();
            var evento = await baseDatos.Eventos.FirstOrDefaultAsync(e => e.Id == id);
            var reserva = evento?.Reservas.FirstOrDefault(r => r.UsuarioId == propio);
            if (evento == null || reserva == null) return NotFound(new { mensaje = "Reserva propia no encontrada." });
            if (reserva.PagoId != "") return Conflict(new { mensaje = "Cancela este boleto desde su pago PayPal para solicitar el reembolso." });
            if (reserva.Estado == "cancelada") return NoContent();
            if (reserva.FechaEntrada != null) return Conflict(new { mensaje = "No puedes cancelar un boleto que ya se utilizó para entrar." });
            if (ReglasReservas.YaInicio(evento, AhoraLocal)) return Conflict(new { mensaje = "No puedes cancelar después del inicio del evento." });
            reserva.Estado = "cancelada";
            reserva.FechaCancelacion = DateTime.UtcNow;
            evento.Version++;
            try
            {
                await baseDatos.SaveChangesAsync();
                log.Escribir($"Reserva cancelada: evento {id}, usuario {propio}");
                return NoContent();
            }
            catch (DbUpdateConcurrencyException) { }
        }
        return Conflict(new { mensaje = "La reserva cambió. Recarga y vuelve a intentarlo." });
    }
}
