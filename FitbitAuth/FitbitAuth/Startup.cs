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
                options.ClientId = "22CT8Q";
                options.ClientSecret = "a03c6ac373f96897212aced2b9baeb18";

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