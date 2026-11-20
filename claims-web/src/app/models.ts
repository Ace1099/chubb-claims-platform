
export type Role = 'Claimant' | 'Officer' | 'Manager';

export interface User {
  id: string;
  name: string;
  email: string;
  role: Role;
}

export interface ClaimSummary {
  id: string;
  claimNumber: string;
  type: string;
  market: string;
  status: string;
  claimedAmount: number;
  createdAt: string;
  updatedAt: string;
  assignedOfficerName: string | null;
  ageHours: number;
}

export interface InfoRequestItem {
  id: string;
  question: string;
  requestedAt: string;
  response: string | null;
  respondedAt: string | null;
}

export interface DecisionItem {
  isApproved: boolean;
  approvedAmount: number;
  reason: string;
  decidedAt: string;
}

export interface TimelineItem {
  from: string | null;
  to: string;
  note: string | null;
  occurredAt: string;
}

export interface ClaimDetail extends ClaimSummary {
  description: string;
  incidentDate: string;
  claimantName: string;
  infoRequests: InfoRequestItem[];
  decision: DecisionItem | null;
  timeline: TimelineItem[];
}

export interface SubmitClaim {
  type: string;
  market: string;
  incidentDate: string;
  description: string;
  claimedAmount: number;
}

export interface NotificationItem {
  id: string;
  claimId: string;
  message: string;
  createdAt: string;
}

export interface OfficerWorkload {
  unassignedCount: number;
  underReview: number;
  awaitingClaimant: number;
  awaitingSettlement: number;
  items: ClaimSummary[];
}

export interface ExposureLine {
  key: string;
  openClaims: number;
  exposure: number;
}
