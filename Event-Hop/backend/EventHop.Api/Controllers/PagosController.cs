using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using EventHop.Api.Data;
using EventHop.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MongoDB.Bson;

namespace EventHop.Api.Controllers;

[ApiController, Authorize(Roles = "usuario"), Route("api/pagos")]
public class PagosController(EventHopContext db, PagosService pagos, CoordinadorPagos coordinador) : ControllerBase
{
    private ObjectId MiId => ObjectId.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private static ObjectId Id(string texto) => ObjectId.TryParse(texto, out var id) ? id :
        throw new OperacionException("Identificador de evento inválido.", 400);
    private static string Clave(string texto) => Guid.TryParseExact(texto, "N", out _) ? texto :
        throw new OperacionException("Identificador de pago inválido.", 400);
    private async Task<IActionResult> Ejecutar(Func<Task<object>> accion)
    {
        if (!await coordinador.Turno.WaitAsync(TimeSpan.FromSeconds(5)))
            throw new OperacionException("Hay otra operación PayPal en proceso. Espera unos segundos y recupera el estado.");
        try { return Ok(await accion()); }
        finally { coordinador.Turno.Release(); }
    }
    [HttpGet]
    public async Task<IActionResult> Consultar()
    {
        var propio = MiId;
        var eventos = await db.Eventos.AsNoTracking().Where(e => e.Pagos.Any(p => p.UsuarioId == propio)).ToListAsync();
        return Ok(eventos.SelectMany(e => e.Pagos.Where(p => p.UsuarioId == propio)
            .OrderByDescending(p => p.FechaRegistro).Select(p => PagosService.Vista(e, p))));
    }
    [HttpPost]
    public Task<IActionResult> Crear(CrearPago datos) => Ejecutar(() => pagos.Crear(Id(datos.EventoId), MiId));
    [HttpPost("{eventoId}/{pagoId}/orden")]
    public Task<IActionResult> Orden(string eventoId, string pagoId) =>
        Ejecutar(() => pagos.PrepararOrden(Id(eventoId), Clave(pagoId), MiId));
    [HttpPost("{eventoId}/{pagoId}/confirmacion")]
    public Task<IActionResult> Confirmar(string eventoId, string pagoId) =>
        Ejecutar(() => pagos.Confirmar(Id(eventoId), Clave(pagoId), MiId));
    [HttpDelete("{eventoId}/{pagoId}")]
    public Task<IActionResult> Cancelar(string eventoId, string pagoId) =>
        Ejecutar(() => pagos.Cancelar(Id(eventoId), Clave(pagoId), MiId));
}
public class CrearPago
{
    [Required, StringLength(24, MinimumLength = 24)]
    public string EventoId { get; set; } = "";
}
