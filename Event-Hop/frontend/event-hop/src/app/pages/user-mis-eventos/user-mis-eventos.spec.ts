import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { vi } from 'vitest';
import { UserMisEventos } from './user-mis-eventos';
import { ParticipacionService } from '../../services/participacion.service';
import { AuthService } from '../../services/auth.service';
import { PagosService } from '../../services/pagos.service';

describe('Mis eventos con API', () => {
  const favoritos = vi.fn();
  const quitar = vi.fn();
  const reservar = vi.fn();
  const evento = { id: 'e1', titulo: 'Evento guardado', cafeteria: 'Café real', fecha: '2026-12-20',
    hora: '12:00', esGratis: true, costo: 0, disponibles: 1, imagenUrl: '' };
  beforeEach(async () => {
    favoritos.mockReset().mockResolvedValue([evento]);
    quitar.mockReset().mockResolvedValue(null);
    reservar.mockReset().mockResolvedValue({ mensaje: 'Lugar reservado.' });
    await TestBed.configureTestingModule({ imports: [UserMisEventos], providers: [provideRouter([]),
      { provide: PagosService, useValue: { iniciar: vi.fn() } },
      { provide: ParticipacionService, useValue: { favoritos, quitar, reservar } },
      { provide: AuthService, useValue: { mensajeError: () => 'No se pudo completar.' } }
    ] }).compileComponents();
  });
  async function abrir() {
    const fixture = TestBed.createComponent(UserMisEventos);
    fixture.detectChanges(); await Promise.resolve(); await fixture.whenStable();
    return fixture;
  }
  it('muestra guardados reales sin otro clic', async () => {
    const fixture = await abrir();
    expect(fixture.nativeElement.textContent).toContain('Evento guardado');
    expect(fixture.nativeElement.textContent).not.toContain('Cargando');
  });
  it('quita solo después de respuesta exitosa', async () => {
    const fixture = await abrir();
    fixture.nativeElement.querySelector('.btn-delete').click(); await fixture.whenStable();
    expect(quitar).toHaveBeenCalledWith('e1');
    expect(fixture.componentInstance.misEventosGuardados).toEqual([]);
  });
  it('si quitar falla conserva el favorito y muestra error', async () => {
    quitar.mockRejectedValue(new Error());
    const fixture = await abrir();
    fixture.nativeElement.querySelector('.btn-delete').click(); await fixture.whenStable();
    expect(fixture.componentInstance.misEventosGuardados.length).toBe(1);
    expect(fixture.nativeElement.textContent).toContain('No se pudo completar.');
    expect(fixture.componentInstance.enviando).toBe(false);
  });
  it('reservar no elimina el favorito', async () => {
    const fixture = await abrir();
    fixture.nativeElement.querySelector('.btn-primary').click(); await fixture.whenStable();
    expect(reservar).toHaveBeenCalledWith('e1');
    expect(fixture.componentInstance.misEventosGuardados.length).toBe(1);
    expect(fixture.nativeElement.textContent).toContain('Lugar reservado.');
  });
  it('ofrece comprar con PayPal un evento de paga', async () => {
    favoritos.mockResolvedValue([{ ...evento, esGratis: false, costo: 50 }]);
    const fixture = await abrir();
    expect(fixture.nativeElement.querySelector('.btn-primary').disabled).toBe(false);
    expect(fixture.nativeElement.querySelector('.btn-primary').textContent).toContain('PayPal Sandbox');
  });
});
