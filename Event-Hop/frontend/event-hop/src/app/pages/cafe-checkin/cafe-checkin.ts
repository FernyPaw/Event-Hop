import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

@Component({
  selector: 'app-cafe-checkin',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './cafe-checkin.html',
  styleUrl: './cafe-checkin.css'
})
export class CafeCheckin {
  
  misEventos = [
    { id: 1, titulo: 'Noche de poesía y café', fecha: '2026-11-20', hora: '18:00' },
    { id: 2, titulo: 'Taller de filtrado artesanal', fecha: '2026-11-25', hora: '16:00' }
  ];

  // Evento seleccionado 
  eventoSeleccionado: any = null;

  codigoIngreso: string = '';
  resultadoCheckin: { exito: boolean; mensaje: string } | null = null;

  // Datos random
  asistentesRegistrados = [
    { codigo: 'EH-101', nombre: 'María González', eventoId: 1 },
    { codigo: 'EH-102', nombre: 'Carlos Ramírez', eventoId: 1 },
    { codigo: 'EH-201', nombre: 'Ana López', eventoId: 2 }
  ];

 
  seleccionarEvento(evento: any) {
    this.eventoSeleccionado = evento;
    this.codigoIngreso = '';
    this.resultadoCheckin = null;
  }
  regresarASeccionEventos() {
    this.eventoSeleccionado = null;
    this.codigoIngreso = '';
    this.resultadoCheckin = null;
  }

  // Verificar 
  verificarCodigo() {
    if (!this.codigoIngreso.trim()) {
      this.resultadoCheckin = { exito: false, mensaje: 'Por favor ingresa un código válido.' };
      return;
    }

    // Buscar 
    const encontrado = this.asistentesRegistrados.find(
      a => a.codigo.toUpperCase() === this.codigoIngreso.trim().toUpperCase() && a.eventoId === this.eventoSeleccionado.id
    );

    if (encontrado) {
      this.resultadoCheckin = {
        exito: true,
        mensaje: `Bienvenido @${encontrado.nombre}. Acceso concedido.`
      };
    } else {
      this.resultadoCheckin = {
        exito: false,
        mensaje: 'Código no encontrado o no pertenece a este evento.'
      };
    }

    this.codigoIngreso = '';
  }
}