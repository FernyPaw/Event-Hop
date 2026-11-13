import { ComponentFixture, TestBed } from '@angular/core/testing';
import { UserMisEventos } from './user-mis-eventos';

describe('UserMisEventos', () => {
  let component: UserMisEventos;
  let fixture: ComponentFixture<UserMisEventos>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [UserMisEventos],
    }).compileComponents();

    fixture = TestBed.createComponent(UserMisEventos);
    component = fixture.componentInstance;
    await fixture.whenStable();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
