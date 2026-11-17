import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';

export interface EventoFormulario {
  titulo: string;
  descripcion: string;
  categoria: string;
  direccion: string;
  fecha: string;
  hora: string;
  esGratis: boolean;
  costo: number;
  cupo: number;
  imagenUrl: string;
}

export interface Evento extends EventoFormulario {
  id: string;
  cafeteriaId: string;
  cafeteria: string;
  disponibles?: number;
}

@Injectable({ providedIn: 'root' })
export class EventosService {
  private http = inject(HttpClient);
  consultar() { return firstValueFrom(this.http.get<Evento[]>('/api/eventos')); }
  consultarMios() { return firstValueFrom(this.http.get<Evento[]>('/api/eventos/mios')); }
  crear(datos: EventoFormulario) { return firstValueFrom(this.http.post('/api/eventos', datos)); }
  editar(id: string, datos: EventoFormulario) {
    return firstValueFrom(this.http.put('/api/eventos/' + id, datos));
  }
  eliminar(id: string) { return firstValueFrom(this.http.delete('/api/eventos/' + id)); }
}
