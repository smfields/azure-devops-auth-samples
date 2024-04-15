using System.IdentityModel.Tokens.Jwt;
using Azure.Core;
using Azure.Identity;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi;
using Microsoft.VisualStudio.Services.Client;
using Microsoft.VisualStudio.Services.WebApi;
using System.Net.Http.Headers;

namespace ServicePrincipalsSamples
{
    public static class Program
    {
        public const string AdoBaseUrl = "https://microsoft.visualstudio.com/";
        public const string AdoOrgName = "OS";
        public static readonly TimeSpan Delay = TimeSpan.FromMinutes(5);
        
        public const string AadTenantId = "72f988bf-86f1-41af-91ab-2d7cd011db47";
        // ClientId for User Assigned Managed Identity. Leave null for System Assigned Managed Identity
        public const string AadUserAssignedManagedIdentityClientId = null;

        public static List<ProductInfoHeaderValue> AppUserAgent { get; } = new()
        {
            new ProductInfoHeaderValue("Identity.ManagedIdentitySamples", "1.0"),
            new ProductInfoHeaderValue("(2-ConsoleApp-ManagedIdentity)")
        };

        public static async Task Main()
        {
            int workItemId = 49815058;
            Console.WriteLine($"Work item ID: {workItemId}");

            Console.WriteLine("Getting token");
            var accessToken = await GetManagedIdentityAccessToken();
            var token = new VssAadToken("Bearer", accessToken.Token);
            var credentials = new VssAadCredential(token);
            var connection = new VssConnection(new Uri(AdoBaseUrl), credentials);

            var attemptNumber = 1;
            while (true)
            {
                try
                {
                    Console.WriteLine($" --- Attempt {attemptNumber} ---");
                    Console.WriteLine($"Time to Live: {accessToken.ExpiresOn - DateTimeOffset.UtcNow}");

                    Console.WriteLine("Getting HTTP Client");
                    var workItemTrackingHttpClient = await connection.GetClientAsync<WorkItemTrackingHttpClient>();

                    Console.WriteLine("Getting work item");
                    var workItem = await workItemTrackingHttpClient.GetWorkItemAsync(workItemId);

                    Console.WriteLine($"Work Item Title: {workItem.Fields["System.Title"]}");
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }
                finally
                {
                    Console.WriteLine($" --- Attempt {attemptNumber++} Complete ---");
                    Console.WriteLine($"Next attempt at: {DateTimeOffset.Now + Delay}");
                    await Task.Delay(Delay);
                }
            }
        }

        // private static async Task<VssConnection> CreateVssConnection()
        // {
        //
        //
        //     var settings = VssClientHttpRequestSettings.Default.Clone();
        //     settings.UserAgent = AppUserAgent;
        //
        //     var organizationUrl = new Uri(new Uri(AdoBaseUrl), AdoOrgName);
        //     return new VssConnection(organizationUrl, credentials, settings);
        // }

        private static async Task<AccessToken> GetManagedIdentityAccessToken()
        {
            // DefaultAzureCredential will use VisualStudioCredentials or other appropriate credentials for local development
            // but will use ManagedIdentityCredential when deployed to an Azure Host with Managed Identity enabled.
            // https://learn.microsoft.com/en-us/dotnet/api/overview/azure/identity-readme?view=azure-dotnet#defaultazurecredential
            var credential =
                new DefaultAzureCredential(
                    new DefaultAzureCredentialOptions
                    {
                        TenantId = AadTenantId,
                        ManagedIdentityClientId = AadUserAssignedManagedIdentityClientId,
                        ExcludeEnvironmentCredential = true // Excluding because EnvironmentCredential was not using correct identity when running in Visual Studio
                    });

            var tokenRequestContext = new TokenRequestContext(VssAadSettings.DefaultScopes);
            var token = await credential.GetTokenAsync(tokenRequestContext, CancellationToken.None);

            return token;
        }
    }
}