import { Component } from '@angular/core';
import { PerfilCuenta } from '../../components/perfil-cuenta/perfil-cuenta';

@Component({
  selector: 'app-cafe-perfil', standalone: true,
  imports: [PerfilCuenta], template: '<app-perfil-cuenta />'
})
export class CafePerfil {}
