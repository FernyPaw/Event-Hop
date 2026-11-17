import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
export interface Categoria { id: string; nombre: string; descripcion: string; activa: boolean; }
@Injectable({ providedIn: 'root' })
export class CategoriasService {
  private http = inject(HttpClient);
  consultar() { return firstValueFrom(this.http.get<Categoria[]>('/api/categorias')); }
  guardar(datos: Omit<Categoria, 'id'>, id = '') {
    return firstValueFrom(id ? this.http.put('/api/categorias/' + encodeURIComponent(id), datos) : this.http.post('/api/categorias', datos));
  }
  quitar(id: string) { return firstValueFrom(this.http.delete('/api/categorias/' + encodeURIComponent(id))); }
}
