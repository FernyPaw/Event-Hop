import { Injectable, inject, signal } from '@angular/core';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Router } from '@angular/router';
import { firstValueFrom } from 'rxjs';

export interface UsuarioSesion {
  id: string;
  nombre: string;
  correo: string;
  rol: 'usuario' | 'cafeteria' | 'admin';
  rfc: string;
  estadoSolicitud: string;
}

interface Sesion {
  token: string;
  vence: string;
  usuario: UsuarioSesion;
}

@Injectable({ providedIn: 'root' })
export class AuthService {
  private http = inject(HttpClient);
  private router = inject(Router);
  private sesion = signal<Sesion | null>(this.leerSesion());

  get usuario(): UsuarioSesion | null { return this.sesion()?.usuario ?? null; }

  get token(): string | null {
    const datos = this.sesion();
    return datos && Date.parse(datos.vence) > Date.now() ? datos.token : null;
  }

  private leerSesion(): Sesion | null {
    try {
      const datos = JSON.parse(sessionStorage.getItem('eventhopSesion') ?? 'null');
      if (datos?.token && datos?.usuario?.id && Date.parse(datos.vence) > Date.now()) return datos;
    } catch {
      // Una sesión dañada se descarta.
    }
    sessionStorage.removeItem('eventhopSesion');
    return null;
  }

  registrar(datos: { nombre: string; correo: string; password: string; rol: string; rfc: string }) {
    return firstValueFrom(this.http.post<{ mensaje: string }>('/api/auth/registro', datos));
  }

  async iniciarSesion(correo: string, password: string) {
    const datos = await firstValueFrom(this.http.post<Sesion>('/api/auth/login', { correo, password }));
    sessionStorage.setItem('eventhopSesion', JSON.stringify(datos));
    this.sesion.set(datos);
    return datos.usuario;
  }

  async verificarSesion(): Promise<boolean> {
    if (!this.token) { this.cerrarSesion(); return false; }
    try {
      const usuario = await firstValueFrom(this.http.get<UsuarioSesion>('/api/auth/yo'));
      const actualizada = { ...this.sesion()!, usuario };
      this.sesion.set(actualizada);
      sessionStorage.setItem('eventhopSesion', JSON.stringify(actualizada));
      return true;
    } catch {
      this.cerrarSesion();
      return false;
    }
  }

  cerrarSesion() {
    sessionStorage.removeItem('eventhopSesion');
    this.sesion.set(null);
  }

  actualizarUsuario(usuario: UsuarioSesion) {
    const actual = this.sesion();
    if (!actual || actual.usuario.id !== usuario.id) return;
    const nueva = { ...actual, usuario };
    sessionStorage.setItem('eventhopSesion', JSON.stringify(nueva));
    this.sesion.set(nueva);
  }

  irAlInicio() {
    const rol = this.usuario?.rol;
    this.router.navigate([rol === 'admin' ? '/admin' : rol === 'cafeteria' ? '/cafeteria' : '/']);
  }

  mensajeError(error: unknown): string {
    if (error instanceof HttpErrorResponse) {
      if (error.status === 0) return 'No se pudo conectar con el backend. Comprueba que esté ejecutándose.';
      return error.error?.mensaje ?? 'No se pudo completar la solicitud.';
    }
    return 'No se pudo completar la operación.';
  }
}
