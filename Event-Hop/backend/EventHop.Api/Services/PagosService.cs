using System.Globalization;
using System.Text.Json;
using EventHop.Api.Data;
using EventHop.Api.Models;
using Microsoft.EntityFrameworkCore;
using MongoDB.Bson;

namespace EventHop.Api.Services;

public class PagosService(EventHopContext db, IPayPalService paypal, IConfiguration configuracion, ArchivoLog log)
{
    private DateTime AhoraLocal => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow,
        TimeZoneInfo.FindSystemTimeZoneById(configuracion["Eventos:ZonaHoraria"] ?? "America/Monterrey"));

    public async Task<(Evento evento, Pago pago)> Cargar(ObjectId eventoId, string pagoId, ObjectId usuario)
    {
        db.ChangeTracker.Clear();
        var evento = await db.Eventos.FirstOrDefaultAsync(e => e.Id == eventoId);
        var pago = evento?.Pagos.FirstOrDefault(p => p.Identificador == pagoId && p.UsuarioId == usuario);
        if (evento == null || pago == null) throw new OperacionException("Pago propio no encontrado.", 404);
        return (evento, pago);
    }

    // Los cambios de cupo, boleto e historial de pago son atómicos: un documento y una versión.
    public async Task<(Evento evento, Pago pago)> Cambiar(ObjectId eventoId, string pagoId,
        ObjectId usuario, Action<Evento, Pago> cambio)
    {
        for (var intento = 0; intento < 6; intento++)
        {
            var (evento, pago) = await Cargar(eventoId, pagoId, usuario);
            cambio(evento, pago);
            evento.Version++;
            try { await db.SaveChangesAsync(); return (evento, pago); }
            catch (DbUpdateConcurrencyException) { }
        }
        throw new OperacionException("El evento está cambiando. Recupera el pago desde Mis pagos.");
    }

    public async Task<object> Crear(ObjectId eventoId, ObjectId usuario, string proveedor = "paypal")
    {
        if (proveedor is not ("paypal" or "stripe")) throw new OperacionException("Método de pago inválido.", 400);
        for (var intento = 0; intento < 6; intento++)
        {
            db.ChangeTracker.Clear();
            var evento = await db.Eventos.FirstOrDefaultAsync(e => e.Id == eventoId);
            if (evento == null) throw new OperacionException("Evento no encontrado.", 404);
            var cafe = await db.Usuarios.AsNoTracking().FirstOrDefaultAsync(u => u.Id == evento.CafeteriaId);
            if (cafe == null || !cafe.Activo || cafe.TipoUsuario != "cafeteria" || cafe.EstadoSolicitud != "aprobada")
                throw new OperacionException("Evento no disponible.", 404);
            if (evento.EsGratis) throw new OperacionException("Este evento se reserva gratis, sin PayPal.");
            if (ReglasReservas.YaInicio(evento, AhoraLocal)) throw new OperacionException("El evento ya inició.");
            if (evento.Reservas.Any(r => r.UsuarioId == usuario && r.Estado == "confirmada"))
                throw new OperacionException("Ya tienes un boleto. Consulta Mis boletos.");
            var anterior = evento.Pagos.FirstOrDefault(p => p.UsuarioId == usuario &&
                (ReglasReservas.Retiene(p, DateTime.UtcNow) || p.Estado == "reembolsando"));
            if (anterior != null) return Vista(evento, anterior);
            if (ReglasReservas.Ocupados(evento) >= evento.Cupo) throw new OperacionException("El evento ya no tiene lugares disponibles.");
            // Límite defensivo de historial embebido; evita acercarse al máximo BSON con intentos ilimitados.
            if (evento.Pagos.Count >= 500) throw new OperacionException("Este evento requiere revisión del administrador antes de admitir más intentos de pago.");
            var pago = new Pago { UsuarioId = usuario, Proveedor = proveedor, Centavos = checked((int)Math.Round(evento.Costo * 100)) };
            if (pago.Centavos <= 0) throw new OperacionException("El precio del evento no es válido.");
            if (proveedor == "stripe" && pago.Centavos < 1000) throw new OperacionException("Stripe requiere al menos $10.00 MXN en esta integración de prueba. Usa PayPal para este evento.", 400);
            evento.Pagos.Add(pago);
            evento.Version++;
            try
            {
                await db.SaveChangesAsync();
                log.Escribir($"Inicio de compra Sandbox {pago.Identificador}, evento {eventoId}, usuario {usuario}");
                return Vista(evento, pago);
            }
            catch (DbUpdateConcurrencyException) { }
        }
        throw new OperacionException("El cupo está cambiando. Vuelve a intentarlo.");
    }

    public async Task<object> PrepararOrden(ObjectId eventoId, string pagoId, ObjectId usuario)
    {
        var (evento, pago) = await Cargar(eventoId, pagoId, usuario);
        ExigirPayPal(pago);
        if (pago.Estado != "pendiente" || pago.Vence <= DateTime.UtcNow)
            throw new OperacionException("Este intento no admite aprobación. Revisa Mis pagos.");
        if (ReglasReservas.YaInicio(evento, AhoraLocal)) throw new OperacionException("El evento ya inició.");
        if (pago.OrdenId != "") return Vista(evento, pago);
        var orden = await paypal.Crear(pago, eventoId.ToString());
        ValidarOrden(orden, pago);
        var ordenId = orden.GetProperty("id").GetString()!;
        var enlace = orden.GetProperty("links").EnumerateArray().FirstOrDefault(l =>
            l.GetProperty("rel").GetString() is "payer-action" or "approve");
        var url = enlace.ValueKind == JsonValueKind.Object ? enlace.GetProperty("href").GetString() : null;
        if (!UrlSegura(url)) throw new OperacionException("PayPal no devolvió un enlace Sandbox válido.", 502);
        var actualizado = await Cambiar(eventoId, pagoId, usuario, (e, p) => {
            if (p.Estado != "pendiente" || p.Vence <= DateTime.UtcNow)
                throw new OperacionException("El intento fue cancelado o venció. No se ha cobrado.");
            if (p.OrdenId != "" && p.OrdenId != ordenId) throw new OperacionException("La orden requiere revisión.");
            p.OrdenId = ordenId; p.UrlAprobacion = url!;
        });
        return Vista(actualizado.evento, actualizado.pago);
    }

    public async Task<object> Confirmar(ObjectId eventoId, string pagoId, ObjectId usuario)
    {
        ExigirPayPal((await Cargar(eventoId, pagoId, usuario)).pago);
        // Reclamar primero el lugar. Nunca cobrar y después intentar conseguir cupo.
        var actual = await Cambiar(eventoId, pagoId, usuario, (e, p) => {
            if (p.Estado == "pagado") return;
            if (p.Estado != "pendiente" && p.Estado != "capturando") throw new OperacionException("Este pago no puede confirmarse.");
            if (p.OrdenId == "") throw new OperacionException("Primero aprueba la compra en PayPal.");
            if (p.Estado == "pendiente") {
                if (p.Vence <= DateTime.UtcNow || ReglasReservas.YaInicio(e, AhoraLocal))
                    throw new OperacionException("La retención venció o el evento inició. No se ha cobrado.");
                p.Estado = "capturando";
            }
        });
        if (actual.pago.Estado == "pagado") return Vista(actual.evento, actual.pago);
        var orden = await paypal.Orden(actual.pago.OrdenId);
        ValidarOrden(orden, actual.pago);
        var captura = ObtenerCaptura(orden);
        if (captura == null && Estado(orden) == "APPROVED")
        {
            log.Escribir($"Inicio de captura Sandbox {pagoId}");
            orden = await paypal.Capturar(actual.pago);
            ValidarOrden(orden, actual.pago);
            captura = ObtenerCaptura(orden);
        }
        else if (captura == null && Estado(orden) is "CREATED" or "PAYER_ACTION_REQUIRED")
        {
            actual = await Cambiar(eventoId, pagoId, usuario, (e, p) => {
                if (p.Estado == "capturando") p.Estado = "pendiente";
            });
            throw new OperacionException("Todavía no aprobaste la compra en PayPal. Usa Continuar en PayPal.");
        }
        if (captura == null) throw new OperacionException("PayPal todavía no confirma la captura. Recupera el estado desde Mis pagos.");
        ValidarImporte(captura.Value.GetProperty("amount"), actual.pago.Centavos);
        var capturaId = captura.Value.GetProperty("id").GetString()!;
        var estadoCaptura = Estado(captura.Value);
        if (estadoCaptura is "DECLINED" or "FAILED")
        {
            actual = await Cambiar(eventoId, pagoId, usuario, (e, p) => {
                if (p.Estado == "capturando") p.Estado = "rechazado";
            });
            return Vista(actual.evento, actual.pago);
        }
        if (estadoCaptura != "COMPLETED") throw new OperacionException("El pago sigue pendiente en PayPal. No se emitió boleto; revisa Mis pagos.");
        actual = await Cambiar(eventoId, pagoId, usuario, (e, p) => {
            if (p.Estado == "pagado") return;
            if (p.Estado != "capturando") throw new OperacionException("La compra requiere conciliación; no vuelvas a pagar.");
            var reserva = e.Reservas.FirstOrDefault(r => r.UsuarioId == usuario);
            if (reserva?.Estado == "confirmada") throw new OperacionException("Ya hay un boleto confirmado; requiere conciliación.");
            if (reserva == null) { reserva = new Reserva { UsuarioId = usuario }; e.Reservas.Add(reserva); }
            reserva.Estado = "confirmada"; reserva.PagoId = pagoId;
            reserva.Codigo = "EH-" + Guid.NewGuid().ToString("N").ToUpperInvariant();
            reserva.FechaRegistro = DateTime.UtcNow; reserva.FechaCancelacion = null;
            reserva.FechaEntrada = null;
            p.CapturaId = capturaId; p.Estado = "pagado";
        });
        log.Escribir($"Fin de compra Sandbox {pagoId}: boleto confirmado");
        return Vista(actual.evento, actual.pago);
    }

    public async Task<object> Cancelar(ObjectId eventoId, string pagoId, ObjectId usuario)
    {
        ExigirPayPal((await Cargar(eventoId, pagoId, usuario)).pago);
        var actual = await Cambiar(eventoId, pagoId, usuario, (e, p) => {
            if (p.Estado is "cancelado" or "reembolsado" or "rechazado") return;
            if (p.Estado == "pendiente") { p.Estado = "cancelado"; return; }
            if (p.Estado == "capturando") throw new OperacionException("Primero recupera el cobro. No se puede liberar un lugar con resultado incierto.");
            if (p.Estado == "pagado") {
                if (e.Reservas.Any(r => r.PagoId == pagoId && r.FechaEntrada != null))
                    throw new OperacionException("No se puede reembolsar un boleto que ya se utilizó para entrar.");
                if (ReglasReservas.YaInicio(e, AhoraLocal)) throw new OperacionException("No se puede cancelar después del inicio del evento.");
                p.Estado = "reembolsando"; p.InicioReembolso = DateTime.UtcNow;
            }
        });
        if (actual.pago.Estado != "reembolsando") return Vista(actual.evento, actual.pago);
        log.Escribir($"Inicio de reembolso Sandbox {pagoId}");
        JsonElement reembolso;
        if (actual.pago.ReembolsoId != "") reembolso = await paypal.Reembolso(actual.pago.ReembolsoId);
        else
        {
            // Si se perdió una respuesta anterior, consultar primero el estado remoto.
            var captura = await paypal.Captura(actual.pago.CapturaId);
            ValidarImporte(captura.GetProperty("amount"), actual.pago.Centavos);
            if (Estado(captura) == "REFUNDED") return await TerminarReembolso(eventoId, pagoId, usuario);
            if (Estado(captura) != "COMPLETED") throw new OperacionException("La captura necesita revisión en PayPal. El lugar no se ha liberado.");
            if (actual.pago.InicioReembolso == null || DateTime.UtcNow - actual.pago.InicioReembolso > TimeSpan.FromHours(4))
                throw new OperacionException("El reembolso perdió su respuesta hace varias horas. Requiere revisión en PayPal; no se enviará otra devolución automáticamente.");
            reembolso = await paypal.Reembolsar(actual.pago);
            ValidarImporte(reembolso.GetProperty("amount"), actual.pago.Centavos);
            var id = reembolso.GetProperty("id").GetString()!;
            actual = await Cambiar(eventoId, pagoId, usuario, (e, p) => p.ReembolsoId = id);
        }
        ValidarImporte(reembolso.GetProperty("amount"), actual.pago.Centavos);
        if (Estado(reembolso) == "COMPLETED") return await TerminarReembolso(eventoId, pagoId, usuario);
        // PENDING/FAILED no se presentan como cancelación terminada.
        return Vista(actual.evento, actual.pago);
    }

    private async Task<object> TerminarReembolso(ObjectId eventoId, string pagoId, ObjectId usuario)
    {
        var actual = await Cambiar(eventoId, pagoId, usuario, (e, p) => {
            if (p.Estado == "reembolsado") return;
            if (p.Estado != "reembolsando") throw new OperacionException("El reembolso requiere revisión.");
            var reserva = e.Reservas.FirstOrDefault(r => r.UsuarioId == usuario && r.PagoId == pagoId);
            if (reserva == null) throw new OperacionException("No se encontró el boleto asociado al pago.");
            reserva.Estado = "cancelada"; reserva.FechaCancelacion = DateTime.UtcNow;
            p.Estado = "reembolsado";
        });
        log.Escribir($"Fin de reembolso Sandbox {pagoId}: lugar liberado");
        return Vista(actual.evento, actual.pago);
    }

    public static string Estado(JsonElement json) => json.GetProperty("status").GetString()!;
    public static bool UrlSegura(string? url) => Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
        uri.Scheme == "https" && uri.Host is "www.sandbox.paypal.com" or "sandbox.paypal.com" && uri.UserInfo == "";
    public static void ValidarImporte(JsonElement importe, int centavos)
    {
        if (importe.GetProperty("currency_code").GetString() != "MXN" ||
            !decimal.TryParse(importe.GetProperty("value").GetString(), NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture, out var cantidad) || cantidad != centavos / 100m)
            throw new OperacionException("El importe o moneda de PayPal no coincide. No se emitió boleto; requiere revisión.", 502);
    }
    public static void ValidarOrden(JsonElement orden, Pago pago)
    {
        var unidades = orden.GetProperty("purchase_units");
        if (orden.GetProperty("intent").GetString() != "CAPTURE" || unidades.GetArrayLength() != 1 ||
            unidades[0].GetProperty("custom_id").GetString() != pago.Identificador ||
            (pago.OrdenId != "" && orden.GetProperty("id").GetString() != pago.OrdenId))
            throw new OperacionException("La orden PayPal no coincide con esta compra.", 502);
        ValidarImporte(unidades[0].GetProperty("amount"), pago.Centavos);
    }
    private static JsonElement? ObtenerCaptura(JsonElement orden)
    {
        var unidad = orden.GetProperty("purchase_units")[0];
        if (!unidad.TryGetProperty("payments", out var pagos) || !pagos.TryGetProperty("captures", out var capturas) || capturas.GetArrayLength() == 0) return null;
        if (capturas.GetArrayLength() != 1) throw new OperacionException("Hay varias capturas: requiere revisión.", 502);
        return capturas[0];
    }
    private static void ExigirPayPal(Pago pago) {
        if (pago.Proveedor == "stripe") throw new OperacionException("Este pago se gestiona con Stripe, no con PayPal.", 409);
    }
    public static object Vista(Evento evento, Pago pago) => new {
        eventoId = evento.Id.ToString(), pagoId = pago.Identificador, evento.Titulo,
        proveedor = pago.Proveedor == "stripe" ? "stripe" : "paypal",
        estado = pago.Proveedor != "stripe" && pago.Estado == "pendiente" && pago.Vence <= DateTime.UtcNow ? "vencido" : pago.Estado,
        importe = pago.Centavos / 100m, moneda = "MXN", sandbox = true, pago.Vence,
        boletoUtilizado = evento.Reservas.Any(r => r.PagoId == pago.Identificador && r.UsuarioId == pago.UsuarioId && r.FechaEntrada != null),
        urlAprobacion = pago.Estado == "pendiente" && (pago.Proveedor == "stripe" || pago.Vence > DateTime.UtcNow) ? pago.UrlAprobacion : ""
    };
}
