using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace EventHop.Api.Models;

public class Usuario
{
    public ObjectId Id { get; set; } = ObjectId.GenerateNewId();
    [BsonElement("nombre")]
    public string Nombre { get; set; } = "";
    [BsonElement("correo")]
    public string Correo { get; set; } = "";
    [BsonElement("contrasena")]
    public string Contrasena { get; set; } = "";
    [BsonElement("tipoUsuario")]
    public string TipoUsuario { get; set; } = "usuario";
    [BsonElement("rfc")]
    public string Rfc { get; set; } = "";
    [BsonElement("estadoSolicitud")]
    public string EstadoSolicitud { get; set; } = "no_aplica";
    [BsonElement("motivoRechazo")]
    public string MotivoRechazo { get; set; } = "";
    [BsonElement("fechaRegistro")]
    public DateTime FechaRegistro { get; set; } = DateTime.UtcNow;
    [BsonElement("activo")]
    public bool Activo { get; set; } = true;
}
