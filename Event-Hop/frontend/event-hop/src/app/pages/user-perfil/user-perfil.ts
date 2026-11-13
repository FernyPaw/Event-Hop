import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-user-perfil',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './user-perfil.html',
  styleUrl: './user-perfil.css'
})
export class UserPerfil implements OnInit {
  // Simulamos el rol actual (esto puede venir de un servicio de autenticación)
  esAdmin: boolean = false;

  perfil = {
    nombre: 'María González',
    iniciales: 'MG',
    subtitulo: 'Miembro de Event-Hop',
    estadisticaTitulo: 'Eventos Asistidos',
    estadisticaValor: 5
  };

  ngOnInit() {
    // Aquí puedes evaluar si el usuario logueado es admin o usuario normal
    // Por ejemplo, leyendo de localStorage o de un servicio compartido:
    const rol = localStorage.getItem('rolActual') || 'usuario';
    
    if (rol === 'admin') {
      this.esAdmin = true;
      this.perfil = {
        nombre: 'Administrador General',
        iniciales: 'AD',
        subtitulo: 'Control del Sistema Event-Hop',
        estadisticaTitulo: 'Cafeterías Gestionadas',
        estadisticaValor: 12
      };
    }
  }
}