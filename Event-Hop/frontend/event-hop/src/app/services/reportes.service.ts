import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';

export interface ResumenAdmin { totalUsuarios: number; cafeteriasAceptadas: number; totalSolicitudesCafeterias: number; totalEventos: number; }
export interface ResumenCafe { totalEventos: number; favoritos: number; reservas: number; entradas: number; }
export interface Reporte { tipo: string; generadoUtc: string; desde: string; hasta: string;
  columnas: string[]; filas: string[][]; indicadores: Record<string, string>; }
@Injectable({ providedIn: 'root' })
export class ReportesService {
  private http = inject(HttpClient);
  resumenAdmin() { return firstValueFrom(this.http.get<ResumenAdmin>('/api/reportes/resumen')); }
  resumenCafe() { return firstValueFrom(this.http.get<ResumenCafe>('/api/reportes/resumen')); }
  cafeterias() { return firstValueFrom(this.http.get<{ id: string; nombre: string }[]>('/api/reportes/cafeterias')); }
  consultar(tipo: string, desde = '', hasta = '', cafeteriaId = '') {
    let params = new HttpParams();
    if (desde) params = params.set('desde', desde);
    if (hasta) params = params.set('hasta', hasta);
    if (cafeteriaId) params = params.set('cafeteriaId', cafeteriaId);
    return firstValueFrom(this.http.get<Reporte>('/api/reportes/' + encodeURIComponent(tipo), { params }));
  }
}

