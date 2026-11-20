\# AI Working Journal



\## Entry 1: Planning

\- \*\*Asked:\*\* Read the brief, list the requirements, recommend a stack.

\- \*\*Accepted:\*\* .NET over Spring Boot (less boilerplate, easier to explain).

\- \*\*Accepted:\*\* One modular service instead of microservices (4-hour budget).

\- \*\*Accepted:\*\* REST for request/response, Kafka for domain events behind an IEventPublisher interface.

\- \*\*Adjusted:\*\* Docker is not installed, so Kafka is deferred; the app must run without it.

\- \*\*Adjusted:\*\* Machine has .NET 9, so the project targets net9.0.





\## Entry 2: Project setup

\- \*\*Asked:\*\* AI for scaffold commands (solution, API, xUnit, EF Core + SQLite, Swagger).

\- \*\*Problem:\*\* AI did not pin EF Core versions; NuGet pulled 11.0.0, which is incompatible with net9.0 (NU1202).

\- \*\*Overrode:\*\* Pinned EF Core packages to 9.0.10 to match the .NET 9 SDK.

\- \*\*Lesson:\*\* Always match package major versions to the target framework; verify AI-generated commands against the actual environment.





\## Entry 3: Domain model

\- \*\*Asked:\*\* AI to design entities (Claim, User, InfoRequest, Decision, ClaimEvent, Notification) and a status state machine.

\- \*\*Accepted:\*\* Business rules live inside the Claim entity (factory method, private setters, PickUp/RequestInfo/Approve/Reject/Settle) so no code path can create an invalid claim or skip a status.

\- \*\*Accepted:\*\* ClaimEvent audit trail written on every transition.

\- \*\*Accepted:\*\* Optimistic concurrency (Version token) so two officers cannot pick up the same claim.

\- \*\*Accepted:\*\* EnsureCreated instead of migrations (sprint trade-off; production would use migrations).

\- \*\*Challenged:\*\* SQLite has no decimal type; money stored as double via a value converter. Trade-off: tiny precision risk; production uses decimal(18,2).

\- \*\*Overrode:\*\* AI's first draft put DomainException inside the state machine file and used it for validation too; split into InvalidInput/Forbidden/NotFound/Domain exceptions so each maps to a proper HTTP status.

\- \*\*Assumption:\*\* The six APAC markets are not named in the brief; I picked six.





\## Entry 4: Events, errors, auth

\- \*\*Asked:\*\* AI to design REST vs Kafka usage.

\- \*\*Accepted:\*\* REST for anything needing an immediate answer; events (Kafka-shaped) for side effects (notifications). Services depend on IEventPublisher; transport is swappable.

\- \*\*Overrode (plan change):\*\* Original plan had events maintain the liability-exposure figure. I changed it: exposure is computed from the database on demand (always consistent, simpler); events only drive notifications.

\- \*\*Accepted:\*\* Idempotent consumer (SourceEventId unique) because Kafka is at-least-once.

\- \*\*Accepted:\*\* Demo-only X-User-Id auth with server-side role checks; real OIDC/JWT is out of scope.

\- \*\*Noted:\*\* Publishing after the DB commit can lose an event if the broker is down (dual-write problem). Production fix: transactional outbox.





\## Entry 5: API surface

\- \*\*Asked:\*\* AI to design controllers, services and DTOs.

\- \*\*Accepted:\*\* Thin controllers -> ClaimService/ReportingService -> domain methods. Domain entities never leave the API (DTO mappers).

\- \*\*Accepted:\*\* Separate endpoint groups per audience (claims, officer, manager) with role checks on every call.

\- \*\*Accepted:\*\* Exposure rule: approved amount once approved, else claimed amount; excludes Rejected/Settled.

\- \*\*Challenged:\*\* Event publish failure should not fail a request that already committed; logged instead, outbox noted as the production fix.

\- \*\*Accepted (trade-off):\*\* Reporting aggregates in memory; fine for the sprint, would move to SQL GROUP BY / read model at scale.



\## Entry 6: Wiring and first run

\- \*\*Asked:\*\* AI to wire DI, seed data, Swagger auth, CORS, health check.

\- \*\*Accepted:\*\* Seed with fixed GUIDs for demo users and six sample claims in different states so dashboards are meaningful on first run.

\- \*\*Overrode:\*\* Connection string and seed flag are read lazily from final configuration (not at registration time) so integration tests can override them.

\- \*\*Accepted:\*\* Swagger "Authorize" button for the X-User-Id header to test role-based endpoints.

\- \*\*Verified:\*\* Ran the API, hit /api/users, /api/manager/summary and /api/officer/queue.


