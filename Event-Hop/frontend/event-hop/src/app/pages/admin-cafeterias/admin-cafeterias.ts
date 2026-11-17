import { Component, OnInit, inject, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { AuthService, UsuarioSesion } from '../../services/auth.service';

@Component({
  imports: [CommonModule, FormsModule],
  selector: 'app-admin-cafeterias',
  styleUrl: './admin-cafeterias.css',
  templateUrl: './admin-cafeterias.html'
})
export class AdminCafeterias implements OnInit {
  private http = inject(HttpClient);
  private auth = inject(AuthService);
  private detector = inject(ChangeDetectorRef);
  cafesPendientes: UsuarioSesion[] = [];
  cafeSeleccionada: UsuarioSesion | null = null;
  mostrandoRechazo = false;
  mensajeRechazo = '';
  error = '';
  cargando = true;
  enviando = false;

  async ngOnInit() {
    try {
      this.cafesPendientes = await firstValueFrom(this.http.get<UsuarioSesion[]>('/api/admin/solicitudes'));
    } catch (error) {
      this.error = this.auth.mensajeError(error);
    } finally {
      this.cargando = false;
      this.detector.markForCheck();
    }
  }
  verDetalles(cafe: UsuarioSesion) {
    this.cafeSeleccionada = cafe;
    this.mostrandoRechazo = false;
    this.mensajeRechazo = '';
  }
  cerrarDetalles() { this.cafeSeleccionada = null; }
  aprobarCafeteria(id: string) { return this.revisar(id, 'aprobada'); }
  confirmarRechazo(id: string) {
    if (!this.mensajeRechazo.trim()) { this.error = 'Escribe el motivo del rechazo.'; return; }
    return this.revisar(id, 'rechazada');
  }
  private async revisar(id: string, estado: string) {
    if (this.enviando) return;
    this.enviando = true;
    this.error = '';
    try {
      await firstValueFrom(this.http.put('/api/admin/solicitudes/' + id,
        { estado, motivo: this.mensajeRechazo }));
      this.cafesPendientes = this.cafesPendientes.filter(cafe => cafe.id !== id);
      this.cerrarDetalles();
    } catch (error) {
      this.error = this.auth.mensajeError(error);
    } finally {
      this.enviando = false;
      this.detector.markForCheck();
    }
  }
}
