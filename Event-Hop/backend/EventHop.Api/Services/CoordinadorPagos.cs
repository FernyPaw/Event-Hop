namespace EventHop.Api.Services;

// Este proyecto local ejecuta una sola instancia de la API. Serializa llamadas de pago
// para que capturar/cancelar no se crucen. MongoDB sigue protegiendo el cupo con Version.
// Antes de desplegar varias instancias se necesita coordinación distribuida.
public class CoordinadorPagos
{
    public SemaphoreSlim Turno { get; } = new(1, 1);
}
