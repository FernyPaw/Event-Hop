import { TestBed } from '@angular/core/testing';
import { vi } from 'vitest';
import { Comunidad } from './comunidad';
import { ComunidadService } from '../../services/comunidad.service';
import { EventosService } from '../../services/eventos.service';
import { AuthService } from '../../services/auth.service';
describe('Reseñas y avisos', () => {
  const api = { consultar: vi.fn(), resenar: vi.fn(), quitarResena: vi.fn(), avisar: vi.fn(), quitarAviso: vi.fn() };
  const eventos = { consultar: vi.fn(), consultarMios: vi.fn() };
  const auth = { usuario: { rol: 'usuario' }, mensajeError: () => 'Rechazado por API' };
  const datos = { titulo: 'Evento real', puedeResenar: true, puedeAvisar: false, puedeVerAvisos: true, avisos: [], resenas: [] };
  beforeEach(async () => {
    for (const fn of Object.values(api)) fn.mockReset().mockResolvedValue({});
    api.consultar.mockResolvedValue(datos);
    eventos.consultar.mockReset().mockResolvedValue([{ id: 'e1', titulo: 'Evento real' }]);
    eventos.consultarMios.mockReset().mockResolvedValue([]);
    auth.usuario.rol = 'usuario';
    await TestBed.configureTestingModule({ imports: [Comunidad], providers: [
      { provide: ComunidadService, useValue: api }, { provide: EventosService, useValue: eventos },
      { provide: AuthService, useValue: auth }
    ] }).compileComponents();
  });
  async function abrir() {
    const f = TestBed.createComponent(Comunidad);
    await f.componentInstance.cargarEventos(); f.componentInstance.eventoId = 'e1';
    await f.componentInstance.seleccionar(); return f;
  }
  it('consulta eventos de usuario y comunidad por id', async () => {
    const f = await abrir(); expect(eventos.consultar).toHaveBeenCalled(); expect(api.consultar).toHaveBeenCalledWith('e1');
    expect(f.componentInstance.datos?.titulo).toBe('Evento real');
  });
  it('cafetería carga solo sus eventos', async () => {
    auth.usuario.rol = 'cafeteria'; const f = TestBed.createComponent(Comunidad);
    await f.componentInstance.cargarEventos(); expect(eventos.consultarMios).toHaveBeenCalled();
    expect(eventos.consultar).not.toHaveBeenCalled();
  });
  it('guarda reseña sin identidad elegida por cliente', async () => {
    const f = await abrir(); f.componentInstance.comentario = ' Muy bueno '; await f.componentInstance.guardarResena();
    expect(api.resenar).toHaveBeenCalledWith('e1', 5, 'Muy bueno');
    expect(f.componentInstance.mensaje).toBe('Reseña guardada.');
  });
  it('no envía reseña cuando backend no habilitó entrada', async () => {
    api.consultar.mockResolvedValue({ ...datos, puedeResenar: false });
    const f = await abrir(); f.componentInstance.comentario = 'Comentario'; await f.componentInstance.guardarResena();
    expect(api.resenar).not.toHaveBeenCalled();
  });
  it('valida rango y comentario vacío', async () => {
    const f = await abrir(); f.componentInstance.comentario = ' '; await f.componentInstance.guardarResena();
    f.componentInstance.comentario = 'Válido'; f.componentInstance.calificacion = 6; await f.componentInstance.guardarResena();
    expect(api.resenar).not.toHaveBeenCalled();
  });
  it('prepara edición de reseña propia', async () => {
    api.consultar.mockResolvedValue({ ...datos, resenas: [{ id: 'r', esMia: true, calificacion: 3, comentario: 'Mi comentario' }] });
    const f = await abrir(); expect(f.componentInstance.comentario).toBe('Mi comentario');
    expect(f.componentInstance.tengoResena).toBe(true);
  });
  it('guarda aviso propio y limpia formulario', async () => {
    api.consultar.mockResolvedValue({ ...datos, puedeAvisar: true });
    const f = await abrir(); f.componentInstance.titulo = 'Aviso'; f.componentInstance.contenido = 'Contenido';
    await f.componentInstance.guardarAviso(); expect(api.avisar).toHaveBeenCalledWith('e1', 'Aviso', 'Contenido', '');
    expect(f.componentInstance.titulo).toBe('');
  });
  it('no anuncia éxito tras rechazo de servidor', async () => {
    const f = await abrir(); api.resenar.mockRejectedValue(new Error());
    f.componentInstance.comentario = 'Comentario'; await f.componentInstance.guardarResena();
    expect(f.componentInstance.error).toBe('Rechazado por API'); expect(f.componentInstance.mensaje).toBe('');
  });
  it('no repite una escritura si falla consulta posterior', async () => {
    const f = await abrir(); api.consultar.mockRejectedValue(new Error());
    f.componentInstance.comentario = 'Comentario'; await f.componentInstance.guardarResena();
    expect(api.resenar).toHaveBeenCalledOnce(); expect(f.componentInstance.datos).toBeNull();
    expect(f.componentInstance.error).toContain('Se guardó');
  });
});
