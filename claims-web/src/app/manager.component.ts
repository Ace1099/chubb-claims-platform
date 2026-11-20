
import { DecimalPipe } from '@angular/common';
import { Component, DestroyRef, OnInit, inject, signal } from '@angular/core';
import { ApiService } from './api.service';
import { ExposureLine } from './models';

interface OfficerPerformance {
  officerId: string;
  name: string;
  openClaims: number;
  awaitingClaimant: number;
  approved: number;
  rejected: number;
  settled: number;
  avgHoursToDecision: number | null;
}

interface ManagerSummary {
  totalClaims: number;
  openClaims: number;
  totalExposure: number;
  byMarket: ExposureLine[];
  byType: ExposureLine[];
  byStatus: Record<string, number>;
}

@Component({
  selector: 'app-manager',
  standalone: true,
  imports: [DecimalPipe],
  template: `
    <h2>Manager Dashboard</h2>
    <p>Monitor claims, outstanding liability, and team performance.</p>

    @if (error()) {
      <p class="error">{{ error() }}</p>
    }

    <button (click)="refresh()">Refresh Dashboard</button>

    @if (summary(); as s) {
      <section class="panel">
        <h3>Claims Overview</h3>
        <div class="stats">
          <div class="stat">
            <span>Total Claims</span>
            <strong>{{ s.totalClaims }}</strong>
          </div>
          <div class="stat">
            <span>Open Claims</span>
            <strong>{{ s.openClaims }}</strong>
          </div>
          <div class="stat">
            <span>Outstanding Liability Exposure</span>
            <strong>{{ s.totalExposure | number:'1.0-2' }}</strong>
          </div>
        </div>
      </section>

      <section class="panel">
        <h3>Exposure by Market</h3>
        <table>
          <thead>
            <tr>
              <th>Market</th>
              <th>Open Claims</th>
              <th>Exposure</th>
            </tr>
          </thead>
          <tbody>
            @for (item of s.byMarket; track item.key) {
              <tr>
                <td>{{ item.key }}</td>
                <td>{{ item.openClaims }}</td>
                <td>{{ item.exposure | number:'1.0-2' }}</td>
              </tr>
            }
          </tbody>
        </table>
      </section>

      <section class="panel">
        <h3>Exposure by Claim Type</h3>
        <table>
          <thead>
            <tr>
              <th>Type</th>
              <th>Open Claims</th>
              <th>Exposure</th>
            </tr>
          </thead>
          <tbody>
            @for (item of s.byType; track item.key) {
              <tr>
                <td>{{ item.key }}</td>
                <td>{{ item.openClaims }}</td>
                <td>{{ item.exposure | number:'1.0-2' }}</td>
              </tr>
            }
          </tbody>
        </table>
      </section>

      <section class="panel">
        <h3>Claims by Status</h3>
        <table>
          <thead>
            <tr>
              <th>Status</th>
              <th>Count</th>
            </tr>
          </thead>
          <tbody>
            @for (item of statusRows(s); track item[0]) {
              <tr>
                <td>{{ item[0] }}</td>
                <td>{{ item[1] }}</td>
              </tr>
            }
          </tbody>
        </table>
      </section>
    }

    <section class="panel">
      <h3>Team Performance</h3>
      <table>
        <thead>
          <tr>
            <th>Officer</th>
            <th>Open</th>
            <th>Awaiting Claimant</th>
            <th>Approved</th>
            <th>Rejected</th>
            <th>Settled</th>
            <th>Avg. Decision Hours</th>
          </tr>
        </thead>
        <tbody>
          @for (officer of team(); track officer.officerId) {
            <tr>
              <td>{{ officer.name }}</td>
              <td>{{ officer.openClaims }}</td>
              <td>{{ officer.awaitingClaimant }}</td>
              <td>{{ officer.approved }}</td>
              <td>{{ officer.rejected }}</td>
              <td>{{ officer.settled }}</td>
              <td>{{ officer.avgHoursToDecision ?? '-' }}</td>
            </tr>
          }
        </tbody>
      </table>
    </section>
  `
})
export class ManagerComponent implements OnInit {
  private api = inject(ApiService);
  private destroyRef = inject(DestroyRef);

  summary = signal<ManagerSummary | null>(null);
  team = signal<OfficerPerformance[]>([]);
  error = signal('');

  async ngOnInit() {
    await this.refresh();

    const timer = setInterval(() => {
      void this.refresh();
    }, 5000);

    this.destroyRef.onDestroy(() => clearInterval(timer));
  }

  async refresh() {
    try {
      const [summary, team] = await Promise.all([
        this.api.managerSummary() as Promise<ManagerSummary>,
        this.api.teamPerformance() as Promise<OfficerPerformance[]>
      ]);

      this.summary.set(summary);
      this.team.set(team);
      this.error.set('');
    } catch (e) {
      this.error.set(ApiService.message(e));
    }
  }

  statusRows(summary: ManagerSummary) {
    return Object.entries(summary.byStatus);
  }
}
