import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';

export interface ListaAsistentes {
  eventoId: string; titulo: string; fecha: string; hora: string;
  confirmadas: number; entradas: number;
  asistentes: { nombre: string; codigo: string; estado: string; pago: string; fechaEntrada: string | null }[];
}
@Injectable({ providedIn: 'root' })
export class AsistenciasService {
  private http = inject(HttpClient);
  consultar(id: string) { return firstValueFrom(this.http.get<ListaAsistentes>('/api/asistencias/' + id)); }
  registrar(id: string, codigo: string) {
    return firstValueFrom(this.http.post<{ mensaje: string }>('/api/asistencias/' + id, { codigo }));
  }
}
