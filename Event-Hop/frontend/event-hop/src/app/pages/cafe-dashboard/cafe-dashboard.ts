import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

@Component({
  selector: 'app-cafe-dashboard',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './cafe-dashboard.html',
  styleUrl: './cafe-dashboard.css'
})
export class CafeDashboard {
  // Solo tres pestañas para la gestión interna de la cafetería
  pestanaActiva: 'panel' | 'gestion' | 'crear' = 'panel';

  cafeteria = {
    nombre: 'Café El Conejito',
    verificado: true
  };

  estadisticas = {
    totalEventos: 2,
    guardadosTotal: 45,
    inscritosTotal: 30,
    asistenciasTotales: 22,
    porcentajeAsistencia: 73
  };

  misEventos = [
    { 
      id: 1, 
      titulo: 'Noche de poesía y café', 
      fecha: '2026-11-20', 
      hora: '18:00', 
      esGratis: false,
      costo: 50, 
      cupo: 20,
      guardados: 25,
      inscritos: 20,
      asistidos: 15,
      imagenUrl: 'https://images.unsplash.com/photo-1511920170033-f8396924c348?auto=format&fit=crop&q=80&w=400' 
    },
    { 
      id: 2, 
      titulo: 'Taller de filtrado artesanal', 
      fecha: '2026-11-25', 
      hora: '16:00', 
      esGratis: true,
      costo: 0, 
      cupo: 10,
      guardados: 20,
      inscritos: 10,
      asistidos: 7,
      imagenUrl: 'https://images.unsplash.com/photo-1495474472287-4d71bcdd2085?auto=format&fit=crop&q=80&w=400' 
    }
  ];

  nuevoEvento = {
    titulo: '',
    descripcion: '',
    fecha: '',
    hora: '',
    esGratis: false,
    costo: 0,
    cupo: 15,
    imagenUrl: ''
  };

  onFileSelected(event: any) {
    const file = event.target.files[0];
    if (file) {
      const reader = new FileReader();
      reader.onload = () => {
        this.nuevoEvento.imagenUrl = reader.result as string;
      };
      reader.readAsDataURL(file);
    }
  }

  crearEvento() {
    if (!this.cafeteria.verificado) {
      alert('Tu cuenta aún no ha sido aprobada por la administración.');
      return;
    }

    if (!this.nuevoEvento.titulo || !this.nuevoEvento.fecha || !this.nuevoEvento.hora) {
      alert('Por favor completa los campos obligatorios.');
      return;
    }

    this.misEventos.push({
      id: this.misEventos.length + 1,
      titulo: this.nuevoEvento.titulo,
      fecha: this.nuevoEvento.fecha,
      hora: this.nuevoEvento.hora,
      esGratis: this.nuevoEvento.esGratis,
      costo: this.nuevoEvento.esGratis ? 0 : Number(this.nuevoEvento.costo),
      cupo: Number(this.nuevoEvento.cupo),
      guardados: 0,
      inscritos: 0,
      asistidos: 0,
      imagenUrl: this.nuevoEvento.imagenUrl || 'https://images.unsplash.com/photo-1501339847302-ac426a4a7cbb?auto=format&fit=crop&q=80&w=400'
    });

    this.estadisticas.totalEventos = this.misEventos.length;
    alert('Evento creado con éxito');
    
    this.nuevoEvento = { titulo: '', descripcion: '', fecha: '', hora: '', esGratis: false, costo: 0, cupo: 15, imagenUrl: '' };
    this.pestanaActiva = 'gestion';
  }

  eliminarEvento(id: number) {
    if (confirm('¿Estás seguro de eliminar este evento?')) {
      this.misEventos = this.misEventos.filter(e => e.id !== id);
      this.estadisticas.totalEventos = this.misEventos.length;
    }
  }
}