import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { Evento } from './eventos.service';

export interface Boleto {
  eventoId: string;
  titulo: string;
  cafeteria: string;
  fecha: string;
  hora: string;
  direccion: string;
  imagenUrl: string;
  codigo: string;
  estado: 'confirmada' | 'cancelada';
  pagoId?: string;
  esGratis?: boolean;
  fechaEntrada?: string | null;
}

@Injectable({ providedIn: 'root' })
export class ParticipacionService {
  private http = inject(HttpClient);
  favoritos() { return firstValueFrom(this.http.get<Evento[]>('/api/favoritos')); }
  guardar(id: string) { return firstValueFrom(this.http.put('/api/favoritos/' + id, {})); }
  quitar(id: string) { return firstValueFrom(this.http.delete('/api/favoritos/' + id)); }
  boletos() { return firstValueFrom(this.http.get<Boleto[]>('/api/reservas')); }
  reservar(id: string) {
    return firstValueFrom(this.http.put<{ mensaje: string }>('/api/reservas/' + id, {}));
  }
  cancelar(id: string) { return firstValueFrom(this.http.delete('/api/reservas/' + id)); }
}
