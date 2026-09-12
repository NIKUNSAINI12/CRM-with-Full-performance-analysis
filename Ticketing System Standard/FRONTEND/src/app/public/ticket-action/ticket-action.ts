import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { HttpClient } from '@angular/common/http';
import { API_BASE_URL } from '../../app';

@Component({
  selector: 'app-public-ticket-action',
  standalone: true,
  imports: [CommonModule, RouterModule],
  template: `
    <div class="min-h-screen bg-brand-dark-950 flex items-center justify-center p-4" style="background-color: #0B0E14; font-family: 'Inter', sans-serif;">
      <div class="max-w-md w-full bg-brand-dark-900 border border-brand-dark-800 rounded-2xl p-8 shadow-2xl text-center animate-fade-in" style="background-color: #11151D; border-color: #1F2633;">
        
        <!-- Loading State -->
        <div *ngIf="state === 'loading'" class="py-6">
          <div class="animate-spin rounded-full h-12 w-12 border-b-2 border-brand-primary mx-auto mb-6" style="border-color: transparent; border-bottom-color: #3b82f6;"></div>
          <h3 class="text-xl font-bold text-white mb-2">Processing Your Request...</h3>
          <p class="text-sm" style="color: #9CA3AF;">Please wait while we verify and update your ticket status.</p>
        </div>

        <!-- Success State -->
        <div *ngIf="state === 'success'" class="py-6">
          <div class="w-16 h-16 bg-green-500/10 text-green-400 rounded-full flex items-center justify-center mx-auto mb-6 border border-green-500/20">
            <span class="material-icons text-4xl" style="font-size: 36px; color: #10b981;">check_circle</span>
          </div>
          <h3 class="text-2xl font-bold text-white mb-2">Success!</h3>
          <p class="text-sm mb-6" style="color: #D1D5DB;">
            Ticket status has been successfully updated to 
            <strong class="text-green-400 font-semibold px-2 py-0.5 bg-green-500/10 rounded border border-green-500/20 text-xs inline-block ml-1" style="color: #10b981; background-color: rgba(16, 185, 129, 0.1); border-color: rgba(16, 185, 129, 0.2);">{{ newStatus }}</strong>.
          </p>
          <button routerLink="/login" class="px-6 py-2.5 bg-brand-primary text-white rounded-lg hover:bg-brand-primary-600 transition font-medium text-sm shadow-lg w-full" style="background-color: #3b82f6;">
            Go to Login
          </button>
        </div>

        <!-- Error State -->
        <div *ngIf="state === 'error'" class="py-6">
          <div class="w-16 h-16 bg-red-500/10 text-red-400 rounded-full flex items-center justify-center mx-auto mb-6 border border-red-500/20">
            <span class="material-icons text-4xl" style="font-size: 36px; color: #ef4444;">error</span>
          </div>
          <h3 class="text-2xl font-bold text-white mb-2">Request Failed</h3>
          <p class="text-sm text-red-300 mb-6 px-4" style="color: #fca5a5;">{{ errorMessage }}</p>
          <button routerLink="/login" class="px-6 py-2.5 bg-brand-dark-800 hover:bg-brand-dark-700 text-white rounded-lg transition font-medium text-sm w-full border" style="background-color: #1F2633; border-color: #2D3748;">
            Back to Login
          </button>
        </div>

      </div>
    </div>
  `
})
export class PublicTicketActionComponent implements OnInit {
  state: 'loading' | 'success' | 'error' = 'loading';
  newStatus: string = '';
  errorMessage: string = '';

  constructor(
    private route: ActivatedRoute,
    private http: HttpClient
  ) {}

  ngOnInit(): void {
    const payload = this.route.snapshot.queryParamMap.get('payload');
    if (!payload) {
      this.state = 'error';
      this.errorMessage = 'Invalid or missing secure action payload.';
      return;
    }

    this.http.get<any>(`${API_BASE_URL}/Tickets/public-action?payload=${encodeURIComponent(payload)}`)
      .subscribe({
        next: (res) => {
          this.state = 'success';
          this.newStatus = res.newStatus;
        },
        error: (err) => {
          this.state = 'error';
          this.errorMessage = err.error?.error || err.error || 'Failed to update ticket status. The link may have expired or is invalid.';
        }
      });
  }
}
