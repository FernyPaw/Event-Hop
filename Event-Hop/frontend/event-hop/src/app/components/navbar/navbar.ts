import { Component, inject, output, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterLink, RouterLinkActive } from '@angular/router';
import { AuthService } from '../../services/auth.service';

@Component({
  selector: 'app-navbar',
  standalone: true,
  imports: [CommonModule, RouterLink, RouterLinkActive],
  templateUrl: './navbar.html',
  styleUrl: './navbar.css'
})
export class Navbar implements OnInit {
  private auth = inject(AuthService);
  private router = inject(Router);
  menuAbierto = true;
  menuCambio = output<boolean>();
  ngOnInit() {
    this.menuAbierto = window.innerWidth > 760;
    this.menuCambio.emit(this.menuAbierto);
  }
  get estaLogueado() { return !!this.auth.token; }
  get rolActual() { return this.auth.usuario?.rol ?? 'usuario'; }
  get perfil() {
    const nombre = this.auth.usuario?.nombre ?? '';
    const iniciales = nombre.trim().split(/\s+/).slice(0, 2).map(parte => parte[0]).join('').toUpperCase();
    return { nombre, iniciales, subtitulo: this.rolActual };
  }
  alternarMenu() { this.menuAbierto = !this.menuAbierto; this.menuCambio.emit(this.menuAbierto); }
  cerrarMenuMovil() { if (window.innerWidth <= 760 && this.menuAbierto) this.alternarMenu(); }
  cerrarSesion() {
    this.auth.cerrarSesion();
    this.router.navigate(['/login']);
  }
  iniciarSesionDemo() { this.router.navigate(['/login']); }
}
