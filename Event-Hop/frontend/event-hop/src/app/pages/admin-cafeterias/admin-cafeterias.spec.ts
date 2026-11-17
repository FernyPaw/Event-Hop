import { ComponentFixture, TestBed } from '@angular/core/testing';
import { AdminCafeterias } from './admin-cafeterias';
import { provideHttpClient, HttpClient } from '@angular/common/http';
import { provideRouter } from '@angular/router';
import { of } from 'rxjs';

describe('AdminCafeterias', () => {
  let component: AdminCafeterias;
  let fixture: ComponentFixture<AdminCafeterias>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [AdminCafeterias],
      providers: [provideHttpClient(), provideRouter([])],
    }).compileComponents();

    vi.spyOn(TestBed.inject(HttpClient), 'get').mockReturnValue(of([]));
    fixture = TestBed.createComponent(AdminCafeterias);
    component = fixture.componentInstance;
    await fixture.whenStable();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
