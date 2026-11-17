import { ChangeDetectorRef, Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { AuthService } from '../../services/auth.service';
import { Categoria, CategoriasService } from '../../services/categorias.service';
@Component({
  selector: 'app-admin-categorias', standalone: true, imports: [CommonModule, FormsModule],
  templateUrl: './admin-categorias.html', styleUrl: './admin-categorias.css'
})
export class AdminCategorias implements OnInit {
  private servicio = inject(CategoriasService);
  private auth = inject(AuthService);
  private detector = inject(ChangeDetectorRef);
  lista: Categoria[] = [];
  id = ''; nombre = ''; descripcion = ''; activa = true;
  ocupado = false; error = ''; mensaje = '';
  ngOnInit() { void this.cargar(); }
  async cargar() {
    this.ocupado = true; this.error = '';
    try { this.lista = await this.servicio.consultar(); }
    catch (e) { this.error = this.auth.mensajeError(e); }
    finally { this.ocupado = false; this.detector.markForCheck(); }
  }
  editar(c: Categoria) { this.id = c.id; this.nombre = c.nombre; this.descripcion = c.descripcion; this.activa = c.activa; this.mensaje = ''; }
  limpiar() { this.id = ''; this.nombre = ''; this.descripcion = ''; this.activa = true; }
  async guardar() {
    if (this.ocupado) return;
    this.error = ''; this.mensaje = '';
    if (this.nombre.trim().length < 2 || this.nombre.length > 50 || this.descripcion.length > 300) {
      this.error = 'Revisa nombre (2 a 50 caracteres) y descripción (máximo 300).'; return;
    }
    this.ocupado = true;
    try {
      await this.servicio.guardar({ nombre: this.nombre.trim(), descripcion: this.descripcion.trim(), activa: this.activa }, this.id);
      this.limpiar(); this.mensaje = 'Categoría guardada.';
      try { this.lista = await this.servicio.consultar(); }
      catch { this.error = 'Se guardó, pero no se pudo actualizar la lista. Pulsa Actualizar.'; }
    } catch (e) { this.error = this.auth.mensajeError(e); }
    finally { this.ocupado = false; this.detector.markForCheck(); }
  }
  async quitar(c: Categoria) {
    if (this.ocupado || !confirm('¿Desactivar esta categoría? Los eventos existentes la conservarán.')) return;
    this.ocupado = true; this.error = ''; this.mensaje = '';
    try { await this.servicio.quitar(c.id); c.activa = false; this.mensaje = 'Categoría desactivada.'; }
    catch (e) { this.error = this.auth.mensajeError(e); }
    finally { this.ocupado = false; this.detector.markForCheck(); }
  }
}
