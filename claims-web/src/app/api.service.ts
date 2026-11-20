
import { Injectable, inject, signal } from '@angular/core';
import { HttpClient, HttpHeaders } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import {
  User, ClaimSummary, ClaimDetail, SubmitClaim,
  NotificationItem, OfficerWorkload
} from './models';

@Injectable({ providedIn: 'root' })
export class ApiService {
  private http = inject(HttpClient);
  private base = 'http://localhost:5139/api';

  user = signal<User | null>(null);

  private headers(): HttpHeaders {
    const id = this.user()?.id;
    return id
      ? new HttpHeaders({ 'X-User-Id': id })
      : new HttpHeaders();
  }

  private get<T>(path: string): Promise<T> {
    return firstValueFrom(
      this.http.get<T>(this.base + path, { headers: this.headers() })
    );
  }

  private post<T>(path: string, body: unknown = {}): Promise<T> {
    return firstValueFrom(
      this.http.post<T>(this.base + path, body, { headers: this.headers() })
    );
  }

  users() {
    return this.get<User[]>('/users');
  }

  myClaims() { return this.get<ClaimSummary[]>('/claims/mine'); }

  claim(id: string) {
    return this.get<ClaimDetail>(`/claims/${id}`);
  }

  submit(body: SubmitClaim) {
    return this.post<ClaimDetail>('/claims', body);
  }

  respond(id: string, requestId: string, response: string) {
    return this.post<unknown>(
      `/claims/${id}/info-requests/${requestId}/respond`,
      { response }
    );
  }

  notifications() {
    return this.get<NotificationItem[]>('/notifications');
  }

  queue() {
    return this.get<ClaimSummary[]>('/officer/queue');
  }

  workload() {
    return this.get<OfficerWorkload>('/officer/workload');
  }

  pickup(id: string) {
    return this.post<unknown>(`/officer/claims/${id}/pickup`);
  }

  requestInfo(id: string, question: string) {
    return this.post<unknown>(
      `/officer/claims/${id}/request-info`,
      { question }
    );
  }

  approve(id: string, approvedAmount: number, reason: string) {
    return this.post<unknown>(
      `/officer/claims/${id}/approve`,
      { approvedAmount, reason }
    );
  }

  reject(id: string, reason: string) {
    return this.post<unknown>(
      `/officer/claims/${id}/reject`,
      { reason }
    );
  }

  settle(id: string) {
    return this.post<unknown>(`/officer/claims/${id}/settle`);
  }

  managerSummary() {
    return this.get<unknown>('/manager/summary');
  }
 teamPerformance() {
    return this.get<unknown>('/manager/team');
  }

  static message(error: unknown): string {
    const e = error as {
      error?: { message?: string; title?: string };
      message?: string;
    };
    return e?.error?.message
      ?? e?.error?.title
      ?? e?.message
      ?? 'An unexpected error occurred.';
  }
}
