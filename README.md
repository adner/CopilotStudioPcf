# Copilot Studio Web Chat Client with a Custom UI

This is a web application that embeds a Microsoft Copilot Studio agent using the [Microsoft 365 Agents SDK](https://learn.microsoft.com/en-us/microsoft-365/agents-sdk/), specifically the [Copilot Studio Client](https://github.com/microsoft/Agents-for-net/tree/main/src/libraries/Client/Microsoft.Agents.CopilotStudio.Client). I posted this to LinkedIn [here](https://www.linkedin.com/posts/andreas-adner-70b1153_copilot-studio-agent-with-a-custom-ui-activity-7369826563938398208-XXS7?utm_source=share&utm_medium=member_desktop&rcm=ACoAAACM8rsBEgQIrYgb4NZAbnxwfDRk_Tu5e3w).

## Overview

This project provides a custom chat interface for Microsoft Copilot Studio agents with a pretty spaced out design. It's based on the [.NET AI Template](https://devblogs.microsoft.com/dotnet/announcing-dotnet-ai-template-preview2/) which has been modified to use the Copilot Studio Client, instead of OpenAI.

The major modifications that have been made to the template is added plumbing for injecting CopilotStudioClient-instances, when IChatClient is requested. `CopilotStudioIChatClient` has been implemented for this purpose, which wraps the CopilotStudioClient and implements the [Microsoft.Extensions.AI.IChatClient](https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.ai.ichatclient?view=net-9.0-pp) interface.

### Setup

1. **Clone the repository**

2. **Configure Copilot Studio**
   Follow the [Copilot Studio documentation](https://learn.microsoft.com/en-us/microsoft-copilot-studio/publication-integrate-web-or-native-app-m365-agents-sdk?tabs=dotnet) to set up your agent and obtain credentials. Note that service principal authentication is not supported (yet). This example is built with normal user authentication, the first time the app is launched the user will be prompted for credentials is an OAuth flow.

3. **Update configuration**
   Configure your Copilot Studio client settings in appettings.Development.json (see appsettings.json for the structure).

4. **Run the application**
   ```bash
   dotnet run
   ```

## Adaptive Card Integration

The application "piggy backs" on Adaptive cards sent from Copilot Studio, where some extra information has been entered to the `data` attribute of the Adaptive Card JSON. Example:

```json
{
    "type": "AdaptiveCard",
    "$schema": "https://adaptivecards.io/schemas/adaptive-card.json",
    "version": "1.5",
    "body": [
        {
            "type": "Image",
            "url": "https://upload.wikimedia.org/wikipedia/commons/thumb/e/e0/Sudoku_Puzzle_by_L2G-20050714_standardized_layout.svg/375px-Sudoku_Puzzle_by_L2G-20050714_standardized_layout.svg.png",
            "horizontalAlignment": "Center"
        }
    ],
    "actions": [
        {
            "type": "Action.Submit",
            "title": "OK!",
            "data": {
                "action": "PlaySudoku"
            }
        }
    ]
}
```

The `action` attribute - "PlaySokudo" in this case is what the client triggers on to render the custom GUI.

# Overview of the Adaptive Card Function Calling implementation

When a "RenderAdaptiveCardAsync" function call is encountered in chat responses, the client renders custom UI components - for example a Sudoku game. The system uses Microsoft.Extensions.AI's function invocation framework to "automatically" route function calls to registered methods. To be honest, this is really not AI function calling, since the AI isn't really involved - it is more a way of using the Function-calling plumbing in Microsoft.Extensions.AI to "manually" call functions that render custom UI elements.

## How It Works

### 1. Function Call Generation

In `CopilotStudioIChatClient.cs`, when an adaptive card attachment is detected:

```csharp
else if (activity.Type == "message" && activity.Attachments.Count == 1 && activity.Attachments[0].ContentType == "application/vnd.microsoft.card.adaptive")
{ 
    // Extract the adaptive card JSON and yield a function call to render it
    var adaptiveCardJson = JsonSerializer.Serialize(activity.Attachments[0].Content);
    
    yield return new ChatResponseUpdate
    {
        CreatedAt = createdAt,
        Contents = [new FunctionCallContent("RenderAdaptiveCardAsync", adaptiveCardJson) 
        { 
            Arguments = new Dictionary<string, object?> 
            { 
                ["adaptiveCardJson"] = adaptiveCardJson 
            }
        }],
        Role = ChatRole.Assistant
    };
}
```

This creates a `FunctionCallContent` with:
- **Function Name**: "RenderAdaptiveCardAsync"
- **Arguments**: Dictionary containing the adaptive card JSON

### 2. UI Rendering

In `ChatMessageItem.razor`, the function call is detected and rendered in the UI. For example, this is what it looks like for the Sudoku example:

```csharp
 else if (content is FunctionCallContent { CallId: "RenderAdaptiveCardAsync" } acc && acc.Arguments?.TryGetValue("adaptiveCardJson", out var cardJsonObj) is true && cardJsonObj is string cardJson)
        {
            @if (ShouldRenderSudokuCard(cardJson))
            {
                <div class="adaptive-card-container">
                    <div class="adaptive-card-content">
                        <iframe src="/sudoku.html" 
                                width="100%" 
                                height="630" 
                                frameborder="0" 
                                style="border-radius: 8px; background: #0f0f23;">
                        </iframe>
                    </div>
                </div>
            }
        ...
```
## Overview of Dataverse MCP Server consent flow using Adaptive Cards

The Dataverse MCP Server consent flow uses Adaptive Cards to handle user authorization for connecting to Microsoft Dataverse. 

### Technical Implementation

#### 1. Consent Card Detection

When an Adaptive Card is received from Copilot Studio, the system checks if it's a consent request by examining its structure in `ChatMessageItem.razor`:

```csharp
private bool ShouldRenderConsentCard(string cardJson)
{
    // Parse the JSON and check for "Connect to continue" title
    // This identifies consent cards that require user authorization
    if (firstItem.TryGetProperty("text", out var titleText) && 
        titleText.GetString() == "Connect to continue")
    {
        return true;
    }
}
```

#### 2. Consent UI Rendering

When a consent card is detected, a custom UI is rendered showing:
- Service information (Microsoft Dataverse)
- Required permissions and capabilities
- Privacy notice about credential sharing
- Allow/Cancel action buttons

#### 3. Handling User Response

When the user clicks Allow or Cancel, the response is sent back to Copilot Studio:

```csharp
private async Task HandleConnectionResponse(bool allowed, string? incomingActivityId)
{
    var invokeActivity = new Activity
    {
        Type = ActivityTypes.Invoke,
        Name = "connectors/consentCard",
        Value = new
        {
            action = allowed ? "Allow" : "Cancel",
            id = "submit",
            shouldAwaitUserInput = true
        },
        ReplyToId = incomingActivityId ?? string.Empty,
    };
    
    await OnAdaptiveCardInvokeAction.InvokeAsync(invokeActivity);
}
```

#### 4. Processing the Response

The response is sent through `CopilotStudioIChatClient.SendAdaptiveCardResponseToCopilotStudio`:

```csharp
public async IAsyncEnumerable<ChatResponseUpdate> SendAdaptiveCardResponseToCopilotStudio(Activity invokeActivity)
{
    // Send the consent response to Copilot Studio
    await foreach (var activity in _copilotClient.AskQuestionAsync(invokeActivity))
    {
        // Process the response and update UI accordingly
        if (activity.Type == "message" && !string.IsNullOrEmpty(activity.Text))
        {
            yield return new ChatResponseUpdate
            {
                Contents = [new TextContent("Successfully connected to Dataverse MCP Server.")],
                Role = ChatRole.Assistant
            };
        }
    }
}
```
A key point is the usage of `ReplyToId` to correlate consent responses with the original request.



