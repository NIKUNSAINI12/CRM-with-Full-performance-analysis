import { ComponentFixture, TestBed } from '@angular/core/testing';

import { TicketDetail } from './ticket-detail';

describe('TicketDetail', () => {
  let component: TicketDetail;
  let fixture: ComponentFixture<TicketDetail>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      declarations: [TicketDetail]
    })
    .compileComponents();

    fixture = TestBed.createComponent(TicketDetail);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
