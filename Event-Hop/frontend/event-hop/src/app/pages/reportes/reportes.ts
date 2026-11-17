import { Component, ChangeDetectorRef, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { AuthService } from '../../services/auth.service';
import { Reporte, ReportesService } from '../../services/reportes.service';

@Component({ selector: 'app-reportes', standalone: true, imports: [CommonModule, FormsModule],
  templateUrl: './reportes.html', styleUrl: './reportes.css' })
export class Reportes implements OnInit {
  private api = inject(ReportesService);
  private auth = inject(AuthService);
  private detector = inject(ChangeDetectorRef);
  readonly tipos = [ { id: 'eventos', nombre: 'Eventos y cupo' }, { id: 'asistencia', nombre: 'Reservas y asistencia' },
    { id: 'pagos', nombre: 'Pagos y reembolsos Sandbox' }, { id: 'resenas', nombre: 'Reseñas por evento' } ];
  tipo = 'eventos'; desde = ''; hasta = ''; cafeteriaId = '';
  cafeterias: { id: string; nombre: string }[] = [];
  errorCatalogo = '';
  cargando = false; error = ''; reporte: Reporte | null = null;
  pagina = 1; readonly tamano = 15;
  get esAdmin() { return this.auth.usuario?.rol === 'admin'; }
  get filas() { return this.reporte?.filas.slice((this.pagina - 1) * this.tamano, this.pagina * this.tamano) ?? []; }
  get paginas() { return Math.max(1, Math.ceil((this.reporte?.filas.length ?? 0) / this.tamano)); }
  get titulo() { return this.tipos.find(t => t.id === this.reporte?.tipo)?.nombre ?? ''; }
  ngOnInit() { void this.consultar(); if (this.esAdmin) void this.cargarCafeterias(); }
  async cargarCafeterias() {
    try { this.cafeterias = await this.api.cafeterias(); }
    catch { this.errorCatalogo = 'No se pudo cargar el filtro de cafeterías. Recarga para reintentar; la consulta general sigue disponible.'; }
    finally { this.detector.markForCheck(); }
  }
  async consultar() {
    if (this.cargando) return;
    this.error = ''; this.reporte = null; this.pagina = 1;
    if (this.desde && this.hasta && this.desde > this.hasta) { this.error = 'La fecha inicial no puede ser posterior a la final.'; return; }
    this.cargando = true;
    try { this.reporte = await this.api.consultar(this.tipo, this.desde, this.hasta, this.esAdmin ? this.cafeteriaId.trim() : ''); }
    catch (error) { this.error = this.auth.mensajeError(error); }
    finally { this.cargando = false; this.detector.markForCheck(); }
  }
  limpiar() { this.desde = ''; this.hasta = ''; this.cafeteriaId = ''; void this.consultar(); }
}
