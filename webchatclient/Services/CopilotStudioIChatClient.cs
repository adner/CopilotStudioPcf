using Microsoft.Extensions.AI;
using Microsoft.Agents.CopilotStudio.Client;
using Microsoft.Agents.Core.Models;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace webchatclient.Services
{
    public class CopilotStudioIChatClient : IChatClient
    {
        private readonly CopilotClient _copilotClient;
        private bool _conversationStarted = false;

        public ChatClientMetadata Metadata { get; }

        public CopilotStudioIChatClient(CopilotClient copilotClient)
        {
            _copilotClient = copilotClient ?? throw new ArgumentNullException(nameof(copilotClient));
            
            Metadata = new ChatClientMetadata("CopilotStudio", new Uri("https://copilotstudio.microsoft.com"));
        }

        public async Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
        {
            var lastMessage = messages.LastOrDefault();
            if (lastMessage == null)
            {
                throw new ArgumentException("At least one message is required", nameof(messages));
            }

            if (!_conversationStarted)
            {
                await foreach (var activity in _copilotClient.StartConversationAsync(emitStartConversationEvent: true, cancellationToken))
                {
                    // Consume start conversation activities
                }
                _conversationStarted = true;
            }

            var responseMessages = new List<ChatMessage>();
            var responseText = new StringBuilder();

            await foreach (var activity in _copilotClient.AskQuestionAsync(lastMessage.Text ?? string.Empty, ct: cancellationToken))
            {
                if (activity.Type == "message" && !string.IsNullOrEmpty(activity.Text))
                {
                    responseText.AppendLine(activity.Text);
                }
            }

            if (responseText.Length > 0)
            {
                responseMessages.Add(new ChatMessage(ChatRole.Assistant, responseText.ToString().Trim()));
            }

            var usage = new UsageDetails 
            { 
                InputTokenCount = EstimateTokenCount(lastMessage.Text ?? string.Empty),
                OutputTokenCount = EstimateTokenCount(responseText.ToString())
            };

            return new ChatResponse(responseMessages) 
            { 
                Usage = usage,
                CreatedAt = DateTimeOffset.UtcNow,
                ModelId = "CopilotStudio"
            };
        }

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var lastMessage = messages.LastOrDefault();
            if (lastMessage == null)
            {
                throw new ArgumentException("At least one message is required", nameof(messages));
            }

            if (!_conversationStarted)
            {
                await foreach (var activity in _copilotClient.StartConversationAsync(emitStartConversationEvent: true, cancellationToken))
                {
                    // Consume start conversation activities
                }
                _conversationStarted = true;
            }

            var completionId = Guid.NewGuid().ToString();
            var createdAt = DateTimeOffset.UtcNow;

            await foreach (var activity in _copilotClient.AskQuestionAsync(lastMessage.Text ?? string.Empty, ct: cancellationToken))
            {
                if (activity.Type == "message" && !string.IsNullOrEmpty(activity.Text))
                {
                    yield return new ChatResponseUpdate
                    {
                        CreatedAt = createdAt,
                        Contents = [new TextContent(activity.Text)],
                        Role = ChatRole.Assistant
                    };
                }
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
            }
        }

        public TService? GetService<TService>(object? key = null) where TService : class
        {
            if (typeof(TService) == typeof(CopilotClient))
            {
                return _copilotClient as TService;
            }
            
            return null;
        }

        object? IChatClient.GetService(Type serviceType, object? key)
        {
            if (serviceType == typeof(CopilotClient))
            {
                return _copilotClient;
            }
            
            return null;
        }

        private static int EstimateTokenCount(string text)
        {
            // Simple token estimation - roughly 4 characters per token for English text
            return string.IsNullOrEmpty(text) ? 0 : Math.Max(1, text.Length / 4);
        }

        public void Dispose()
        {
            // CopilotClient doesn't implement IDisposable, so nothing to dispose
        }
    }
}