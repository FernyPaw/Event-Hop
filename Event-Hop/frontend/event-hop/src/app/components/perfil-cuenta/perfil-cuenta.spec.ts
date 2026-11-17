import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { vi } from 'vitest';
import { PerfilCuenta } from './perfil-cuenta';
import { AuthService } from '../../services/auth.service';

describe('Perfil conectado', () => {
  const usuario = { id: 'u1', nombre: 'Pedro Huerta', correo: 'pedro@example.test', rol: 'usuario', rfc: '', estadoSolicitud: 'no_aplica' };
  const actualizarUsuario = vi.fn();
  let http: HttpTestingController;
  beforeEach(async () => {
    actualizarUsuario.mockReset();
    await TestBed.configureTestingModule({ imports: [PerfilCuenta], providers: [
      provideHttpClient(), provideHttpClientTesting(),
      { provide: AuthService, useValue: { actualizarUsuario, mensajeError: () => 'Solicitud rechazada' } }
    ] }).compileComponents();
    http = TestBed.inject(HttpTestingController);
  });
  afterEach(() => http.verify());
  async function abrir(datos = usuario) {
    const f = TestBed.createComponent(PerfilCuenta); f.detectChanges();
    http.expectOne('/api/perfil').flush(datos);
    await f.whenStable(); f.detectChanges(); return f;
  }
  it('consulta nombre y correo reales, sin estadísticas inventadas', async () => {
    const f = await abrir();
    expect(f.componentInstance.nombre).toBe('Pedro Huerta');
    expect(f.nativeElement.textContent).toContain('pedro@example.test');
    expect(f.nativeElement.textContent).not.toContain('María González');
    expect(f.componentInstance.iniciales).toBe('PH');
  });
  it('guarda únicamente el nombre y actualiza la sesión tras respuesta', async () => {
    const f = await abrir(); actualizarUsuario.mockClear();
    f.componentInstance.nombre = '  Pedro Nuevo  ';
    const guardado = f.componentInstance.guardar();
    const req = http.expectOne('/api/perfil');
    expect(req.request.method).toBe('PUT');
    expect(req.request.body).toEqual({ nombre: 'Pedro Nuevo' });
    expect(actualizarUsuario).not.toHaveBeenCalled();
    req.flush({ ...usuario, nombre: 'Pedro Nuevo' }); await guardado;
    expect(actualizarUsuario).toHaveBeenCalledWith({ ...usuario, nombre: 'Pedro Nuevo' });
    expect(f.componentInstance.mensaje).toBe('Perfil guardado correctamente.');
  });
  it('rechaza espacios, nombre corto, controles y exceso de longitud', async () => {
    const f = await abrir();
    for (const nombre of ['  ', 'a', 'a'.repeat(101), 'Pedro\nNuevo']) {
      f.componentInstance.nombre = nombre; await f.componentInstance.guardar();
      http.expectNone('/api/perfil'); expect(f.componentInstance.error).toContain('nombre válido');
    }
  });
  it('un fallo al guardar no anuncia éxito ni actualiza sesión', async () => {
    const f = await abrir(); actualizarUsuario.mockClear();
    f.componentInstance.nombre = 'Nombre nuevo';
    const guardado = f.componentInstance.guardar();
    http.expectOne('/api/perfil').flush({}, { status: 500, statusText: 'Error' }); await guardado;
    expect(f.componentInstance.error).toBe('Solicitud rechazada');
    expect(f.componentInstance.mensaje).toBe(''); expect(actualizarUsuario).not.toHaveBeenCalled();
    expect(f.componentInstance.ocupado).toBe(false);
  });
  it('una cuenta cafetería consulta RFC y solicitud', async () => {
    const f = await abrir({ ...usuario, rol: 'cafeteria', rfc: 'ABC010101XXX', estadoSolicitud: 'aprobada' });
    expect(f.nativeElement.textContent).toContain('ABC010101XXX');
    expect(f.nativeElement.textContent).toContain('aprobada');
  });
  it('permite reintentar tras un error de carga', async () => {
    const f = TestBed.createComponent(PerfilCuenta); f.detectChanges();
    http.expectOne('/api/perfil').flush({}, { status: 500, statusText: 'Error' }); await f.whenStable();
    expect(f.componentInstance.usuario).toBeNull();
    const carga = f.componentInstance.cargar(); http.expectOne('/api/perfil').flush(usuario); await carga;
    expect(f.componentInstance.nombre).toBe('Pedro Huerta');
  });
});
