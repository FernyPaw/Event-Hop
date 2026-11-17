import { Component } from '@angular/core';
import { PerfilCuenta } from '../../components/perfil-cuenta/perfil-cuenta';

@Component({
  selector: 'app-user-perfil', standalone: true,
  imports: [PerfilCuenta], template: '<app-perfil-cuenta />'
})
export class UserPerfil {}
