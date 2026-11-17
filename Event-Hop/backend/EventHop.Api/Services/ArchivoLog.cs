namespace EventHop.Api.Services;

public class ArchivoLog
{
    private readonly string ruta;
    private readonly object bloqueo = new();

    public ArchivoLog(IWebHostEnvironment entorno)
    {
        var carpeta = Path.Combine(entorno.ContentRootPath, "logs");
        Directory.CreateDirectory(carpeta);
        ruta = Path.Combine(carpeta, "eventhop.log");
    }

    public void Escribir(string mensaje)
    {
        // No registrar contraseñas, tokens ni cuerpos completos de solicitudes.
        var linea = $"{DateTime.UtcNow:O} {mensaje}{Environment.NewLine}";
        lock (bloqueo)
        {
            File.AppendAllText(ruta, linea);
        }
    }
}
