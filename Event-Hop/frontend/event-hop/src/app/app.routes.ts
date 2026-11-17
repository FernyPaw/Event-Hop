import { Routes } from '@angular/router';
import { Home } from './pages/home/home';
import { Login } from './pages/login/login';
import { Registro } from './pages/registro/registro';
import { AdminDashboardComponent } from './pages/admin-dashboard/admin-dashboard';
import { AdminCafeterias } from './pages/admin-cafeterias/admin-cafeterias';
import { UserPerfil } from './pages/user-perfil/user-perfil';
import { CafeDashboard } from './pages/cafe-dashboard/cafe-dashboard';
import { CafeCheckin } from './pages/cafe-checkin/cafe-checkin';
import { CafePerfil } from './pages/cafe-perfil/cafe-perfil';
import { UserMisEventos } from './pages/user-mis-eventos/user-mis-eventos';
import { UserMisBoletos } from './pages/user-mis-boletos/user-mis-boletos';
import { ListaEvent } from './pages/lista-event/lista-event';
import { authGuard } from './guards/auth.guard';
import { UserPagos } from './pages/user-pagos/user-pagos';
import { AdminCategorias } from './pages/admin-categorias/admin-categorias';
import { Comunidad } from './pages/comunidad/comunidad';
import { Reportes } from './pages/reportes/reportes';

export const routes: Routes = [
  { path: 'admin/reportes', component: Reportes, canActivate: [authGuard], data: { roles: ['admin'] } },
  { path: 'cafeteria/reportes', component: Reportes, canActivate: [authGuard], data: { roles: ['cafeteria'] } },
  { path: 'admin/categorias', component: AdminCategorias, canActivate: [authGuard], data: { roles: ['admin'] } },
  { path: 'cafeteria/comunidad', component: Comunidad, canActivate: [authGuard], data: { roles: ['cafeteria'] } },
  { path: 'usuario/comunidad', component: Comunidad, canActivate: [authGuard], data: { roles: ['usuario'] } },
  { path: '', component: Home, canActivate: [authGuard], data: { roles: ['usuario'] } },
  { path: 'login', component: Login },
  { path: 'registro', component: Registro },
  { path: 'eventos', component: ListaEvent, canActivate: [authGuard], data: { roles: ['usuario'] } },
  { path: 'admin', component: AdminDashboardComponent, canActivate: [authGuard], data: { roles: ['admin'] } },
  { path: 'admin/cafeterias', component: AdminCafeterias, canActivate: [authGuard], data: { roles: ['admin'] } },
  { path: 'admin/perfil', component: UserPerfil, canActivate: [authGuard], data: { roles: ['admin'] } },
  { path: 'cafeteria', component: CafeDashboard, canActivate: [authGuard], data: { roles: ['cafeteria'] } },
  { path: 'cafeteria/checkin', component: CafeCheckin, canActivate: [authGuard], data: { roles: ['cafeteria'] } },
  { path: 'cafeteria/perfil', component: CafePerfil, canActivate: [authGuard], data: { roles: ['cafeteria'] } },
  { path: 'usuario/mis-eventos', component: UserMisEventos, canActivate: [authGuard], data: { roles: ['usuario'] } },
  { path: 'usuario/mis-boletos', component: UserMisBoletos, canActivate: [authGuard], data: { roles: ['usuario'] } },
  { path: 'usuario/pagos', component: UserPagos, canActivate: [authGuard], data: { roles: ['usuario'] } },
  { path: 'usuario/perfil', component: UserPerfil, canActivate: [authGuard], data: { roles: ['usuario'] } },
  { path: '**', redirectTo: '' }
];
