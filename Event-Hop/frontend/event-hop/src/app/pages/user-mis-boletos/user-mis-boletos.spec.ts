import { ComponentFixture, TestBed } from '@angular/core/testing';
import { UserMisBoletos } from './user-mis-boletos';

describe('UserMisBoletos', () => {
  let component: UserMisBoletos;
  let fixture: ComponentFixture<UserMisBoletos>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [UserMisBoletos],
    }).compileComponents();

    fixture = TestBed.createComponent(UserMisBoletos);
    component = fixture.componentInstance;
    await fixture.whenStable();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
