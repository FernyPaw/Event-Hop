import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

@Component({
  selector: 'app-home',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './home.html',
  styleUrl: './home.css'
})
export class Home {

  vistaActual: 'explorar' | 'gestion' | 'detalle' = 'explorar';

  // Lista eventos
  eventos = [
    {
      id: 1,
      categoria: 'Taller',
      titulo: 'Cata de Cafe',
      cafeteria: 'Cafeteria El conejito',
      direccion: 'Av. Principal #123, Centro',
      dia: 'Sábado',
      fechaTexto: 'Sábado 15 de Noviembre - 5:00 PM',
      esGratis: false,
      precio: '$150 MXN',
      descripcion: 'Aprende a identificar notas y aromas de granos locales guiado por baristas expertos.',
      guardado: false
    },
    {
      id: 2,
      categoria: 'Musica',
      titulo: 'Noche de sonidos',
      cafeteria: 'Cafe del Parque',
      direccion: 'Calle Juárez #45, frente al parque',
      dia: 'Domingo',
      fechaTexto: 'Domingo 16 de Noviembre - 7:30 PM',
      esGratis: true,
      precio: 'Gratuito',
      descripcion: 'Disfruta de música acustica en vivo con artistas locales en un ambiente relajado.',
      guardado: false
    },
    {
      id: 3,
      categoria: 'Lectura',
      titulo: 'Tarde de lectura y cafe',
      cafeteria: 'La Madriguera Cafe',
      direccion: 'Blvd. Las Flores #890',
      dia: 'Martes',
      fechaTexto: 'Martes 18 de Noviembre - 6:00 PM',
      esGratis: true,
      precio: 'Gratuito',
      descripcion: 'Trae tu libro favorito o elige uno de nuestra biblioteca comunitaria para compartir opiniones acompañados de un buen cafe.',
      guardado: false
    },
    {
      id: 4,
      categoria: 'Aficion',
      titulo: 'tarde de Kpop',
      cafeteria: 'La Madriguera Cafe',
      direccion: 'Blvd. Las Flores #890',
      dia: 'Martes',
      fechaTexto: 'Martes 25 de Noviembre - 4:00 PM',
      esGratis: true,
      precio: 'Gratuito',
      descripcion: 'Disfruta con mas fans del kpop de musica y actividades acompañados de un buen cafe.',
      guardado: false
    }
  ];

  //estadisticas
  estadisticasCafeteria = {
    eventosActivos: 2,
    enCurso: 1,
    terminados: 5,
    interesadosGuardados: 34,
    asistenciasConfirmadas: 28,
    porcentajeAsistencia: 82
  };

  //eventos propios
  misEventosCafeteria = [
    {
      id: 1,
      titulo: 'Cata de Café',
      estado: 'Activo',
      fecha: 'Sábado 15 de Noviembre',
      interesados: 15,
      asistidos: 12
    },
    {
      id: 2,
      titulo: 'Tarde de lectura y café',
      estado: 'En curso',
      fecha: 'Martes 18 de Noviembre',
      interesados: 19,
      asistidos: 16
    }
  ];

  eventoSeleccionado: any = null;

  // Variables busqueda
  filtroCafeteria: string = '';
  filtroDia: string = '';
  filtroTipo: string = '';

  // cafeterias
  get listaCafeterias() {
    const cafeteriasUnicas = this.eventos.map(evento => evento.cafeteria);
    return [...new Set(cafeteriasUnicas)]; 
  }

  // Filtro
  get eventosFiltrados() {
    return this.eventos.filter(evento => {
      const coincideCafeteria = this.filtroCafeteria === '' || evento.cafeteria === this.filtroCafeteria;
      const coincideDia = this.filtroDia === '' || evento.dia === this.filtroDia;
      
      let coincideTipo = true;
      if (this.filtroTipo === 'gratis') {
        coincideTipo = evento.esGratis === true;
      } else if (this.filtroTipo === 'paga') {
        coincideTipo = evento.esGratis === false;
      }

      return coincideCafeteria && coincideDia && coincideTipo;
    });
  }

  //cambiar de vista
  verDetalle(evento: any) {
    this.eventoSeleccionado = evento;
    this.vistaActual = 'detalle';
  }

  cerrarDetalle() {
    this.eventoSeleccionado = null;
    this.vistaActual = 'explorar';
  }

  irAGestion() {
    this.vistaActual = 'gestion';
  }

  irAExplorar() {
    this.vistaActual = 'explorar';
    this.eventoSeleccionado = null;
  }

  alternarGuardar(evento: any) {
    evento.guardado = !evento.guardado;
  }
}