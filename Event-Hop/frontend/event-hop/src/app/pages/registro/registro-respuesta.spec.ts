import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { Registro } from './registro';

describe('Actualización de error de registro', () => {
  it('muestra correo duplicado y libera el formulario', async () => {
    await TestBed.configureTestingModule({ imports: [Registro],
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])] }).compileComponents();
    const fixture = TestBed.createComponent(Registro);
    fixture.detectChanges();
    const componente = fixture.componentInstance;
    componente.nombre = 'Usuario de prueba';
    componente.correo = 'prueba@example.com';
    componente.password = 'Prueba123456';
    const intento = componente.registrar();
    const http = TestBed.inject(HttpTestingController);
    http.expectOne('/api/auth/registro').flush({ mensaje: 'Correo duplicado.' },
      { status: 409, statusText: 'Conflict' });
    await intento;
    await fixture.whenStable();
    expect(fixture.nativeElement.textContent).toContain('Correo duplicado.');
    expect(componente.enviando).toBe(false);
    http.verify();
  });
});
