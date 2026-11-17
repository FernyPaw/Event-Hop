import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { AdminCafeterias } from './admin-cafeterias';

describe('Actualización de solicitudes', () => {
  it('presenta la lista y quita una cafetería después de aprobarla', async () => {
    await TestBed.configureTestingModule({ imports: [AdminCafeterias],
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])] }).compileComponents();
    const fixture = TestBed.createComponent(AdminCafeterias);
    fixture.detectChanges();
    const http = TestBed.inject(HttpTestingController);
    http.expectOne('/api/admin/solicitudes').flush([{ id: 'cafe1', nombre: 'Café pendiente',
      correo: 'prueba@example.com', rol: 'cafeteria', estadoSolicitud: 'pendiente', rfc: 'ABC010101AB1' }]);
    await Promise.resolve();
    await fixture.whenStable();
    expect(fixture.nativeElement.textContent).toContain('Café pendiente');
    expect(fixture.nativeElement.textContent).not.toContain('Cargando solicitudes');
    const intento = fixture.componentInstance.aprobarCafeteria('cafe1');
    const peticion = http.expectOne('/api/admin/solicitudes/cafe1');
    expect(peticion.request.method).toBe('PUT');
    expect(peticion.request.body.estado).toBe('aprobada');
    peticion.flush({ mensaje: 'Solicitud actualizada.' });
    await intento;
    await fixture.whenStable();
    expect(fixture.nativeElement.textContent).not.toContain('Café pendiente');
    http.verify();
  });
});
