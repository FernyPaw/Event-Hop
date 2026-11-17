import { TestBed } from '@angular/core/testing';
import { vi } from 'vitest';
import { Reportes } from './reportes';
import { ReportesService, Reporte } from '../../services/reportes.service';
import { AuthService } from '../../services/auth.service';
describe('Reportes', () => {
  const reporte: Reporte = { tipo: 'eventos', generadoUtc: '2026-11-16T02:00:00Z', desde: '', hasta: '', columnas: ['Evento', 'Cupo'], filas: [['Café', '2']], indicadores: { Eventos: '1' } };
  const api = { consultar: vi.fn(), cafeterias: async () => [{ id: 'id', nombre: 'Café' }] };
  const auth = { usuario: { rol: 'cafeteria' }, mensajeError: () => 'Error de prueba' };
  beforeEach(async () => {
    api.consultar.mockReset().mockResolvedValue(structuredClone(reporte)); auth.usuario.rol = 'cafeteria';
    await TestBed.configureTestingModule({ imports: [Reportes], providers: [ { provide: ReportesService, useValue: api },
      { provide: AuthService, useValue: auth } ] }).compileComponents();
  });
  async function abrir() { const f = TestBed.createComponent(Reportes); f.detectChanges(); await vi.waitFor(() => expect(f.componentInstance.cargando).toBe(false)); await f.whenStable(); return f; }
  it('consulta al abrir y muestra tabla e indicadores', async () => {
    const f = await abrir(); expect(api.consultar).toHaveBeenCalledWith('eventos', '', '', ''); expect(f.nativeElement.textContent).toContain('Café');
  });
  it('envía filtros sin ampliar alcance de cafetería', async () => {
    const f = await abrir(); const c = f.componentInstance; c.desde = '2026-11-15'; c.hasta = '2026-11-16'; c.cafeteriaId = 'ajena';
    await c.consultar(); expect(api.consultar).toHaveBeenLastCalledWith('eventos', '2026-11-15', '2026-11-16', '');
  });
  it('administrador puede filtrar por cafetería', async () => {
    auth.usuario.rol = 'admin'; const f = await abrir(); f.componentInstance.cafeteriaId = 'id'; await f.componentInstance.consultar();
    expect(api.consultar).toHaveBeenLastCalledWith('eventos', '', '', 'id');
  });
  it('rechaza fechas invertidas antes de consultar', async () => {
    const f = await abrir(); f.componentInstance.desde = '2026-11-17'; f.componentInstance.hasta = '2026-11-15'; await f.componentInstance.consultar();
    expect(api.consultar).toHaveBeenCalledTimes(1); expect(f.componentInstance.reporte).toBeNull();
  });
  it('falla sin dejar cifras viejas visibles', async () => {
    const f = await abrir(); api.consultar.mockRejectedValue(new Error()); await f.componentInstance.consultar(); await f.whenStable();
    expect(f.componentInstance.reporte).toBeNull(); expect(f.nativeElement.textContent).toContain('Error de prueba');
  });
  it('muestra vacío explícito', async () => {
    api.consultar.mockResolvedValue({ ...reporte, filas: [] }); const f = await abrir(); expect(f.nativeElement.textContent).toContain('No hay eventos');
  });
  it('pagina en pantalla sin perder resultados', async () => {
    api.consultar.mockResolvedValue({ ...reporte, filas: Array.from({ length: 16 }, (_, n) => ['Evento ' + n, '1']) });
    const f = await abrir(); expect(f.componentInstance.filas.length).toBe(15); f.componentInstance.pagina = 2;
    expect(f.componentInstance.filas.length).toBe(1); expect(f.componentInstance.filas[0][0]).toBe('Evento 15');
  });
  it('solo permite consultar en pantalla, sin exportación', async () => {
    const f = await abrir();
    expect(f.nativeElement.textContent).not.toContain('Descargar');
    expect(f.nativeElement.textContent).not.toContain('CSV');
  });
});
