import { Component, OnInit, ChangeDetectorRef, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { AuthService } from '../../services/auth.service';
import { ReportesService, ResumenAdmin } from '../../services/reportes.service';

@Component({
  selector: 'app-admin-dashboard',
  standalone: true,
  imports: [CommonModule, RouterLink],
  templateUrl: './admin-dashboard.html',
  styleUrl: './admin-dashboard.css'
})
export class AdminDashboardComponent implements OnInit {
  private api = inject(ReportesService);
  private auth = inject(AuthService);
  private detector = inject(ChangeDetectorRef);
  resumen: ResumenAdmin | null = null;
  cargando = false;
  error = '';
  ngOnInit() { void this.actualizar(); }
  async actualizar() {
    if (this.cargando) return;
    this.cargando = true; this.error = ''; this.resumen = null;
    try { this.resumen = await this.api.resumenAdmin(); }
    catch (error) { this.error = this.auth.mensajeError(error); }
    finally { this.cargando = false; this.detector.markForCheck(); }
  }
}
