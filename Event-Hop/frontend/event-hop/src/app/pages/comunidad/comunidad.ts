import { ChangeDetectorRef, Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { AuthService } from '../../services/auth.service';
import { Evento, EventosService } from '../../services/eventos.service';
import { Aviso, ComunidadEvento, ComunidadService } from '../../services/comunidad.service';
@Component({
  selector: 'app-comunidad', standalone: true, imports: [CommonModule, FormsModule],
  templateUrl: './comunidad.html', styleUrl: './comunidad.css'
})
export class Comunidad implements OnInit {
  private auth = inject(AuthService);
  private eventosServicio = inject(EventosService);
  private servicio = inject(ComunidadService);
  private detector = inject(ChangeDetectorRef);
  eventos: Evento[] = []; eventoId = ''; datos: ComunidadEvento | null = null;
  ocupado = false; error = ''; mensaje = '';
  calificacion = 5; comentario = ''; avisoId = ''; titulo = ''; contenido = '';
  get esCafe() { return this.auth.usuario?.rol === 'cafeteria'; }
  get tengoResena() { return this.datos?.resenas.some(r => r.esMia) ?? false; }
  ngOnInit() { void this.cargarEventos(); }
  async cargarEventos() {
    this.ocupado = true; this.error = '';
    try { this.eventos = await (this.esCafe ? this.eventosServicio.consultarMios() : this.eventosServicio.consultar()); }
    catch (e) { this.error = this.auth.mensajeError(e); }
    finally { this.ocupado = false; this.detector.markForCheck(); }
  }
  private async consultar() {
    this.datos = await this.servicio.consultar(this.eventoId);
    const mia = this.datos.resenas.find(r => r.esMia);
    this.calificacion = mia?.calificacion ?? 5; this.comentario = mia?.comentario ?? '';
  }
  async seleccionar() {
    if (this.ocupado) return;
    this.datos = null; this.mensaje = ''; this.error = ''; this.limpiarAviso();
    if (!this.eventoId) return;
    this.ocupado = true;
    try { await this.consultar(); }
    catch (e) { this.error = this.auth.mensajeError(e); }
    finally { this.ocupado = false; this.detector.markForCheck(); }
  }
  private async escribir(accion: () => Promise<unknown>, mensaje: string) {
    if (this.ocupado || !this.datos) return;
    this.ocupado = true; this.error = ''; this.mensaje = '';
    try {
      await accion(); this.mensaje = mensaje; this.limpiarAviso();
      try { await this.consultar(); }
      catch { this.datos = null; this.error = 'Se guardó el cambio, pero no se pudo actualizar. Pulsa Actualizar evento.'; }
    } catch (e) { this.error = this.auth.mensajeError(e); }
    finally { this.ocupado = false; this.detector.markForCheck(); }
  }
  async guardarResena() {
    if (!this.datos?.puedeResenar) return;
    if (!Number.isInteger(Number(this.calificacion)) || Number(this.calificacion) < 1 || Number(this.calificacion) > 5 ||
      this.comentario.trim().length < 3 || this.comentario.length > 1000) { this.error = 'Califica de 1 a 5 y comenta entre 3 y 1000 caracteres.'; return; }
    await this.escribir(() => this.servicio.resenar(this.eventoId, Number(this.calificacion), this.comentario.trim()), 'Reseña guardada.');
  }
  async borrarResena() {
    if (this.ocupado || !confirm('¿Eliminar tu reseña?')) return;
    await this.escribir(() => this.servicio.quitarResena(this.eventoId), 'Reseña eliminada.');
  }
  editarAviso(a: Aviso) { this.avisoId = a.id; this.titulo = a.titulo; this.contenido = a.contenido; }
  limpiarAviso() { this.avisoId = ''; this.titulo = ''; this.contenido = ''; }
  async guardarAviso() {
    if (!this.datos?.puedeAvisar) return;
    if (this.titulo.trim().length < 3 || this.titulo.length > 100 || this.contenido.trim().length < 3 || this.contenido.length > 2000) {
      this.error = 'Revisa título (3 a 100 caracteres) y contenido (3 a 2000).'; return;
    }
    await this.escribir(() => this.servicio.avisar(this.eventoId, this.titulo.trim(), this.contenido.trim(), this.avisoId), 'Aviso guardado.');
  }
  async borrarAviso(a: Aviso) {
    if (this.ocupado || !confirm('¿Eliminar este aviso?')) return;
    await this.escribir(() => this.servicio.quitarAviso(this.eventoId, a.id), 'Aviso eliminado.');
  }
}
