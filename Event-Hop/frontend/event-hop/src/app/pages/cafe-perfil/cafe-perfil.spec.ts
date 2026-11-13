import { ComponentFixture, TestBed } from '@angular/core/testing';
import { CafePerfil } from './cafe-perfil';

describe('CafePerfil', () => {
  let component: CafePerfil;
  let fixture: ComponentFixture<CafePerfil>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [CafePerfil],
    }).compileComponents();

    fixture = TestBed.createComponent(CafePerfil);
    component = fixture.componentInstance;
    await fixture.whenStable();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
