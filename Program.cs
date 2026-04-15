using Azure.Identity;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.FeatureManagement;
using Microsoft.Identity.Web;
using Microsoft.Identity.Web.UI;
using MPC.PlanSched.Common;
using MPC.PlanSched.Model;
using MPC.PlanSched.Shared.Common;
using MPC.PlanSched.Shared.Common.Extensions;
using MPC.PlanSched.Shared.Common.Service;
using MPC.PlanSched.Shared.Notification.Interface;
using MPC.PlanSched.Shared.Notification.Service;
using MPC.PlanSched.UI;
using MPC.PlanSched.UI.Pages;
using MPC.PlanSched.UI.Services;
using MPC.PlanSched.UI.Shared;
using MPC.PlanSched.UI.ViewModel;
using System.IdentityModel.Tokens.Jwt;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);
var configurationBuilder = new ConfigurationBuilder();
var userRoleSettingsPath = Path.Combine(Directory.GetCurrentDirectory(), "Settings//UserRoleManagementSettings.json");
configurationBuilder.AddJsonFile(userRoleSettingsPath, false);
var root = configurationBuilder.Build();

// Get user roles and policies from UserRoleManagementSettings.json
var applicationPolicies = root.GetSection("RoleManagement:Policies").Get<List<UserRolePolicy>>()
    ?? throw new ArgumentException("'RoleManagement:Policies' is not defined in application settings");

// Set up KeyVault service to fetch AppConfigConnectionString
builder.Services.AddMemoryCache();
builder.Services.AddOptions();
builder.Services.AddScoped<IPopupService, PopupService>();

#region GetAppSettings
// Add appsettings.json to configuration
builder.Configuration.AddJsonFile("appsettings.json");
builder.Configuration.AddJsonFile("Settings/NavigationMenuViews.json");

// Connect to Azure App Configuration Resource to retrieve feature flags
var endpoint = builder.Configuration.GetValue<string>("AppSettings:AppConfigurationEndpoint")
    ?? throw new InvalidOperationException("The setting `AppSettings:AppConfigurationEndpoint` was not found.");

builder.Configuration.AddAzureAppConfiguration(options =>
{
    options.Connect(new Uri(endpoint), new VisualStudioCredential())
           .UseFeatureFlags();
    options.ConfigureKeyVault(keyVaultOptions =>
    {
        keyVaultOptions.SetCredential(new VisualStudioCredential());
    });
});

// Overwrite appsettings with development settings if running locally
if (Environment.GetEnvironmentVariable("WEBSITE_SITE_NAME") == null)
{
    builder.Configuration.AddJsonFile("appsettings.Development.json")
        .AddJsonFile(userRoleSettingsPath, true, true);

}
builder.Services.Configure<BaseAppSettings>(builder.Configuration.GetSection("AppSettings"));
#endregion GetAppSettings

#region ConfigureServices
builder.Services.AddDataProtection()
    .PersistKeysToAzureBlobStorage(new Uri(builder.Configuration["AppSettings:DataProtectionBlobUri"]), new VisualStudioCredential())
    .ProtectKeysWithAzureKeyVault(new Uri(builder.Configuration["AppSettings:KeyVaultEncryptionKeyUri"]), new VisualStudioCredential());

builder.Services.AddScoped<IHttpClientWrapper, HttpClientWrapper>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<IUINotificationService, UINotificationService>();
builder.Services.AddAzureAppConfiguration();
builder.Services.AddFeatureManagement();

JwtSecurityTokenHandler.DefaultMapInboundClaims = false;
builder.Services.AddAuthentication(OpenIdConnectDefaults.AuthenticationScheme)
    .AddMicrosoftIdentityWebApp(builder.Configuration.GetSection("AzureAd"));
builder.Services.AddControllersWithViews()
    .AddJsonOptions(o => o.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.Preserve)
    .AddMicrosoftIdentityUI();
builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
    foreach (var rolePolicy in applicationPolicies)
    {
        options.AddPolicy(rolePolicy.Policy, policy => policy.RequireRole(rolePolicy.Role));
    }
});
builder.Services.Configure<CookieAuthenticationOptions>(CookieAuthenticationDefaults.AuthenticationScheme, options =>
{
    options.Events.OnRedirectToAccessDenied = new Func<RedirectContext<CookieAuthenticationOptions>, Task>(context =>
    {
        context.Response.Redirect("/AccessDenied");
        return context.Response.CompleteAsync();
    });
});

builder.Services.AddRazorComponents(o => o.DetailedErrors = true)
    .AddInteractiveServerComponents()
    .AddMicrosoftIdentityConsentHandler();
builder.Services.AddSingleton<CommonModel>();
builder.Services.AddScoped<CommonBase>();
builder.Services.AddScoped<PlanBaseComponent>();
builder.Services.AddTransient<IExcelUtility, ExcelUtilityModel>();
builder.Services.AddTransient<ExcelUtilityModel>();
builder.Services.AddTransient<IExcelUtilityPIMSModel, ExcelUtilityPIMSModel>();
builder.Services.AddTransient<ExcelUtilityPIMSModel>();
builder.Services.AddTransient<IExcelUtilityDPOModel, ExcelUtilityDPOModel>();
builder.Services.AddTransient<ExcelUtilityDPOModel>();
builder.Services.AddTransient<IExcelUtilityPPIMSModel, ExcelUtilityPPIMSModel>();
builder.Services.AddTransient<ExcelUtilityPPIMSModel>();
builder.Services.AddScoped<CookieService>();
builder.Services.AddTransient<INavigationMenuService, NavigationMenuService>();
builder.Services.AddScoped<NavigationMenuService>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped(c => c.GetRequiredService<IHttpContextAccessor>().HttpContext?.User ?? throw new Exception("We need a user. Where is our user?"));
builder.Services.AddScoped<IUserSettingsProvider, UserSettingsProvider>();
builder.Services.AddScoped(ctx => ctx.GetRequiredService<IUserSettingsProvider>().GetAsync().GetAwaiter().GetResult());
builder.Services.AddScoped<IActiveUser, ActiveUser>();
builder.Services.AddCascadingValue(sp => sp.GetRequiredService<IActiveUser>());
builder.Services.AddSingleton<IClaimsTransformation, DebugClaimsTransformation>();

builder.Services.AddHttpClient("WebAPI", Client =>
{
    Client.BaseAddress = new Uri("http://localhost:7169/");
    Client.DefaultRequestHeaders.Add("Accept", "application/json");
    Client.Timeout = TimeSpan.FromSeconds(1800);
});
builder.Services.AddHttpClient("ExcelAPI", Client =>
{
    Client.BaseAddress = new Uri("http://localhost:7169/");
    Client.DefaultRequestHeaders.Add("Accept", "application/json");
    Client.Timeout = TimeSpan.FromSeconds(1800);
});
builder.Services.AddTelerikBlazor();
builder.Services.AddTransient<ISessionService, SessionService>();
builder.Services.AddTransient<IOverrideValueCalculatorService, OverrideValueCalculatorService>();
builder.Services.AddTransient<ICommonUtilityService, CommonUtilityService>();
builder.Services.AddTransient<IExcelCommon, ExcelCommon>();
builder.Services.AddTransient<CommonUtilityService>();
builder.Services.AddTransient<SessionService>();
builder.Services.AddApplicationInsightsTelemetry();
builder.Services.AddSession();
builder.Services.AddTransient(provider =>
{
    var loggerFactory = provider.GetRequiredService<ILoggerFactory>();
    const string categoryName = "MPC.Interceptor.Planning&SchedulingUI.Log";
    return loggerFactory.CreateLogger(categoryName);
});
var configuration = builder.Services.BuildServiceProvider().GetRequiredService<IConfiguration>();
builder.Services.AddSingleton<IServiceBusClient>(
               sb => new AzureServiceBusClient(configuration["AppSettings:" + ServiceBus.Notification.Description()]));
/// Register a proxy for IMyService that uses the MethodInterceptor.
#endregion ConfigureServices

var app = builder.Build();
ConfigurationUI.Build_ConfigurationUI(builder.Configuration);
/// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}
app.UseAzureAppConfiguration();
app.UseAuthentication();
app.UseAuthorization();
app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAntiforgery();
app.UseSession();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();
app.Run();