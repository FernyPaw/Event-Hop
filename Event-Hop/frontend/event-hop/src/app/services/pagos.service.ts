import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';

export interface Pago {
  proveedor?: 'paypal' | 'stripe';
  eventoId: string;
  pagoId: string;
  titulo: string;
  estado: 'pendiente' | 'vencido' | 'capturando' | 'pagado' | 'cancelado' | 'rechazado' | 'reembolsando' | 'reembolsado';
  importe: number;
  moneda: string;
  sandbox: boolean;
  boletoUtilizado?: boolean;
  vence: string;
  urlAprobacion: string;
}
@Injectable({ providedIn: 'root' })
export class PagosService {
  private http = inject(HttpClient);
  consultar() { return firstValueFrom(this.http.get<Pago[]>('/api/pagos')); }
  iniciar(eventoId: string) { return firstValueFrom(this.http.post<Pago>('/api/pagos', { eventoId })); }
  iniciarStripe(eventoId: string) { return firstValueFrom(this.http.post<Pago>('/api/stripe/pagos', { eventoId })); }
  orden(p: Pago) { return firstValueFrom(this.http.post<Pago>(this.ruta(p) + '/orden', {})); }
  confirmar(p: Pago) { return firstValueFrom(this.http.post<Pago>(this.ruta(p) + '/confirmacion', {})); }
  cancelar(p: Pago) { return firstValueFrom(this.http.delete<Pago>(this.ruta(p))); }
  private ruta(p: Pago) { return (p.proveedor === 'stripe' ? '/api/stripe/pagos/' : '/api/pagos/') + p.eventoId + '/' + p.pagoId; }
  irAStripe(url: string) {
    const destino = new URL(url);
    if (destino.protocol !== 'https:' || destino.hostname !== 'checkout.stripe.com' || destino.username || destino.password || (destino.port && destino.port !== '443'))
      throw new Error('Enlace de Stripe no válido');
    window.location.assign(destino.href);
  }
  irAPayPal(url: string) {
    const destino = new URL(url);
    if (destino.protocol !== 'https:' || !['www.sandbox.paypal.com', 'sandbox.paypal.com'].includes(destino.hostname) || destino.username || destino.password)
      throw new Error('Enlace de pago no válido');
    window.location.assign(destino.href);
  }
}
