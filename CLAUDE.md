# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

This is a **Blazor Server web application** that provides a custom chat interface for Microsoft Copilot Studio agents using the [Microsoft 365 Agents SDK](https://learn.microsoft.com/en-us/microsoft-365/agents-sdk/) and specifically the [Copilot Studio Client](https://github.com/microsoft/Agents-for-net/tree/main/src/libraries/Client/Microsoft.Agents.CopilotStudio.Client).

The application is based on the [.NET AI Template](https://devblogs.microsoft.com/dotnet/announcing-dotnet-ai-template-preview2/) but modified to use Copilot Studio Client instead of OpenAI, wrapping it with `CopilotStudioIChatClient` that implements `Microsoft.Extensions.AI.IChatClient`.

## Development Commands

- **Run the application**: `dotnet run` (from webchatclient directory or root)
- **Build solution**: `dotnet build CopilotStudioPcf.sln`
- **Clean solution**: `dotnet clean`

## Architecture Overview

### Core Components

- **`CopilotStudioIChatClient.cs`**: Primary wrapper that implements `IChatClient` interface, bridging Copilot Studio SDK with Microsoft.Extensions.AI framework
- **`Program.cs`**: Dependency injection setup, authentication configuration, and service registration
- **Blazor Components**: Located in `Components/` directory with interactive server-side rendering

### Key Architecture Patterns

1. **Dependency Injection**: Uses Microsoft.Extensions.DI with transient `CopilotClient` and singleton settings
2. **Authentication**: OAuth flow with MSAL (Microsoft Authentication Library) using `AddTokenHandler`
3. **Function Call Simulation**: Uses Microsoft.Extensions.AI function calling framework to render custom UI elements when adaptive cards are detected
4. **Streaming Responses**: Implements `IAsyncEnumerable<ChatResponseUpdate>` for real-time chat updates

### Authentication Flow

- Uses `IPublicClientApplication` with interactive OAuth
- Token caching with platform-specific storage (KeyChain on macOS, file-based elsewhere)
- Automatic token refresh via `AddTokenHandler`
- Requires user authentication on first launch (no service principal support yet)

### Adaptive Card Integration

The application detects adaptive card attachments in Copilot Studio responses and automatically converts them to function calls:
- Adaptive cards trigger `RenderAdaptiveCardAsync` function calls
- Function calls are rendered in `ChatMessageItem.razor` component
- Custom UI elements can be triggered via `data.action` attributes in adaptive card JSON

## Configuration

Configuration is managed in `appsettings.json` and `appsettings.Development.json`:

```json
{
  "CopilotStudioClientSettings": {
    "DirectConnectUrl": "",
    "EnvironmentId": "Get this info from Copilot Studio",
    "SchemaName": "Get this info from Copilot Studio", 
    "TenantId": "Your tenant ID",
    "UseS2SConnection": false,
    "AppClientId": "Get this from your app registration in Entra ID",
    "AppClientSecret": ""
  }
}
```

## Solution Structure

- **Root**: Contains Visual Studio solution file (`CopilotStudioPcf.sln`)
- **webchatclient/**: Main Blazor Server application
  - `Components/Pages/Chat/`: Chat-specific Razor components
  - `Services/`: Business logic and API integrations
  - `Program.cs`: Application startup and DI configuration
  - `webchatclient.csproj`: .NET 9.0 web project with required NuGet packages

## Key Dependencies

- **Microsoft.Agents.CopilotStudio.Client**: Core SDK for Copilot Studio integration
- **Microsoft.Extensions.AI**: AI abstraction layer and function calling framework
- **Microsoft.Identity.Client.Extensions.Msal**: Authentication and token management
- **AdaptiveCards**: Rendering support for adaptive card responses

## Development Notes

- Uses .NET 9.0 target framework
- Interactive Server Components for real-time updates
- Token caching directory: `{AppContext.BaseDirectory}/mcs_client_console`
- HTTPS redirection and antiforgery protection enabled
- Static file serving for client-side assets