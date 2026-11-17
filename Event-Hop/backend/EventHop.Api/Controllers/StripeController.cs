using System.Security.Claims;
using EventHop.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;

namespace EventHop.Api.Controllers;

[ApiController, Authorize(Roles = "usuario"), Route("api/stripe/pagos")]
public class StripeController(PagosStripeService stripe, CoordinadorPagos coordinador) : ControllerBase
{
    private ObjectId MiId => ObjectId.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private static ObjectId Id(string id) => ObjectId.TryParse(id, out var valor) ? valor : throw new OperacionException("Evento inválido.", 400);
    private static string PagoId(string id) => Guid.TryParseExact(id, "N", out _) ? id : throw new OperacionException("Pago inválido.", 400);
    private async Task<IActionResult> Ejecutar(Func<Task<object>> accion) {
        if (!await coordinador.Turno.WaitAsync(TimeSpan.FromSeconds(5))) throw new OperacionException("Hay otra operación de pago en proceso. Recupera el estado en unos segundos.");
        try { return Ok(await accion()); } finally { coordinador.Turno.Release(); }
    }
    [HttpPost] public Task<IActionResult> Crear(CrearPago datos) => Ejecutar(() => stripe.Crear(Id(datos.EventoId), MiId));
    [HttpPost("{eventoId}/{pagoId}/orden")] public Task<IActionResult> Orden(string eventoId, string pagoId) => Ejecutar(() => stripe.Orden(Id(eventoId), PagoId(pagoId), MiId));
    [HttpPost("{eventoId}/{pagoId}/confirmacion")] public Task<IActionResult> Confirmar(string eventoId, string pagoId) => Ejecutar(() => stripe.Confirmar(Id(eventoId), PagoId(pagoId), MiId));
    [HttpDelete("{eventoId}/{pagoId}")] public Task<IActionResult> Cancelar(string eventoId, string pagoId) => Ejecutar(() => stripe.Cancelar(Id(eventoId), PagoId(pagoId), MiId));
}
