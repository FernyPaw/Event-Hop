import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

@Component({
  imports: [CommonModule, FormsModule],
  selector: 'app-admin-cafeterias',
  styleUrl: './admin-cafeterias.css',
  templateUrl: './admin-cafeterias.html',
})
export class AdminCafeterias {

  cafesPendientes = [
    {
      id: 1,
      nombre: 'Café El Rincón',
      email: 'contacto@elrincon.com',
      rfc: 'CER901012ABC',
      direccion: 'Av. Universidad #123, San Nicolás',
      descripcion: 'Cafetería cultural con espacio para música en vivo y lectura.'
    },
    {
      id: 2,
      nombre: 'Tostadores del Norte',
      email: 'hola@tostadores.com',
      rfc: 'TDN850520XYZ',
      direccion: 'Calle Morelos #450, Centro',
      descripcion: 'Venta de café de especialidad y talleres de barismo.'
    }
  ];


  cafeSeleccionada: any = null;
  mostrandoRechazo = false;
  mensajeRechazo = '';


  verDetalles(cafe: any) {
    this.cafeSeleccionada = cafe;
    this.mostrandoRechazo = false;
    this.mensajeRechazo = '';
  }

  cerrarDetalles() {
    this.cafeSeleccionada = null;
  }

  aprobarCafeteria(id: number) {
    alert(`Cafetería aprobada: ${this.cafeSeleccionada.nombre}`);
    this.cafesPendientes = this.cafesPendientes.filter(c => c.id !== id);
    this.cerrarDetalles();
  }

  confirmarRechazo(id: number) {
    if (this.mensajeRechazo.trim() === '') {
      alert('Por favor escribe un motivo para rechazar la solicitud.');
      return;
    }

    alert(`Solicitud rechazada.\nMensaje guardado para la cafetería: "${this.mensajeRechazo}"`);
    this.cafesPendientes = this.cafesPendientes.filter(c => c.id !== id);
    this.cerrarDetalles();
  }
}