import { TestBed } from '@angular/core/testing';
import { App } from './app';
import { provideHttpClient } from '@angular/common/http';
import { provideRouter } from '@angular/router';

describe('App', () => {
  const anchoOriginal = window.innerWidth;
  afterEach(() => Object.defineProperty(window, 'innerWidth', { value: anchoOriginal, configurable: true }));
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [App],
      providers: [provideHttpClient(), provideRouter([])],
    })
      .compileComponents();
  });

  it('should create the app', () => {
    const fixture = TestBed.createComponent(App);
    const app = fixture.componentInstance;
    expect(app).toBeTruthy();
  });

  it('en móvil inicia sin reservar espacio para un menú cerrado', async () => {
    Object.defineProperty(window, 'innerWidth', { value: 390, configurable: true });
    const fixture = TestBed.createComponent(App);
    expect(() => fixture.detectChanges()).not.toThrow(); await fixture.whenStable();
    expect(fixture.nativeElement.querySelector('main').classList.contains('con-menu')).toBe(false);
  });

  it('en escritorio el menú puede liberar el espacio del contenido', async () => {
    Object.defineProperty(window, 'innerWidth', { value: 1365, configurable: true });
    const fixture = TestBed.createComponent(App); fixture.detectChanges(); await fixture.whenStable();
    expect(fixture.nativeElement.querySelector('main').classList.contains('con-menu')).toBe(true);
    fixture.nativeElement.querySelector('.boton-hamburguesa').click(); await fixture.whenStable();
    expect(fixture.nativeElement.querySelector('main').classList.contains('con-menu')).toBe(false);
  });

  it('should render navigation outlet', async () => {
    const fixture = TestBed.createComponent(App);
    await fixture.whenStable();
    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('router-outlet')).toBeTruthy();
  });
});
