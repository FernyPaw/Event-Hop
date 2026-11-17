import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { ReportesService } from './reportes.service';
describe('Reportes HTTP', () => {
  let api: ReportesService; let http: HttpTestingController;
  beforeEach(() => { TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] }); api = TestBed.inject(ReportesService); http = TestBed.inject(HttpTestingController); });
  afterEach(() => http.verify());
  it('resumen admin solo realiza GET', async () => {
    const llamada = api.resumenAdmin(); const req = http.expectOne('/api/reportes/resumen'); expect(req.request.method).toBe('GET'); req.flush({ totalUsuarios: 2 }); expect((await llamada).totalUsuarios).toBe(2);
  });
  it('resumen cafetería solo realiza GET', async () => {
    const llamada = api.resumenCafe(); const req = http.expectOne('/api/reportes/resumen'); expect(req.request.method).toBe('GET'); req.flush({ reservas: 2 }); expect((await llamada).reservas).toBe(2);
  });
  it('catálogo usa la ruta admin sin exponer otros datos', async () => {
    const llamada = api.cafeterias(); http.expectOne('/api/reportes/cafeterias').flush([{ id: 'a', nombre: 'Café' }]); expect((await llamada)[0].nombre).toBe('Café');
  });
  it('envía filtros en query sin escribir datos', async () => {
    const llamada = api.consultar('asistencia', '2026-11-15', '2026-11-16', 'cafe');
    const req = http.expectOne(r => r.url === '/api/reportes/asistencia'); expect(req.request.method).toBe('GET'); expect(req.request.params.get('desde')).toBe('2026-11-15'); expect(req.request.params.get('hasta')).toBe('2026-11-16'); expect(req.request.params.get('cafeteriaId')).toBe('cafe'); req.flush({ filas: [] }); await llamada;
  });
  it('sin filtros no agrega valores artificiales', async () => {
    const llamada = api.consultar('pagos'); const req = http.expectOne('/api/reportes/pagos'); expect(req.request.params.keys()).toEqual([]); req.flush({ filas: [] }); await llamada;
  });
});
