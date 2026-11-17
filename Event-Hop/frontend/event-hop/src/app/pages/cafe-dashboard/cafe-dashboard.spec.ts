import { TestBed } from '@angular/core/testing';
import { CafeDashboard } from './cafe-dashboard';
import { AuthService } from '../../services/auth.service';
import { Evento, EventosService } from '../../services/eventos.service';
import { vi } from 'vitest';
import { CategoriasService } from '../../services/categorias.service';
import { ReportesService } from '../../services/reportes.service';

const evento: Evento = {
  id: 'abc', cafeteriaId: 'cafe1', cafeteria: 'Café de prueba', titulo: 'Taller de café',
  descripcion: 'Descripción de prueba', categoria: 'Taller', direccion: 'Calle 1',
  fecha: '2026-11-20', hora: '18:00', esGratis: true, costo: 0, cupo: 15, imagenUrl: ''
};

describe('CafeDashboard con API', () => {
  const api = { consultarMios: vi.fn(), crear: vi.fn(), editar: vi.fn(), eliminar: vi.fn() };
  beforeEach(async () => {
    vi.resetAllMocks();
    api.consultarMios.mockResolvedValue([{ ...evento }]);
    api.crear.mockResolvedValue({});
    api.editar.mockResolvedValue({});
    api.eliminar.mockResolvedValue(null);
    await TestBed.configureTestingModule({ imports: [CafeDashboard], providers: [
      { provide: EventosService, useValue: api },
      { provide: ReportesService, useValue: { resumenCafe: async () => ({ totalEventos: 1, favoritos: 2, reservas: 3, entradas: 1 }) } },
      { provide: CategoriasService, useValue: { consultar: async () => [{ nombre: 'Taller' }, { nombre: 'Otro' }] } },
      { provide: AuthService, useValue: { usuario: { nombre: 'Café de prueba', estadoSolicitud: 'aprobada' },
        mensajeError: () => 'Error de prueba' } }
    ] }).compileComponents();
  });

  async function abrir() {
    const fixture = TestBed.createComponent(CafeDashboard);
    fixture.detectChanges();
    await Promise.resolve();
    await vi.waitFor(() => expect(fixture.componentInstance.cargando).toBe(false));
    await fixture.whenStable();
    return fixture;
  }

  it('muestra el nombre de la sesión y termina la carga sin otro clic', async () => {
    const fixture = await abrir();
    expect(fixture.nativeElement.textContent).toContain('Café de prueba');
    expect(fixture.nativeElement.textContent).not.toContain('Cargando eventos');
    expect(fixture.componentInstance.misEventos.length).toBe(1);
  });

  it('muestra un error de carga en la vista', async () => {
    api.consultarMios.mockRejectedValue(new Error());
    const fixture = await abrir();
    expect(fixture.nativeElement.textContent).toContain('Error de prueba');
    expect(fixture.componentInstance.cargando).toBe(false);
  });

  it('crea un evento y vuelve a consultar MongoDB mediante la API', async () => {
    const fixture = await abrir();
    fixture.componentInstance.nuevoEvento = { ...evento };
    await fixture.componentInstance.crearEvento();
    await fixture.whenStable();
    expect(api.crear).toHaveBeenCalledTimes(1);
    expect(api.consultarMios).toHaveBeenCalledTimes(2);
    expect(fixture.nativeElement.textContent).toContain('Evento creado correctamente.');
    expect(fixture.componentInstance.enviando).toBe(false);
  });

  it('edita sin crear un segundo evento y no envía el dueño en el formulario', async () => {
    const fixture = await abrir();
    fixture.componentInstance.editarEvento(evento);
    fixture.componentInstance.nuevoEvento.titulo = 'Título actualizado';
    await fixture.componentInstance.crearEvento();
    expect(api.editar).toHaveBeenCalledWith('abc', expect.objectContaining({ titulo: 'Título actualizado' }));
    expect(api.editar.mock.calls[0][1]).not.toHaveProperty('cafeteriaId');
    expect(api.crear).not.toHaveBeenCalled();
  });

  it('no repite la escritura cuando la lista falla después de guardar', async () => {
    const fixture = await abrir();
    api.consultarMios.mockRejectedValue(new Error());
    fixture.componentInstance.nuevoEvento = { ...evento };
    await fixture.componentInstance.crearEvento();
    expect(fixture.componentInstance.pestanaActiva).toBe('gestion');
    expect(fixture.componentInstance.error).toContain('El evento se guardó');
    expect(api.crear).toHaveBeenCalledTimes(1);
  });

  it('rechaza un cupo fraccionario antes de enviar', async () => {
    const fixture = await abrir();
    fixture.componentInstance.nuevoEvento = { ...evento, cupo: 1.5 };
    await fixture.componentInstance.crearEvento();
    expect(api.crear).not.toHaveBeenCalled();
  });

  it('elimina únicamente después de confirmar', async () => {
    const fixture = await abrir();
    const confirmar = vi.spyOn(window, 'confirm').mockReturnValue(false);
    await fixture.componentInstance.eliminarEvento('abc');
    expect(api.eliminar).not.toHaveBeenCalled();
    confirmar.mockReturnValue(true);
    await fixture.componentInstance.eliminarEvento('abc');
    expect(api.eliminar).toHaveBeenCalledWith('abc');
    expect(fixture.componentInstance.misEventos).toEqual([]);
    confirmar.mockRestore();
  });
});
