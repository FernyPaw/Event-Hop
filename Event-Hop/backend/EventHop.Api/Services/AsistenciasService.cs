using EventHop.Api.Data;
using EventHop.Api.Models;
using Microsoft.EntityFrameworkCore;
using MongoDB.Bson;

namespace EventHop.Api.Services;

public class AsistenciasService(EventHopContext db, IConfiguration configuracion, ArchivoLog log)
{
    public async Task<Evento> Cargar(ObjectId eventoId, ObjectId cafeteriaId)
    {
        db.ChangeTracker.Clear();
        return await db.Eventos.FirstOrDefaultAsync(e => e.Id == eventoId && e.CafeteriaId == cafeteriaId)
            ?? throw new OperacionException("Evento propio no encontrado.", 404);
    }

    // Regla compartida por consulta y escritura. La fecha del evento es local.
    public static string? Validar(Evento evento, Reserva reserva, DateTime ahoraLocal)
    {
        if (reserva.Estado != "confirmada") return "El boleto está cancelado y no permite el acceso.";
        if (reserva.FechaEntrada != null) return "Este boleto ya fue utilizado para entrar.";
        if (reserva.PagoId != "" && !evento.Pagos.Any(p => p.Identificador == reserva.PagoId &&
            p.UsuarioId == reserva.UsuarioId && p.Estado == "pagado"))
            return "El pago no está confirmado o tiene un reembolso en proceso.";
        if (evento.Fecha != ahoraLocal.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture))
            return "Solo puedes registrar la entrada el día del evento.";
        return null;
    }

    public async Task<object> Registrar(ObjectId eventoId, ObjectId cafeteriaId, string codigo)
    {
        for (var intento = 0; intento < 6; intento++)
        {
            var evento = await Cargar(eventoId, cafeteriaId);
            var reserva = evento.Reservas.FirstOrDefault(r => r.Codigo == codigo);
            if (reserva == null) throw new OperacionException("Código no encontrado en este evento.", 404);
            var ahora = DateTime.UtcNow;
            var local = TimeZoneInfo.ConvertTimeFromUtc(ahora,
                TimeZoneInfo.FindSystemTimeZoneById(configuracion["Eventos:ZonaHoraria"] ?? "America/Monterrey"));
            var error = Validar(evento, reserva, local);
            if (error != null) throw new OperacionException(error);
            reserva.FechaEntrada = ahora;
            evento.Version++;
            try
            {
                await db.SaveChangesAsync();
                log.Escribir($"Entrada registrada: evento {eventoId}, usuario {reserva.UsuarioId}, cafetería {cafeteriaId}");
                return new { mensaje = "Entrada registrada correctamente.", fechaEntrada = ahora };
            }
            catch (DbUpdateConcurrencyException) { /* Releer también cancelaciones y reembolsos. */ }
        }
        throw new OperacionException("El boleto cambió. Actualiza la lista antes de volver a intentarlo.");
    }
}
