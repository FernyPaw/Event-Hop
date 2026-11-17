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

public static class PruebasReportes
{
    public static async Task Ejecutar()
    {
        var total = 0;
        void Ok(bool valor, string texto) { if (!valor) throw new Exception("Reporte: " + texto); total++; Console.WriteLine("OK reporte: " + texto); }
        var ahora = DateTime.UtcNow;
        var cafe = new Usuario { Nombre = "Café A", TipoUsuario = "cafeteria", EstadoSolicitud = "aprobada" };
        var otro = new Usuario { Nombre = "Café B", TipoUsuario = "cafeteria", EstadoSolicitud = "pendiente" };
        var e = new Evento { Titulo = "Evento A", Categoria = "Juegos", Fecha = "2026-11-15", CafeteriaId = cafe.Id, Cupo = 5,
            Reservas = [new Reserva { FechaEntrada = ahora }, new Reserva(), new Reserva { Estado = "cancelada" }],
            Pagos = [new Pago { Estado = "pagado", Centavos = 5000 }, new Pago { Estado = "reembolsado", Centavos = 2000 },
                new Pago { Estado = "reembolsando", Centavos = 1000 }, new Pago { Estado = "pendiente", Centavos = 9000, Vence = ahora.AddMinutes(5) },
                new Pago { Estado = "pendiente", Centavos = 8000, Vence = ahora.AddMinutes(-5) }, new Pago { Estado = "cancelado", Centavos = 7000 } ] };
        var e2 = new Evento { Titulo = "Evento B", Fecha = "2026-11-16", CafeteriaId = otro.Id, Cupo = 0 };
        var opiniones = new List<Resena> { new() { Id = "r1", EventoId = e.Id, Calificacion = 5 }, new() { Id = "r2", EventoId = e.Id, Calificacion = 3 }, new() { Id = "r3", EventoId = e2.Id, Calificacion = 1 } };
        var favoritos = new List<Favorito> { new() { Id = "f1", EventoId = e.Id } };
        var nombres = new Dictionary<ObjectId, string> { [cafe.Id] = cafe.Nombre, [otro.Id] = otro.Nombre };
        ReporteRespuesta Calcular(string tipo) => ReportesController.Calcular(tipo, [e, e2], nombres, favoritos, opiniones, ahora);
        var r = Calcular("eventos");
        Ok(r.Filas.Count == 2 && r.Filas[0][1] == "Café A", "Combina evento y nombre de cafetería");
        Ok(r.Filas[0][5] == "2" && r.Filas[0][6] == "1" && r.Filas[0][7] == "2", "Confirmadas y retención vigente; vencida no ocupa");
        Ok(r.Filas[0][8] == "60.0" && r.Filas[0][9] == "1", "Ocupación y favoritos reales");
        Ok(r.Filas[1][8] == "0.0", "Cupo cero no divide entre cero");
        r = Calcular("asistencia");
        Ok(r.Filas[0][3] == "2" && r.Filas[0][4] == "1" && r.Filas[0][5] == "1" && r.Filas[0][6] == "1" && r.Filas[0][7] == "50.0", "Asistencia excluye canceladas y sin entrada");
        Ok(r.Indicadores["Asistencia %"] == "50.0", "Porcentaje global sobre reservas confirmadas");
        r = Calcular("pagos");
        Ok(r.Filas[0][7] == "80.00" && r.Filas[0][8] == "20.00" && r.Filas[0][9] == "60.00", "Centavos exactos: cobrado menos reembolsado");
        Ok(r.Filas[0][5] == "1" && r.Filas[0][6] == "2", "En reembolso y sin resolver separados");
        Ok(r.Indicadores["Neto MXN (Sandbox)"] == "60.00", "Pendientes y cancelados no suman dinero");
        r = Calcular("resenas");
        Ok(r.Filas[0][3] == "2" && r.Filas[0][4] == "4.00" && r.Filas[0][7] == "1" && r.Filas[0][9] == "1", "Promedio y distribución de estrellas");
        Ok(r.Indicadores["Promedio / 5"] == "3.00", "Promedio global ponderado por reseñas");
        var vacio = ReportesController.Calcular("resenas", [], [], [], [], ahora);
        Ok(vacio.Filas.Count == 0 && vacio.Indicadores["Promedio / 5"] == "Sin reseñas", "Vacío no inventa reseñas ni promedio cero");
        Ok(!ReportesController.FechaValida("2026-02-30") && !ReportesController.FechaValida("15/11/2026") && ReportesController.FechaValida("2028-02-29"), "Fechas estrictas y bisiesto");
        using var db = new EventHopContext(new DbContextOptionsBuilder<EventHopContext>().UseInMemoryDatabase("reportes-" + Guid.NewGuid()).Options);
        db.Usuarios.AddRange(cafe, otro, new Usuario { Nombre = "Asistente" }, new Usuario { TipoUsuario = "admin" });
        db.Eventos.AddRange(e, e2); db.Resenas.AddRange(opiniones); db.Favoritos.AddRange(favoritos); await db.SaveChangesAsync();
        var controller = new ReportesController(db, new ArchivoLog(new EntornoPrueba()));
        void Identidad(string rol) => controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext {
            User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Role, rol), new Claim(ClaimTypes.NameIdentifier, cafe.Id.ToString())], "test")) } };
        JsonElement Json(IActionResult respuesta) => JsonSerializer.SerializeToElement(((OkObjectResult)respuesta).Value, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Identidad("cafeteria");
        var lectura = Json(await controller.Consultar("eventos"));
        Ok(lectura.GetProperty("filas").GetArrayLength() == 1 && lectura.GetProperty("filas")[0][0].GetString() == "Evento A", "Cafetería solo consulta su evento");
        Ok(await controller.Consultar("eventos", cafeteriaId: otro.Id.ToString()) is ForbidResult, "No amplía alcance con ID ajeno");
        Ok(Json(await controller.Consultar("eventos", "2026-11-15", "2026-11-15")).GetProperty("filas").GetArrayLength() == 1, "Rango incluye extremos");
        Ok(Json(await controller.Consultar("eventos", "2026-11-16")).GetProperty("filas").GetArrayLength() == 0, "Filtro sin coincidencias vacío");
        Ok(await controller.Consultar("pagos", "2026-11-17", "2026-11-15") is BadRequestObjectResult, "Rango invertido rechazado");
        Ok(await controller.Consultar("desconocido") is BadRequestObjectResult, "No acepta reporte arbitrario");
        var resumen = Json(await controller.Resumen());
        Ok(resumen.GetProperty("favoritos").GetInt32() == 1 && resumen.GetProperty("reservas").GetInt32() == 2 && resumen.GetProperty("entradas").GetInt32() == 1, "Resumen cafetería real");
        Identidad("admin");
        Ok(Json(await controller.Consultar("eventos")).GetProperty("filas").GetArrayLength() == 2, "Administrador ve ambos propietarios");
        Ok(Json(await controller.Consultar("eventos", cafeteriaId: otro.Id.ToString())).GetProperty("filas").GetArrayLength() == 1, "Administrador filtra cafetería");
        Ok(await controller.Consultar("eventos", cafeteriaId: "no-valido") is BadRequestObjectResult, "ID inválido rechazado");
        resumen = Json(await controller.Resumen());
        Ok(resumen.GetProperty("totalUsuarios").GetInt32() == 1 && resumen.GetProperty("cafeteriasAceptadas").GetInt32() == 1 && resumen.GetProperty("totalSolicitudesCafeterias").GetInt32() == 2 && resumen.GetProperty("totalEventos").GetInt32() == 2, "Panel no utiliza cifras de ejemplo");
        Ok(typeof(ReportesController).GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>().Any(a => a.Roles == "admin,cafeteria"), "Usuario normal no tiene rol de reportes");
        Ok(!db.ChangeTracker.HasChanges() && e.Version == 0 && e.Pagos[0].Estado == "pagado", "Consultas no modifican reservas, pagos ni versión");
        Ok(!lectura.ToString().Contains("contrasena") && !lectura.ToString().Contains("codigo"), "Reporte no expone hash ni código de boleto");
        Console.WriteLine($"{total} comprobaciones de reportes aprobadas.");
    }
}
