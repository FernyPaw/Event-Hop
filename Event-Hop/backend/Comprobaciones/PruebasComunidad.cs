using System.Security.Claims;
using System.Text.Json;
using EventHop.Api.Controllers;
using EventHop.Api.Data;
using EventHop.Api.Models;
using EventHop.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MongoDB.Bson;

public static class PruebasComunidad
{
    public static async Task Ejecutar()
    {
        var total = 0;
        void Ok(bool valor, string mensaje) {
            if (!valor) throw new Exception("Falló comunidad: " + mensaje);
            total++; Console.WriteLine("OK comunidad: " + mensaje);
        }
        void Identidad(ControllerBase c, ObjectId id, string rol) => c.ControllerContext = new ControllerContext {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([
                new Claim(ClaimTypes.NameIdentifier, id.ToString()), new Claim(ClaimTypes.Role, rol)], "prueba")) }
        };
        JsonElement Json(IActionResult resultado) => JsonSerializer.SerializeToElement(((OkObjectResult)resultado).Value,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        async Task Rechaza(Func<Task> accion, int estado, string mensaje) {
            try { await accion(); throw new Exception("No rechazó " + mensaje); }
            catch (OperacionException e) { Ok(e.Estado == estado, mensaje); }
        }
        using var db = new EventHopContext(new DbContextOptionsBuilder<EventHopContext>()
            .UseInMemoryDatabase("comunidad-" + Guid.NewGuid()).Options);
        var log = new ArchivoLog(new EntornoPrueba());
        var cafe = new Usuario { Nombre = "Café", TipoUsuario = "cafeteria", EstadoSolicitud = "aprobada" };
        var usuario = new Usuario { Nombre = "Asistente" };
        var otro = new Usuario { Nombre = "Sin entrada" };
        db.Usuarios.AddRange(cafe, usuario, otro);
        var reserva = new Reserva { UsuarioId = usuario.Id, FechaEntrada = DateTime.UtcNow };
        var evento = new Evento { CafeteriaId = cafe.Id, Titulo = "Prueba", Categoria = "Taller", Reservas = [reserva] };
        var segundo = new Evento { CafeteriaId = cafe.Id, Titulo = "Segundo", Categoria = "Taller" };
        db.Eventos.AddRange(evento, segundo); await db.SaveChangesAsync();
        var categorias = new CategoriasController(db, log); Identidad(categorias, cafe.Id, "admin");
        Ok(await categorias.Crear(new CategoriaSolicitud { Nombre = " Taller ", Descripcion = "Descripción" }) is ObjectResult { StatusCode: 201 }, "Crear categoría");
        var categoria = await db.Categorias.SingleAsync();
        Ok(await categorias.Crear(new CategoriaSolicitud { Nombre = "taller" }) is ConflictObjectResult, "Duplicado normalizado bloqueado");
        Ok(await categorias.Editar(categoria.Id, new CategoriaSolicitud { Nombre = "Taller", Descripcion = "Nueva" }) is OkObjectResult, "Editar descripción");
        Ok(await categorias.Editar(categoria.Id, new CategoriaSolicitud { Nombre = "Cambiar clave" }) is ConflictObjectResult, "No romper referencia de categoría");
        await categorias.Eliminar(categoria.Id);
        Ok(!categoria.Activa && (await db.Eventos.FindAsync(evento.Id))!.Categoria == "Taller", "Baja lógica conserva eventos");
        Identidad(categorias, usuario.Id, "usuario");
        Ok(Json(await categorias.Consultar()).GetArrayLength() == 0, "Usuario no recibe categorías inactivas");
        Identidad(categorias, cafe.Id, "admin");
        await categorias.Editar(categoria.Id, new CategoriaSolicitud { Nombre = "Taller", Activa = true });
        Ok(categoria.Activa, "Reactivar categoría");
        Ok(CategoriasController.Validar(new CategoriaSolicitud { Nombre = " " }) != null, "Nombre inválido bloqueado");
        var eventosController = new EventosController(db, log); Identidad(eventosController, cafe.Id, "cafeteria");
        Ok(await eventosController.Crear(new EventoSolicitud { Titulo = "Prueba", Descripcion = "Prueba",
            Categoria = "Inexistente", Direccion = "Calle", Fecha = "2027-01-01", Hora = "18:00", EsGratis = true, Cupo = 2 })
            is BadRequestObjectResult, "Evento exige categoría existente");
        Ok(ComunidadController.PuedeResenar(evento, usuario.Id), "Entrada habilita reseña");
        reserva.FechaEntrada = null;
        Ok(!ComunidadController.PuedeResenar(evento, usuario.Id), "Reserva sin entrada no reseña");
        reserva.FechaEntrada = DateTime.UtcNow; reserva.Estado = "cancelada";
        Ok(!ComunidadController.PuedeResenar(evento, usuario.Id), "Reserva cancelada no reseña");
        reserva.Estado = "confirmada"; reserva.PagoId = "p1";
        evento.Pagos.Add(new Pago { Identificador = "p1", UsuarioId = usuario.Id, Estado = "reembolsando" });
        Ok(!ComunidadController.PuedeResenar(evento, usuario.Id), "Pago reembolsándose no reseña");
        evento.Pagos[0].Estado = "pagado";
        Ok(ComunidadController.PuedeResenar(evento, usuario.Id), "Entrada pagada confirmada reseña");
        reserva.PagoId = ""; evento.Pagos.Clear();
        Ok(!ComunidadController.PuedeResenar(evento, otro.Id), "No asistente no reseña");
        var comunidad = new ComunidadController(db, log); Identidad(comunidad, usuario.Id, "usuario");
        Ok(await comunidad.GuardarResena(evento.Id.ToString(), new ResenaSolicitud { Calificacion = 5, Comentario = "Excelente evento" }) is OkObjectResult, "Crear reseña propia");
        await comunidad.GuardarResena(evento.Id.ToString(), new ResenaSolicitud { Calificacion = 4, Comentario = "Comentario editado" });
        Ok(await db.Resenas.CountAsync() == 1 && (await db.Resenas.SingleAsync()).Calificacion == 4, "Editar sin duplicar reseña");
        var lectura = Json(await comunidad.Consultar(evento.Id.ToString()));
        Ok(lectura.GetProperty("puedeResenar").GetBoolean() && lectura.GetProperty("resenas")[0].GetProperty("esMia").GetBoolean(), "Consulta reseña y autor propio");
        Identidad(comunidad, otro.Id, "usuario");
        Ok(await comunidad.GuardarResena(evento.Id.ToString(), new ResenaSolicitud { Calificacion = 5, Comentario = "Intruso" }) is ConflictObjectResult, "No asistente no modifica reseña");
        Ok(await comunidad.EliminarResena(evento.Id.ToString()) is NotFoundObjectResult, "Otra cuenta no borra reseña");
        Identidad(comunidad, usuario.Id, "usuario");
        Ok(await comunidad.GuardarResena(evento.Id.ToString(), new ResenaSolicitud { Calificacion = 6, Comentario = "Mal" }) is BadRequestObjectResult, "Calificación fuera de rango bloqueada");
        Identidad(comunidad, cafe.Id, "cafeteria");
        Ok(await comunidad.CrearAviso(evento.Id.ToString(), new AvisoSolicitud { Titulo = "Recepción", Contenido = "Trae tu código completo." }) is ObjectResult { StatusCode: 201 }, "Cafetería publica aviso propio");
        var aviso = await db.Avisos.SingleAsync();
        Ok(await comunidad.EditarAviso(evento.Id.ToString(), aviso.Id, new AvisoSolicitud { Titulo = "Actualización", Contenido = "Nueva indicación." }) is OkObjectResult, "Editar aviso propio");
        Ok(await comunidad.EditarAviso(segundo.Id.ToString(), aviso.Id, new AvisoSolicitud { Titulo = "Intruso", Contenido = "Otro evento" }) is NotFoundObjectResult, "Aviso no se edita desde otro evento");
        Identidad(comunidad, ObjectId.GenerateNewId(), "cafeteria");
        await Rechaza(async () => await comunidad.Consultar(evento.Id.ToString()), 404, "Otra cafetería no consulta comunidad ajena");
        await Rechaza(async () => await comunidad.CrearAviso(evento.Id.ToString(), new AvisoSolicitud { Titulo = "Otro", Contenido = "Intruso" }), 404, "Otra cafetería no publica");
        Identidad(comunidad, usuario.Id, "usuario");
        lectura = Json(await comunidad.Consultar(evento.Id.ToString()));
        Ok(lectura.GetProperty("avisos").GetArrayLength() == 1, "Reserva confirmada ve aviso");
        Identidad(comunidad, otro.Id, "usuario");
        lectura = Json(await comunidad.Consultar(evento.Id.ToString()));
        Ok(!lectura.GetProperty("puedeVerAvisos").GetBoolean() && lectura.GetProperty("avisos").GetArrayLength() == 0, "Sin reserva no se filtra aviso");
        Identidad(comunidad, usuario.Id, "usuario");
        await comunidad.EliminarResena(evento.Id.ToString());
        Ok(await db.Resenas.CountAsync() == 0, "Eliminar reseña propia");
        Identidad(comunidad, cafe.Id, "cafeteria");
        await comunidad.EliminarAviso(evento.Id.ToString(), aviso.Id);
        Ok(await db.Avisos.CountAsync() == 0, "Eliminar aviso propio");
        await comunidad.CrearAviso(segundo.Id.ToString(), new AvisoSolicitud { Titulo = "Historial", Contenido = "No borrar evento." });
        Ok(await eventosController.Eliminar(segundo.Id.ToString()) is ConflictObjectResult, "No eliminar evento con historial de avisos");
        await Rechaza(async () => await comunidad.Consultar("invalido"), 400, "Identificador inválido controlado");
        Ok(ComunidadController.ValidarAviso(new AvisoSolicitud { Titulo = " ", Contenido = " " }) != null, "Aviso vacío rechazado");
        foreach (var metodo in new[] { "CrearAviso", "EditarAviso", "EliminarAviso" })
            Ok(typeof(ComunidadController).GetMethod(metodo)!.GetCustomAttributes(typeof(AuthorizeAttribute), true)
                .Cast<AuthorizeAttribute>().Any(a => a.Roles == "cafeteria"), metodo + " exige cafetería");
        foreach (var metodo in new[] { "GuardarResena", "EliminarResena" })
            Ok(typeof(ComunidadController).GetMethod(metodo)!.GetCustomAttributes(typeof(AuthorizeAttribute), true)
                .Cast<AuthorizeAttribute>().Any(a => a.Roles == "usuario"), metodo + " exige usuario");
        Console.WriteLine($"{total} comprobaciones de categorías, reseñas y avisos aprobadas con EF InMemory.");
    }
}
