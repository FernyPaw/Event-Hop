using EventHop.Api.Data;
using EventHop.Api.Models;
using EventHop.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MongoDB.Bson;

namespace EventHop.Api.Controllers;

[ApiController]
[Authorize(Roles = "admin")]
[Route("api/admin/solicitudes")]
public class AdminController : ControllerBase
{
    private readonly EventHopContext baseDatos;
    private readonly ArchivoLog log;

    public AdminController(EventHopContext baseDatos, ArchivoLog log)
    {
        this.baseDatos = baseDatos;
        this.log = log;
    }

    [HttpGet]
    public async Task<IActionResult> Consultar()
    {
        var usuarios = await baseDatos.Usuarios.AsNoTracking()
            .Where(u => u.TipoUsuario == "cafeteria" && u.EstadoSolicitud == "pendiente")
            .ToListAsync();
        return Ok(usuarios.Select(UsuarioRespuesta.Desde));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Revisar(string id, RevisionSolicitud datos)
    {
        if (!ObjectId.TryParse(id, out var identificador))
            return BadRequest(new { mensaje = "Identificador inválido." });

        if (datos.Estado == "rechazada" && string.IsNullOrWhiteSpace(datos.Motivo))
            return BadRequest(new { mensaje = "Escribe el motivo del rechazo." });

        var usuario = await baseDatos.Usuarios.FirstOrDefaultAsync(u => u.Id == identificador);
        if (usuario == null || usuario.TipoUsuario != "cafeteria")
            return NotFound(new { mensaje = "Solicitud no encontrada." });

        if (usuario.EstadoSolicitud != "pendiente")
            return Conflict(new { mensaje = "Esta solicitud ya fue revisada." });

        log.Escribir($"Inicio de revisión de solicitud: {usuario.Id}");
        usuario.EstadoSolicitud = datos.Estado;
        usuario.MotivoRechazo = datos.Estado == "rechazada" ? datos.Motivo.Trim() : "";
        await baseDatos.SaveChangesAsync();
        log.Escribir($"Fin de revisión de solicitud: {usuario.Id}, estado: {datos.Estado}");
        return Ok(new { mensaje = "Solicitud actualizada." });
    }
}
