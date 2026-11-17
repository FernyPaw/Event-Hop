import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { AsistenciasService } from './asistencias.service';

describe('Rutas de asistentes', () => {
  beforeEach(() => TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] }));
  afterEach(() => TestBed.inject(HttpTestingController).verify());
  it('consulta asistentes del evento por GET', async () => {
    const api = TestBed.inject(AsistenciasService), http = TestBed.inject(HttpTestingController);
    const pendiente = api.consultar('e1');
    const peticion = http.expectOne('/api/asistencias/e1');
    expect(peticion.request.method).toBe('GET'); peticion.flush({ asistentes: [] }); await pendiente;
  });
  it('envía solo código: no dueño, estado ni fecha elegidos por cliente', async () => {
    const api = TestBed.inject(AsistenciasService), http = TestBed.inject(HttpTestingController);
    const pendiente = api.registrar('e1', 'EH-' + 'A'.repeat(32));
    const peticion = http.expectOne('/api/asistencias/e1');
    expect(peticion.request.method).toBe('POST');
    expect(peticion.request.body).toEqual({ codigo: 'EH-' + 'A'.repeat(32) });
    peticion.flush({ mensaje: 'Entrada registrada' }); await pendiente;
  });
});
