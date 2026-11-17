using System.Globalization;
using System.Security.Claims;
using EventHop.Api.Data;
using EventHop.Api.Models;
using EventHop.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MongoDB.Bson;

namespace EventHop.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/eventos")]
public class EventosController : ControllerBase
{
    private readonly EventHopContext baseDatos;
    private readonly ArchivoLog log;

    public EventosController(EventHopContext baseDatos, ArchivoLog log)
    {
        this.baseDatos = baseDatos;
        this.log = log;
    }

    private ObjectId MiId => ObjectId.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet]
    public async Task<IActionResult> Consultar()
    {
        var cafes = await baseDatos.Usuarios.AsNoTracking()
            .Where(u => u.TipoUsuario == "cafeteria" && u.Activo && u.EstadoSolicitud == "aprobada")
            .ToListAsync();
        var nombres = cafes.ToDictionary(c => c.Id, c => c.Nombre);
        var eventos = await baseDatos.Eventos.AsNoTracking().OrderBy(e => e.Fecha).ThenBy(e => e.Hora).ToListAsync();
        return Ok(eventos.Where(e => nombres.ContainsKey(e.CafeteriaId))
            .Select(e => Respuesta(e, nombres[e.CafeteriaId])));
    }

    [HttpGet("mios")]
    [Authorize(Roles = "cafeteria")]
    public async Task<IActionResult> ConsultarMios()
    {
        var id = MiId;
        var cafe = await baseDatos.Usuarios.AsNoTracking().FirstAsync(u => u.Id == id);
        var eventos = await baseDatos.Eventos.AsNoTracking().Where(e => e.CafeteriaId == id)
            .OrderBy(e => e.Fecha).ThenBy(e => e.Hora).ToListAsync();
        return Ok(eventos.Select(e => Respuesta(e, cafe.Nombre)));
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> ConsultarUno(string id)
    {
        if (!ObjectId.TryParse(id, out var identificador))
            return BadRequest(new { mensaje = "Identificador inválido." });
        var evento = await baseDatos.Eventos.AsNoTracking().FirstOrDefaultAsync(e => e.Id == identificador);
        if (evento == null) return NotFound(new { mensaje = "Evento no encontrado." });
        var cafe = await baseDatos.Usuarios.AsNoTracking().FirstOrDefaultAsync(u => u.Id == evento.CafeteriaId);
        if (cafe == null || !cafe.Activo || cafe.TipoUsuario != "cafeteria" || cafe.EstadoSolicitud != "aprobada")
            return NotFound(new { mensaje = "Evento no disponible." });
        return Ok(Respuesta(evento, cafe.Nombre));
    }

    [HttpPost]
    [Authorize(Roles = "cafeteria")]
    [RequestSizeLimit(1600000)]
    public async Task<IActionResult> Crear(EventoSolicitud datos)
    {
        var error = Validar(datos);
        if (error != null) return BadRequest(new { mensaje = error });
        var evento = new Evento { CafeteriaId = MiId };
        if (!await baseDatos.Categorias.AnyAsync(c => c.Nombre == datos.Categoria && c.Activa))
            return BadRequest(new { mensaje = "Selecciona una categoría activa del catálogo." });
        Copiar(datos, evento);
        log.Escribir($"Inicio de creación de evento: {evento.Id}, cafetería: {evento.CafeteriaId}");
        baseDatos.Eventos.Add(evento);
        await baseDatos.SaveChangesAsync();
        log.Escribir($"Fin de creación de evento: {evento.Id}");
        return CreatedAtAction(nameof(ConsultarUno), new { id = evento.Id.ToString() },
            new { id = evento.Id.ToString(), mensaje = "Evento creado correctamente." });
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "cafeteria")]
    [RequestSizeLimit(1600000)]
    public async Task<IActionResult> Editar(string id, EventoSolicitud datos)
    {
        if (!ObjectId.TryParse(id, out var identificador))
            return BadRequest(new { mensaje = "Identificador inválido." });
        var error = Validar(datos);
        if (error != null) return BadRequest(new { mensaje = error });
        var propio = MiId;
        var evento = await baseDatos.Eventos.FirstOrDefaultAsync(e => e.Id == identificador && e.CafeteriaId == propio);
        if (evento == null) return NotFound(new { mensaje = "Evento propio no encontrado." });
        var ocupados = ReglasReservas.Ocupados(evento);
        if (datos.Categoria != evento.Categoria &&
            !await baseDatos.Categorias.AnyAsync(c => c.Nombre == datos.Categoria && c.Activa))
            return BadRequest(new { mensaje = "Selecciona una categoría activa del catálogo." });
        if (datos.Cupo < ocupados)
            return Conflict(new { mensaje = "El cupo no puede ser menor que las reservas confirmadas." });
        if (ocupados > 0 && (datos.Fecha != evento.Fecha || datos.Hora != evento.Hora ||
            datos.Direccion.Trim() != evento.Direccion || datos.EsGratis != evento.EsGratis ||
            (!datos.EsGratis && datos.Costo != evento.Costo)))
            return Conflict(new { mensaje = "Con reservas confirmadas no puedes cambiar fecha, hora, dirección o modalidad." });
        log.Escribir($"Inicio de edición de evento: {evento.Id}, cafetería: {propio}");
        Copiar(datos, evento);
        evento.Version++;
        try { await baseDatos.SaveChangesAsync(); }
        catch (DbUpdateConcurrencyException) {
            return Conflict(new { mensaje = "El evento cambió mientras lo editabas. Recarga y vuelve a intentarlo." });
        }
        log.Escribir($"Fin de edición de evento: {evento.Id}");
        return Ok(new { mensaje = "Evento actualizado." });
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "cafeteria")]
    public async Task<IActionResult> Eliminar(string id)
    {
        if (!ObjectId.TryParse(id, out var identificador))
            return BadRequest(new { mensaje = "Identificador inválido." });
        var propio = MiId;
        var evento = await baseDatos.Eventos.FirstOrDefaultAsync(e => e.Id == identificador && e.CafeteriaId == propio);
        if (evento == null) return NotFound(new { mensaje = "Evento propio no encontrado." });
        if (evento.Reservas.Count > 0 || evento.Pagos.Count > 0 ||
            await baseDatos.Avisos.AnyAsync(a => a.EventoId == identificador) ||
            await baseDatos.Resenas.AnyAsync(r => r.EventoId == identificador))
            return Conflict(new { mensaje = "El evento tiene reservas, pagos, avisos o reseñas y no puede eliminarse." });
        log.Escribir($"Inicio de eliminación de evento: {evento.Id}, cafetería: {propio}");
        baseDatos.Eventos.Remove(evento);
        try { await baseDatos.SaveChangesAsync(); }
        catch (DbUpdateConcurrencyException) {
            return Conflict(new { mensaje = "El evento cambió. Recarga antes de eliminarlo." });
        }
        log.Escribir($"Fin de eliminación de evento: {evento.Id}");
        return NoContent();
    }

    public static object Respuesta(Evento e, string cafeteria) => new
    {
        id = e.Id.ToString(), cafeteriaId = e.CafeteriaId.ToString(), cafeteria,
        e.Titulo, e.Descripcion, e.Categoria, e.Direccion, e.Fecha, e.Hora,
        e.EsGratis, e.Costo, e.Cupo, e.ImagenUrl,
        disponibles = Math.Max(0, e.Cupo - ReglasReservas.Ocupados(e))
    };

    private static void Copiar(EventoSolicitud datos, Evento evento)
    {
        evento.Titulo = datos.Titulo.Trim();
        evento.Descripcion = datos.Descripcion.Trim();
        evento.Categoria = datos.Categoria;
        evento.Direccion = datos.Direccion.Trim();
        evento.Fecha = datos.Fecha;
        evento.Hora = datos.Hora;
        evento.EsGratis = datos.EsGratis;
        evento.Costo = datos.EsGratis ? 0 : datos.Costo;
        evento.Cupo = datos.Cupo;
        evento.ImagenUrl = datos.ImagenUrl;
    }

    // Se valida también en el servidor: no se confía solo en el formulario.
    public static string? Validar(EventoSolicitud datos)
    {
        if (string.IsNullOrWhiteSpace(datos.Titulo) || datos.Titulo.Trim().Length < 3 ||
            string.IsNullOrWhiteSpace(datos.Descripcion) || string.IsNullOrWhiteSpace(datos.Direccion))
            return "Completa el título (mínimo 3 caracteres), la descripción y la dirección.";
        if (!DateOnly.TryParseExact(datos.Fecha, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out _) ||
            !TimeOnly.TryParseExact(datos.Hora, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
            return "Escribe una fecha y una hora válidas.";
        if (!double.IsFinite(datos.Costo) || (!datos.EsGratis && (datos.Costo <= 0 ||
            Math.Round(datos.Costo, 2) != datos.Costo)))
            return "Un evento de paga debe tener un costo positivo con máximo dos decimales.";
        if (string.IsNullOrEmpty(datos.ImagenUrl)) { datos.ImagenUrl = ""; return null; }
        var coma = datos.ImagenUrl.IndexOf(',');
        if (coma < 0) return "La imagen no es válida.";
        var prefijo = datos.ImagenUrl[..coma];
        if (prefijo != "data:image/png;base64" && prefijo != "data:image/jpeg;base64" && prefijo != "data:image/webp;base64")
            return "Utiliza una imagen PNG, JPEG o WebP.";
        try
        {
            var bytes = Convert.FromBase64String(datos.ImagenUrl[(coma + 1)..]);
            if (bytes.Length < 12 || bytes.Length > 1048576) return "La imagen debe pesar como máximo 1 MB.";
            var png = bytes.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
            var jpg = bytes[0] == 255 && bytes[1] == 216 && bytes[2] == 255;
            var webp = System.Text.Encoding.ASCII.GetString(bytes, 0, 4) == "RIFF" &&
                System.Text.Encoding.ASCII.GetString(bytes, 8, 4) == "WEBP";
            if (!(prefijo == "data:image/png;base64" && png || prefijo == "data:image/jpeg;base64" && jpg ||
                prefijo == "data:image/webp;base64" && webp)) return "El contenido no coincide con el tipo de imagen.";
        }
        catch (FormatException) { return "La imagen no es válida."; }
        return null;
    }
}
