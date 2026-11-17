import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
export interface Resena { id: string; autor: string; calificacion: number; comentario: string; esMia: boolean; fechaActualizacion: string; }
export interface Aviso { id: string; titulo: string; contenido: string; fechaActualizacion: string; }
export interface ComunidadEvento {
  titulo: string; puedeVerAvisos: boolean; puedeAvisar: boolean; puedeResenar: boolean;
  resenas: Resena[]; avisos: Aviso[];
}
@Injectable({ providedIn: 'root' })
export class ComunidadService {
  private http = inject(HttpClient);
  private url(evento: string) { return '/api/comunidad/' + encodeURIComponent(evento); }
  consultar(evento: string) { return firstValueFrom(this.http.get<ComunidadEvento>(this.url(evento))); }
  resenar(evento: string, calificacion: number, comentario: string) {
    return firstValueFrom(this.http.put(this.url(evento) + '/resena', { calificacion, comentario }));
  }
  quitarResena(evento: string) { return firstValueFrom(this.http.delete(this.url(evento) + '/resena')); }
  avisar(evento: string, titulo: string, contenido: string, id = '') {
    const url = this.url(evento) + '/avisos';
    return firstValueFrom(id ? this.http.put(url + '/' + encodeURIComponent(id), { titulo, contenido })
      : this.http.post(url, { titulo, contenido }));
  }
  quitarAviso(evento: string, id: string) {
    return firstValueFrom(this.http.delete(this.url(evento) + '/avisos/' + encodeURIComponent(id)));
  }
}
