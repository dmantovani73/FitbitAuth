# FitbitAuth
L'invocazione ai servizi REST Fitbit prevede che ogni chiamata sia accompagnata da un "access token" che autorizza l'app a raccogliere (ed eventualmente modificare) i dati di uno specifico utente. 

L'utente, dopo essersi autententicato su Fitbit, dando il consenso autorizza l'applicazione ad accedere ai suoi dati via API REST. La fase di autorizzazione consente all'utente di decidere a quali dati l'applicazione è autorizzata ad accedere.

Per ogni utente autenticato l'applicazione riceve un access token diverso che potrà memorizzare e utilizzare per accedere ai dati di quello specifico utente.

L'access token, una volta generato, ha una scadenza (di default 1 giorno).

Vediamo ora come creare un'applicazione ASP.NET Core MVC in grado di:
* Permettere ad un utente di autenticarsi tramite credenziali Fitbit
* Come ottenere un accesso token per quell'utente.

Un procedimento analogo si applica a tutti i siti che implementano OAuth (https://it.wikipedia.org/wiki/OAuth).

1. Creare un account Fitbit (se già non lo si possiede)
2. Navigare all'indirizzo https://dev.fitbit.com/
3. Accedere a Manage > Register an App
4. Creare un'applicazione valorizzando tutti i parametri obbligatori; i parametri importanti sono
	- OAuth 2.0 Application Type = Client
	- Callback URL = URL su cui l'utente verrà rediretto da Fitbit una volta autenticato. Deve essere una URL accessibile all'utente che fa login a cui risponde l'applicazione ASP.NET MVC descritta a seguire. Es. _https://localhost:44320/signin-fitbit_
	- Default Access Type = Read & Write
5. Dopo aver salvato, Fitbit genenerà alcuni parametri univoci per la vostra applicazione, quelli importanti sono "OAuth 2.0 Client ID" e "Client Secret"
6. Creare un'applicazione ASP.NET Core MVC
7. Referenziare il package nuget _Microsoft.AspNetCore.Authentication.OAuth_ (contiene l'implementazione "core" del protocollo OAuth).
8. Nella finestra che appare quando si sceglie "Manage NuGet Packages for Solution...", flaggare "Include prelease" (il package che implementa OAuth per Fitbit è ad oggi disponibile solo in "beta") e quindi referenziare il package _AspNet.Security.OAuth.Fitbit_
9. Si può quindi togliere la spunta a "Include prelease" per evitare di aggiungere involontariamente, in futuro, versioni non RTM (Release To Manifacturing)
10. Modifichiamo ora _Startup.cs_ per gestire il callback in cui verrà restituito l'access token che dovrà essere utilizzato per accompagnare tutte le chiamate successive alle API REST Fitbit; l'access token è la chiave di accesso ai dati per i quali l'utente ha concesso l'autorizzazione.

```csharp
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace FitbitAuth
{
    public class Startup
    {
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddAuthentication(options =>
            {
                options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
            })

            .AddCookie(options =>
            {
                options.LoginPath = "/login";
                options.LogoutPath = "/signout";
            })

            .AddFitbit(options =>
            {
                // https://dev.fitbit.com/build/reference/web-api/oauth2/
                // https://dev.fitbit.com/
                options.ClientId = "<OAuth 2.0 Client ID>";
                options.ClientSecret = "<Client Secret>";

                // Importante per avere accesso all'access token.
                options.SaveTokens = true;

                options.Events = new OAuthEvents
                {
                    OnCreatingTicket = async context =>
                    {
                        // Qui è possibile salvare il token.
                        var accessToken = context.AccessToken;
                    }
                };
            });

            services.AddMvc();
        }

        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseStaticFiles();
            app.UseAuthentication();
            app.UseMvc();

            app.Run(async (context) =>
            {
                await context.Response.WriteAsync("oops something went wrong...");
            });
        }
    }
}
```

11. Aggiungere il folder _Extensions_ e all'interno il file _HttpContextExtensions.cs_:

```csharp
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace FitbitAuth.Extensions
{
    public static class HttpContextExtensions
    {
        public static async Task<AuthenticationScheme[]> GetExternalProvidersAsync(this HttpContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            var schemes = context.RequestServices.GetRequiredService<IAuthenticationSchemeProvider>();

            return (from scheme in await schemes.GetAllSchemesAsync()
                    where !string.IsNullOrEmpty(scheme.DisplayName)
                    select scheme).ToArray();
        }

        public static async Task<bool> IsProviderSupportedAsync(this HttpContext context, string provider)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            return (from scheme in await context.GetExternalProvidersAsync()
                    where string.Equals(scheme.Name, provider, StringComparison.OrdinalIgnoreCase)
                    select scheme).Any();
        }
    }
}
```

12. Aggiungere il controller _AuthenticationController_ che espone l'endpoint /sigin su cui Fitbit fa callback in fase di autenticazione:

```csharp
using System.Threading.Tasks;
using FitbitAuth.Extensions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;

namespace FitbitAuth.Controllers
{
    public class AuthenticationController : Controller
    {
        [HttpGet("~/signin")]
        public async Task<IActionResult> SignIn() => View("SignIn", await HttpContext.GetExternalProvidersAsync());

        [HttpPost("~/signin")]
        public async Task<IActionResult> SignIn([FromForm] string provider)
        {
            // Note: the "provider" parameter corresponds to the external
            // authentication provider choosen by the user agent.
            if (string.IsNullOrWhiteSpace(provider))
            {
                return BadRequest();
            }

            if (!await HttpContext.IsProviderSupportedAsync(provider))
            {
                return BadRequest();
            }

            // Instruct the middleware corresponding to the requested external identity
            // provider to redirect the user agent to its own authorization endpoint.
            // Note: the authenticationScheme parameter must match the value configured in Startup.cs
            return Challenge(new AuthenticationProperties { RedirectUri = "/" }, provider);
        }

        [HttpGet("~/signout"), HttpPost("~/signout")]
        public IActionResult SignOut()
        {
            // Instruct the cookies middleware to delete the local cookie created
            // when the user agent is redirected from the external identity provider
            // after a successful authentication flow (e.g Google or Facebook).
            return SignOut(new AuthenticationProperties { RedirectUri = "/" },
                CookieAuthenticationDefaults.AuthenticationScheme);
        }
    }
}
```
13. Aggiungere il controller _HomeController_:

```csharp
using Microsoft.AspNetCore.Mvc;

namespace FitbitAuth.Controllers
{
    public class HomeController : Controller
    {
        [HttpGet("~/")]
        public IActionResult Index() => View();
    }
}
```

14. Aggiungere il folder _Views_ e quindi all'interno _Shared_. In _Shared_ aggiungere __Layout.cshtml_:

```csharp
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="utf-8" />
    <meta http-equiv="X-UA-Compatible" content="IE=edge" />
    <meta name="viewport" content="width=device-width, initial-scale=1" />
    <meta name="description" content="" />
    <meta name="author" content="" />

    <title>@ViewBag.Title</title>

    <link href="//maxcdn.bootstrapcdn.com/bootstrap/3.3.7/js/bootstrap.min.js" rel="stylesheet" />
</head>

<body>
    <div class="container">
        @RenderBody()
    </div>
</body>
</html>
```

15. Nel folder _Views_ aggiungere il file __ViewStart.cshtml_:

```csharp
@{
    Layout = "_Layout";
}
```

16. Creare il folder _Views > Home_ e aggiungere il file _Index.cshtml_:

```csharp
@model string
@using Microsoft.AspNetCore.Authentication

<div>
    @if (User?.Identity?.IsAuthenticated ?? false)
    {
        <h1>Welcome, @User.Identity.Name</h1>

        <p>Access Token: @await Context.GetTokenAsync("access_token")</p>
        <p>
            @foreach (var claim in Context.User.Claims)
            {
                <div>@claim.Type: <b>@claim.Value</b></div>
            }
        </p>

        <a class="btn btn-lg btn-danger" href="/signout?returnUrl=%2F">Sign out</a>
    }

    else
    {
        <h1>Welcome, anonymous</h1>
        <a class="btn btn-lg btn-success" href="/signin?returnUrl=%2F">Sign in</a>
    }
</div>
```

17. Creare il folder _Views > Authentication_ e aggiungere il file _SignIn.cshtml_:

```csharp
@using Microsoft.AspNetCore.Authentication
@model AuthenticationScheme[]

<div>
    <h1>Authentication</h1>
    <p class="lead text-left">Sign in using one of these external providers:</p>

    @foreach (var scheme in Model)
    {
        <form action="/signin" method="post">
            <input type="hidden" name="Provider" value="@scheme.Name" />
            <input type="hidden" name="ReturnUrl" value="@ViewBag.ReturnUrl" />

            <button class="btn btn-lg btn-success" type="submit">Connect using @scheme.DisplayName</button>
        </form>
    }
</div>
```

18. Fare click con il tasto destro sul progetto > Properties. Selezionare il tab _Debug_ e flaggare la voce _Enable SSL_. La URL base (es. https://localhost:44320/) deve essere utilizzata per sistemare opportunamente la _Callback URL_ dell'app Fitbit che quindi risulterà essere qualcosa del tipo: https://localhost:44320/signin-fitbit.

19. Lanciare quindi la webapp e fare login.

# Riferimenti
* https://github.com/aspnet-contrib/AspNet.Security.OAuth.Providers
* https://dev.fitbit.com/
* https://dev.fitbit.com/build/reference/web-api/oauth2/
