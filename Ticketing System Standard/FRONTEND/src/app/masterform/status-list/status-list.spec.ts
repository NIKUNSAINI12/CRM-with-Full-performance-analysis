import { ComponentFixture, TestBed } from '@angular/core/testing';

import { StatusList } from './status-list';

describe('StatusList', () => {
  let component: StatusList;
  let fixture: ComponentFixture<StatusList>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [StatusList]
    })
    .compileComponents();

    fixture = TestBed.createComponent(StatusList);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
