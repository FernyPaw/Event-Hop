import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { UserPerfil } from './user-perfil';
import { AuthService } from '../../services/auth.service';

describe('UserPerfil', () => {
  it('abre el perfil conectado', async () => {
    await TestBed.configureTestingModule({ imports: [UserPerfil], providers: [
      provideHttpClient(), provideHttpClientTesting(),
      { provide: AuthService, useValue: { actualizarUsuario: () => {}, mensajeError: () => 'Error' } }
    ] }).compileComponents();
    const f = TestBed.createComponent(UserPerfil); f.detectChanges();
    const http = TestBed.inject(HttpTestingController);
    http.expectOne('/api/perfil').flush({ id: 'u1', nombre: 'Cuenta real', correo: 'cuenta@example.test', rol: 'usuario' });
    await f.whenStable(); f.detectChanges();
    expect(f.nativeElement.textContent).toContain('cuenta@example.test'); http.verify();
  });
});
