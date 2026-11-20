
import { Component, OnInit, inject, signal } from '@angular/core';
import { ApiService } from './api.service';
import { ClaimSummary, ClaimDetail, OfficerWorkload } from './models';

@Component({
  selector: 'app-officer',
  standalone: true,
  template: `
    <h2>Claims Officer Dashboard</h2>
    <p>Manage incoming claims and monitor your workload.</p>

    @if (error()) {
      <p class="error">{{ error() }}</p>
    }
    @if (success()) {
      <p class="success">{{ success() }}</p>
    }

    <section class="panel">
      <h3>My Workload</h3>

      @if (workload(); as w) {
        <p><strong>Unassigned:</strong> {{ w.unassignedCount }}</p>
        <p><strong>Under Review:</strong> {{ w.underReview }}</p>
        <p><strong>Awaiting Claimant:</strong> {{ w.awaitingClaimant }}</p>
        <p><strong>Awaiting Settlement:</strong> {{ w.awaitingSettlement }}</p>

        <h4>Assigned Claims</h4>
        @for (claim of w.items; track claim.id) {
          <div class="claim-card">
            <strong>{{ claim.claimNumber }}</strong>
            <p>{{ claim.type }} — {{ claim.status }}</p>
            <button (click)="view(claim.id)">Review Claim</button>
          </div>
        }
      }
    </section>

    <section class="panel">
      <h3>Incoming Claims Queue</h3>
      <button (click)="refresh()">Refresh Queue</button>

      @if (queue().length === 0) {
        <p>No unassigned claims.</p>
      }

      @for (claim of queue(); track claim.id) {
        <div class="claim-card">
          <h4>{{ claim.claimNumber }}</h4>
          <p><strong>Type:</strong> {{ claim.type }}</p>
          <p><strong>Market:</strong> {{ claim.market }}</p>
          <p><strong>Amount:</strong> {{ claim.claimedAmount }}</p>

          <button [disabled]="busy()" (click)="pickup(claim.id)">
            Pick Up Claim
          </button>
        </div>
      }
    </section>

    @if (selected(); as claim) {
      <section class="panel">
        <h3>Reviewing {{ claim.claimNumber }}</h3>

        <p><strong>Status:</strong> {{ claim.status }}</p>
        <p><strong>Description:</strong> {{ claim.description }}</p>
        <p><strong>Claimed Amount:</strong> {{ claim.claimedAmount }}</p>

        <h4>Information Requests</h4>
        @for (request of claim.infoRequests; track request.id) {
          <p>
            <strong>{{ request.question }}</strong>
            <span> — {{ request.response ?? 'Awaiting response' }}</span>
          </p>
        }

        @if (claim.status === 'UnderReview') {
          <h4>Request More Information</h4>
          <textarea #question placeholder="Question for claimant"></textarea>
          <button
            [disabled]="busy()"
            (click)="requestInfo(claim.id, question.value)"
          >
            Request Information
          </button>

          <h4>Approve Claim</h4>
          <input
            #amount
            type="number"
            min="0"
            [value]="claim.claimedAmount"
          />
          <textarea #approvalReason placeholder="Approval reason"></textarea>
          <button
            [disabled]="busy()"
            (click)="approve(claim.id, amount.value, approvalReason.value)"
          >
            Approve
          </button>

          <h4>Reject Claim</h4>
          <textarea #rejectionReason placeholder="Rejection reason"></textarea>
          <button
            [disabled]="busy()"
            (click)="reject(claim.id, rejectionReason.value)"
          >
            Reject
          </button>
        }

        @if (claim.status === 'Approved') {
          <button [disabled]="busy()" (click)="settle(claim.id)">
            Mark as Settled
          </button>
        }

        <h4>Claim Timeline</h4>
        @for (event of claim.timeline; track $index) {
          <p>{{ event.occurredAt }} — {{ event.to }} {{ event.note ?? '' }}</p>
        }
      </section>
    }
  `
})
export class OfficerComponent implements OnInit {
  private api = inject(ApiService);

  queue = signal<ClaimSummary[]>([]);
  workload = signal<OfficerWorkload | null>(null);
  selected = signal<ClaimDetail | null>(null);
  error = signal('');
  success = signal('');
  busy = signal(false);

  ngOnInit() {
    void this.refresh();
  }

  async refresh() {
    try {
      this.queue.set(await this.api.queue());
      this.workload.set(await this.api.workload());

      if (this.selected()) {
        this.selected.set(await this.api.claim(this.selected()!.id));
      }
    } catch (e) {
      this.error.set(ApiService.message(e));
    }
  }

  async view(id: string) {
    try {
      this.selected.set(await this.api.claim(id));
    } catch (e) {
      this.error.set(ApiService.message(e));
    }
  }

  async pickup(id: string) {
    await this.action(async () => {
      await this.api.pickup(id);
      await this.refresh();
      await this.view(id);
    });
  }

  async requestInfo(id: string, question: string) {
    if (!question.trim()) {
      this.error.set('Enter a question.');
      return;
    }
    await this.action(() => this.api.requestInfo(id, question));
  }

  async approve(id: string, amount: string, reason: string) {
    const value = Number(amount);
    if (!Number.isFinite(value) || value < 0 || !reason.trim()) {
      this.error.set('Enter a valid amount and reason.');
      return;
    }
    await this.action(() => this.api.approve(id, value, reason));
  }

  async reject(id: string, reason: string) {
    if (!reason.trim()) {
      this.error.set('Enter a rejection reason.');
      return;
    }
    await this.action(() => this.api.reject(id, reason));
  }

  async settle(id: string) {
    await this.action(() => this.api.settle(id));
  }

  private async action(fn: () => Promise<unknown>) {
    this.error.set('');
    this.success.set('');
    this.busy.set(true);

    try {
      await fn();
      await this.refresh();
      this.success.set('Action completed successfully.');
    } catch (e) {
      this.error.set(ApiService.message(e));
    } finally {
      this.busy.set(false);
    }
  }
}
