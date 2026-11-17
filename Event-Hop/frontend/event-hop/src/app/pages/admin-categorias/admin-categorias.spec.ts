import { TestBed } from '@angular/core/testing';
import { vi } from 'vitest';
import { AdminCategorias } from './admin-categorias';
import { CategoriasService } from '../../services/categorias.service';
import { AuthService } from '../../services/auth.service';
describe('Categorías reales', () => {
  const api = { consultar: vi.fn(), guardar: vi.fn(), quitar: vi.fn() };
  const categoria = { id: 'c1', nombre: 'Lectura', descripcion: 'Clubes', activa: true };
  beforeEach(async () => {
    api.consultar.mockReset().mockResolvedValue([{ ...categoria }]);
    api.guardar.mockReset().mockResolvedValue({}); api.quitar.mockReset().mockResolvedValue({});
    await TestBed.configureTestingModule({ imports: [AdminCategorias], providers: [
      { provide: CategoriasService, useValue: api }, { provide: AuthService, useValue: { mensajeError: () => 'Error API' } }
    ] }).compileComponents();
  });
  async function abrir() {
    const f = TestBed.createComponent(AdminCategorias);
    await f.componentInstance.cargar(); f.detectChanges(); await f.whenStable(); return f;
  }
  it('consulta catálogo real', async () => {
    const f = await abrir(); expect(f.componentInstance.lista[0].nombre).toBe('Lectura');
  });
  it('crea categoría y actualiza lista', async () => {
    const f = await abrir(); f.componentInstance.nombre = 'Juegos'; await f.componentInstance.guardar();
    expect(api.guardar).toHaveBeenCalledWith({ nombre: 'Juegos', descripcion: '', activa: true }, '');
    expect(f.componentInstance.mensaje).toBe('Categoría guardada.');
  });
  it('edita por identificador sin duplicar', async () => {
    const f = await abrir(); f.componentInstance.editar(categoria);
    f.componentInstance.descripcion = 'Nueva'; await f.componentInstance.guardar();
    expect(api.guardar).toHaveBeenCalledWith(expect.objectContaining({ descripcion: 'Nueva', nombre: 'Lectura' }), 'c1');
  });
  it('rechaza nombre en blanco', async () => {
    const f = await abrir(); f.componentInstance.nombre = ' '; await f.componentInstance.guardar();
    expect(api.guardar).not.toHaveBeenCalled();
  });
  it('no anuncia éxito si falla API', async () => {
    const f = await abrir(); api.guardar.mockRejectedValue(new Error()); f.componentInstance.nombre = 'Nuevo';
    await f.componentInstance.guardar(); expect(f.componentInstance.mensaje).toBe('');
    expect(f.componentInstance.error).toBe('Error API');
  });
});
