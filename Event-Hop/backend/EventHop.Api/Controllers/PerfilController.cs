using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using EventHop.Api.Data;
using EventHop.Api.Models;
using EventHop.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MongoDB.Bson;

namespace EventHop.Api.Controllers;

[ApiController, Authorize]
[Route("api/perfil")]
public class PerfilController(EventHopContext baseDatos, ArchivoLog log) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Consultar()
    {
        if (!ObjectId.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id))
            return Unauthorized();
        var usuario = await baseDatos.Usuarios.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id);
        return usuario == null ? Unauthorized() : Ok(UsuarioRespuesta.Desde(usuario));
    }

    [HttpPut]
    public async Task<IActionResult> Actualizar(PerfilSolicitud datos)
    {
        var nombre = (datos.Nombre ?? "").Trim();
        if (nombre.Length < 2 || nombre.Length > 100 || nombre.Any(char.IsControl))
            return BadRequest(new { mensaje = "Escribe un nombre válido de 2 a 100 caracteres." });
        if (!ObjectId.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id))
            return Unauthorized();
        var usuario = await baseDatos.Usuarios.FirstOrDefaultAsync(u => u.Id == id);
        if (usuario == null) return Unauthorized();
        // Solo se modifica el nombre de la cuenta identificada por el token.
        usuario.Nombre = nombre;
        await baseDatos.SaveChangesAsync();
        log.Escribir($"Perfil actualizado. Usuario: {id}");
        return Ok(UsuarioRespuesta.Desde(usuario));
    }
}

public class PerfilSolicitud
{
    [Required, StringLength(100, MinimumLength = 2)]
    public string Nombre { get; set; } = "";
}
