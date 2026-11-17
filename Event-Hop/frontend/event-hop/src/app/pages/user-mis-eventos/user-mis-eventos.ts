import { ChangeDetectorRef, Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterLink } from '@angular/router';
import { PagosService } from '../../services/pagos.service';
import { AuthService } from '../../services/auth.service';
import { Evento } from '../../services/eventos.service';
import { ParticipacionService } from '../../services/participacion.service';

@Component({
  selector: 'app-user-mis-eventos',
  imports: [CommonModule, RouterLink],
  templateUrl: './user-mis-eventos.html',
  styleUrl: './user-mis-eventos.css'
})
export class UserMisEventos implements OnInit {
  private servicio = inject(ParticipacionService);
  private auth = inject(AuthService);
  private detector = inject(ChangeDetectorRef);
  private pagos = inject(PagosService);
  private router = inject(Router);
  misEventosGuardados: Evento[] = [];
  cargando = true;
  enviando = false;
  error = '';
  mensaje = '';

  async ngOnInit() {
    try { this.misEventosGuardados = await this.servicio.favoritos(); }
    catch (error) { this.error = this.auth.mensajeError(error); }
    finally { this.cargando = false; this.detector.markForCheck(); }
  }
  async removerDeGuardados(id: string) {
    if (this.enviando) return;
    this.enviando = true; this.error = ''; this.mensaje = '';
    try {
      await this.servicio.quitar(id);
      this.misEventosGuardados = this.misEventosGuardados.filter(e => e.id !== id);
      this.mensaje = 'Evento quitado de guardados.';
    } catch (error) { this.error = this.auth.mensajeError(error); }
    finally { this.enviando = false; this.detector.markForCheck(); }
  }
  async comprarBoleto(evento: Evento) {
    if (this.enviando) return;
    this.enviando = true; this.error = ''; this.mensaje = '';
    try {
      if (!evento.esGratis) {
        const pago = await this.pagos.iniciar(evento.id);
        await this.router.navigate(['/usuario/pagos'], { queryParams: { evento: evento.id, pago: pago.pagoId } });
        return;
      }
      this.mensaje = (await this.servicio.reservar(evento.id)).mensaje;
      try { this.misEventosGuardados = await this.servicio.favoritos(); }
      catch { this.mensaje += ' Recarga para actualizar el cupo.'; }
    } catch (error) { this.error = this.auth.mensajeError(error); }
    finally { this.enviando = false; this.detector.markForCheck(); }
  }
}
