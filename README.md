# FitbitAuth
L'invocazione ai servizi REST Fitbit preve che ogni chiamata sia accompagnata da un "access token" che autorizza l'app a raccogliere (ed eventualmente modificare) i dati di uno specifico utente. 

L'utente, dopo essersi autententicato su Fitbit, dando il consenso autorizza l'applicazione ad accedere ai suoi dati via API REST. La fase di autorizzazione consente all'utente di decidere a quali dati l'applicazione è autorizzata ad accedere.

Per ogni utente autenticato l'applicazione riceve un access token diverso che potrà memorizzare e utilizzare per accedere ai dati di quello specifico utente.

L'access token, una volta generato, ha una scadenza (di default 1 giorno)


1. Creare un account Fitbit se non lo si possiede già
2. Navigare all'indirizzo https://dev.fitbit.com/
3. Accedere a Manage > Register an App
4. Creare un'applicazione valorizzando tutti i parametri obbligatori; i parametri importanti sono
	- OAuth 2.0 Application Type = Client
	- Callback URL = URL su cui l'utente verrà rediretto da Fitbit una volta autenticato. Deve essere una URL accessibile all'utente che fa login a cui risponde l'applicazione ASP.NET MVC descritta a seguire. Es. _http://localhost:52153/signin_
	- Default Access Type = Read & Write
5. Dopo aver salvato, Fitbit genenerà alcuni parametri univoci per la vostra applicazione, quelli importanti sono "OAuth 2.0 Client ID" e "Client Secret"
6. Creare un'applicazione ASP.NET Core MVC
7. Referenziare il package nuget _Microsoft.AspNetCore.Authentication.OAuth_ (contiene l'implementazione "core" del protocollo OAuth).
8. Nella finestra che appare quando si sceglie "Manage NuGet Packages for Solution...", flaggare "Include prelease" (il package che implementa OAuth per Fitbit è ad oggi disponibile solo in "beta") e quindi referenziare il package _AspNet.Security.OAuth.Fitbit_
9. Si può quindi togliere la spunta a "Include prelease" per evitare di aggiungere involontariamente, in futuro, versioni non RTM (Release To Manifacturing)
10. Modifichiamo ora _Startup.cs_ per gestire il callback in cui verrà restituito l'access token che dovrà essere utilizzato per accompagnare tutte le chiamate successive alle API REST Fitbit; l'access token è la chiave di accesso ai dati per i quali l'utente ha concesso l'autorizzazione. L'a

```csharp
using Microsoft.AspNetCore.Authentication.Cookies;
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
                // "OAuth 2.0 Client ID" e "Client Secret" restituiti in fase di creazione dell'app su https://dev.fitbit.com/
                options.ClientId = "22CT28";
                options.ClientSecret = "04f7ff76b81dde4e907e1a5ab68bbba3";

                // Importante per poter recuperare successivamente l'access token dell'utente.
                options.SaveTokens = true;

                options.Events.OnCreatingTicket = async context =>
                {
                    var accessToken = context.AccessToken;

                    // Questo è il punto in cui è possibile salvare l'access token dell'utente.
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

11. 



Riferimenti: https://github.com/aspnet-contrib/AspNet.Security.OAuth.Providers
