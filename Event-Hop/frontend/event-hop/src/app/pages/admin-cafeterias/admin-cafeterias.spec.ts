import { ComponentFixture, TestBed } from '@angular/core/testing';
import { AdminCafeterias } from './admin-cafeterias';

describe('AdminCafeterias', () => {
  let component: AdminCafeterias;
  let fixture: ComponentFixture<AdminCafeterias>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [AdminCafeterias],
    }).compileComponents();

    fixture = TestBed.createComponent(AdminCafeterias);
    component = fixture.componentInstance;
    await fixture.whenStable();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
