import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { vi } from 'vitest';
import { AdminDashboardComponent } from './admin-dashboard';
import { ReportesService } from '../../services/reportes.service';
import { AuthService } from '../../services/auth.service';
describe('Panel admin real', () => {
  const api = { resumenAdmin: vi.fn() };
  beforeEach(async () => {
    api.resumenAdmin.mockReset().mockResolvedValue({ totalUsuarios: 2, cafeteriasAceptadas: 1, totalSolicitudesCafeterias: 3, totalEventos: 7 });
    await TestBed.configureTestingModule({ imports: [AdminDashboardComponent], providers: [provideRouter([]),
      { provide: ReportesService, useValue: api }, { provide: AuthService, useValue: { mensajeError: () => 'Error de prueba' } }
    ] }).compileComponents();
  });
  it('muestra el resultado de API y no valores fijos', async () => {
    const f = TestBed.createComponent(AdminDashboardComponent); f.detectChanges(); await vi.waitFor(() => expect(f.componentInstance.cargando).toBe(false)); await f.whenStable();
    expect(f.componentInstance.resumen?.totalUsuarios).toBe(2); expect(f.nativeElement.textContent).not.toContain('24');
  });
  it('no muestra cero como resultado cuando falla la API', async () => {
    api.resumenAdmin.mockRejectedValue(new Error());
    const f = TestBed.createComponent(AdminDashboardComponent); f.detectChanges(); await vi.waitFor(() => expect(f.componentInstance.cargando).toBe(false)); await f.whenStable();
    expect(f.componentInstance.resumen).toBeNull(); expect(f.nativeElement.textContent).toContain('Error de prueba');
  });
});
