import { Component, inject, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { AuthService } from '../../services/auth.service';

@Component({
  selector: 'app-registro',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  templateUrl: './registro.html',
  styleUrl: './registro.css'
})
export class Registro {
  private auth = inject(AuthService);
  private detector = inject(ChangeDetectorRef);
  private router = inject(Router);
  rol: 'usuario' | 'cafeteria' = 'usuario';
  nombre = '';
  correo = '';
  password = '';
  rfc = '';
  error = '';
  enviando = false;

  async registrar() {
    if (this.enviando) return;
    this.error = '';
    if (this.password.length < 8 || !/[a-záéíóúñ]/i.test(this.password) || !/\d/.test(this.password)) {
      this.error = 'Utiliza al menos 8 caracteres, incluyendo letras y números.';
      return;
    }
    this.enviando = true;
    try {
      const respuesta = await this.auth.registrar({ nombre: this.nombre, correo: this.correo,
        password: this.password, rol: this.rol, rfc: this.rfc });
      this.password = '';
      alert(respuesta.mensaje);
      this.router.navigate(['/login']);
    } catch (error) {
      this.error = this.auth.mensajeError(error);
    } finally {
      this.enviando = false;
      this.detector.markForCheck();
    }
  }
}
