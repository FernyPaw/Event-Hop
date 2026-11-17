import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { Login } from './login';

describe('Aviso de login pendiente', () => {
  it('muestra la respuesta 403 y libera el botón sin otro clic', async () => {
    await TestBed.configureTestingModule({ imports: [Login],
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])] }).compileComponents();
    const fixture = TestBed.createComponent(Login);
    fixture.detectChanges();
    const componente = fixture.componentInstance;
    componente.correo = 'prueba@example.com';
    componente.password = 'Prueba123456';
    const intento = componente.iniciarSesion();
    const http = TestBed.inject(HttpTestingController);
    http.expectOne('/api/auth/login').flush({ mensaje: 'Solicitud pendiente.' },
      { status: 403, statusText: 'Forbidden' });
    await intento;
    await fixture.whenStable();
    expect(fixture.nativeElement.textContent).toContain('Solicitud pendiente.');
    expect(fixture.nativeElement.querySelector('button').disabled).toBe(false);
    http.verify();
  });
});
