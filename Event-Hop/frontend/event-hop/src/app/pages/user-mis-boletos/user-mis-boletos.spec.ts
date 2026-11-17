import { TestBed } from '@angular/core/testing';
import { vi } from 'vitest';
import { provideRouter } from '@angular/router';
import { UserMisBoletos } from './user-mis-boletos';
import { ParticipacionService } from '../../services/participacion.service';
import { AuthService } from '../../services/auth.service';

describe('Mis boletos con API', () => {
  const boletos = vi.fn();
  const cancelar = vi.fn();
  const boleto = { eventoId: 'e1', titulo: 'Evento reservado', cafeteria: 'Café real', fecha: '2026-12-20',
    hora: '12:00', direccion: 'Calle 1', codigo: 'EH-PRUEBA', estado: 'confirmada', imagenUrl: '' };
  beforeEach(async () => {
    boletos.mockReset().mockResolvedValue([boleto]);
    cancelar.mockReset().mockResolvedValue(null);
    vi.spyOn(globalThis, 'confirm').mockReturnValue(true);
    await TestBed.configureTestingModule({ imports: [UserMisBoletos], providers: [
      provideRouter([]),
      { provide: ParticipacionService, useValue: { boletos, cancelar } },
      { provide: AuthService, useValue: { mensajeError: () => 'No se pudo cancelar.' } }
    ] }).compileComponents();
  });
  afterEach(() => vi.restoreAllMocks());
  async function abrirDetalle() {
    const fixture = TestBed.createComponent(UserMisBoletos);
    fixture.detectChanges(); await Promise.resolve(); await fixture.whenStable();
    fixture.nativeElement.querySelector('.boleto-card').click(); await fixture.whenStable();
    return fixture;
  }
  it('muestra el folio real al seleccionar boleto', async () => {
    const fixture = await abrirDetalle();
    expect(fixture.nativeElement.textContent).toContain('EH-PRUEBA');
    expect(fixture.nativeElement.textContent).not.toContain('Cargando');
  });
  it('cancela y oculta el código sin otro clic', async () => {
    const fixture = await abrirDetalle();
    fixture.nativeElement.querySelector('.ticket-body button').click(); await fixture.whenStable();
    expect(cancelar).toHaveBeenCalledWith('e1');
    expect(fixture.nativeElement.textContent).toContain('Reserva cancelada');
    expect(fixture.nativeElement.querySelector('.codigo-valor')).toBeNull();
  });
  it('no cancela si se rechaza la confirmación', async () => {
    vi.mocked(globalThis.confirm).mockReturnValue(false);
    const fixture = await abrirDetalle();
    fixture.nativeElement.querySelector('.ticket-body button').click(); await fixture.whenStable();
    expect(cancelar).not.toHaveBeenCalled();
    expect(fixture.nativeElement.textContent).toContain('EH-PRUEBA');
  });
  it('conserva el boleto si el servidor rechaza la cancelación', async () => {
    cancelar.mockRejectedValue(new Error());
    const fixture = await abrirDetalle();
    fixture.nativeElement.querySelector('.ticket-body button').click(); await fixture.whenStable();
    expect(fixture.nativeElement.textContent).toContain('No se pudo cancelar.');
    expect(fixture.nativeElement.textContent).toContain('EH-PRUEBA');
    expect(fixture.componentInstance.enviando).toBe(false);
  });
});
