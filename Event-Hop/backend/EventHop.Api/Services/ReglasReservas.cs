using EventHop.Api.Models;
using MongoDB.Bson;
using System.Globalization;

namespace EventHop.Api.Services;

public static class ReglasReservas
{
    public static bool Retiene(Pago pago, DateTime ahoraUtc) => pago.Estado == "capturando" ||
        (pago.Estado == "pendiente" && (pago.Proveedor == "stripe" || pago.Vence > ahoraUtc));
    public static int Ocupados(Evento evento) => evento.Reservas.Count(r => r.Estado == "confirmada") +
        evento.Pagos.Count(p => Retiene(p, DateTime.UtcNow));
    public static bool YaInicio(Evento evento, DateTime ahoraLocal) =>
        string.CompareOrdinal(evento.Fecha + "T" + evento.Hora,
            ahoraLocal.ToString("yyyy-MM-ddTHH:mm", CultureInfo.InvariantCulture)) <= 0;

    public static string? Validar(Evento evento, ObjectId usuario, DateTime ahoraLocal)
    {
        if (!evento.EsGratis) return "Los pagos de prueba todavía no están conectados.";
        if (YaInicio(evento, ahoraLocal)) return "El evento ya inició; no admite nuevas reservas.";
        if (evento.Reservas.Any(r => r.UsuarioId == usuario && r.Estado == "confirmada")) return null;
        if (Ocupados(evento) >= evento.Cupo) return "El evento ya no tiene lugares disponibles.";
        return null;
    }
}
