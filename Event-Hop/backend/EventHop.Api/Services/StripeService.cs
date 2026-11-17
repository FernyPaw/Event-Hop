using System.Net.Http.Headers;
using System.Globalization;
using System.Text.Json;
using EventHop.Api.Models;

namespace EventHop.Api.Services;

public interface IStripeService
{
    void ComprobarConfiguracion();
    Task<JsonElement> Crear(Pago pago, string eventoId);
    Task<JsonElement> Sesion(string id);
    Task<JsonElement> Expirar(string id, string pagoId);
    Task<JsonElement> Intencion(string id);
    Task<JsonElement> Reembolsos(string id);
    Task<JsonElement> Reembolsar(Pago pago);
    Task<JsonElement> Reembolso(string id);
}

// Checkout alojado. Nunca recibimos tarjetas y nunca aceptamos claves live.
public class StripeService(HttpClient http, IConfiguration configuracion, ArchivoLog log) : IStripeService
{
    public void ComprobarConfiguracion() {
        var clave = configuracion["Stripe:SecretKey"] ?? "";
        if (!clave.StartsWith("sk_test_", StringComparison.Ordinal) || clave.Length < 16)
            throw new OperacionException("Configura Stripe:SecretKey de prueba (sk_test_) en user-secrets. No uses una clave live.", 503);
    }
    private async Task<JsonElement> Llamar(HttpMethod metodo, string ruta, Dictionary<string,string>? datos = null, string? idempotencia = null) {
        ComprobarConfiguracion();
        using var solicitud = new HttpRequestMessage(metodo, "https://api.stripe.com/v1/" + ruta);
        solicitud.Headers.Authorization = new AuthenticationHeaderValue("Bearer", configuracion["Stripe:SecretKey"]);
        solicitud.Headers.Add("Stripe-Version", "2026-10-28.endive");
        if (idempotencia != null) solicitud.Headers.Add("Idempotency-Key", idempotencia);
        if (metodo == HttpMethod.Post) solicitud.Content = new FormUrlEncodedContent(datos ?? []);
        try {
            using var respuesta = await http.SendAsync(solicitud);
            if (!respuesta.IsSuccessStatusCode) {
                log.Escribir($"Stripe pruebas HTTP {(int)respuesta.StatusCode}");
                throw new OperacionException("Stripe no completó la operación. Recupera su estado desde Mis pagos; no inicies otra compra.", 502);
            }
            using var json = JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync());
            var valor = json.RootElement;
            if (valor.TryGetProperty("livemode", out var live) && live.GetBoolean())
                throw new OperacionException("Se rechazó una respuesta Stripe fuera del modo de prueba.", 502);
            return valor.Clone();
        } catch (HttpRequestException) { throw new OperacionException("Sin conexión con Stripe. No se conoce el resultado; recupera el pago.", 503); }
        catch (TaskCanceledException) { throw new OperacionException("Stripe tardó en responder. Recupera el mismo intento desde Mis pagos.", 503); }
    }
    public Task<JsonElement> Crear(Pago pago, string eventoId) {
        var regreso = configuracion["FrontendUrl"]!.TrimEnd('/') + "/usuario/pagos?evento=" + eventoId + "&pago=" + pago.Identificador;
        // Parámetros estables en los reintentos. Checkout usa su vencimiento predeterminado.
        return Llamar(HttpMethod.Post, "checkout/sessions", new() {
            ["mode"] = "payment", ["allowed_payment_method_types[0]"] = "card",
            ["adaptive_pricing[enabled]"] = "false",
            ["client_reference_id"] = pago.Identificador, ["metadata[pagoId]"] = pago.Identificador,
            ["metadata[eventoId]"] = eventoId, ["metadata[usuarioId]"] = pago.UsuarioId.ToString(),
            ["payment_intent_data[metadata][pagoId]"] = pago.Identificador,
            ["line_items[0][price_data][currency]"] = "mxn",
            ["line_items[0][price_data][unit_amount]"] = pago.Centavos.ToString(CultureInfo.InvariantCulture),
            ["line_items[0][price_data][product_data][name]"] = "Entrada Event-Hop de prueba",
            ["line_items[0][quantity]"] = "1", ["success_url"] = regreso, ["cancel_url"] = regreso + "&cancelado=1"
        }, "eh-sesion-v2-" + pago.Identificador);
    }
    private static string Id(string id) => Uri.EscapeDataString(id);
    public Task<JsonElement> Sesion(string id) => Llamar(HttpMethod.Get, "checkout/sessions/" + Id(id));
    public Task<JsonElement> Expirar(string id, string pagoId) => Llamar(HttpMethod.Post, "checkout/sessions/" + Id(id) + "/expire", idempotencia: "eh-expirar-" + pagoId);
    public Task<JsonElement> Intencion(string id) => Llamar(HttpMethod.Get, "payment_intents/" + Id(id));
    public Task<JsonElement> Reembolsos(string id) => Llamar(HttpMethod.Get, "refunds?payment_intent=" + Id(id) + "&limit=100");
    public Task<JsonElement> Reembolsar(Pago p) => Llamar(HttpMethod.Post, "refunds", new() {
        ["payment_intent"] = p.CapturaId, ["amount"] = p.Centavos.ToString(CultureInfo.InvariantCulture),
        ["metadata[pagoId]"] = p.Identificador
    }, "eh-reembolso-" + p.Identificador);
    public Task<JsonElement> Reembolso(string id) => Llamar(HttpMethod.Get, "refunds/" + Id(id));
}
