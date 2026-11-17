using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace EventHop.Api.Models;

public class Favorito
{
    // La combinación como _id hace imposible duplicar el favorito, incluso con dos peticiones.
    public string Id { get; set; } = "";
    [BsonElement("usuarioId")]
    public ObjectId UsuarioId { get; set; }
    [BsonElement("eventoId")]
    public ObjectId EventoId { get; set; }
    [BsonElement("fechaRegistro")]
    public DateTime FechaRegistro { get; set; } = DateTime.UtcNow;
}
