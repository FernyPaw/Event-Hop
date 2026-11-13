import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterLink, RouterLinkActive } from '@angular/router'; // <--- 1. Importar RouterLinkActive

@Component({
  selector: 'app-navbar',
  standalone: true,
  imports: [CommonModule, RouterLink, RouterLinkActive], // <--- 2. Añadirlo aquí
  templateUrl: './navbar.html',
  styleUrl: './navbar.css'
})
export class Navbar {
  // menu 
  menuAbierto: boolean = true;

  // sesion
  estaLogueado: boolean = true;

  // Rol 
  rolActual: 'usuario' | 'cafeteria' | 'admin' = 'usuario';

  // Datos
  perfil = {
    nombre: 'María González',
    iniciales: 'MG',
    subtitulo: 'Exploradora de Café'
  };

  constructor(private router: Router) {} 

  alternarMenu() {
    this.menuAbierto = !this.menuAbierto;
  }

  // menu rol
  cambiarRol(nuevoRol: 'usuario' | 'cafeteria' | 'admin') {
    this.rolActual = nuevoRol;
    this.estaLogueado = true;

    if (nuevoRol === 'usuario') {
      this.perfil = { nombre: 'Maria Gomez', iniciales: 'MG', subtitulo: 'Usuario' };
    } else if (nuevoRol === 'cafeteria') {
      this.perfil = { nombre: 'Cafetería El Conejito', iniciales: 'EC', subtitulo: 'Cafetería Verificada' };
    } else if (nuevoRol === 'admin') {
      this.perfil = { nombre: 'Admin ', iniciales: 'AD', subtitulo: 'Administrador' };
    }
  }

  cerrarSesion() {
    this.estaLogueado = false;
    this.router.navigate(['/login']); 
  }

  iniciarSesionDemo() {
    this.router.navigate(['/login']);
  }
}