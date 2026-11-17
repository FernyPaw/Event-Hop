import { ChangeDetectorRef, Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { AuthService } from '../../services/auth.service';
import { Boleto, ParticipacionService } from '../../services/participacion.service';

@Component({
  selector: 'app-user-mis-boletos',
  imports: [CommonModule, RouterLink],
  templateUrl: './user-mis-boletos.html',
  styleUrl: './user-mis-boletos.css'
})
export class UserMisBoletos implements OnInit {
  private servicio = inject(ParticipacionService);
  private auth = inject(AuthService);
  private detector = inject(ChangeDetectorRef);
  misBoletos: Boleto[] = [];
  boletoSeleccionado: Boleto | null = null;
  cargando = true;
  enviando = false;
  error = '';
  mensaje = '';

  async ngOnInit() {
    try { this.misBoletos = await this.servicio.boletos(); }
    catch (error) { this.error = this.auth.mensajeError(error); }
    finally { this.cargando = false; this.detector.markForCheck(); }
  }
  verDetalleBoleto(boleto: Boleto) { this.boletoSeleccionado = boleto; this.error = ''; this.mensaje = ''; }
  cerrarDetalle() { this.boletoSeleccionado = null; }
  async cancelar(boleto: Boleto) {
    if (this.enviando || !confirm('¿Cancelar tu reserva y liberar el lugar?')) return;
    this.enviando = true; this.error = ''; this.mensaje = '';
    try {
      await this.servicio.cancelar(boleto.eventoId);
      this.misBoletos = this.misBoletos.map(b => b.eventoId === boleto.eventoId ? { ...b, estado: 'cancelada' } : b);
      this.boletoSeleccionado = this.misBoletos.find(b => b.eventoId === boleto.eventoId) ?? null;
      this.mensaje = 'Reserva cancelada. El lugar volvió a estar disponible.';
    } catch (error) { this.error = this.auth.mensajeError(error); }
    finally { this.enviando = false; this.detector.markForCheck(); }
  }
}
