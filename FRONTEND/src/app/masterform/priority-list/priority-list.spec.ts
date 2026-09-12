import { ComponentFixture, TestBed } from '@angular/core/testing';

import { PriorityList } from './priority-list';

describe('PriorityList', () => {
  let component: PriorityList;
  let fixture: ComponentFixture<PriorityList>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [PriorityList]
    })
    .compileComponents();

    fixture = TestBed.createComponent(PriorityList);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
