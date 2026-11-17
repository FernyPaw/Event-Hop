import { TestBed } from '@angular/core/testing';
import { Home } from './home';
import { AuthService } from '../../services/auth.service';
import { Evento, EventosService } from '../../services/eventos.service';
import { vi } from 'vitest';
import { ParticipacionService } from '../../services/participacion.service';
import { PagosService } from '../../services/pagos.service';
import { provideRouter } from '@angular/router';

const evento: Evento = { id: '1', cafeteriaId: 'c1', cafeteria: 'Mi cafetería', titulo: 'Lectura real',
  descripcion: 'Leer y comentar', categoria: 'Lectura', direccion: 'Calle de prueba', fecha: '2026-11-20',
  hora: '18:00', esGratis: true, costo: 0, cupo: 10, imagenUrl: '' };

describe('Home con API', () => {
  const consultar = vi.fn();
  const guardar = vi.fn();
  const reservar = vi.fn();
  beforeEach(async () => {
    consultar.mockReset().mockResolvedValue([evento]);
    guardar.mockReset().mockResolvedValue(null);
    reservar.mockReset().mockResolvedValue({ mensaje: 'Lugar reservado.' });
    await TestBed.configureTestingModule({ imports: [Home], providers: [
      provideRouter([]), { provide: PagosService, useValue: { iniciar: vi.fn() } },
      { provide: EventosService, useValue: { consultar } },
      { provide: AuthService, useValue: { usuario: { rol: 'usuario' }, mensajeError: () => 'Error de consulta' } },
      { provide: ParticipacionService, useValue: { favoritos: async () => [], boletos: async () => [], guardar, reservar } }
    ] }).compileComponents();
  });

  async function abrir() {
    const fixture = TestBed.createComponent(Home);
    fixture.detectChanges();
    await Promise.resolve();
    await fixture.whenStable();
    return fixture;
  }

  it('muestra datos recibidos y quita Cargando sin otro clic', async () => {
    const fixture = await abrir();
    expect(fixture.nativeElement.textContent).toContain('Lectura real');
    expect(fixture.nativeElement.textContent).not.toContain('Cargando eventos');
  });
  it('filtra por cafetería, día y tipo', async () => {
    const fixture = await abrir();
    const home = fixture.componentInstance;
    home.filtroCafeteria = 'Mi cafetería';
    home.filtroDia = 'Viernes';
    home.filtroTipo = 'gratis';
    expect(home.eventosFiltrados.length).toBe(1);
    home.filtroTipo = 'paga';
    expect(home.eventosFiltrados.length).toBe(0);
  });
  it('muestra el detalle del evento seleccionado', async () => {
    const fixture = await abrir();
    fixture.nativeElement.querySelector('.boton-ver').click();
    await fixture.whenStable();
    expect(fixture.nativeElement.textContent).toContain('Calle de prueba');
    expect(fixture.nativeElement.querySelector('.boton-comprar').disabled).toBe(false);
  });
  it('muestra errores de consulta sin quedar cargando', async () => {
    consultar.mockRejectedValue(new Error());
    const fixture = await abrir();
    expect(fixture.nativeElement.textContent).toContain('Error de consulta');
    expect(fixture.componentInstance.cargando).toBe(false);
  });
  it('guarda desde detalle y actualiza el botón sin otro clic', async () => {
    const fixture = await abrir();
    fixture.nativeElement.querySelector('.boton-ver').click();
    await fixture.whenStable();
    fixture.nativeElement.querySelector('.boton-guardar').click();
    await fixture.whenStable();
    expect(guardar).toHaveBeenCalledWith('1');
    expect(fixture.nativeElement.textContent).toContain('Quitar de guardados');
  });
  it('reserva y evita enviar otra reserva desde el botón', async () => {
    const fixture = await abrir();
    fixture.nativeElement.querySelector('.boton-ver').click();
    await fixture.whenStable();
    fixture.nativeElement.querySelector('.boton-comprar').click();
    await fixture.whenStable();
    expect(reservar).toHaveBeenCalledWith('1');
    expect(fixture.nativeElement.querySelector('.boton-comprar').disabled).toBe(true);
    expect(fixture.nativeElement.textContent).toContain('Lugar reservado.');
  });
});
