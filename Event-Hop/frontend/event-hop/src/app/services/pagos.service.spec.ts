import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { PagosService, Pago } from './pagos.service';

describe('Rutas de pagos', () => {
  beforeEach(() => TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] }));
  afterEach(() => TestBed.inject(HttpTestingController).verify());
  it('no permite que el navegador elija importe, dueño u orden PayPal', async () => {
    const servicio = TestBed.inject(PagosService), http = TestBed.inject(HttpTestingController);
    const pago = { eventoId: 'e1', pagoId: 'p1' } as Pago;
    for (const [accion, url, metodo, cuerpo] of [
      [() => servicio.consultar(), '/api/pagos', 'GET', null],
      [() => servicio.iniciar('e1'), '/api/pagos', 'POST', { eventoId: 'e1' }],
      [() => servicio.orden(pago), '/api/pagos/e1/p1/orden', 'POST', {}],
      [() => servicio.confirmar(pago), '/api/pagos/e1/p1/confirmacion', 'POST', {}],
      [() => servicio.cancelar(pago), '/api/pagos/e1/p1', 'DELETE', null]
    ] as const) {
      const pendiente = accion(); const peticion = http.expectOne(url);
      expect(peticion.request.method).toBe(metodo); expect(peticion.request.body).toEqual(cuerpo);
      peticion.flush(metodo === 'GET' ? [] : {}); await pendiente;
    }
  });
  it('bloquea enlaces fuera de Sandbox', () => {
    const servicio = TestBed.inject(PagosService);
    expect(() => servicio.irAPayPal('https://www.paypal.com/checkout')).toThrow();
    expect(() => servicio.irAPayPal('https://sandbox.paypal.com.ejemplo.com/')).toThrow();
  });
  it('envía las operaciones Stripe sin aceptar importe ni identidad del navegador', async () => {
    const servicio = TestBed.inject(PagosService), http = TestBed.inject(HttpTestingController);
    const pago = { eventoId: 'e1', pagoId: 'p1', proveedor: 'stripe' } as Pago;
    for (const [accion, url, metodo, cuerpo] of [
      [() => servicio.iniciarStripe('e1'), '/api/stripe/pagos', 'POST', { eventoId: 'e1' }],
      [() => servicio.orden(pago), '/api/stripe/pagos/e1/p1/orden', 'POST', {}],
      [() => servicio.confirmar(pago), '/api/stripe/pagos/e1/p1/confirmacion', 'POST', {}],
      [() => servicio.cancelar(pago), '/api/stripe/pagos/e1/p1', 'DELETE', null]
    ] as const) {
      const pendiente = accion(), peticion = http.expectOne(url);
      expect(peticion.request.method).toBe(metodo); expect(peticion.request.body).toEqual(cuerpo);
      peticion.flush({}); await pendiente;
    }
  });
  it('rechaza enlaces Stripe ajenos, inseguros o con credenciales', () => {
    const servicio = TestBed.inject(PagosService);
    for (const url of ['http://checkout.stripe.com/', 'https://checkout.stripe.com.ejemplo.com/', 'https://usuario@checkout.stripe.com/', 'https://checkout.stripe.com:444/', 'javascript:alert(1)'])
      expect(() => servicio.irAStripe(url)).toThrow();
  });
});
