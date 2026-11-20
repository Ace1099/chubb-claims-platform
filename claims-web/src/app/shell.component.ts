
import { DatePipe } from '@angular/common';
import { Component, DestroyRef, OnInit, inject, signal } from '@angular/core';
import { ApiService } from './api.service';
import { ClaimantComponent } from './claimant.component';
import { OfficerComponent } from './officer.component';
import { ManagerComponent } from './manager.component';
import { NotificationItem, User } from './models';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [
    DatePipe,
    ClaimantComponent,
    OfficerComponent,
    ManagerComponent
  ],
  template: `
    <header>
      <h1>Chubb APAC Claims Platform</h1>

      @if (api.user(); as user) {
        <div class="right">
          <button (click)="toggleNotifications()">
            Notifications ({{ notes().length }})
          </button>

          <span>{{ user.name }} — {{ user.role }}</span>

          <button (click)="logout()">Switch User</button>
        </div>
      }
    </header>

    @if (showNotes()) {
      <section class="panel notes">
        <h3>Notifications</h3>

        @for (note of notes(); track note.id) {
          <div class="claim-card">
            <p>{{ note.message }}</p>
            <small>{{ note.createdAt | date:'short' }}</small>
          </div>
        } @empty {
          <p>No notifications yet.</p>
        }
      </section>
    }

    <main>
      @if (error()) {
        <p class="error">{{ error() }}</p>
      }

      @if (api.user(); as user) {
        @switch (user.role) {
          @case ('Claimant') {
            <app-claimant />
          }

          @case ('Officer') {
            <app-officer />
          }

          @case ('Manager') {
            <app-manager />
          }
        }
      } @else {
        <section class="panel">
          <h2>Choose a Demo User</h2>

          <p>
            Select a user to explore the claims platform.
            Each user has a different role and set of permissions.
          </p>

          <p class="hint">
            Demo authentication uses an X-User-Id header.
            Production authentication would require OIDC or JWT.
          </p>

          <div class="cards">
            @for (user of users(); track user.id) {
              <button
                class="card"
                (click)="login(user)"
              >
                <strong>{{ user.name }}</strong>
                <span>{{ user.role }}</span>
              </button>
            }
          </div>
        </section>
      }
    </main>
  `
})
export class ShellComponent implements OnInit {
  api = inject(ApiService);
  private destroyRef = inject(DestroyRef);

  users = signal<User[]>([]);
  notes = signal<NotificationItem[]>([]);
  showNotes = signal(false);
  error = signal('');

  private timer: ReturnType<typeof setInterval> | null = null;

  constructor() {
    this.destroyRef.onDestroy(() => this.stopPolling());
  }

  async ngOnInit() {
    try {
      this.users.set(await this.api.users());
    } catch (e) {
      this.error.set(ApiService.message(e));
    }
  }

  async login(user: User) {
    this.stopPolling();

    this.api.user.set(user);
    this.notes.set([]);
    this.showNotes.set(false);
    this.error.set('');

    await this.refreshNotes();

    this.timer = setInterval(() => {
      void this.refreshNotes();
    }, 5000);
  }

  logout() {
    this.stopPolling();
    this.api.user.set(null);
    this.notes.set([]);
    this.showNotes.set(false);
    this.error.set('');
  }

  toggleNotifications() {
    this.showNotes.set(!this.showNotes());

    if (this.showNotes()) {
      void this.refreshNotes();
    }
  }

  async refreshNotes() {
    if (!this.api.user()) {
      return;
    }

    try {
      this.notes.set(await this.api.notifications());
    } catch {
      // Ignore temporary notification errors.
    }
  }

  private stopPolling() {
    if (this.timer !== null) {
      clearInterval(this.timer);
      this.timer = null;
    }
  }
}
