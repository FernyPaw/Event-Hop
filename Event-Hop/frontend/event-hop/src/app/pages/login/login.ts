import { Component, inject, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { AuthService } from '../../services/auth.service';

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  templateUrl: './login.html',
  styleUrl: './login.css'
})
export class Login {
  private auth = inject(AuthService);
  private detector = inject(ChangeDetectorRef);

  correo = '';
  password = '';
  enviando = false;
  error = '';

  async iniciarSesion() {
    if (this.enviando) return;

    this.enviando = true;
    this.error = '';

    try {
      await this.auth.iniciarSesion(this.correo, this.password);
      this.password = '';
      this.auth.irAlInicio();
    } catch (error) {
      this.error = this.auth.mensajeError(error);
    } finally {
      this.enviando = false;
      this.detector.markForCheck();
    }
  }
}