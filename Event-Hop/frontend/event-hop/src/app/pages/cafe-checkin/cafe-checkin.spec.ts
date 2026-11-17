import { TestBed } from '@angular/core/testing';
import { vi } from 'vitest';
import { CafeCheckin } from './cafe-checkin';
import { EventosService } from '../../services/eventos.service';
import { AsistenciasService } from '../../services/asistencias.service';
import { AuthService } from '../../services/auth.service';

describe('Asistentes reales', () => {
  const consultarMios = vi.fn(), consultar = vi.fn(), registrar = vi.fn();
  const evento = { id: 'e1', titulo: 'Evento propio', fecha: '2026-11-15', hora: '18:00' };
  const lista = { eventoId: 'e1', titulo: 'Evento propio', confirmadas: 1, entradas: 0,
    asistentes: [{ nombre: 'Pedro', codigo: 'EH-' + 'A'.repeat(32), estado: 'confirmada', pago: 'gratuito', fechaEntrada: null }] };
  beforeEach(async () => {
    consultarMios.mockReset().mockResolvedValue([evento]);
    consultar.mockReset().mockResolvedValue(lista);
    registrar.mockReset().mockResolvedValue({ mensaje: 'Entrada registrada correctamente.' });
    vi.spyOn(window, 'confirm').mockReturnValue(true);
    await TestBed.configureTestingModule({ imports: [CafeCheckin], providers: [
      { provide: EventosService, useValue: { consultarMios } },
      { provide: AsistenciasService, useValue: { consultar, registrar } },
      { provide: AuthService, useValue: { mensajeError: () => 'Boleto rechazado por servidor' } }
    ] }).compileComponents();
  });
  afterEach(() => vi.restoreAllMocks());
  async function abrir() {
    const f = TestBed.createComponent(CafeCheckin);
    f.detectChanges(); await Promise.resolve(); await f.whenStable(); return f;
  }
  it('carga solo eventos del servicio autenticado, sin datos de demostración', async () => {
    const f = await abrir();
    expect(f.nativeElement.textContent).toContain('Evento propio');
    expect(f.nativeElement.textContent).not.toContain('Noche de poesía');
    expect(consultarMios).toHaveBeenCalledOnce();
  });
  it('muestra asistentes devueltos por la API sin otro clic', async () => {
    const f = await abrir();
    await f.componentInstance.seleccionarEvento(evento as any); await f.whenStable();
    expect(consultar).toHaveBeenCalledWith('e1');
    expect(f.nativeElement.textContent).toContain('Pedro');
  });
  it('rechaza código corto antes de solicitar entrada', async () => {
    const f = await abrir(); await f.componentInstance.seleccionarEvento(evento as any);
    f.componentInstance.codigoIngreso = 'EH-101';
    await f.componentInstance.verificarCodigo();
    expect(registrar).not.toHaveBeenCalled();
  });
  it('registra solo al confirmar y vuelve a consultar el resultado', async () => {
    const f = await abrir(); await f.componentInstance.seleccionarEvento(evento as any);
    f.componentInstance.codigoIngreso = ' eh-' + 'a'.repeat(32) + ' ';
    await f.componentInstance.verificarCodigo(); await f.whenStable();
    expect(registrar).toHaveBeenCalledWith('e1', 'EH-' + 'A'.repeat(32));
    expect(consultar).toHaveBeenCalledTimes(2);
    expect(f.nativeElement.textContent).toContain('Entrada registrada correctamente.');
  });
  it('cancelar el diálogo no registra una entrada', async () => {
    const f = await abrir(); await f.componentInstance.seleccionarEvento(evento as any);
    f.componentInstance.codigoIngreso = 'EH-' + 'A'.repeat(32);
    vi.mocked(window.confirm).mockReturnValue(false);
    await f.componentInstance.verificarCodigo(); expect(registrar).not.toHaveBeenCalled();
  });
  it('un rechazo no muestra acceso concedido y consulta el estado', async () => {
    const f = await abrir(); await f.componentInstance.seleccionarEvento(evento as any);
    f.componentInstance.codigoIngreso = 'EH-' + 'A'.repeat(32);
    registrar.mockRejectedValue(new Error());
    await f.componentInstance.verificarCodigo(); await f.whenStable();
    expect(f.nativeElement.textContent).toContain('Boleto rechazado por servidor');
    expect(f.nativeElement.textContent).not.toContain('Entrada registrada correctamente.');
    expect(f.componentInstance.enviando).toBe(false);
    expect(consultar).toHaveBeenCalledTimes(2);
  });
  it('muestra errores de carga y no datos anteriores', async () => {
    const f = await abrir(); consultar.mockRejectedValue(new Error());
    await f.componentInstance.seleccionarEvento(evento as any); await f.whenStable();
    expect(f.componentInstance.lista).toBeNull();
    expect(f.nativeElement.textContent).toContain('Boleto rechazado por servidor');
  });
});
