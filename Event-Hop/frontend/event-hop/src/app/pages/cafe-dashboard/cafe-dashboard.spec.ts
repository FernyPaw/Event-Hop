import { ComponentFixture, TestBed } from '@angular/core/testing';
import { CafeDashboard } from './cafe-dashboard';

describe('CafeDashboard', () => {
  let component: CafeDashboard;
  let fixture: ComponentFixture<CafeDashboard>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [CafeDashboard],
    }).compileComponents();

    fixture = TestBed.createComponent(CafeDashboard);
    component = fixture.componentInstance;
    await fixture.whenStable();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
