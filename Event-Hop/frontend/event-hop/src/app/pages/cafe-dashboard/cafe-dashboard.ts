import { Component, OnInit, inject, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { AuthService } from '../../services/auth.service';
import { Evento, EventoFormulario, EventosService } from '../../services/eventos.service';
import { CategoriasService } from '../../services/categorias.service';
import { ReportesService, ResumenCafe } from '../../services/reportes.service';

@Component({
  selector: 'app-cafe-dashboard',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './cafe-dashboard.html',
  styleUrl: './cafe-dashboard.css'
})
export class CafeDashboard implements OnInit {
  private auth = inject(AuthService);
  private eventos = inject(EventosService);
  private detector = inject(ChangeDetectorRef);
  private catalogo = inject(CategoriasService);
  private reportes = inject(ReportesService);
  resumen: ResumenCafe | null = null;
  errorResumen = '';
  pestanaActiva: 'panel' | 'gestion' | 'crear' = 'panel';
  misEventos: Evento[] = [];
  editandoId: string | null = null;
  cargando = true;
  enviando = false;
  leyendoImagen = false;
  error = '';
  mensaje = '';
  categorias: string[] = [];
  nuevoEvento = this.formularioVacio();

  get cafeteria() {
    return { nombre: this.auth.usuario?.nombre ?? '',
      verificado: this.auth.usuario?.estadoSolicitud === 'aprobada' };
  }

  private formularioVacio(): EventoFormulario {
    return { titulo: '', descripcion: '', categoria: this.categorias.includes('Otro') ? 'Otro' : (this.categorias[0] ?? ''), direccion: '', fecha: '',
      hora: '', esGratis: true, costo: 0, cupo: 15, imagenUrl: '' };
  }

  async ngOnInit() {
    try {
      this.misEventos = await this.eventos.consultarMios();
      this.categorias = (await this.catalogo.consultar()).map(c => c.nombre);
    } catch (error) {
      this.error = this.auth.mensajeError(error);
    } finally {
      this.cargando = false;
      this.detector.markForCheck();
    }
    await this.actualizarResumen();
  }

  async actualizarResumen() {
    this.resumen = null; this.errorResumen = '';
    try { this.resumen = await this.reportes.resumenCafe(); }
    catch { this.errorResumen = 'No se pudieron cargar los indicadores. Puedes reintentarlo.'; }
    finally { this.detector.markForCheck(); }
  }

  prepararNuevo() {
    this.editandoId = null;
    this.nuevoEvento = this.formularioVacio();
    this.error = '';
    this.mensaje = '';
    this.pestanaActiva = 'crear';
  }

  editarEvento(evento: Evento) {
    this.editandoId = evento.id;
    this.nuevoEvento = { titulo: evento.titulo, descripcion: evento.descripcion,
      categoria: evento.categoria, direccion: evento.direccion, fecha: evento.fecha,
      hora: evento.hora, esGratis: evento.esGratis, costo: evento.costo,
      cupo: evento.cupo, imagenUrl: evento.imagenUrl };
    this.error = '';
    this.mensaje = '';
    this.pestanaActiva = 'crear';
  }

  onFileSelected(event: Event) {
    const entrada = event.target as HTMLInputElement;
    const archivo = entrada.files?.[0];
    if (!archivo) return;
    this.error = '';
    if (!['image/png', 'image/jpeg', 'image/webp'].includes(archivo.type) || archivo.size > 1048576) {
      this.error = 'Selecciona una imagen PNG, JPEG o WebP de máximo 1 MB.';
      entrada.value = '';
      return;
    }
    const lector = new FileReader();
    this.leyendoImagen = true;
    lector.onload = () => {
      this.nuevoEvento.imagenUrl = lector.result as string;
      this.leyendoImagen = false;
      this.detector.markForCheck();
    };
    lector.onerror = () => {
      this.error = 'No se pudo leer la imagen.';
      this.leyendoImagen = false;
      this.detector.markForCheck();
    };
    lector.readAsDataURL(archivo);
  }

  async crearEvento() {
    if (this.enviando || this.leyendoImagen || this.cargando) return;
    this.error = '';
    this.mensaje = '';
    const datos = { ...this.nuevoEvento, costo: this.nuevoEvento.esGratis ? 0 : Number(this.nuevoEvento.costo),
      cupo: Number(this.nuevoEvento.cupo) };
    if (datos.titulo.trim().length < 3 || !datos.descripcion.trim() || !datos.direccion.trim() ||
        !datos.fecha || !datos.hora || !Number.isInteger(datos.cupo) || datos.cupo < 1 || datos.cupo > 10000 ||
        !Number.isFinite(datos.costo) || datos.costo > 100000 || (!datos.esGratis && datos.costo <= 0)) {
      this.error = 'Completa los campos; utiliza un cupo entero de 1 a 10000 y un costo positivo para eventos de paga.';
      return;
    }
    this.enviando = true;
    try {
      const editando = this.editandoId;
      if (editando) await this.eventos.editar(editando, datos);
      else await this.eventos.crear(datos);
      // La escritura ya terminó. No se debe repetir si falla la consulta posterior.
      this.editandoId = null;
      this.nuevoEvento = this.formularioVacio();
      this.pestanaActiva = 'gestion';
      this.mensaje = editando ? 'Evento actualizado correctamente.' : 'Evento creado correctamente.';
      await this.actualizarResumen();
      try {
        this.misEventos = await this.eventos.consultarMios();
      } catch {
        this.error = 'El evento se guardó, pero no se pudo actualizar la lista. Recarga la página.';
      }
    } catch (error) {
      this.error = this.auth.mensajeError(error);
    } finally {
      this.enviando = false;
      this.detector.markForCheck();
    }
  }

  async eliminarEvento(id: string) {
    if (this.enviando || !confirm('¿Eliminar este evento definitivamente? Esta acción no se puede deshacer.')) return;
    this.enviando = true;
    this.error = '';
    this.mensaje = '';
    try {
      await this.eventos.eliminar(id);
      this.misEventos = this.misEventos.filter(evento => evento.id !== id);
      this.mensaje = 'Evento eliminado.';
      await this.actualizarResumen();
    } catch (error) {
      this.error = this.auth.mensajeError(error);
    } finally {
      this.enviando = false;
      this.detector.markForCheck();
    }
  }
}
