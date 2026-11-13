import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

@Component({
  selector: 'app-cafe-perfil',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './cafe-perfil.html',
  styleUrl: './cafe-perfil.css'
})
export class CafePerfil {
  cafeteria = {
    nombre: 'Café El Conejito',
    descripcion: 'Espacio cultural, café de especialidad y lectura.',
    telefono: '5512345678'
  };

  guardarPerfil() {
    alert('Cambios guardados correctamente en tu perfil.');
  }
}