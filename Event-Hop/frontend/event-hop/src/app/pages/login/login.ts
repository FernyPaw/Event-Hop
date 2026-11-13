import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  templateUrl: './login.html',
  styleUrl: './login.css'
})
export class Login {
  correo: string = '';
  password: string = '';

  // prueba
  usuariosPrueba = [
    { correo: 'admin@gmail.com', pass: '1234', ruta: '/admin', rol: 'Administrador' },
    { correo: 'cafe@gmail.com', pass: '1234', ruta: '/cafeteria', rol: 'Cafeteria' },
    { correo: 'usuario@gmail.com', pass: '1234', ruta: '/', rol: 'Usuario' }
  ];

  constructor(private router: Router) {}

  iniciarSesion() {
    //  correo y contraseña coinciden 
    const encontrado = this.usuariosPrueba.find(u => 
      u.correo.toLowerCase() === this.correo.toLowerCase() && 
      u.pass === this.password
    );

    if (encontrado) {
      alert(`¡Bienvenido! Iniciando sesión como ${encontrado.rol}`);
      this.router.navigate([encontrado.ruta]);
    } else {
      alert('Correo o contraseña incorrectos.');
    }
  }

  // Relleno rápido para probar sin escribir
  llenarPrueba(correoPrueba: string) {
    const datos = this.usuariosPrueba.find(u => u.correo === correoPrueba);
    if (datos) {
      this.correo = datos.correo;
      this.password = datos.pass;
    }
  }
}