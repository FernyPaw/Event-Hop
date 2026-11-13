import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-user-mis-boletos',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './user-mis-boletos.html',
  styleUrl: './user-mis-boletos.css'
})
export class UserMisBoletos {
  misBoletos = [
    { 
      id: 1, 
      titulo: 'Cata de Café y Libros', 
      fecha: '2026-11-18', 
      hora: '17:00', 
      cafeteria: 'Café El Conejito',
      codigo: 'EH-101',
      estado: 'Apartado / Pagado',
      imagenUrl: 'https://images.unsplash.com/photo-1501339847302-ac426a4a7cbb?auto=format&fit=crop&q=80&w=400' 
    }
  ];

  boletoSeleccionado: any = null;

  verDetalleBoleto(boleto: any) {
    this.boletoSeleccionado = boleto;
  }

  cerrarDetalle() {
    this.boletoSeleccionado = null;
  }
}