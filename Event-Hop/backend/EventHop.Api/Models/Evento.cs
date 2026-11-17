using System.ComponentModel.DataAnnotations;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace EventHop.Api.Models;

public class Evento
{
    public ObjectId Id { get; set; } = ObjectId.GenerateNewId();
    [BsonElement("cafeteriaId")]
    public ObjectId CafeteriaId { get; set; }
    [BsonElement("titulo")]
    public string Titulo { get; set; } = "";
    [BsonElement("descripcion")]
    public string Descripcion { get; set; } = "";
    [BsonElement("categoria")]
    public string Categoria { get; set; } = "";
    [BsonElement("direccion")]
    public string Direccion { get; set; } = "";
    // Fecha y hora locales del lugar del evento, sin convertirlas a UTC.
    [BsonElement("fecha")]
    public string Fecha { get; set; } = "";
    [BsonElement("hora")]
    public string Hora { get; set; } = "";
    [BsonElement("esGratis")]
    public bool EsGratis { get; set; }
    [BsonElement("costo")]
    public double Costo { get; set; }
    [BsonElement("cupo")]
    public int Cupo { get; set; }
    [BsonElement("imagenUrl")]
    public string ImagenUrl { get; set; } = "";
    [BsonElement("fechaRegistro")]
    public DateTime FechaRegistro { get; set; } = DateTime.UtcNow;
    // El proveedor compara esta versión al guardar: evita vender el último lugar dos veces.
    [ConcurrencyCheck, BsonElement("version")]
    public long Version { get; set; }
    [BsonElement("reservas")]
    public List<Reserva> Reservas { get; set; } = [];
    [BsonElement("pagos")]
    public List<Pago> Pagos { get; set; } = [];
}

// Se guarda dentro del evento: cupo y reservas cambian en un solo documento MongoDB.
public class Reserva
{
    [BsonElement("fechaEntrada")]
    public DateTime? FechaEntrada { get; set; }
    [BsonElement("usuarioId")]
    public ObjectId UsuarioId { get; set; }
    [BsonElement("codigo")]
    public string Codigo { get; set; } = "EH-" + Guid.NewGuid().ToString("N").ToUpperInvariant();
    [BsonElement("estado")]
    public string Estado { get; set; } = "confirmada";
    [BsonElement("fechaRegistro")]
    public DateTime FechaRegistro { get; set; } = DateTime.UtcNow;
    [BsonElement("fechaCancelacion")]
    public DateTime? FechaCancelacion { get; set; }
    [BsonElement("pagoId")]
    public string PagoId { get; set; } = "";
}

// Historial de intentos PayPal y Stripe, sin datos bancarios ni credenciales.
public class Pago
{
    // Los documentos anteriores sin este campo siguen siendo PayPal.
    [BsonElement("proveedor")]
    public string? Proveedor { get; set; } = "paypal";
    [BsonElement("inicioSesionStripe")]
    public DateTime? InicioSesionStripe { get; set; }
    [BsonElement("identificador")]
    public string Identificador { get; set; } = Guid.NewGuid().ToString("N");
    [BsonElement("usuarioId")]
    public ObjectId UsuarioId { get; set; }
    [BsonElement("estado")]
    public string Estado { get; set; } = "pendiente";
    [BsonElement("centavos")]
    public int Centavos { get; set; }
    [BsonElement("ordenId")]
    public string OrdenId { get; set; } = "";
    [BsonElement("capturaId")]
    public string CapturaId { get; set; } = "";
    [BsonElement("reembolsoId")]
    public string ReembolsoId { get; set; } = "";
    [BsonElement("urlAprobacion")]
    public string UrlAprobacion { get; set; } = "";
    [BsonElement("fechaRegistro")]
    public DateTime FechaRegistro { get; set; } = DateTime.UtcNow;
    [BsonElement("vence")]
    public DateTime Vence { get; set; } = DateTime.UtcNow.AddMinutes(15);
    [BsonElement("inicioReembolso")]
    public DateTime? InicioReembolso { get; set; }
}

// Solo se reciben campos editables: el dueño se obtiene del token.
public class EventoSolicitud
{
    [Required, StringLength(120, MinimumLength = 3)]
    public string Titulo { get; set; } = "";
    [Required, StringLength(2000)]
    public string Descripcion { get; set; } = "";
    [Required, StringLength(50, MinimumLength = 2)]
    public string Categoria { get; set; } = "Otro";
    [Required, StringLength(250)]
    public string Direccion { get; set; } = "";
    [Required, StringLength(10)]
    public string Fecha { get; set; } = "";
    [Required, StringLength(5)]
    public string Hora { get; set; } = "";
    public bool EsGratis { get; set; }
    [Range(0, 100000)]
    public double Costo { get; set; }
    [Range(1, 10000)]
    public int Cupo { get; set; }
    [StringLength(1400000)]
    public string ImagenUrl { get; set; } = "";
}
