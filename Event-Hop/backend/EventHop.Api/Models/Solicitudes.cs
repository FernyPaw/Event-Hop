using System.ComponentModel.DataAnnotations;

namespace EventHop.Api.Models;

public class RegistroSolicitud
{
    [Required, StringLength(100, MinimumLength = 2)]
    public string Nombre { get; set; } = "";
    [Required, EmailAddress, StringLength(150)]
    public string Correo { get; set; } = "";
    [Required, StringLength(100, MinimumLength = 8)]
    public string Password { get; set; } = "";
    [Required, RegularExpression("^(usuario|cafeteria)$")]
    public string Rol { get; set; } = "usuario";
    [StringLength(13)]
    public string Rfc { get; set; } = "";
}

public class LoginSolicitud
{
    [Required, EmailAddress, StringLength(150)]
    public string Correo { get; set; } = "";
    [Required, StringLength(100)]
    public string Password { get; set; } = "";
}

public class RevisionSolicitud
{
    [Required, RegularExpression("^(aprobada|rechazada)$")]
    public string Estado { get; set; } = "";
    [StringLength(500)]
    public string Motivo { get; set; } = "";
}

// Esta respuesta nunca contiene la contraseña ni su hash.
public class UsuarioRespuesta
{
    public string Id { get; set; } = "";
    public string Nombre { get; set; } = "";
    public string Correo { get; set; } = "";
    public string Rol { get; set; } = "";
    public string Rfc { get; set; } = "";
    public string EstadoSolicitud { get; set; } = "";

    public static UsuarioRespuesta Desde(Usuario usuario)
    {
        return new UsuarioRespuesta
        {
            Id = usuario.Id.ToString(),
            Nombre = usuario.Nombre,
            Correo = usuario.Correo,
            Rol = usuario.TipoUsuario,
            Rfc = usuario.Rfc,
            EstadoSolicitud = usuario.EstadoSolicitud
        };
    }
}
