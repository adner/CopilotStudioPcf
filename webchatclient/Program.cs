using Microsoft.Extensions.AI;
using webchatclient.Components;
using webchatclient.Services;
using Microsoft.Agents.CopilotStudio.Client;
using System.Net.Http.Headers;
using System.Runtime.InteropServices;
using Microsoft.Identity.Client.Extensions.Msal;
using Microsoft.Identity.Client;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddRazorComponents().AddInteractiveServerComponents();

CopilotStudioConnectionSettings settings = new CopilotStudioConnectionSettings(builder.Configuration.GetSection("CopilotStudioClientSettings"));

// Plumbing for injecting the CopilotClient when an IChatClient is requested (The .NET AI Template that the client is based on uses IChatClient)
builder.Services
    .AddSingleton(settings)
    .AddTransient<CopilotClient>((s) =>
    {
        var logger = s.GetRequiredService<ILoggerFactory>().CreateLogger<CopilotClient>();
        return new CopilotClient(settings, s.GetRequiredService<IHttpClientFactory>(), logger, "mcs");
    });

// Register the CopilotStudio IChatClient as the primary chat client
builder.Services.AddChatClient(serviceProvider =>
{
    var copilotClient = serviceProvider.GetRequiredService<CopilotClient>();
    return new CopilotStudioIChatClient(copilotClient);
}).UseFunctionInvocation().UseLogging(); 

// Needed for Copilot Studio authentication.
builder.Services.AddHttpClient("mcs").ConfigurePrimaryHttpMessageHandler(() =>
{
    return new AddTokenHandler(settings);
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseAntiforgery();

app.UseStaticFiles();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();



internal class CopilotStudioConnectionSettings : ConnectionSettings
{
    /// <summary>
    /// Use S2S connection for authentication.
    /// </summary>
    public bool UseS2SConnection { get; set; } = false;

    /// <summary>
    /// Tenant ID for creating the authentication for the connection
    /// </summary>
    public string? TenantId { get; set; }
    /// <summary>
    /// Application ID for creating the authentication for the connection
    /// </summary>
    public string? AppClientId { get; set; }

    /// <summary>
    /// Application secret for creating the authentication for the connection
    /// </summary>
    public string? AppClientSecret { get; set; }

    /// <summary>
    /// Create ConnectionSettings from a configuration section.
    /// </summary>
    /// <param name="config"></param>
    /// <exception cref="ArgumentException"></exception>
    public CopilotStudioConnectionSettings(IConfigurationSection config) : base(config)
    {
        AppClientId = config[nameof(AppClientId)] ?? throw new ArgumentException($"{nameof(AppClientId)} not found in config");
        TenantId = config[nameof(TenantId)] ?? throw new ArgumentException($"{nameof(TenantId)} not found in config");

        UseS2SConnection = config.GetValue<bool>(nameof(UseS2SConnection), false);
        AppClientSecret = config[nameof(AppClientSecret)];

    }
}

 internal class AddTokenHandler(CopilotStudioConnectionSettings settings) : DelegatingHandler(new HttpClientHandler())
    {
        private static readonly string _keyChainServiceName = "copilot_studio_client_app";
        private static readonly string _keyChainAccountName = "copilot_studio_client";
        
        private async Task<AuthenticationResult> AuthenticateAsync(CancellationToken ct = default!)
        {
            ArgumentNullException.ThrowIfNull(settings);

            // Gets the correct scope for connecting to Copilot Studio based on the settings provided. 
            string[] scopes = [CopilotClient.ScopeFromSettings(settings)];

            // Setup a Public Client application for authentication.
            IPublicClientApplication app = PublicClientApplicationBuilder.Create(settings.AppClientId)
                 .WithAuthority(AadAuthorityAudience.AzureAdMyOrg)
                 .WithTenantId(settings.TenantId)
                 .WithRedirectUri("http://localhost")
                 .Build();

            string currentDir = Path.Combine(AppContext.BaseDirectory, "mcs_client_console");

            if (!Directory.Exists(currentDir))
            {
                Directory.CreateDirectory(currentDir);
            }

            StorageCreationPropertiesBuilder storageProperties = new("TokenCache", currentDir);
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                storageProperties.WithLinuxUnprotectedFile();
            }
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                storageProperties.WithMacKeyChain(_keyChainServiceName, _keyChainAccountName);
            }
            MsalCacheHelper tokenCacheHelper = await MsalCacheHelper.CreateAsync(storageProperties.Build());
            tokenCacheHelper.RegisterCache(app.UserTokenCache);

            IAccount? account = (await app.GetAccountsAsync()).FirstOrDefault();

            AuthenticationResult authResponse;
            try
            {
                authResponse = await app.AcquireTokenSilent(scopes, account).ExecuteAsync(ct);
            }
            catch (MsalUiRequiredException)
            {
                authResponse = await app.AcquireTokenInteractive(scopes).ExecuteAsync(ct);
            }
            return authResponse;
        }

        /// <summary>
        /// Handles sending the request and adding the token to the request.
        /// </summary>
        /// <param name="request">Request to be sent</param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.Headers.Authorization is null)
            {
                AuthenticationResult authResponse = await AuthenticateAsync(cancellationToken);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", authResponse.AccessToken);
            }
            return await base.SendAsync(request, cancellationToken);
        }
    }