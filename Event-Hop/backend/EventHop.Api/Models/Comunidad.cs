using System.ComponentModel.DataAnnotations;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace EventHop.Api.Models;

public class Categoria
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    [BsonElement("nombre")] public string Nombre { get; set; } = "";
    [BsonElement("nombreNormalizado")] public string NombreNormalizado { get; set; } = "";
    [BsonElement("descripcion")] public string Descripcion { get; set; } = "";
    [BsonElement("activa")] public bool Activa { get; set; } = true;
}

public class Resena
{
    // Una reseña por cuenta y evento; la clave primaria evita duplicados.
    public string Id { get; set; } = "";
    [BsonElement("eventoId")] public ObjectId EventoId { get; set; }
    [BsonElement("usuarioId")] public ObjectId UsuarioId { get; set; }
    [BsonElement("calificacion")] public int Calificacion { get; set; }
    [BsonElement("comentario")] public string Comentario { get; set; } = "";
    [BsonElement("fechaActualizacion")] public DateTime FechaActualizacion { get; set; } = DateTime.UtcNow;
}

public class Aviso
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    [BsonElement("eventoId")] public ObjectId EventoId { get; set; }
    [BsonElement("cafeteriaId")] public ObjectId CafeteriaId { get; set; }
    [BsonElement("titulo")] public string Titulo { get; set; } = "";
    [BsonElement("contenido")] public string Contenido { get; set; } = "";
    [BsonElement("fechaActualizacion")] public DateTime FechaActualizacion { get; set; } = DateTime.UtcNow;
}

public class CategoriaSolicitud
{
    [Required, StringLength(50, MinimumLength = 2)] public string Nombre { get; set; } = "";
    [StringLength(300)] public string Descripcion { get; set; } = "";
    public bool Activa { get; set; } = true;
}
public class ResenaSolicitud
{
    [Range(1, 5)] public int Calificacion { get; set; }
    [Required, StringLength(1000, MinimumLength = 3)] public string Comentario { get; set; } = "";
}
public class AvisoSolicitud
{
    [Required, StringLength(100, MinimumLength = 3)] public string Titulo { get; set; } = "";
    [Required, StringLength(2000, MinimumLength = 3)] public string Contenido { get; set; } = "";
}
