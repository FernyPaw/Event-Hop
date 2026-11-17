using EventHop.Api.Models;
using Microsoft.EntityFrameworkCore;
using MongoDB.EntityFrameworkCore.Extensions;

namespace EventHop.Api.Data;

public class EventHopContext : DbContext
{
    public EventHopContext(DbContextOptions<EventHopContext> opciones) : base(opciones)
    {
        // Las operaciones de este módulo guardan un solo documento a la vez.
        // Permite trabajar con el MongoDB local sin configurar un replica set.
        Database.AutoTransactionBehavior = AutoTransactionBehavior.Never;
    }

    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<Evento> Eventos => Set<Evento>();
    public DbSet<Favorito> Favoritos => Set<Favorito>();
    public DbSet<Categoria> Categorias => Set<Categoria>();
    public DbSet<Resena> Resenas => Set<Resena>();
    public DbSet<Aviso> Avisos => Set<Aviso>();

    protected override void OnModelCreating(ModelBuilder modelo)
    {
        base.OnModelCreating(modelo);
        modelo.Entity<Usuario>().ToCollection("usuarios");
        modelo.Entity<Usuario>().HasKey(usuario => usuario.Id);
        modelo.Entity<Evento>().ToCollection("eventos");
        modelo.Entity<Evento>().HasKey(evento => evento.Id);
        modelo.Entity<Evento>().OwnsMany(evento => evento.Reservas);
        modelo.Entity<Evento>().OwnsMany(evento => evento.Pagos);
        modelo.Entity<Favorito>().ToCollection("favoritos");
        modelo.Entity<Favorito>().HasKey(favorito => favorito.Id);
        modelo.Entity<Categoria>().ToCollection("categorias");
        modelo.Entity<Categoria>().HasKey(c => c.Id);
        modelo.Entity<Resena>().ToCollection("resenas");
        modelo.Entity<Resena>().HasKey(r => r.Id);
        modelo.Entity<Aviso>().ToCollection("avisos");
        modelo.Entity<Aviso>().HasKey(a => a.Id);
    }
}
