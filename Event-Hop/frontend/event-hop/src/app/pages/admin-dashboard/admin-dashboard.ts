import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-admin-dashboard',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './admin-dashboard.html',
  styleUrl: './admin-dashboard.css'
})
export class AdminDashboardComponent {
  // prueba
  totalUsuarios = 24;
  cafeteriasAceptadas = 5;
  totalSolicitudesCafeterias = 7;
  totalEventos = 8;
}