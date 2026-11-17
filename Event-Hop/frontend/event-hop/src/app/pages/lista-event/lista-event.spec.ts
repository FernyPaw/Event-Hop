import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ListaEvent } from './lista-event';

describe('ListaEvent', () => {
  let component: ListaEvent;
  let fixture: ComponentFixture<ListaEvent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ListaEvent],
    }).compileComponents();

    fixture = TestBed.createComponent(ListaEvent);
    component = fixture.componentInstance;
    await fixture.whenStable();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
