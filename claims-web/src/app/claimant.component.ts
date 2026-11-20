
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ApiService } from './api.service';
import { ClaimSummary, ClaimDetail, SubmitClaim } from './models';

@Component({
  selector: 'app-claimant',
  standalone: true,
  imports: [FormsModule],
  template: `
    <h2>My Claims</h2>
    <p>Report an incident, track your claims, and respond to information requests.</p>

    @if (error()) {
      <p class="error">{{ error() }}</p>
    }
    @if (success()) {
      <p class="success">{{ success() }}</p>
    }

    <section class="panel">
      <h3>Submit a New Claim</h3>

      <form (ngSubmit)="submit()">
        <label>
          Claim Type
          <select name="type" [(ngModel)]="form.type">
            <option value="Motor">Motor</option>
            <option value="Property">Property</option>
          </select>
        </label>

        <label>
          Market
          <select name="market" [(ngModel)]="form.market">
            @for (market of markets; track market) {
              <option [value]="market">{{ market }}</option>
            }
          </select>
        </label>

        <label>
          Incident Date
          <input
            type="date"
            name="incidentDate"
            [(ngModel)]="form.incidentDate"
            required
          />
        </label>

        <label>
          Claimed Amount
          <input
            type="number"
            name="claimedAmount"
            [(ngModel)]="form.claimedAmount"
            min="1"
            required
          />
        </label>

        <label>
          Incident Description
          <textarea
            name="description"
            [(ngModel)]="form.description"
            required
          ></textarea>
        </label>

        <button type="submit" [disabled]="busy()">
          {{ busy() ? 'Submitting...' : 'Submit Claim' }}
        </button>
      </form>
    </section>

    <section class="panel">
      <h3>Existing Claims</h3>

      <button type="button" (click)="refresh()">Refresh Claims</button>

      @if (claims().length === 0) {
        <p>No claims found.</p>
      }

      <div class="claim-list">
        @for (claim of claims(); track claim.id) {
          <article class="claim-card">
            <h4>{{ claim.claimNumber }}</h4>
            <p><strong>Type:</strong> {{ claim.type }}</p>
            <p><strong>Market:</strong> {{ claim.market }}</p>
            <p><strong>Status:</strong> {{ claim.status }}</p>
            <p><strong>Amount:</strong> {{ claim.claimedAmount }}</p>

            <button type="button" (click)="view(claim.id)">
              View Details
            </button>
          </article>
        }
      </div>
    </section>

    @if (selected(); as claim) {
      <section class="panel">
        <h3>Claim Details — {{ claim.claimNumber }}</h3>

        <p><strong>Status:</strong> {{ claim.status }}</p>
        <p><strong>Description:</strong> {{ claim.description }}</p>
        <p><strong>Incident Date:</strong> {{ claim.incidentDate }}</p>
        <p><strong>Assigned Officer:</strong>
          {{ claim.assignedOfficerName ?? 'Not assigned' }}
        </p>

        <h4>Information Requests</h4>

        @if (claim.infoRequests.length === 0) {
          <p>No information requests.</p>
        }

        @for (request of claim.infoRequests; track request.id) {
          <div class="claim-card">
            <p><strong>Question:</strong> {{ request.question }}</p>

            @if (request.response) {
              <p><strong>Your Response:</strong> {{ request.response }}</p>
            } @else {
              <textarea
                #responseInput
                placeholder="Enter your response"
              ></textarea>

              <button
                type="button"
                [disabled]="busy()"
                (click)="respond(claim.id, request.id, responseInput.value)"
              >
                Send Response
              </button>
            }
          </div>
        }

        <h4>Decision</h4>
        @if (claim.decision; as decision) {
          <p>{{ decision.isApproved ? 'Approved' : 'Rejected' }}</p>
          <p><strong>Reason:</strong> {{ decision.reason }}</p>
          @if (decision.isApproved) {
            <p><strong>Approved Amount:</strong>
              {{ decision.approvedAmount }}
            </p>
          }
        } @else {
          <p>No decision has been made.</p>
        }

        <h4>Claim Timeline</h4>
        @for (event of claim.timeline; track $index) {
          <p>
            {{ event.occurredAt }} —
            {{ event.from ?? 'New' }} → {{ event.to }}
            {{ event.note ?? '' }}
          </p>
        }
      </section>
    }
  `
})
export class ClaimantComponent implements OnInit {
  private api = inject(ApiService);

  claims = signal<ClaimSummary[]>([]);
  selected = signal<ClaimDetail | null>(null);
  error = signal('');
  success = signal('');
  busy = signal(false);

  markets = [
    'Singapore',
    'Malaysia',
    'Indonesia',
    'Thailand',
    'HongKong',
    'Philippines'
  ];

  form: SubmitClaim = {
    type: 'Motor',
    market: 'Malaysia',
    incidentDate: new Date().toISOString().slice(0, 10),
    description: '',
    claimedAmount: 0
  };

  ngOnInit() {
    void this.refresh();
  }

  async refresh() {
    try {
      this.claims.set(await this.api.myClaims());

      const current = this.selected();
      if (current) {
        this.selected.set(await this.api.claim(current.id));
      }
    } catch (e) {
      this.error.set(ApiService.message(e));
    }
  }

  async submit() {
    this.error.set('');
    this.success.set('');

    if (
      !this.form.description.trim() ||
      !this.form.incidentDate ||
      this.form.claimedAmount <= 0
    ) {
      this.error.set('Please complete all required fields.');
      return;
    }

    this.busy.set(true);

    try {
      await this.api.submit(this.form);
      this.success.set('Claim submitted successfully.');
      this.form.description = '';
      this.form.claimedAmount = 0;
      await this.refresh();
    } catch (e) {
      this.error.set(ApiService.message(e));
    } finally {
      this.busy.set(false);
    }
  }

  async view(id: string) {
    this.error.set('');

    try {
      this.selected.set(await this.api.claim(id));
    } catch (e) {
      this.error.set(ApiService.message(e));
    }
  }

  async respond(claimId: string, requestId: string, response: string) {
    this.error.set('');
    this.success.set('');

    if (!response.trim()) {
      this.error.set('Please enter a response.');
      return;
    }

    this.busy.set(true);

    try {
      await this.api.respond(claimId, requestId, response);
      this.success.set('Response submitted successfully.');
      await this.refresh();
    } catch (e) {
      this.error.set(ApiService.message(e));
    } finally {
      this.busy.set(false);
    }
  }
}
