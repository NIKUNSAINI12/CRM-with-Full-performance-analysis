import { ComponentFixture, TestBed } from '@angular/core/testing';

import { PmDashboard } from './pm-dashboard';

describe('PmDashboard', () => {
  let component: PmDashboard;
  let fixture: ComponentFixture<PmDashboard>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      declarations: [PmDashboard]
    })
    .compileComponents();

    fixture = TestBed.createComponent(PmDashboard);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
