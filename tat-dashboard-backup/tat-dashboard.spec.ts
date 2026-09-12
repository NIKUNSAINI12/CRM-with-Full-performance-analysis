import { ComponentFixture, TestBed } from '@angular/core/testing';

import { TatDashboard } from './tat-dashboard';

describe('TatDashboard', () => {
  let component: TatDashboard;
  let fixture: ComponentFixture<TatDashboard>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      declarations: [TatDashboard]
    })
    .compileComponents();

    fixture = TestBed.createComponent(TatDashboard);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
