import { Component, OnInit, inject, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { AuthService } from '../../services/auth.service';
import { Evento, EventosService } from '../../services/eventos.service';
import { ParticipacionService } from '../../services/participacion.service';
import { PagosService } from '../../services/pagos.service';
import { Router } from '@angular/router';

@Component({
  selector: 'app-home',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './home.html',
  styleUrl: './home.css'
})
export class Home implements OnInit {
  private servicio = inject(EventosService);
  private auth = inject(AuthService);
  private detector = inject(ChangeDetectorRef);
  private participacion = inject(ParticipacionService);
  private pagos = inject(PagosService);
  private router = inject(Router);
  guardados: string[] = [];
  reservados: string[] = [];
  enviando = false;
  mensaje = '';
  get esUsuario() { return this.auth.usuario?.rol === 'usuario'; }
  eventos: Evento[] = [];
  eventoSeleccionado: Evento | null = null;
  cargando = true;
  error = '';
  filtroCafeteria = '';
  filtroDia = '';
  filtroTipo = '';
  dias = ['Domingo', 'Lunes', 'Martes', 'Miércoles', 'Jueves', 'Viernes', 'Sábado'];

  async ngOnInit() {
    try {
      this.eventos = await this.servicio.consultar();
      if (this.esUsuario) {
        const [favoritos, boletos] = await Promise.all([this.participacion.favoritos(), this.participacion.boletos()]);
        this.guardados = favoritos.map(e => e.id);
        this.reservados = boletos.filter(b => b.estado === 'confirmada').map(b => b.eventoId);
      }
    } catch (error) {
      this.error = this.auth.mensajeError(error);
    } finally {
      this.cargando = false;
      this.detector.markForCheck();
    }
  }

  get listaCafeterias() {
    return [...new Set(this.eventos.map(evento => evento.cafeteria))];
  }

  diaEvento(evento: Evento) {
    return this.dias[new Date(evento.fecha + 'T12:00:00').getDay()];
  }

  get eventosFiltrados() {
    return this.eventos.filter(evento =>
      (!this.filtroCafeteria || evento.cafeteria === this.filtroCafeteria) &&
      (!this.filtroDia || this.diaEvento(evento) === this.filtroDia) &&
      (!this.filtroTipo || (this.filtroTipo === 'gratis' ? evento.esGratis : !evento.esGratis)));
  }

  verDetalle(evento: Evento) { this.eventoSeleccionado = evento; this.error = ''; this.mensaje = ''; }
  cerrarDetalle() { this.eventoSeleccionado = null; }

  async guardar(evento: Evento) {
    if (this.enviando) return;
    this.enviando = true; this.error = ''; this.mensaje = '';
    try {
      if (this.guardados.includes(evento.id)) {
        await this.participacion.quitar(evento.id);
        this.guardados = this.guardados.filter(id => id !== evento.id);
        this.mensaje = 'Evento quitado de guardados.';
      } else {
        await this.participacion.guardar(evento.id);
        this.guardados = [...this.guardados, evento.id];
        this.mensaje = 'Evento guardado. Puedes verlo en Mis eventos.';
      }
    } catch (error) { this.error = this.auth.mensajeError(error); }
    finally { this.enviando = false; this.detector.markForCheck(); }
  }

  async reservar(evento: Evento) {
    if (this.enviando) return;
    if (!evento.esGratis) { await this.comprar(evento); return; }
    this.enviando = true; this.error = ''; this.mensaje = '';
    try {
      const respuesta = await this.participacion.reservar(evento.id);
      this.reservados = [...this.reservados.filter(id => id !== evento.id), evento.id];
      this.mensaje = respuesta.mensaje;
      try {
        this.eventos = await this.servicio.consultar();
        this.eventoSeleccionado = this.eventos.find(e => e.id === evento.id) ?? null;
      } catch { this.mensaje += ' Recarga para actualizar los lugares disponibles.'; }
    } catch (error) { this.error = this.auth.mensajeError(error); }
    finally { this.enviando = false; this.detector.markForCheck(); }
  }
  async comprar(evento: Evento, proveedor: 'paypal' | 'stripe' = 'paypal') {
    this.enviando = true; this.error = ''; this.mensaje = '';
    try {
      const pago = proveedor === 'stripe' ? await this.pagos.iniciarStripe(evento.id) : await this.pagos.iniciar(evento.id);
      await this.router.navigate(['/usuario/pagos'], { queryParams: { evento: evento.id, pago: pago.pagoId } });
    } catch (error) { this.error = this.auth.mensajeError(error); }
    finally { this.enviando = false; this.detector.markForCheck(); }
  }
}
