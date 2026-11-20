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

