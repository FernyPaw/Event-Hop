using System.Globalization;
using System.Text.Json;
using EventHop.Api.Data;
using EventHop.Api.Models;
using EventHop.Api.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using MongoDB.Bson;

// Integración de la lógica con EF InMemory y un PayPal falso. No usa dinero,
// credenciales, peticiones externas ni MongoDB. No prueba atomicidad del proveedor MongoDB.
public static class PruebasPagos
{
    public static async Task Ejecutar()
    {
        var total = 0;
        void Ok(bool condicion, string nombre) {
            if (!condicion) throw new Exception("Falló pago: " + nombre);
            total++; Console.WriteLine("OK pago: " + nombre);
        }
        async Task Rechaza(Func<Task> accion, string nombre, int estado = 409) {
            try { await accion(); throw new Exception("No rechazó: " + nombre); }
            catch (OperacionException e) { Ok(e.Estado == estado, nombre); }
        }
        using var db = new EventHopContext(new DbContextOptionsBuilder<EventHopContext>()
            .UseInMemoryDatabase("pagos-" + Guid.NewGuid()).Options);
        var cafe = new Usuario { TipoUsuario = "cafeteria", Activo = true, EstadoSolicitud = "aprobada", Nombre = "Café prueba" };
        var evento = new Evento { CafeteriaId = cafe.Id, Fecha = "2099-12-20", Hora = "12:00", Cupo = 1, EsGratis = false, Costo = 50 };
        db.Usuarios.Add(cafe); db.Eventos.Add(evento); await db.SaveChangesAsync();
        var usuario = ObjectId.GenerateNewId(); var otro = ObjectId.GenerateNewId();
        var falso = new PayPalFalso();
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> { ["Eventos:ZonaHoraria"] = "America/Monterrey" }).Build();
        var log = new ArchivoLog(new EntornoPrueba());
        var servicio = new PagosService(db, falso, config, log);
        await servicio.Crear(evento.Id, usuario);
        var e = await db.Eventos.SingleAsync(); var pagoId = e.Pagos.Single().Identificador;
        Ok(ReglasReservas.Ocupados(e) == 1, "Crear retiene el único lugar");
        await servicio.Crear(evento.Id, usuario);
        Ok((await db.Eventos.SingleAsync()).Pagos.Count == 1, "Reintentar inicio reutiliza el intento");
        await Rechaza(async () => await servicio.Crear(evento.Id, otro), "Otro usuario no ocupa el lugar retenido");
        await Rechaza(async () => await servicio.Cargar(evento.Id, pagoId, otro), "No leer pago de otro usuario", 404);
        await servicio.PrepararOrden(evento.Id, pagoId, usuario);
        await servicio.PrepararOrden(evento.Id, pagoId, usuario);
        Ok(falso.Creaciones == 1, "Reutiliza orden PayPal existente");
        falso.Aprobada = false;
        await Rechaza(async () => await servicio.Confirmar(evento.Id, pagoId, usuario), "Sin aprobación no se cobra");
        Ok(falso.Cobros == 0, "No captura una orden no aprobada");
        falso.Aprobada = true; falso.PerderCobro = true;
        await Rechaza(async () => await servicio.Confirmar(evento.Id, pagoId, usuario), "Corte después de cobrar deja estado recuperable", 503);
        var cargado = await servicio.Cargar(evento.Id, pagoId, usuario);
        Ok(cargado.pago.Estado == "capturando" && ReglasReservas.Ocupados(cargado.evento) == 1, "Resultado incierto conserva lugar");
        await Rechaza(async () => await servicio.Cancelar(evento.Id, pagoId, usuario), "No cancela un cobro incierto");
        await servicio.Confirmar(evento.Id, pagoId, usuario);
        cargado = await servicio.Cargar(evento.Id, pagoId, usuario);
        var codigo = cargado.evento.Reservas.Single().Codigo;
        Ok(cargado.pago.Estado == "pagado" && cargado.evento.Reservas.Single().Estado == "confirmada", "Recupera captura y emite boleto");
        await servicio.Confirmar(evento.Id, pagoId, usuario);
        cargado = await servicio.Cargar(evento.Id, pagoId, usuario);
        Ok(falso.Cobros == 1 && cargado.evento.Reservas.Single().Codigo == codigo, "Confirmación repetida no cobra ni cambia folio");
        Ok(ReglasReservas.Ocupados(cargado.evento) == 1, "No cuenta retención y boleto dos veces");
        falso.PerderReembolso = true;
        await Rechaza(async () => await servicio.Cancelar(evento.Id, pagoId, usuario), "Corte después de reembolsar es recuperable", 503);
        cargado = await servicio.Cargar(evento.Id, pagoId, usuario);
        Ok(cargado.pago.Estado == "reembolsando" && ReglasReservas.Ocupados(cargado.evento) == 1, "No libera hasta comprobar reembolso");
        await servicio.Cancelar(evento.Id, pagoId, usuario);
        cargado = await servicio.Cargar(evento.Id, pagoId, usuario);
        Ok(cargado.pago.Estado == "reembolsado" && cargado.evento.Reservas.Single().Estado == "cancelada", "Recupera reembolso y cancela boleto");
        Ok(ReglasReservas.Ocupados(cargado.evento) == 0 && falso.Devoluciones == 1, "Libera lugar sin duplicar devolución");
        await servicio.Cancelar(evento.Id, pagoId, usuario);
        Ok(falso.Devoluciones == 1, "Cancelar de nuevo es idempotente");
        await servicio.Crear(evento.Id, otro);
        var ultimo = (await db.Eventos.SingleAsync()).Pagos.Last();
        await servicio.Cancelar(evento.Id, ultimo.Identificador, otro);
        Ok(falso.Cobros == 1 && ReglasReservas.Ocupados((await db.Eventos.SingleAsync())) == 0, "Abandonar intento no cobra y libera");
        var vencido = new Pago { Vence = DateTime.UtcNow.AddMinutes(-1) };
        Ok(!ReglasReservas.Retiene(vencido, DateTime.UtcNow), "Retención vencida no ocupa cupo");
        vencido.Estado = "capturando";
        Ok(ReglasReservas.Retiene(vencido, DateTime.UtcNow), "Captura incierta no vence automáticamente");
        Ok(!PagosService.UrlSegura("https://www.paypal.com/checkout") && !PagosService.UrlSegura("https://sandbox.paypal.com.ejemplo.com/") &&
            PagosService.UrlSegura("https://www.sandbox.paypal.com/checkoutnow?token=TEST"), "Solo acepta enlace Sandbox");
        await Rechaza(() => { PagosService.ValidarImporte(JsonSerializer.SerializeToElement(new { currency_code = "USD", value = "50.00" }), 5000); return Task.CompletedTask; }, "Rechaza moneda distinta", 502);
        await Rechaza(() => { PagosService.ValidarImporte(JsonSerializer.SerializeToElement(new { currency_code = "MXN", value = "0.01" }), 5000); return Task.CompletedTask; }, "Rechaza importe distinto", 502);
        var segundo = new Evento { CafeteriaId = cafe.Id, Fecha = "2099-12-20", Hora = "12:00", Cupo = 1, Costo = 50 };
        db.Eventos.Add(segundo); await db.SaveChangesAsync();
        var falsoPendiente = new PayPalFalso { ReembolsoPendiente = true };
        var servicioPendiente = new PagosService(db, falsoPendiente, config, log);
        await servicioPendiente.Crear(segundo.Id, usuario);
        var pagoSegundo = (await db.Eventos.SingleAsync(e => e.Id == segundo.Id)).Pagos.Single().Identificador;
        await servicioPendiente.PrepararOrden(segundo.Id, pagoSegundo, usuario);
        await servicioPendiente.Confirmar(segundo.Id, pagoSegundo, usuario);
        await servicioPendiente.Cancelar(segundo.Id, pagoSegundo, usuario);
        var pendiente = await servicioPendiente.Cargar(segundo.Id, pagoSegundo, usuario);
        Ok(pendiente.pago.Estado == "reembolsando" && pendiente.pago.ReembolsoId != "" && ReglasReservas.Ocupados(pendiente.evento) == 1, "Reembolso PENDING conserva boleto y cupo");
        await servicioPendiente.Cancelar(segundo.Id, pagoSegundo, usuario);
        Ok(falsoPendiente.Devoluciones == 1, "Revisar pendiente consulta el mismo reembolso");
        falsoPendiente.ReembolsoPendiente = false;
        await servicioPendiente.Cancelar(segundo.Id, pagoSegundo, usuario);
        pendiente = await servicioPendiente.Cargar(segundo.Id, pagoSegundo, usuario);
        Ok(pendiente.pago.Estado == "reembolsado" && ReglasReservas.Ocupados(pendiente.evento) == 0, "Solo COMPLETED libera el lugar");
        Ok(pendiente.evento.Pagos.Count == 1, "Historial de pago conservado después de cancelar");
        await PruebasHttpPayPal.Ejecutar(Ok, log);
        Console.WriteLine($"{total} comprobaciones adicionales de pagos aprobadas (EF InMemory y PayPal falso).");
    }
}

class PayPalFalso : IPayPalService
{
    public int Creaciones, Cobros, Devoluciones;
    public bool Aprobada = true, PerderCobro, PerderReembolso, ReembolsoPendiente;
    private Pago? pago;
    private bool capturada, reembolsada;
    private object Importe => new { currency_code = "MXN", value = (pago!.Centavos / 100m).ToString("F2", CultureInfo.InvariantCulture) };
    public Task<JsonElement> Crear(Pago p, string eventoId) { pago = p; Creaciones++; return Orden("ORDEN-PRUEBA"); }
    public Task<JsonElement> Orden(string id) => Task.FromResult(JsonSerializer.SerializeToElement(new {
        id = "ORDEN-PRUEBA", intent = "CAPTURE", status = capturada ? "COMPLETED" : Aprobada ? "APPROVED" : "CREATED",
        purchase_units = new[] { new { custom_id = pago!.Identificador, amount = Importe,
            payments = new { captures = capturada ? new[] { new { id = "CAPTURA-PRUEBA", status = "COMPLETED", amount = Importe } } : [] } } },
        links = new[] { new { rel = "payer-action", href = "https://www.sandbox.paypal.com/checkoutnow?token=PRUEBA" } }
    }));
    public Task<JsonElement> Capturar(Pago p) { Cobros++; capturada = true;
        if (PerderCobro) { PerderCobro = false; throw new OperacionException("Corte simulado", 503); }
        return Orden(p.OrdenId);
    }
    public Task<JsonElement> Captura(string id) => Task.FromResult(JsonSerializer.SerializeToElement(new {
        id = "CAPTURA-PRUEBA", status = reembolsada ? "REFUNDED" : "COMPLETED", amount = Importe
    }));
    public Task<JsonElement> Reembolsar(Pago p) { Devoluciones++; reembolsada = !ReembolsoPendiente;
        if (PerderReembolso) { PerderReembolso = false; throw new OperacionException("Corte simulado", 503); }
        return Reembolso("REEMBOLSO-PRUEBA");
    }
    public Task<JsonElement> Reembolso(string id) => Task.FromResult(JsonSerializer.SerializeToElement(new {
        id = "REEMBOLSO-PRUEBA", status = ReembolsoPendiente ? "PENDING" : "COMPLETED", amount = Importe
    }));
}
class EntornoPrueba : IWebHostEnvironment
{
    public string ApplicationName { get; set; } = "Pruebas";
    public string EnvironmentName { get; set; } = "Testing";
    public string WebRootPath { get; set; } = "";
    public string ContentRootPath { get; set; } = Path.Combine(Path.GetTempPath(), "eventhop-pruebas-pagos");
    public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
    public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
}
