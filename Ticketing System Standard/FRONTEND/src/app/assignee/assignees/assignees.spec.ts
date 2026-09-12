import { ComponentFixture, TestBed } from '@angular/core/testing';

import { Assignees } from './assignees';

describe('Assignees', () => {
  let component: Assignees;
  let fixture: ComponentFixture<Assignees>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [Assignees]
    })
    .compileComponents();

    fixture = TestBed.createComponent(Assignees);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
