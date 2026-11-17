using System.Security.Claims;
using System.Text.RegularExpressions;
using EventHop.Api.Data;
using EventHop.Api.Models;
using EventHop.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MongoDB.Bson;

namespace EventHop.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly EventHopContext baseDatos;
    private readonly PasswordHasher<Usuario> hasher;
    private readonly TokenService tokens;
    private readonly ArchivoLog log;

    public AuthController(EventHopContext baseDatos, PasswordHasher<Usuario> hasher,
        TokenService tokens, ArchivoLog log)
    {
        this.baseDatos = baseDatos;
        this.hasher = hasher;
        this.tokens = tokens;
        this.log = log;
    }

    [AllowAnonymous]
    [HttpPost("registro")]
    public async Task<IActionResult> Registrar(RegistroSolicitud datos)
    {
        var nombre = datos.Nombre.Trim();
        var correo = datos.Correo.Trim().ToLowerInvariant();
        var rfc = (datos.Rfc ?? "").Trim().ToUpperInvariant();

        if (nombre.Length < 2)
            return BadRequest(new { mensaje = "Escribe un nombre válido." });

        if (!datos.Password.Any(char.IsLetter) || !datos.Password.Any(char.IsDigit))
            return BadRequest(new { mensaje = "La contraseña necesita letras y números." });

        if (datos.Rol == "cafeteria" &&
            !Regex.IsMatch(rfc, @"^[A-ZÑ&]{3,4}\d{6}[A-Z0-9]{3}$"))
            return BadRequest(new { mensaje = "El RFC debe tener un formato válido de 12 o 13 caracteres." });

        if (await baseDatos.Usuarios.AnyAsync(usuario => usuario.Correo == correo))
            return Conflict(new { mensaje = "Este correo ya está registrado." });

        var usuario = new Usuario
        {
            Nombre = nombre,
            Correo = correo,
            TipoUsuario = datos.Rol,
            Rfc = datos.Rol == "cafeteria" ? rfc : "",
            EstadoSolicitud = datos.Rol == "cafeteria" ? "pendiente" : "no_aplica"
        };
        usuario.Contrasena = hasher.HashPassword(usuario, datos.Password);

        log.Escribir("Inicio de registro de cuenta.");
        baseDatos.Usuarios.Add(usuario);
        await baseDatos.SaveChangesAsync();
        log.Escribir($"Fin de registro. Usuario: {usuario.Id}");

        return StatusCode(201, new
        {
            mensaje = datos.Rol == "cafeteria"
                ? "Solicitud enviada. Espera la aprobación de un administrador."
                : "Cuenta creada correctamente.",
            usuario = UsuarioRespuesta.Desde(usuario)
        });
    }

    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<IActionResult> IniciarSesion(LoginSolicitud datos)
    {
        var correo = datos.Correo.Trim().ToLowerInvariant();
        var usuario = await baseDatos.Usuarios.FirstOrDefaultAsync(u => u.Correo == correo);

        if (usuario == null)
            return Unauthorized(new { mensaje = "Correo o contraseña incorrectos." });

        PasswordVerificationResult resultado;
        try
        {
            resultado = hasher.VerifyHashedPassword(usuario, usuario.Contrasena, datos.Password);
        }
        catch (FormatException)
        {
            // No se aceptan contraseñas antiguas guardadas como texto plano.
            return Unauthorized(new { mensaje = "Correo o contraseña incorrectos." });
        }

        if (resultado == PasswordVerificationResult.Failed)
            return Unauthorized(new { mensaje = "Correo o contraseña incorrectos." });

        if (!usuario.Activo || (usuario.TipoUsuario == "cafeteria" && usuario.EstadoSolicitud != "aprobada"))
            return StatusCode(403, new { mensaje = "La cuenta está inactiva o su solicitud no ha sido aprobada." });

        if (resultado == PasswordVerificationResult.SuccessRehashNeeded)
        {
            usuario.Contrasena = hasher.HashPassword(usuario, datos.Password);
            await baseDatos.SaveChangesAsync();
        }

        var vence = DateTime.UtcNow.AddHours(2);
        log.Escribir($"Login correcto. Usuario: {usuario.Id}");
        return Ok(new { token = tokens.Crear(usuario, vence), vence,
            usuario = UsuarioRespuesta.Desde(usuario) });
    }

    [HttpGet("yo")]
    public async Task<IActionResult> ConsultarSesion()
    {
        var id = ObjectId.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var usuario = await baseDatos.Usuarios.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id);
        return usuario == null ? Unauthorized() : Ok(UsuarioRespuesta.Desde(usuario));
    }
}
