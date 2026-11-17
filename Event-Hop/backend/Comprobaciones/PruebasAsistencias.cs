using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using EventHop.Api.Controllers;
using EventHop.Api.Data;
using EventHop.Api.Models;
using EventHop.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using MongoDB.Bson;

public static class PruebasAsistencias
{
    public static async Task Ejecutar()
    {
        var total = 0;
        void Ok(bool condicion, string nombre) {
            if (!condicion) throw new Exception("Falló asistencia: " + nombre);
            total++; Console.WriteLine("OK asistencia: " + nombre);
        }
        async Task Rechaza(Func<Task> accion, string nombre, int estado = 409) {
            try { await accion(); throw new Exception("No rechazó: " + nombre); }
            catch (OperacionException e) { Ok(e.Estado == estado, nombre); }
        }
        using var db = new EventHopContext(new DbContextOptionsBuilder<EventHopContext>()
            .UseInMemoryDatabase("asistencias-" + Guid.NewGuid()).Options);
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> {
            ["Eventos:ZonaHoraria"] = "America/Monterrey"
        }).Build();
        var log = new ArchivoLog(new EntornoPrueba());
        var servicio = new AsistenciasService(db, config, log);
        var local = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("America/Monterrey"));
        var cafe = ObjectId.GenerateNewId();
        var usuario = new Usuario { Nombre = "Usuario de prueba", TipoUsuario = "usuario" };
        var reserva = new Reserva { UsuarioId = usuario.Id };
        var evento = new Evento { CafeteriaId = cafe, Titulo = "Evento de hoy",
            Fecha = local.ToString("yyyy-MM-dd"), Hora = "23:59", Cupo = 2, EsGratis = true, Reservas = [reserva] };
        db.Usuarios.Add(usuario); db.Eventos.Add(evento); await db.SaveChangesAsync();
        Ok(AsistenciasService.Validar(evento, reserva, local) == null, "Boleto gratuito válido el día del evento");
        Ok(AsistenciasService.Validar(evento, reserva, local.AddDays(-1)) != null, "No entrada antes del día");
        Ok(AsistenciasService.Validar(evento, reserva, local.AddDays(1)) != null, "No entrada después del día");
        reserva.Estado = "cancelada";
        Ok(AsistenciasService.Validar(evento, reserva, local) != null, "Código cancelado rechazado");
        reserva.Estado = "confirmada";
        var pago = new Pago { UsuarioId = usuario.Id, Estado = "reembolsando" };
        evento.Pagos.Add(pago); reserva.PagoId = pago.Identificador;
        Ok(AsistenciasService.Validar(evento, reserva, local) != null, "Reembolso en proceso bloquea entrada");
        pago.Estado = "pendiente";
        Ok(AsistenciasService.Validar(evento, reserva, local) != null, "Pago pendiente bloquea entrada");
        pago.Estado = "pagado"; pago.UsuarioId = ObjectId.GenerateNewId();
        Ok(AsistenciasService.Validar(evento, reserva, local) != null, "Pago de otro usuario no acredita boleto");
        pago.UsuarioId = usuario.Id;
        Ok(AsistenciasService.Validar(evento, reserva, local) == null, "Pago confirmado permite entrada");
        reserva.PagoId = ""; evento.Pagos.Clear(); await db.SaveChangesAsync();
        await Rechaza(async () => await servicio.Cargar(evento.Id, ObjectId.GenerateNewId()), "Otra cafetería no consulta asistentes", 404);
        await Rechaza(async () => await servicio.Registrar(evento.Id, ObjectId.GenerateNewId(), reserva.Codigo), "Otra cafetería no registra", 404);
        await Rechaza(async () => await servicio.Registrar(evento.Id, cafe, new Reserva().Codigo), "Código de otro evento rechazado", 404);
        var codigo = reserva.Codigo;
        await servicio.Registrar(evento.Id, cafe, codigo);
        var guardado = await servicio.Cargar(evento.Id, cafe);
        Ok(guardado.Reservas.Single().FechaEntrada != null, "Entrada persistida por ORM");
        Ok(guardado.Version == 1, "Entrada incrementa versión del evento");
        Ok(guardado.Reservas.Single().Estado == "confirmada", "Entrada no libera cupo ni cancela reserva");
        var fechaEntrada = guardado.Reservas.Single().FechaEntrada;
        await Rechaza(async () => await servicio.Registrar(evento.Id, cafe, codigo), "Segundo uso bloqueado");
        Ok((await servicio.Cargar(evento.Id, cafe)).Reservas.Single().FechaEntrada == fechaEntrada, "Reintento conserva primera fecha");
        var claims = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, cafe.ToString())], "prueba"));
        var controlador = new AsistenciasController(db, servicio) {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = claims } }
        };
        Ok(await controlador.Consultar(evento.Id.ToString()) is OkObjectResult, "Dueño consulta lista real");
        Ok(await controlador.Consultar("incorrecto") is BadRequestObjectResult, "Identificador inválido rechazado");
        var reservaController = new ReservasController(db, log, config) {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext {
                User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, usuario.Id.ToString())], "prueba"))
            } }
        };
        Ok(await reservaController.Cancelar(evento.Id.ToString()) is ConflictObjectResult, "No cancelar gratis después de entrar");
        guardado = await servicio.Cargar(evento.Id, cafe);
        var pagado = new Pago { UsuarioId = usuario.Id, Estado = "pagado", CapturaId = "NO-LLAMAR" };
        guardado.Pagos.Add(pagado); guardado.Reservas.Single().PagoId = pagado.Identificador; await db.SaveChangesAsync();
        var paypal = new PayPalFalso();
        var pagos = new PagosService(db, paypal, config, log);
        await Rechaza(async () => await pagos.Cancelar(evento.Id, pagado.Identificador, usuario.Id), "No reembolsar boleto utilizado");
        Ok(paypal.Devoluciones == 0, "Boleto usado no llama API de reembolso");
        Ok(typeof(AsistenciasController).GetCustomAttributes(typeof(AuthorizeAttribute), true)
            .Cast<AuthorizeAttribute>().Any(a => a.Roles == "cafeteria"), "Endpoints restringidos a cafetería");
        bool Formato(string texto) {
            var solicitud = new EntradaSolicitud { Codigo = texto };
            return Validator.TryValidateObject(solicitud, new ValidationContext(solicitud), [], true);
        }
        Ok(Formato(codigo), "Formato real del código admitido");
        Ok(!Formato("EH-101") && !Formato(""), "Códigos incompletos rechazados en backend");
        Console.WriteLine($"{total} comprobaciones de asistentes aprobadas. EF InMemory: no prueba atomicidad real de MongoDB.");
    }
}
