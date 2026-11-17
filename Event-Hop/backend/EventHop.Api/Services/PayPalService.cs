using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using EventHop.Api.Models;

namespace EventHop.Api.Services;

public class OperacionException(string mensaje, int estado = 409) : Exception(mensaje)
{
    public int Estado { get; } = estado;
}

public interface IPayPalService
{
    Task<JsonElement> Crear(Pago pago, string eventoId);
    Task<JsonElement> Orden(string id);
    Task<JsonElement> Capturar(Pago pago);
    Task<JsonElement> Captura(string id);
    Task<JsonElement> Reembolsar(Pago pago);
    Task<JsonElement> Reembolso(string id);
}

// Solo Sandbox: no hay una opción Live que pueda activarse accidentalmente.
public class PayPalService(HttpClient http, IConfiguration configuracion, ArchivoLog log) : IPayPalService
{
    private const string Base = "https://api-m.sandbox.paypal.com/";
    private async Task<string> Token()
    {
        var cliente = configuracion["PayPal:ClientId"];
        var secreto = configuracion["PayPal:Secret"];
        if (string.IsNullOrWhiteSpace(cliente) || string.IsNullOrWhiteSpace(secreto))
            throw new OperacionException("Configura PayPal:ClientId y PayPal:Secret de Sandbox en user-secrets.", 503);
        using var solicitud = new HttpRequestMessage(HttpMethod.Post, Base + "v1/oauth2/token");
        solicitud.Headers.Authorization = new AuthenticationHeaderValue("Basic",
            Convert.ToBase64String(Encoding.UTF8.GetBytes(cliente + ":" + secreto)));
        solicitud.Content = new FormUrlEncodedContent(new Dictionary<string, string> { ["grant_type"] = "client_credentials" });
        var respuesta = await Enviar(solicitud);
        return respuesta.GetProperty("access_token").GetString()!;
    }
    private async Task<JsonElement> Llamar(HttpMethod metodo, string ruta, object? datos = null, string? clave = null)
    {
        using var solicitud = new HttpRequestMessage(metodo, Base + ruta);
        solicitud.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await Token());
        solicitud.Headers.TryAddWithoutValidation("Prefer", "return=representation");
        if (clave != null) solicitud.Headers.Add("PayPal-Request-Id", clave);
        if (datos != null) solicitud.Content = JsonContent.Create(datos);
        return await Enviar(solicitud);
    }
    private async Task<JsonElement> Enviar(HttpRequestMessage solicitud)
    {
        try
        {
            using var respuesta = await http.SendAsync(solicitud);
            if (!respuesta.IsSuccessStatusCode)
            {
                // Nunca registrar cuerpos de respuesta, encabezados de autorización o secretos.
                log.Escribir($"PayPal Sandbox HTTP {(int)respuesta.StatusCode}");
                throw new OperacionException("PayPal no completó la operación. Revisa el estado del pago y vuelve a intentarlo; no crees otra compra.", 502);
            }
            using var json = JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync());
            return json.RootElement.Clone();
        }
        catch (HttpRequestException) { throw new OperacionException("No hubo conexión con PayPal. Recupera la operación desde Mis pagos.", 503); }
        catch (TaskCanceledException) { throw new OperacionException("PayPal tardó en responder. No se conoce aún el resultado: recupéralo desde Mis pagos.", 503); }
    }
    public Task<JsonElement> Crear(Pago pago, string eventoId)
    {
        var frente = configuracion["FrontendUrl"]!.TrimEnd('/');
        var regreso = frente + "/usuario/pagos?evento=" + eventoId + "&pago=" + pago.Identificador;
        return Llamar(HttpMethod.Post, "v2/checkout/orders", new {
            intent = "CAPTURE",
            purchase_units = new[] { new { custom_id = pago.Identificador,
                amount = new { currency_code = "MXN", value = (pago.Centavos / 100m).ToString("F2", CultureInfo.InvariantCulture) } } },
            payment_source = new { paypal = new { experience_context = new {
                brand_name = "Event-Hop (pruebas)", shipping_preference = "NO_SHIPPING", user_action = "CONTINUE",
                return_url = regreso, cancel_url = regreso + "&cancelado=1"
            } } }
        }, "c-" + pago.Identificador);
    }
    public Task<JsonElement> Orden(string id) => Llamar(HttpMethod.Get, "v2/checkout/orders/" + Uri.EscapeDataString(id));
    public Task<JsonElement> Capturar(Pago p) => Llamar(HttpMethod.Post, "v2/checkout/orders/" + Uri.EscapeDataString(p.OrdenId) + "/capture", new { }, "p-" + p.Identificador);
    public Task<JsonElement> Captura(string id) => Llamar(HttpMethod.Get, "v2/payments/captures/" + Uri.EscapeDataString(id));
    public Task<JsonElement> Reembolsar(Pago p) => Llamar(HttpMethod.Post, "v2/payments/captures/" + Uri.EscapeDataString(p.CapturaId) + "/refund", new { }, "r-" + p.Identificador);
    public Task<JsonElement> Reembolso(string id) => Llamar(HttpMethod.Get, "v2/payments/refunds/" + Uri.EscapeDataString(id));
}
