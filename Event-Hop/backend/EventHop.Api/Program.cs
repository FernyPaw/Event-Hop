using System.Security.Claims;
using System.Text;
using EventHop.Api.Data;
using EventHop.Api.Models;
using EventHop.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Bson;
using MongoDB.Driver;

var builder = WebApplication.CreateBuilder(args);
var llave = builder.Configuration["Jwt:Key"];
if (string.IsNullOrWhiteSpace(llave) || Encoding.UTF8.GetByteCount(llave) < 32)
    throw new InvalidOperationException("Configura Jwt:Key con al menos 32 bytes mediante dotnet user-secrets o Jwt__Key.");

builder.Services.AddSingleton<ArchivoLog>();
builder.Services.AddHttpClient<IPayPalService, PayPalService>(cliente => cliente.Timeout = TimeSpan.FromSeconds(25));
builder.Services.AddScoped<PagosService>();
builder.Services.AddHttpClient<IStripeService, StripeService>(cliente => cliente.Timeout = TimeSpan.FromSeconds(25));
builder.Services.AddScoped<PagosStripeService>();
builder.Services.AddScoped<AsistenciasService>();
builder.Services.AddSingleton<CoordinadorPagos>();
builder.Services.AddScoped<PasswordHasher<Usuario>>();
builder.Services.AddScoped<TokenService>();
builder.Services.AddDbContext<EventHopContext>(opciones => opciones.UseMongoDB(
    builder.Configuration["MongoDB:ConnectionString"]!,
    builder.Configuration["MongoDB:DatabaseName"]!));

builder.Services.AddControllers().ConfigureApiBehaviorOptions(opciones =>
{
    opciones.InvalidModelStateResponseFactory = contexto =>
        new BadRequestObjectResult(new { mensaje = "Revisa los campos obligatorios, los formatos y los valores permitidos." });
});
builder.Services.AddCors(opciones => opciones.AddDefaultPolicy(politica => politica
    .WithOrigins(builder.Configuration["FrontendUrl"]!)
    .AllowAnyHeader().AllowAnyMethod()));

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(opciones =>
{
    opciones.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"],
        ValidAudience = builder.Configuration["Jwt:Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(llave)),
        ClockSkew = TimeSpan.FromSeconds(30)
    };
    opciones.Events = new JwtBearerEvents
    {
        OnTokenValidated = async contexto =>
        {
            var textoId = contexto.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!ObjectId.TryParse(textoId, out var id))
            {
                contexto.Fail("Sesión inválida.");
                return;
            }
            var baseDatos = contexto.HttpContext.RequestServices.GetRequiredService<EventHopContext>();
            var usuario = await baseDatos.Usuarios.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id);
            var rol = contexto.Principal?.FindFirstValue(ClaimTypes.Role);
            if (usuario == null || !usuario.Activo || usuario.TipoUsuario != rol ||
                (rol == "cafeteria" && usuario.EstadoSolicitud != "aprobada"))
                contexto.Fail("La cuenta ya no tiene acceso.");
        }
    };
});
builder.Services.AddAuthorization(opciones => opciones.FallbackPolicy =
    new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

var app = builder.Build();

app.Use(async (contexto, siguiente) =>
{
    try
    {
        await siguiente();
    }
    catch (OperacionException error)
    {
        app.Services.GetRequiredService<ArchivoLog>().Escribir($"Operación no completada HTTP {error.Estado}: {contexto.Request.Method} {contexto.Request.Path}");
        contexto.Response.StatusCode = error.Estado;
        await contexto.Response.WriteAsJsonAsync(new { mensaje = error.Message });
    }
    catch (Exception error)
    {
        app.Services.GetRequiredService<ArchivoLog>().Escribir(
            $"Error {error.GetType().Name} en {contexto.Request.Method} {contexto.Request.Path}");
        app.Logger.LogError("Error de tipo {Tipo}", error.GetType().Name);
        if (contexto.Response.HasStarted) throw;
        contexto.Response.Clear();
        var duplicado = error.ToString().Contains("E11000", StringComparison.Ordinal);
        contexto.Response.StatusCode = duplicado ? 409 : 500;
        await contexto.Response.WriteAsJsonAsync(new { mensaje = duplicado
            ? "Ya existe un registro con esos datos únicos."
            : "No se pudo completar la operación. Revisa que MongoDB esté disponible." });
    }
});
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// La creación inicial es un comando local, nunca un endpoint HTTP.
if (args.Contains("--crear-admin"))
{
    using var alcance = app.Services.CreateScope();
    var baseDatos = alcance.ServiceProvider.GetRequiredService<EventHopContext>();
    var correo = builder.Configuration["Admin:Correo"]?.Trim().ToLowerInvariant();
    var password = builder.Configuration["Admin:Password"];
    if (string.IsNullOrWhiteSpace(correo) || !new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(correo)
        || string.IsNullOrWhiteSpace(password) || password.Length < 12 || password.Length > 100
        || !password.Any(char.IsLetter) || !password.Any(char.IsDigit))
        throw new InvalidOperationException("Configura Admin:Correo y Admin:Password (12 a 100 caracteres, letras y números).");
    if (await baseDatos.Usuarios.AnyAsync(u => u.Correo == correo))
        throw new InvalidOperationException("Ese correo ya existe; no se cambiarán sus permisos.");
    var usuario = new Usuario { Nombre = "Administrador", Correo = correo, TipoUsuario = "admin" };
    usuario.Contrasena = new PasswordHasher<Usuario>().HashPassword(usuario, password);
    baseDatos.Usuarios.Add(usuario);
    await baseDatos.SaveChangesAsync();
    Console.WriteLine("Administrador creado. Retira Admin:Password de la configuración de desarrollo.");
    return;
}

app.Run();
