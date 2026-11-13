import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-user-mis-eventos',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './user-mis-eventos.html',
  styleUrl: './user-mis-eventos.css'
})
export class UserMisEventos {
  // datos
  misEventosGuardados = [
    { 
      id: 1, 
      titulo: 'Noche de poesía y café', 
      fecha: '2026-11-20', 
      hora: '18:00', 
      costo: 50, 
      imagenUrl: 'https://images.unsplash.com/photo-1511920170033-f8396924c348?auto=format&fit=crop&q=80&w=400' 
    },
    { 
      id: 2, 
      titulo: 'Taller de filtrado artesanal', 
      fecha: '2026-11-25', 
      hora: '16:00', 
      costo: 0, 
      imagenUrl: 'https://images.unsplash.com/photo-1495474472287-4d71bcdd2085?auto=format&fit=crop&q=80&w=400' 
    }
  ];

  removerDeGuardados(id: number) {
    this.misEventosGuardados = this.misEventosGuardados.filter(e => e.id !== id);
  }

  comprarBoleto(evento: any) {
    alert(`Has adquirido/apartado tu lugar para: ${evento.titulo}. Ahora aparecerá en "Mis boletos".`);
    // comprar o apartar
    this.removerDeGuardados(evento.id);
  }
}