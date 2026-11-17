import { Component, OnInit, inject, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { EventosService, Evento } from '../../services/eventos.service';
import { AsistenciasService, ListaAsistentes } from '../../services/asistencias.service';
import { AuthService } from '../../services/auth.service';

@Component({
  selector: 'app-cafe-checkin', standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './cafe-checkin.html', styleUrl: './cafe-checkin.css'
})
export class CafeCheckin implements OnInit {
  private eventos = inject(EventosService);
  private asistencias = inject(AsistenciasService);
  private auth = inject(AuthService);
  private detector = inject(ChangeDetectorRef);
  misEventos: Evento[] = [];
  eventoSeleccionado: Evento | null = null;
  lista: ListaAsistentes | null = null;
  codigoIngreso = '';
  cargando = false;
  enviando = false;
  error = '';
  mensaje = '';

  async ngOnInit() {
    this.cargando = true;
    try { this.misEventos = await this.eventos.consultarMios(); }
    catch (error) { this.error = this.auth.mensajeError(error); }
    finally { this.cargando = false; this.detector.markForCheck(); }
  }
  async seleccionarEvento(evento: Evento) {
    if (this.cargando || this.enviando) return;
    this.eventoSeleccionado = evento; this.codigoIngreso = ''; this.mensaje = '';
    await this.actualizar();
  }
  regresarASeccionEventos() {
    if (this.cargando || this.enviando) return;
    this.eventoSeleccionado = null; this.lista = null;
    this.codigoIngreso = ''; this.error = ''; this.mensaje = '';
  }
  async actualizar() {
    if (!this.eventoSeleccionado || this.cargando || this.enviando) return;
    this.cargando = true; this.error = ''; this.lista = null;
    try { this.lista = await this.asistencias.consultar(this.eventoSeleccionado.id); }
    catch (error) { this.error = this.auth.mensajeError(error); }
    finally { this.cargando = false; this.detector.markForCheck(); }
  }
  async verificarCodigo() {
    if (!this.eventoSeleccionado || this.cargando || this.enviando) return;
    const codigo = this.codigoIngreso.trim().toUpperCase();
    this.error = ''; this.mensaje = '';
    if (!/^EH-[A-F0-9]{32}$/.test(codigo)) {
      this.error = 'Copia el código completo del boleto: EH- seguido de 32 letras y números.';
      return;
    }
    if (!confirm('¿Registrar la entrada de este boleto? Solo puede utilizarse una vez.')) return;
    this.enviando = true;
    try {
      const respuesta = await this.asistencias.registrar(this.eventoSeleccionado.id, codigo);
      this.mensaje = respuesta.mensaje;
      this.codigoIngreso = '';
    } catch (error) { this.error = this.auth.mensajeError(error); }
    finally {
      // Consultar incluso tras un corte: la entrada pudo haberse guardado.
      try { this.lista = await this.asistencias.consultar(this.eventoSeleccionado.id); }
      catch { this.lista = null; this.error += ' No se pudo actualizar la lista; recarga antes de reintentar.'; }
      this.enviando = false; this.detector.markForCheck();
    }
  }
}
