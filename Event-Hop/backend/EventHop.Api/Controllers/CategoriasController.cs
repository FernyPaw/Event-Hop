using EventHop.Api.Data;
using EventHop.Api.Models;
using EventHop.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EventHop.Api.Controllers;

[ApiController, Authorize, Route("api/categorias")]
public class CategoriasController(EventHopContext db, ArchivoLog log) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Consultar()
    {
        var categorias = await db.Categorias.AsNoTracking().OrderBy(c => c.Nombre).ToListAsync();
        return Ok(User.IsInRole("admin") ? categorias : categorias.Where(c => c.Activa));
    }

    [HttpPost, Authorize(Roles = "admin")]
    public async Task<IActionResult> Crear(CategoriaSolicitud datos)
    {
        var error = Validar(datos);
        if (error != null) return BadRequest(new { mensaje = error });
        var normal = datos.Nombre.Trim().ToUpperInvariant();
        if (await db.Categorias.AnyAsync(c => c.NombreNormalizado == normal))
            return Conflict(new { mensaje = "Ya existe esa categoría, incluso si está desactivada." });
        var categoria = new Categoria { Nombre = datos.Nombre.Trim(), NombreNormalizado = normal,
            Descripcion = (datos.Descripcion ?? "").Trim(), Activa = datos.Activa };
        db.Categorias.Add(categoria); await db.SaveChangesAsync();
        log.Escribir($"Categoría creada: {categoria.Id}");
        return StatusCode(201, categoria);
    }

    [HttpPut("{id}"), Authorize(Roles = "admin")]
    public async Task<IActionResult> Editar(string id, CategoriaSolicitud datos)
    {
        var error = Validar(datos);
        if (error != null) return BadRequest(new { mensaje = error });
        var categoria = await db.Categorias.FirstOrDefaultAsync(c => c.Id == id);
        if (categoria == null) return NotFound(new { mensaje = "Categoría no encontrada." });
        // El nombre es la referencia que ya utilizan los eventos anteriores.
        // Se conserva siempre; descripción y disponibilidad sí son editables.
        if (categoria.Nombre != datos.Nombre.Trim())
            return Conflict(new { mensaje = "Conserva el nombre de referencia; puedes editar descripción y disponibilidad." });
        var normal = datos.Nombre.Trim().ToUpperInvariant();
        if (await db.Categorias.AnyAsync(c => c.Id != id && c.NombreNormalizado == normal))
            return Conflict(new { mensaje = "Ya existe esa categoría." });
        categoria.Nombre = datos.Nombre.Trim(); categoria.NombreNormalizado = normal;
        categoria.Descripcion = (datos.Descripcion ?? "").Trim(); categoria.Activa = datos.Activa;
        await db.SaveChangesAsync(); log.Escribir($"Categoría editada: {id}");
        return Ok(categoria);
    }

    [HttpDelete("{id}"), Authorize(Roles = "admin")]
    public async Task<IActionResult> Eliminar(string id)
    {
        var categoria = await db.Categorias.FirstOrDefaultAsync(c => c.Id == id);
        if (categoria == null) return NotFound(new { mensaje = "Categoría no encontrada." });
        // Baja lógica: los eventos históricos nunca pierden su categoría.
        categoria.Activa = false; await db.SaveChangesAsync();
        log.Escribir($"Categoría desactivada: {id}"); return NoContent();
    }

    public static string? Validar(CategoriaSolicitud d) =>
        string.IsNullOrWhiteSpace(d.Nombre) || d.Nombre.Trim().Length < 2 || d.Nombre.Length > 50 ||
        d.Nombre.Any(char.IsControl) || (d.Descripcion ?? "").Length > 300
            ? "Escribe una categoría de 2 a 50 caracteres y descripción de máximo 300." : null;
}
