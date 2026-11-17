using System.Net;
using System.Text;
using System.Text.Json;
using EventHop.Api.Models;
using EventHop.Api.Services;
using Microsoft.Extensions.Configuration;

public static class PruebasHttpPayPal
{
    public static async Task Ejecutar(Action<bool,string> ok, ArchivoLog log)
    {
        var manejador = new HttpPayPalFalso();
        using var http = new HttpClient(manejador);
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> {
            ["PayPal:ClientId"] = "cliente-ficticio", ["PayPal:Secret"] = "secreto-ficticio",
            ["FrontendUrl"] = "http://localhost:4200"
        }).Build();
        var servicio = new PayPalService(http, config, log);
        var pago = new Pago { Centavos = 5025, OrdenId = "ORDER_TEST", CapturaId = "CAPTURE_TEST" };
        await servicio.Crear(pago, "evento_prueba");
        using var cuerpo = JsonDocument.Parse(manejador.Peticiones.Last().cuerpo);
        var json = cuerpo.RootElement;
        ok(json.GetProperty("purchase_units")[0].GetProperty("amount").GetProperty("value").GetString() == "50.25", "Precio enviado con dos decimales invariantes");
        ok(json.GetProperty("purchase_units")[0].GetProperty("custom_id").GetString() == pago.Identificador, "Orden ligada al intento propio");
        var regreso = json.GetProperty("payment_source").GetProperty("paypal").GetProperty("experience_context").GetProperty("return_url").GetString()!;
        ok(regreso.StartsWith("http://localhost:4200/usuario/pagos?") && !regreso.Contains("secreto"), "Regreso a frontend sin secretos");
        await servicio.Capturar(pago); await servicio.Capturar(pago);
        var capturas = manejador.Peticiones.Where(p => p.url.EndsWith("/capture")).ToArray();
        ok(capturas.Length == 2 && capturas[0].clave == capturas[1].clave && capturas[0].clave == "p-" + pago.Identificador, "Reintento de captura mantiene clave idempotente");
        await servicio.Reembolsar(pago);
        var reembolso = manejador.Peticiones.Last();
        ok(reembolso.url.EndsWith("/v2/payments/captures/CAPTURE_TEST/refund") && reembolso.clave == "r-" + pago.Identificador && reembolso.cuerpo == "{}", "Reembolso completo con clave diferente a captura");
        ok(manejador.Peticiones.All(p => new Uri(p.url).Host == "api-m.sandbox.paypal.com"), "Toda llamada externa se dirige solo a Sandbox");
        ok(manejador.Peticiones.Where(p => !p.url.EndsWith("/token")).All(p => p.autorizacion == "Bearer token-falso"), "Operaciones usan token obtenido por el servidor");
        manejador.Fallar = true;
        try { await servicio.Orden("ORDER_TEST"); throw new Exception("No rechazó HTTP 500"); }
        catch (OperacionException e) { ok(e.Estado == 502 && !e.Message.Contains("DETALLE_PRIVADO"), "No expone respuesta interna del proveedor"); }
    }
}
class HttpPayPalFalso : HttpMessageHandler
{
    public List<(string url,string cuerpo,string clave,string autorizacion)> Peticiones = [];
    public bool Fallar;
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage solicitud, CancellationToken token)
    {
        Peticiones.Add((solicitud.RequestUri!.AbsoluteUri, solicitud.Content == null ? "" : await solicitud.Content.ReadAsStringAsync(token),
            solicitud.Headers.TryGetValues("PayPal-Request-Id", out var claves) ? claves.Single() : "", solicitud.Headers.Authorization?.ToString() ?? ""));
        var oauth = solicitud.RequestUri.AbsolutePath.EndsWith("/token");
        return new HttpResponseMessage(Fallar && !oauth ? HttpStatusCode.InternalServerError : HttpStatusCode.OK) {
            Content = new StringContent(oauth ? "{\"access_token\":\"token-falso\"}" : Fallar ? "DETALLE_PRIVADO" : "{\"id\":\"PRUEBA\",\"status\":\"COMPLETED\"}", Encoding.UTF8, "application/json")
        };
    }
}
