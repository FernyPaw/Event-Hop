using System.Text.Json;
using EventHop.Api.Models;
using MongoDB.Bson;

namespace EventHop.Api.Services;

public class PagosStripeService(PagosService pagos, IStripeService stripe, IConfiguration config, ArchivoLog log)
{
    private DateTime AhoraLocal => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow,
        TimeZoneInfo.FindSystemTimeZoneById(config["Eventos:ZonaHoraria"] ?? "America/Monterrey"));
    public Task<object> Crear(ObjectId evento, ObjectId usuario) {
        stripe.ComprobarConfiguracion();
        return pagos.Crear(evento, usuario, "stripe");
    }
    private async Task<(Evento evento, Pago pago)> Cargar(ObjectId eventoId, string pagoId, ObjectId usuario) {
        var actual = await pagos.Cargar(eventoId, pagoId, usuario);
        if (actual.pago.Proveedor != "stripe") throw new OperacionException("Este intento no pertenece a Stripe.");
        return actual;
    }
    private static string Texto(JsonElement j, string campo) => j.TryGetProperty(campo, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString()! : "";
    public static bool UrlSegura(string? url) => Uri.TryCreate(url, UriKind.Absolute, out var u) &&
        u.Scheme == "https" && u.Host == "checkout.stripe.com" && u.UserInfo == "" && u.IsDefaultPort;
    public static void ValidarSesion(JsonElement j, Pago p, ObjectId evento) {
        if (!j.TryGetProperty("livemode", out var live) || live.GetBoolean() || !Texto(j, "id").StartsWith("cs_test_", StringComparison.Ordinal) ||
            Texto(j, "mode") != "payment" || Texto(j, "currency") != "mxn" || j.GetProperty("amount_total").GetInt64() != p.Centavos ||
            Texto(j, "client_reference_id") != p.Identificador ||
            Texto(j.GetProperty("metadata"), "pagoId") != p.Identificador ||
            Texto(j.GetProperty("metadata"), "eventoId") != evento.ToString() ||
            Texto(j.GetProperty("metadata"), "usuarioId") != p.UsuarioId.ToString() ||
            (p.OrdenId != "" && Texto(j, "id") != p.OrdenId))
            throw new OperacionException("La sesión Stripe no coincide con este pago de prueba. No se emitió boleto.", 502);
    }
    public static void ValidarIntencion(JsonElement j, Pago p, string id) {
        if (j.GetProperty("livemode").GetBoolean() || Texto(j, "id") != id ||
            Texto(j, "currency") != "mxn" || j.GetProperty("amount").GetInt64() != p.Centavos ||
            j.GetProperty("amount_received").GetInt64() != p.Centavos || Texto(j, "status") != "succeeded" ||
            Texto(j.GetProperty("metadata"), "pagoId") != p.Identificador)
            throw new OperacionException("Stripe no acredita el importe de este intento. Requiere revisión.", 502);
    }
    private async Task<(Evento evento, Pago pago)> AsegurarSesion(ObjectId eventoId, string pagoId, ObjectId usuario) {
        var actual = await Cargar(eventoId, pagoId, usuario);
        if (actual.pago.OrdenId != "") return actual;
        if (actual.pago.Estado != "pendiente") throw new OperacionException("Este pago no admite crear una sesión.");
        // No reutilizar una clave después de la ventana mínima de conservación de Stripe.
        if (actual.pago.InicioSesionStripe != null && DateTime.UtcNow - actual.pago.InicioSesionStripe > TimeSpan.FromHours(23))
            throw new OperacionException("La respuesta de creación lleva muchas horas sin recuperarse. Revisa Stripe antes de resolver este intento; no se creará otra sesión.");
        if (actual.pago.InicioSesionStripe == null)
            actual = await pagos.Cambiar(eventoId, pagoId, usuario, (e,p) => p.InicioSesionStripe ??= DateTime.UtcNow);
        var sesion = await stripe.Crear(actual.pago, eventoId.ToString());
        ValidarSesion(sesion, actual.pago, eventoId);
        var url = Texto(sesion, "url");
        if (Texto(sesion, "status") == "open" && !UrlSegura(url)) throw new OperacionException("Stripe no devolvió un enlace de Checkout válido.", 502);
        return await pagos.Cambiar(eventoId, pagoId, usuario, (e, p) => {
            if (p.Estado != "pendiente") throw new OperacionException("El estado cambió. Recupera el pago.");
            if (p.OrdenId != "" && p.OrdenId != Texto(sesion, "id")) throw new OperacionException("Hay otra sesión; requiere revisión.");
            p.OrdenId = Texto(sesion, "id"); p.UrlAprobacion = url;
            p.Vence = DateTimeOffset.FromUnixTimeSeconds(sesion.GetProperty("expires_at").GetInt64()).UtcDateTime;
        });
    }
    public async Task<object> Orden(ObjectId eventoId, string pagoId, ObjectId usuario) {
        var actual = await Cargar(eventoId, pagoId, usuario);
        if (actual.pago.Estado != "pendiente" || ReglasReservas.YaInicio(actual.evento, AhoraLocal))
            throw new OperacionException("Este intento no admite abrir Checkout. Recupera o cancela desde Mis pagos.");
        actual = await AsegurarSesion(eventoId, pagoId, usuario);
        var j = await stripe.Sesion(actual.pago.OrdenId); ValidarSesion(j, actual.pago, eventoId);
        if (Texto(j, "status") != "open") return await Confirmar(eventoId, pagoId, usuario);
        if (!UrlSegura(Texto(j, "url"))) throw new OperacionException("Enlace Checkout inválido.", 502);
        actual = await pagos.Cambiar(eventoId, pagoId, usuario, (e,p) => p.UrlAprobacion = Texto(j, "url"));
        return PagosService.Vista(actual.evento, actual.pago);
    }
    public async Task<object> Confirmar(ObjectId eventoId, string pagoId, ObjectId usuario) {
        var actual = await Cargar(eventoId, pagoId, usuario);
        if (actual.pago.Estado is "pagado" or "reembolsado" or "cancelado" or "reembolsando") return PagosService.Vista(actual.evento, actual.pago);
        actual = await AsegurarSesion(eventoId, pagoId, usuario);
        var j = await stripe.Sesion(actual.pago.OrdenId); ValidarSesion(j, actual.pago, eventoId);
        var estado = Texto(j, "status"); var pagoEstado = Texto(j, "payment_status");
        if (estado == "expired" && pagoEstado == "unpaid") {
            actual = await pagos.Cambiar(eventoId, pagoId, usuario, (e,p) => {
                if (p.Estado != "pendiente") throw new OperacionException("El pago cambió; revisa su estado.");
                p.Estado = "cancelado"; p.UrlAprobacion = "";
            });
        } else if (estado == "complete" && pagoEstado == "paid") {
            var intencion = Texto(j, "payment_intent");
            if (!intencion.StartsWith("pi_", StringComparison.Ordinal)) throw new OperacionException("Stripe no devolvió una intención válida.", 502);
            ValidarIntencion(await stripe.Intencion(intencion), actual.pago, intencion);
            actual = await pagos.Cambiar(eventoId, pagoId, usuario, (e,p) => {
                if (p.Estado == "pagado") return;
                if (p.Estado != "pendiente") throw new OperacionException("El pago requiere conciliación. No vuelvas a pagar.");
                var reserva = e.Reservas.FirstOrDefault(r => r.UsuarioId == usuario);
                if (reserva?.Estado == "confirmada") throw new OperacionException("Ya tienes un boleto; requiere revisión.");
                if (reserva == null) { reserva = new Reserva { UsuarioId = usuario }; e.Reservas.Add(reserva); }
                reserva.Estado = "confirmada"; reserva.PagoId = pagoId; reserva.FechaEntrada = null;
                reserva.FechaCancelacion = null; reserva.FechaRegistro = DateTime.UtcNow;
                reserva.Codigo = "EH-" + Guid.NewGuid().ToString("N").ToUpperInvariant();
                p.Estado = "pagado"; p.CapturaId = intencion; p.UrlAprobacion = "";
            });
            log.Escribir($"Fin de confirmación Stripe prueba {pagoId}: boleto emitido");
        } else throw new OperacionException("Stripe todavía no confirma el pago. Puedes continuar en Checkout o revisar el mismo intento; el lugar permanece retenido.");
        return PagosService.Vista(actual.evento, actual.pago);
    }
    private static void ValidarReembolso(JsonElement j, Pago p) {
        if (Texto(j, "payment_intent") != p.CapturaId || Texto(j, "currency") != "mxn" ||
            j.GetProperty("amount").GetInt64() != p.Centavos || Texto(j.GetProperty("metadata"), "pagoId") != p.Identificador ||
            !Texto(j, "id").StartsWith("re_", StringComparison.Ordinal))
            throw new OperacionException("El reembolso Stripe no corresponde al pago completo.", 502);
    }
    public async Task<object> Cancelar(ObjectId eventoId, string pagoId, ObjectId usuario) {
        var actual = await Cargar(eventoId, pagoId, usuario);
        if (actual.pago.Estado is "cancelado" or "reembolsado") return PagosService.Vista(actual.evento, actual.pago);
        if (actual.pago.Estado == "pendiente") {
            if (actual.pago.OrdenId == "" && actual.pago.InicioSesionStripe == null) {
                actual = await pagos.Cambiar(eventoId, pagoId, usuario, (e,p) => p.Estado = "cancelado");
                return PagosService.Vista(actual.evento, actual.pago);
            }
            actual = await AsegurarSesion(eventoId, pagoId, usuario);
            var j = await stripe.Sesion(actual.pago.OrdenId); ValidarSesion(j, actual.pago, eventoId);
            if (Texto(j, "status") == "open") {
                // Primero cerrar Checkout remoto: una pestaña antigua ya no puede cobrar después de liberar el lugar.
                j = await stripe.Expirar(actual.pago.OrdenId, pagoId); ValidarSesion(j, actual.pago, eventoId);
                if (Texto(j, "status") != "expired" || Texto(j, "payment_status") != "unpaid")
                    throw new OperacionException("No se pudo comprobar la cancelación; recupera el pago.");
            }
            await Confirmar(eventoId, pagoId, usuario);
            actual = await Cargar(eventoId, pagoId, usuario);
            if (actual.pago.Estado == "cancelado") return PagosService.Vista(actual.evento, actual.pago);
        }
        actual = await pagos.Cambiar(eventoId, pagoId, usuario, (e,p) => {
            if (p.Estado == "reembolsando") return;
            if (p.Estado != "pagado") throw new OperacionException("Recupera el pago antes de cancelarlo.");
            if (e.Reservas.Any(r => r.PagoId == pagoId && r.FechaEntrada != null)) throw new OperacionException("No se reembolsa un boleto utilizado para entrar.");
            if (ReglasReservas.YaInicio(e, AhoraLocal)) throw new OperacionException("No se cancela después del inicio del evento.");
            p.Estado = "reembolsando"; p.InicioReembolso = DateTime.UtcNow;
        });
        log.Escribir($"Inicio de reembolso Stripe prueba {pagoId}");
        JsonElement reembolso;
        if (actual.pago.ReembolsoId != "") reembolso = await stripe.Reembolso(actual.pago.ReembolsoId);
        else {
            // Recuperar respuesta perdida sin depender solamente de la clave idempotente temporal.
            var lista = await stripe.Reembolsos(actual.pago.CapturaId);
            if (lista.GetProperty("has_more").GetBoolean()) throw new OperacionException("Hay más reembolsos; requiere revisión manual.");
            var remotos = lista.GetProperty("data").EnumerateArray().ToList();
            if (remotos.Count > 1) throw new OperacionException("Existen varias devoluciones; requiere revisión manual.");
            if (remotos.Count == 1) reembolso = remotos[0];
            else {
                if (actual.pago.InicioReembolso == null || DateTime.UtcNow - actual.pago.InicioReembolso > TimeSpan.FromHours(23))
                    throw new OperacionException("La respuesta de reembolso requiere revisión en Stripe. No se enviará otra devolución.");
                reembolso = await stripe.Reembolsar(actual.pago);
            }
            ValidarReembolso(reembolso, actual.pago);
            actual = await pagos.Cambiar(eventoId, pagoId, usuario, (e,p) => p.ReembolsoId = Texto(reembolso, "id"));
        }
        ValidarReembolso(reembolso, actual.pago);
        if (Texto(reembolso, "status") == "succeeded") {
            actual = await pagos.Cambiar(eventoId, pagoId, usuario, (e,p) => {
                if (p.Estado == "reembolsado") return;
                if (p.Estado != "reembolsando") throw new OperacionException("El reembolso requiere revisión.");
                var reserva = e.Reservas.FirstOrDefault(r => r.PagoId == pagoId && r.UsuarioId == usuario);
                if (reserva == null || reserva.FechaEntrada != null) throw new OperacionException("Boleto inconsistente; requiere revisión.");
                reserva.Estado = "cancelada"; reserva.FechaCancelacion = DateTime.UtcNow; p.Estado = "reembolsado";
            });
            log.Escribir($"Fin de reembolso Stripe prueba {pagoId}: boleto cancelado");
        }
        return PagosService.Vista(actual.evento, actual.pago);
    }
}
