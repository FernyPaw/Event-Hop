using System.ComponentModel.DataAnnotations;
using EventHop.Api.Controllers;
using EventHop.Api.Models;
using Microsoft.AspNetCore.Authorization;
using EventHop.Api.Services;
using EventHop.Api.Data;
using Microsoft.EntityFrameworkCore;
using MongoDB.Bson;

var aprobadas = 0;
void Comprobar(bool resultado, string nombre)
{
    if (!resultado) throw new Exception("Falló: " + nombre);
    Console.WriteLine("OK: " + nombre);
    aprobadas++;
}
EventoSolicitud Valido() => new()
{
    Titulo = "Taller de café", Descripcion = "Descripción de prueba", Categoria = "Taller",
    Direccion = "Calle 1", Fecha = "2026-11-20", Hora = "18:00", EsGratis = true, Cupo = 15
};
bool AtributosValidos(EventoSolicitud datos) => Validator.TryValidateObject(datos,
    new ValidationContext(datos), new List<ValidationResult>(), true);

Comprobar(AtributosValidos(Valido()) && EventosController.Validar(Valido()) == null, "Evento gratuito válido");
var datos = Valido(); datos.EsGratis = false; datos.Costo = 50.25;
Comprobar(EventosController.Validar(datos) == null, "Costo de paga válido");
datos.Costo = 0;
Comprobar(EventosController.Validar(datos) != null, "Paga sin costo rechazada");
datos.Costo = 1.234;
Comprobar(EventosController.Validar(datos) != null, "Más de dos decimales rechazados");
datos = Valido(); datos.Fecha = "2026-02-30";
Comprobar(EventosController.Validar(datos) != null, "Fecha inexistente rechazada");
datos = Valido(); datos.Hora = "25:00";
Comprobar(EventosController.Validar(datos) != null, "Hora inexistente rechazada");
datos = Valido(); datos.Titulo = "   ";
Comprobar(EventosController.Validar(datos) != null, "Título en blanco rechazado");
datos = Valido(); datos.Direccion = "   ";
Comprobar(EventosController.Validar(datos) != null, "Dirección en blanco rechazada");
datos = Valido(); datos.Descripcion = "   ";
Comprobar(EventosController.Validar(datos) != null, "Descripción en blanco rechazada");
datos = Valido(); datos.Cupo = 0;
Comprobar(!AtributosValidos(datos), "Cupo cero rechazado por atributos");
datos = Valido(); datos.Cupo = 10001;
Comprobar(!AtributosValidos(datos), "Cupo excesivo rechazado");
datos = Valido(); datos.Categoria = new string('x', 51);
Comprobar(!AtributosValidos(datos), "Categoría demasiado larga rechazada; existencia se comprueba en catálogo");
datos = Valido(); datos.Costo = 100001;
Comprobar(!AtributosValidos(datos), "Costo excesivo rechazado");
datos = Valido(); datos.Titulo = new string('x', 121);
Comprobar(!AtributosValidos(datos), "Longitud de título limitada");
datos = Valido(); datos.ImagenUrl = "data:image/svg+xml;base64,AAAA";
Comprobar(EventosController.Validar(datos) != null, "SVG no permitido");
datos.ImagenUrl = "data:image/png;base64,no-es-base64";
Comprobar(EventosController.Validar(datos) != null, "Base64 inválido rechazado");
datos.ImagenUrl = "https://example.com/imagen.png";
Comprobar(EventosController.Validar(datos) != null, "No se aceptan URLs externas en el formulario");
datos.ImagenUrl = "data:image/png;base64," + Convert.ToBase64String(new byte[20]);
Comprobar(EventosController.Validar(datos) != null, "Firma de imagen falsa rechazada");
datos.ImagenUrl = "data:image/png;base64," + Convert.ToBase64String(new byte[1048577]);
Comprobar(EventosController.Validar(datos) != null, "Imagen mayor a 1 MB rechazada");
datos.ImagenUrl = "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+aV1sAAAAASUVORK5CYII=";
Comprobar(EventosController.Validar(datos) == null, "PNG pequeño aceptado");
var controlador = typeof(EventosController);
Comprobar(controlador.IsDefined(typeof(AuthorizeAttribute), true), "Controlador requiere autenticación");
foreach (var metodo in new[] { "Crear", "Editar", "Eliminar", "ConsultarMios" })
{
    var atributo = controlador.GetMethod(metodo)!.GetCustomAttributes(typeof(AuthorizeAttribute), true)
        .Cast<AuthorizeAttribute>().Single();
    Comprobar(atributo.Roles == "cafeteria", metodo + " exige rol cafetería");
}
var usuario = ObjectId.GenerateNewId();
var otro = ObjectId.GenerateNewId();
var evento = new Evento { Fecha = "2026-12-20", Hora = "12:00", Cupo = 1, EsGratis = true };
var ahora = new DateTime(2026, 11, 14, 18, 0, 0);
Comprobar(ReglasReservas.Validar(evento, usuario, ahora) == null, "Reserva gratuita con lugar válida");
evento.EsGratis = false;
Comprobar(ReglasReservas.Validar(evento, usuario, ahora) != null, "Reserva de paga rechazada");
evento.EsGratis = true;
Comprobar(ReglasReservas.Validar(evento, usuario, new DateTime(2026, 12, 20, 12, 0, 0)) != null, "No reservar al iniciar");
Comprobar(ReglasReservas.Validar(evento, usuario, new DateTime(2027, 1, 1)) != null, "No reservar evento pasado");
evento.Reservas.Add(new Reserva { UsuarioId = usuario });
Comprobar(ReglasReservas.Validar(evento, usuario, ahora) == null, "Reserva confirmada repetida es idempotente");
Comprobar(ReglasReservas.Validar(evento, otro, ahora) != null, "Otro usuario no rebasa el cupo");
evento.Reservas[0].Estado = "cancelada";
Comprobar(ReglasReservas.Validar(evento, otro, ahora) == null, "Cancelada no ocupa lugar");
Comprobar(new Reserva().Codigo != new Reserva().Codigo, "Folios distintos");
foreach (var tipo in new[] { typeof(FavoritosController), typeof(ReservasController) })
{
    Comprobar(tipo.GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>()
        .Any(a => a.Roles == "usuario"), tipo.Name + " solo admite usuarios");
}
// Construye el modelo real del proveedor sin conectar con MongoDB.
using var contexto = new EventHopContext(new DbContextOptionsBuilder<EventHopContext>()
    .UseMongoDB("mongodb://localhost:27018", "EventHopPruebas").Options);
var entidad = contexto.Model.FindEntityType(typeof(Evento))!;
Comprobar(contexto.Model.GetEntityTypes().Count(e => !e.IsOwned()) == 6, "Modelo MongoDB con seis entidades de colección");
Comprobar(entidad.FindProperty(nameof(Evento.Version))!.IsConcurrencyToken, "Versión de evento controla concurrencia");
Comprobar(entidad.FindNavigation(nameof(Evento.Reservas))!.TargetEntityType.IsOwned(), "Reservas embebidas en evento");
Comprobar(contexto.Model.FindEntityType(typeof(Favorito))!.FindPrimaryKey()!.Properties.Single().Name == "Id", "Favorito con clave única");
Console.WriteLine($"{aprobadas} comprobaciones aprobadas. No realizan operaciones en MongoDB.");
await PruebasPagos.Ejecutar();
await PruebasAsistencias.Ejecutar();
await PruebasPerfil.Ejecutar();
await PruebasComunidad.Ejecutar();
await PruebasReportes.Ejecutar();
await PruebasStripe.Ejecutar();
