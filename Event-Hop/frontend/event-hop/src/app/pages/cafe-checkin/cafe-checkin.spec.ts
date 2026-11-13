import { ComponentFixture, TestBed } from '@angular/core/testing';
import { CafeCheckin } from './cafe-checkin';

describe('CafeCheckin', () => {
  let component: CafeCheckin;
  let fixture: ComponentFixture<CafeCheckin>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [CafeCheckin],
    }).compileComponents();

    fixture = TestBed.createComponent(CafeCheckin);
    component = fixture.componentInstance;
    await fixture.whenStable();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
