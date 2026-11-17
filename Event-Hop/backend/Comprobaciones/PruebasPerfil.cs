using System.ComponentModel.DataAnnotations;
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

public static class PruebasPerfil
{
    public static async Task Ejecutar()
    {
        var total = 0;
        void Ok(bool condicion, string nombre) {
            if (!condicion) throw new Exception("Falló perfil: " + nombre);
            total++; Console.WriteLine("OK perfil: " + nombre);
        }
        using var db = new EventHopContext(new DbContextOptionsBuilder<EventHopContext>()
            .UseInMemoryDatabase("perfil-" + Guid.NewGuid()).Options);
        var uno = new Usuario { Nombre = "Pedro", Correo = "pedro@example.test", Contrasena = "hash-no-exponer" };
        var dos = new Usuario { Nombre = "Otra cuenta", TipoUsuario = "cafeteria", Rfc = "ABC010101XXX", EstadoSolicitud = "aprobada" };
        db.Usuarios.AddRange(uno, dos); await db.SaveChangesAsync();
        var controlador = new PerfilController(db, new ArchivoLog(new EntornoPrueba())) {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext {
                User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, uno.Id.ToString())], "prueba"))
            } }
        };
        var consulta = (OkObjectResult)await controlador.Consultar();
        Ok(((UsuarioRespuesta)consulta.Value!).Nombre == "Pedro", "Consulta cuenta del token");
        Ok(!JsonSerializer.Serialize(consulta.Value).Contains("hash-no-exponer"), "No devuelve hash");
        var guardado = (OkObjectResult)await controlador.Actualizar(new PerfilSolicitud { Nombre = " Pedro Nuevo " });
        Ok(((UsuarioRespuesta)guardado.Value!).Nombre == "Pedro Nuevo", "Normaliza nombre y responde dato guardado");
        db.ChangeTracker.Clear();
        var propio = await db.Usuarios.SingleAsync(u => u.Id == uno.Id);
        var ajeno = await db.Usuarios.SingleAsync(u => u.Id == dos.Id);
        Ok(propio.Nombre == "Pedro Nuevo", "Persiste nombre mediante ORM");
        Ok(ajeno.Nombre == "Otra cuenta", "No modifica otra cuenta");
        Ok(propio.Correo == uno.Correo && propio.TipoUsuario == "usuario" && propio.Contrasena == "hash-no-exponer", "Conserva correo, permisos y hash");
        foreach (var nombre in new[] { " ", "a", new string('a', 101), "Pedro\nNuevo" })
            Ok(await controlador.Actualizar(new PerfilSolicitud { Nombre = nombre }) is BadRequestObjectResult, "Rechaza nombre inválido");
        var solicitud = new PerfilSolicitud { Nombre = "a" };
        Ok(!Validator.TryValidateObject(solicitud, new ValidationContext(solicitud), [], true), "Validación de modelo limita longitud");
        Ok(typeof(PerfilController).IsDefined(typeof(AuthorizeAttribute), true), "Perfil exige autenticación");
        controlador.ControllerContext.HttpContext.User = new ClaimsPrincipal();
        Ok(await controlador.Consultar() is UnauthorizedResult, "Sin identidad no consulta");
        Ok(await controlador.Actualizar(new PerfilSolicitud { Nombre = "Intruso" }) is UnauthorizedResult, "Sin identidad no modifica");
        Console.WriteLine($"{total} comprobaciones de perfil aprobadas. EF InMemory, sin MongoDB real.");
    }
}
