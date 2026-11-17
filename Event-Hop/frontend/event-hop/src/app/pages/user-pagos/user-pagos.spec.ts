import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, provideRouter } from '@angular/router';
import { vi } from 'vitest';
import { UserPagos } from './user-pagos';
import { Pago, PagosService } from '../../services/pagos.service';
import { AuthService } from '../../services/auth.service';

describe('Pagos Sandbox', () => {
  const consultar = vi.fn(), orden = vi.fn(), confirmar = vi.fn(), cancelar = vi.fn(), irAPayPal = vi.fn();
  const pago: Pago = { eventoId: 'e1', pagoId: 'p1', titulo: 'Evento de paga', estado: 'pendiente',
    importe: 50, moneda: 'MXN', sandbox: true, vence: '2099-12-20T12:00:00Z', urlAprobacion: '' };
  beforeEach(async () => {
    consultar.mockReset().mockResolvedValue([pago]);
    orden.mockReset().mockResolvedValue({ ...pago, urlAprobacion: 'https://www.sandbox.paypal.com/checkoutnow?token=TEST' });
    confirmar.mockReset().mockResolvedValue({ ...pago, estado: 'pagado' });
    cancelar.mockReset().mockResolvedValue({ ...pago, estado: 'reembolsado' });
    irAPayPal.mockReset(); vi.spyOn(globalThis, 'confirm').mockReturnValue(true);
    await TestBed.configureTestingModule({ imports: [UserPagos], providers: [provideRouter([]),
      { provide: ActivatedRoute, useValue: { snapshot: { queryParamMap: { get: () => null } } } },
      { provide: PagosService, useValue: { consultar, orden, confirmar, cancelar, irAPayPal } },
      { provide: AuthService, useValue: { mensajeError: () => 'Resultado incierto: recuperar' } }
    ] }).compileComponents();
  });
  afterEach(() => vi.restoreAllMocks());
  async function abrir() {
    const fixture = TestBed.createComponent(UserPagos);
    fixture.detectChanges(); await Promise.resolve(); await fixture.whenStable();
    return fixture;
  }
  async function click(fixture: Awaited<ReturnType<typeof abrir>>, texto: string) {
    const boton = [...fixture.nativeElement.querySelectorAll('button')].find((b: any) => b.textContent.includes(texto)) as HTMLButtonElement;
    boton.click(); await fixture.whenStable();
  }
  it('muestra el importe real y el aviso Sandbox sin otro clic', async () => {
    const fixture = await abrir();
    expect(fixture.nativeElement.textContent).toContain('Evento de paga');
    expect(fixture.nativeElement.textContent).toContain('50.00');
    expect(fixture.nativeElement.textContent).toContain('solo dinero de prueba');
    expect(fixture.nativeElement.textContent).not.toContain('Cargando pagos');
  });
  it('obtiene la orden del backend antes de ir a PayPal', async () => {
    const fixture = await abrir(); await click(fixture, 'Continuar en PayPal');
    expect(orden).toHaveBeenCalledWith(pago);
    expect(irAPayPal).toHaveBeenCalledWith('https://www.sandbox.paypal.com/checkoutnow?token=TEST');
  });
  it('solo muestra pagado después de confirmación del backend', async () => {
    const fixture = await abrir(); await click(fixture, 'Ya aprobé');
    expect(confirmar).toHaveBeenCalledWith(pago);
    expect(fixture.nativeElement.textContent).toContain('Pago confirmado: boleto emitido');
  });
  it('un corte consulta el estado persistido en vez de asumir pago fallido', async () => {
    const fixture = await abrir();
    confirmar.mockRejectedValue(new Error()); consultar.mockResolvedValue([{ ...pago, estado: 'capturando' }]);
    await click(fixture, 'Ya aprobé');
    expect(fixture.nativeElement.textContent).toContain('Resultado incierto: recuperar');
    expect(fixture.nativeElement.textContent).toContain('Recuperar resultado del cobro');
  });
  it('un reembolso pendiente no se presenta como cancelación final', async () => {
    consultar.mockResolvedValue([{ ...pago, estado: 'pagado' }]);
    cancelar.mockResolvedValue({ ...pago, estado: 'reembolsando' });
    const fixture = await abrir(); await click(fixture, 'Cancelar y reembolsar');
    expect(fixture.nativeElement.textContent).toContain('el lugar aún no se libera');
    expect(fixture.nativeElement.textContent).not.toContain('Reembolso confirmado');
  });
  it('rechazar la confirmación no solicita reembolso', async () => {
    consultar.mockResolvedValue([{ ...pago, estado: 'pagado' }]);
    vi.mocked(globalThis.confirm).mockReturnValue(false);
    const fixture = await abrir(); await click(fixture, 'Cancelar y reembolsar');
    expect(cancelar).not.toHaveBeenCalled();
  });
});
