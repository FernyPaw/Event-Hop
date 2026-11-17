import { ChangeDetectorRef, Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { AuthService } from '../../services/auth.service';
import { Pago, PagosService } from '../../services/pagos.service';

@Component({
  selector: 'app-user-pagos', imports: [CommonModule, RouterLink],
  templateUrl: './user-pagos.html', styleUrl: './user-pagos.css'
})
export class UserPagos implements OnInit {
  private servicio = inject(PagosService);
  private auth = inject(AuthService);
  private ruta = inject(ActivatedRoute);
  private detector = inject(ChangeDetectorRef);
  pagos: Pago[] = [];
  cargando = true;
  enviando = false;
  error = '';
  mensaje = '';
  seleccionado = this.ruta.snapshot.queryParamMap.get('pago');

  async ngOnInit() {
    if (this.ruta.snapshot.queryParamMap.get('cancelado')) this.mensaje = 'Saliste de la página de pago. Revisa su estado antes de continuar o cancelar.';
    await this.actualizar();
  }
  async actualizar() {
    this.cargando = true;
    try { this.pagos = await this.servicio.consultar(); }
    catch (error) { this.error = this.auth.mensajeError(error); }
    finally { this.cargando = false; this.detector.markForCheck(); }
  }
  nombreEstado(p: Pago) {
    if (p.proveedor === 'stripe' && p.estado === 'pendiente') return 'Stripe por comprobar: conserva el lugar hasta recuperar o cancelar la sesión';
    const nombres: Record<Pago['estado'], string> = {
      pendiente: 'Esperando aprobación (retención de 15 minutos)', vencido: 'Intento vencido: no se cobró',
      capturando: 'Cobro por comprobar: recupera el resultado', pagado: 'Pago confirmado: boleto emitido',
      cancelado: 'Intento cancelado sin cobro', rechazado: 'Pago rechazado',
      reembolsando: 'Reembolso en proceso o por revisar; el lugar aún no se libera', reembolsado: 'Reembolso confirmado: boleto cancelado'
    };
    return nombres[p.estado];
  }
  async operar(pago: Pago, accion: 'orden' | 'confirmar' | 'cancelar') {
    if (this.enviando) return;
    if (accion === 'cancelar' && !confirm(pago.estado === 'pagado'
      ? '¿Cancelar el boleto y solicitar el reembolso completo de prueba?'
      : pago.estado === 'reembolsando' ? '¿Consultar o reintentar el reembolso pendiente?' : '¿Cancelar la compra? Si Stripe ya recibió el pago se comprobará y solicitará el reembolso, si las reglas lo permiten.')) return;
    this.enviando = true; this.error = ''; this.mensaje = '';
    try {
      const actualizado = await this.servicio[accion](pago);
      this.pagos = this.pagos.map(p => p.pagoId === pago.pagoId ? actualizado : p);
      if (accion === 'orden' && actualizado.urlAprobacion) {
        if (actualizado.proveedor === 'stripe') this.servicio.irAStripe(actualizado.urlAprobacion);
        else this.servicio.irAPayPal(actualizado.urlAprobacion);
      }
      else this.mensaje = this.nombreEstado(actualizado);
    } catch (error) {
      this.error = this.auth.mensajeError(error);
      // Un error de conexión no significa que el cobro falló: mostrar el estado persistido.
      try { this.pagos = await this.servicio.consultar(); } catch { /* Conserva el aviso original. */ }
    }
    finally { this.enviando = false; this.detector.markForCheck(); }
  }
}
