# AI-Generated-Chat-System

This is a .NET 10 Web API and SignalR based chat system generated using Clean Architecture principles. It includes full Role-Based Access Control (RBAC), Microsoft Identity authentication with JWTs, and Google Authenticator 2FA.

## Architecture
- **Domain**: Entities, interfaces, pure C# models.
- **Application**: DTOs, business logic, handlers.
- **Infrastructure**: Entity Framework Core with SQLite, Data Access.
- **API**: ASP.NET Core controllers, SignalR Hubs, and configuration.
- **Tests**: xUnit and Moq tests verifying authentication and RBAC logic.

## Setup Instructions

### Prerequisites
- .NET 10 SDK
- SQLite

### Running the API
```bash
cd AI-Generated-Chat-System.API
dotnet run
```

### Authentication & Roles
- Roles `Super Admin`, `Admin`, `Teacher`, `Student`, `Finance Officer` are automatically seeded.
- Endpoints are protected via Policies (e.g., `FinanceOnly`, `AdminsOnly`).
- CORS is configured to allow `http://localhost:3000`, `http://localhost:4200`, and `https://localhost:5001`.

### SignalR
- Hub at `/chathub`.
- Connect using access token passed in query string `?access_token=...` or standard Bearer headers.

## Development Tasks & Workflows
All setup and configurations follow standard `dotnet` workflows. Entity Framework Core migrations can be updated via:
```bash
dotnet ef migrations add <Name> --project AI-Generated-Chat-System.Infrastructure --startup-project AI-Generated-Chat-System.API
dotnet ef database update --project AI-Generated-Chat-System.Infrastructure --startup-project AI-Generated-Chat-System.API
```
