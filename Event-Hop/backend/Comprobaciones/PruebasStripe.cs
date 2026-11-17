using System.Text.Json;
using EventHop.Api.Data;
using EventHop.Api.Models;
using EventHop.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using MongoDB.Bson;

// Pruebas locales: EF InMemory y proveedor simulado, sin tarjetas ni solicitudes externas.
public static class PruebasStripe
{
    public static async Task Ejecutar() {
        var total = 0;
        void Ok(bool resultado, string nombre) { if (!resultado) throw new Exception("Falló Stripe: " + nombre); total++; Console.WriteLine("OK Stripe: " + nombre); }
        async Task Rechaza(Func<Task> accion, string nombre, int codigo = 409) {
            try { await accion(); throw new Exception("No rechazó: " + nombre); }
            catch (OperacionException e) { Ok(e.Estado == codigo, nombre); }
        }
        using var db = new EventHopContext(new DbContextOptionsBuilder<EventHopContext>().UseInMemoryDatabase("stripe-" + Guid.NewGuid()).Options);
        var cafe = new Usuario { TipoUsuario = "cafeteria", Activo = true, EstadoSolicitud = "aprobada" };
        var evento = new Evento { CafeteriaId = cafe.Id, Fecha = "2099-12-20", Hora = "12:00", Cupo = 1, Costo = 50 };
        db.Usuarios.Add(cafe); db.Eventos.Add(evento); await db.SaveChangesAsync();
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> { ["Eventos:ZonaHoraria"] = "America/Monterrey" }).Build();
        var log = new ArchivoLog(new EntornoPrueba());
        var pagos = new PagosService(db, new PayPalFalso(), config, log);
        var remoto = new StripeFalso(); var stripe = new PagosStripeService(pagos, remoto, config, log);
        var usuario = ObjectId.GenerateNewId(); var otro = ObjectId.GenerateNewId();
        await stripe.Crear(evento.Id, usuario);
        var id = (await db.Eventos.SingleAsync()).Pagos.Single().Identificador;
        await pagos.Crear(evento.Id, usuario);
        Ok((await db.Eventos.SingleAsync()).Pagos.Count == 1, "Cambiar de método no crea otro intento pendiente");
        await Rechaza(async () => await stripe.Crear(evento.Id, otro), "Cupo retenido protege frente a otro usuario");
        await Rechaza(async () => await stripe.Orden(evento.Id, id, otro), "No accede a pago ajeno", 404);
        await Rechaza(async () => await pagos.PrepararOrden(evento.Id, id, usuario), "PayPal rechaza intento Stripe");
        remoto.PerderCreacion = true;
        await Rechaza(async () => await stripe.Orden(evento.Id, id, usuario), "Respuesta perdida conserva intento recuperable", 503);
        await stripe.Orden(evento.Id, id, usuario); await stripe.Orden(evento.Id, id, usuario);
        Ok(remoto.Creaciones == 1, "Recupera la misma sesión sin duplicarla");
        await pagos.Cambiar(evento.Id, id, usuario, (e,p) => p.Vence = DateTime.UtcNow.AddDays(-1));
        Ok(ReglasReservas.Ocupados((await pagos.Cargar(evento.Id, id, usuario)).evento) == 1, "Reloj local no libera un cobro desconocido");
        await Rechaza(async () => await stripe.Confirmar(evento.Id, id, usuario), "Sesión abierta no emite boleto");
        remoto.Pagado = true; remoto.ImporteIncorrecto = true;
        await Rechaza(async () => await stripe.Confirmar(evento.Id, id, usuario), "Rechaza importe alterado", 502);
        remoto.ImporteIncorrecto = false;
        await stripe.Confirmar(evento.Id, id, usuario);
        var actual = await pagos.Cargar(evento.Id, id, usuario); var folio = actual.evento.Reservas.Single().Codigo;
        await stripe.Confirmar(evento.Id, id, usuario);
        actual = await pagos.Cargar(evento.Id, id, usuario);
        Ok(actual.pago.Estado == "pagado" && actual.evento.Reservas.Single().Codigo == folio && ReglasReservas.Ocupados(actual.evento) == 1, "Confirmación idempotente: un boleto y un lugar");
        await pagos.Cambiar(evento.Id, id, usuario, (e,p) => e.Reservas.Single().FechaEntrada = DateTime.UtcNow);
        await Rechaza(async () => await stripe.Cancelar(evento.Id, id, usuario), "No reembolsa boleto utilizado");
        await pagos.Cambiar(evento.Id, id, usuario, (e,p) => e.Reservas.Single().FechaEntrada = null);
        remoto.ReembolsoPendiente = true;
        await stripe.Cancelar(evento.Id, id, usuario);
        actual = await pagos.Cargar(evento.Id, id, usuario);
        Ok(actual.pago.Estado == "reembolsando" && ReglasReservas.Ocupados(actual.evento) == 1, "Devolución pendiente conserva el lugar");
        remoto.ReembolsoPendiente = false;
        await stripe.Cancelar(evento.Id, id, usuario); await stripe.Cancelar(evento.Id, id, usuario);
        actual = await pagos.Cargar(evento.Id, id, usuario);
        Ok(actual.pago.Estado == "reembolsado" && actual.evento.Reservas.Single().Estado == "cancelada" && ReglasReservas.Ocupados(actual.evento) == 0 && remoto.Devoluciones == 1, "Devuelve una sola vez y cancela boleto tras confirmación");
        await stripe.Crear(evento.Id, otro);
        var nuevo = (await db.Eventos.SingleAsync()).Pagos.Last().Identificador;
        await stripe.Cancelar(evento.Id, nuevo, otro);
        Ok((await pagos.Cargar(evento.Id, nuevo, otro)).pago.Estado == "cancelado", "Cancela intento sin sesión sin contactar a Stripe");
        await stripe.Crear(evento.Id, otro);
        nuevo = (await db.Eventos.SingleAsync()).Pagos.Last().Identificador;
        remoto.Reiniciar(); await stripe.Orden(evento.Id, nuevo, otro); await stripe.Cancelar(evento.Id, nuevo, otro);
        Ok(remoto.Expiraciones == 1 && ReglasReservas.Ocupados((await pagos.Cargar(evento.Id, nuevo, otro)).evento) == 0, "Cierra Checkout remoto antes de liberar lugar");
        Ok(PagosStripeService.UrlSegura("https://checkout.stripe.com/c/pay/cs_test_x") && !PagosStripeService.UrlSegura("https://checkout.stripe.com.ejemplo.com/") && !PagosStripeService.UrlSegura("https://usuario@checkout.stripe.com/"), "Valida destino de Checkout");
        var transporte = new StripeService(new HttpClient(), config, log);
        await Rechaza(() => { transporte.ComprobarConfiguracion(); return Task.CompletedTask; }, "Sin clave test no habilita Stripe", 503);
        config["Stripe:SecretKey"] = "sk_live_falsa_solo_prueba";
        await Rechaza(() => { transporte.ComprobarConfiguracion(); return Task.CompletedTask; }, "Rechaza clave live sin hacer solicitudes", 503);
        db.ChangeTracker.Clear(); var barato = await db.Eventos.SingleAsync(); barato.Costo = 5; await db.SaveChangesAsync();
        await Rechaza(async () => await stripe.Crear(evento.Id, otro), "Mínimo MXN se valida antes de retener cupo", 400);
        using var modeloMongo = new EventHopContext(new DbContextOptionsBuilder<EventHopContext>().UseMongoDB("mongodb://localhost:27018", "PruebaModelo").Options);
        var pagoModelo = modeloMongo.Model.GetEntityTypes().Single(t => t.ClrType == typeof(Pago));
        Ok(pagoModelo.FindProperty(nameof(Pago.Proveedor))!.IsNullable, "Proveedor ausente en pagos antiguos es opcional en modelo MongoDB");
        var handler = new StripeSolicitudPrueba();
        config["Stripe:SecretKey"] = "sk_test_falsa_solo_prueba"; config["FrontendUrl"] = "http://localhost:4200";
        var cliente = new StripeService(new HttpClient(handler), config, log);
        var muestra = new Pago { Proveedor = "stripe", Centavos = 7500, UsuarioId = usuario };
        await cliente.Crear(muestra, evento.Id.ToString());
        Ok(handler.Formulario.Contains("allowed_payment_method_types%5B0%5D=card") && !handler.Formulario.Contains("&payment_method_types"), "Formulario HTTP usa filtro de tarjetas compatible con Stripe");
        Ok(handler.Clave == "eh-sesion-v2-" + muestra.Identificador && handler.Formulario.Contains("unit_amount%5D=7500"), "Importe de servidor y clave de creación estable versión dos");
        Console.WriteLine($"{total} comprobaciones Stripe aprobadas (proveedor simulado).");
    }
}

class StripeSolicitudPrueba : HttpMessageHandler
{
    public string Formulario = "", Clave = "";
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) {
        Formulario = await request.Content!.ReadAsStringAsync(token);
        Clave = request.Headers.GetValues("Idempotency-Key").Single();
        if (request.RequestUri!.Host != "api.stripe.com") throw new Exception("Host inesperado");
        return new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new StringContent("{\"livemode\":false}") };
    }
}

class StripeFalso : IStripeService
{
    private Pago? pago; private string evento = ""; private bool creado, expirado;
    public int Creaciones, Expiraciones, Devoluciones;
    public bool Pagado, PerderCreacion, ImporteIncorrecto, ReembolsoPendiente;
    private static JsonElement Json(object valor) => JsonSerializer.SerializeToElement(valor);
    public void ComprobarConfiguracion() { }
    public void Reiniciar() { creado = expirado = Pagado = false; }
    public Task<JsonElement> Crear(Pago p, string e) {
        pago = p; evento = e; if (!creado) { creado = true; Creaciones++; }
        if (PerderCreacion) { PerderCreacion = false; throw new OperacionException("Respuesta perdida simulada", 503); }
        return Sesion("cs_test_prueba");
    }
    public Task<JsonElement> Sesion(string id) => Task.FromResult(Json(new {
        id = "cs_test_prueba", livemode = false, mode = "payment", currency = "mxn", amount_total = pago!.Centavos + (ImporteIncorrecto ? 1 : 0),
        client_reference_id = pago.Identificador, metadata = new { pagoId = pago.Identificador, eventoId = evento, usuarioId = pago.UsuarioId.ToString() },
        status = expirado ? "expired" : Pagado ? "complete" : "open", payment_status = Pagado ? "paid" : "unpaid", payment_intent = Pagado ? "pi_prueba" : null,
        url = "https://checkout.stripe.com/c/pay/cs_test_prueba", expires_at = DateTimeOffset.UtcNow.AddHours(24).ToUnixTimeSeconds()
    }));
    public Task<JsonElement> Expirar(string id, string pagoId) { Expiraciones++; expirado = true; return Sesion(id); }
    public Task<JsonElement> Intencion(string id) => Task.FromResult(Json(new { id, livemode = false, currency = "mxn", amount = pago!.Centavos, amount_received = pago.Centavos, status = "succeeded", metadata = new { pagoId = pago.Identificador } }));
    public Task<JsonElement> Reembolsos(string id) => Task.FromResult(Json(new { has_more = false, data = Array.Empty<object>() }));
    public Task<JsonElement> Reembolsar(Pago p) { Devoluciones++; return Reembolso("re_prueba"); }
    public Task<JsonElement> Reembolso(string id) => Task.FromResult(Json(new { id, payment_intent = "pi_prueba", currency = "mxn", amount = pago!.Centavos, metadata = new { pagoId = pago.Identificador }, status = ReembolsoPendiente ? "pending" : "succeeded" }));
}
