import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';

@Component({
  selector: 'app-registro',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  templateUrl: './registro.html',
  styleUrl: './registro.css'
})
export class Registro {
  rol: 'usuario' | 'cafeteria' = 'usuario';
  nombre: string = '';
  correo: string = '';
  password: string = '';
  rfc: string = '';

  constructor(private router: Router) {}

  registrar() {
    if (!this.nombre || !this.correo || !this.password) {
      alert('Por favor de completa todos los campos obligatorios.');
      return;
    }

    if (this.rol === 'cafeteria') {
      if (!this.rfc || this.rfc.trim().length < 12) {
        alert('Por favor ingresa un RFC válido (12 o 13 caracteres).');
        return;
      }
      alert('Solicitud de cafetería enviada. Tu cuenta estará inactiva hasta que un administrador valide tu RFC.');
    } else {
      alert('¡Cuenta de usuario creada con éxito!');
    }

    this.router.navigate(['/login']);
  }
}