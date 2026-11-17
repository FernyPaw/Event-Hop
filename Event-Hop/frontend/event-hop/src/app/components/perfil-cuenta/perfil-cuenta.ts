import { ChangeDetectorRef, Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { AuthService, UsuarioSesion } from '../../services/auth.service';

@Component({
  selector: 'app-perfil-cuenta', standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './perfil-cuenta.html', styleUrl: './perfil-cuenta.css'
})
export class PerfilCuenta implements OnInit {
  private http = inject(HttpClient);
  private auth = inject(AuthService);
  private detector = inject(ChangeDetectorRef);
  usuario: UsuarioSesion | null = null;
  nombre = '';
  ocupado = false;
  error = '';
  mensaje = '';

  get iniciales() {
    return (this.usuario?.nombre ?? '').trim().split(/\s+/).slice(0, 2)
      .map(parte => parte.charAt(0)).join('').toUpperCase();
  }
  ngOnInit() { void this.cargar(); }

  async cargar() {
    this.ocupado = true;
    this.error = '';
    try {
      this.usuario = await firstValueFrom(this.http.get<UsuarioSesion>('/api/perfil'));
      this.nombre = this.usuario.nombre;
      this.auth.actualizarUsuario(this.usuario);
    } catch (error) { this.error = this.auth.mensajeError(error); }
    finally { this.ocupado = false; this.detector.markForCheck(); }
  }

  async guardar() {
    if (this.ocupado || !this.usuario) return;
    this.error = '';
    this.mensaje = '';
    const nombre = this.nombre.trim();
    if (nombre.length < 2 || nombre.length > 100 || /[\x00-\x1f\x7f-\x9f]/.test(nombre)) {
      this.error = 'Escribe un nombre válido de 2 a 100 caracteres.';
      return;
    }
    this.ocupado = true;
    try {
      this.usuario = await firstValueFrom(this.http.put<UsuarioSesion>('/api/perfil', { nombre }));
      this.nombre = this.usuario.nombre;
      this.auth.actualizarUsuario(this.usuario);
      this.mensaje = 'Perfil guardado correctamente.';
    } catch (error) { this.error = this.auth.mensajeError(error); }
    finally { this.ocupado = false; this.detector.markForCheck(); }
  }
}
