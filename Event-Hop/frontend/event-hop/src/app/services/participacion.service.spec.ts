import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ParticipacionService } from './participacion.service';

describe('ParticipacionService', () => {
  beforeEach(() => TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] }));
  afterEach(() => TestBed.inject(HttpTestingController).verify());
  it('envía las seis operaciones a las rutas reales sin usuarioId del navegador', async () => {
    const servicio = TestBed.inject(ParticipacionService);
    const http = TestBed.inject(HttpTestingController);
    for (const [promesa, url, method] of [
      [() => servicio.favoritos(), '/api/favoritos', 'GET'],
      [() => servicio.guardar('e1'), '/api/favoritos/e1', 'PUT'],
      [() => servicio.quitar('e1'), '/api/favoritos/e1', 'DELETE'],
      [() => servicio.boletos(), '/api/reservas', 'GET'],
      [() => servicio.reservar('e1'), '/api/reservas/e1', 'PUT'],
      [() => servicio.cancelar('e1'), '/api/reservas/e1', 'DELETE']
    ] as const) {
      const pendiente = promesa();
      const peticion = http.expectOne(url);
      expect(peticion.request.method).toBe(method);
      expect(peticion.request.body).toEqual(method === 'PUT' ? {} : null);
      peticion.flush(method === 'GET' ? [] : {});
      await pendiente;
    }
  });
});
