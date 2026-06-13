using Aucy.Application.Auth;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Aucy.API.Extensions;

public static class ApiAuthExtension
{
    public static void AddApiAuth(this IServiceCollection services)
    {
        var jwtOptions = new JwtOptions
        {
            Secretkey = Environment.GetEnvironmentVariable("JwtOptions__SecretKey")!,
            ExpireHours = 12
        };

        services.AddSingleton(Options.Create(jwtOptions));

        services.AddScoped<IJwtProvider, JwtProvider>();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = false,
                    ValidateAudience = false,
                    ValidateLifetime = jwtOptions.ExpireHours.HasValue,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(
                        System.Text.Encoding.UTF8.GetBytes(
                            Environment.GetEnvironmentVariable("JwtOptions__SecretKey")!)
                    ),
                    NameClaimType = "user_id"
                };

                //options.Events = new JwtBearerEvents
                //{
                //    OnTokenValidated = context =>
                //    {
                //        var userId = context.Principal?.FindFirst("user_id")?.Value;

                //        if (string.IsNullOrEmpty(userId))
                //        {
                //            context.Fail("Token must contain 'user_id' claim");
                //            return Task.CompletedTask;
                //        }

                //        if (!Guid.TryParse(userId, out _))
                //        {
                //            context.Fail("Invalid user_id format");
                //            return Task.CompletedTask;
                //        }

                //        context.HttpContext.Items["UserId"] = userId;

                //        return Task.CompletedTask;
                //    },

                //    OnChallenge = context =>
                //    {
                //        context.HandleResponse();
                //        context.Response.StatusCode = 401;
                //        context.Response.ContentType = "application/json";

                //        return context.Response.WriteAsync(
                //            "{\"error\":\"Authentication required\",\"message\":\"Please provide a valid session token\"}");
                //    }
                //};
            });

        services.AddAuthorization();
    }
}
