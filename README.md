\# Claims Platform — Chubb Take-Home Assessment



A full-stack insurance claims management prototype using ASP.NET Core (.NET 9), Angular, Entity Framework Core, and SQLite.



\## Features



\- Claimant: submit and track claims, respond to information requests, and view notifications.

\- Claims Officer: manage assigned claims, request information, record decisions, and settle approved claims.

\- Manager: view team performance and claims reporting.

\- Audit history of claim status changes.

\- Automated backend tests.



\## Prerequisites



\- .NET 9 SDK

\- Node.js and npm



\## Run the backend



From the repository root:



```powershell

dotnet run --project src/ClaimsPlatform.Api

```



API: http://localhost:5139

Swagger: http://localhost:5139/swagger



\## Run the frontend



In a second terminal:



```powershell

cd claims-web

npm install

npm start

```



Frontend: http://localhost:4200



\## Tests and build



```powershell

dotnet test

cd claims-web

npm run build

```



\## Architecture and scope



\- ASP.NET Core REST API with Entity Framework Core and SQLite persistence.

\- Angular frontend with role-specific dashboards.

\- In-process event bus for claim notifications instead of a deployed Kafka broker, to keep the prototype self-contained within the time limit.

\- Demonstration identity supplied through the X-User-Id header; not production authentication.

\- Docker, production security, and distributed messaging are outside this prototype's scope.



\## AI usage



See docs/AI\_JOURNAL.md for the AI-assisted development process, verification, and engineering decisions.



