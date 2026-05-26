using Aucy.Application;
using Aucy.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

DotNetEnv.Env.Load();

builder.Services.AddMagicOnion();
builder.AddDataServices();

//builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
//    .AddJwtBearer(options =>
//    {
//        options.TokenValidationParameters = new TokenValidationParameters
//        {
//            // TODO: Add validation
//            NameClaimType = "user_id"
//        };

//        options.Events = new JwtBearerEvents
//        {
//            OnTokenValidated = context =>
//            {
//                var userId = context.Principal?.FindFirst("user_id")?.Value;

//                if (string.IsNullOrEmpty(userId))
//                {
//                    context.Fail("Token must contain 'user_id' claim");
//                    return Task.CompletedTask;
//                }

//                if (!Guid.TryParse(userId, out _))
//                {
//                    context.Fail("Invalid user_id format");
//                    return Task.CompletedTask;
//                }

//                context.HttpContext.Items["UserId"] = userId;

//                return Task.CompletedTask;
//            },

//            OnChallenge = context =>
//            {
//                context.HandleResponse();
//                context.Response.StatusCode = 401;
//                context.Response.ContentType = "application/json";

//                return context.Response.WriteAsync(
//                    "{\"error\":\"Authentication required\",\"message\":\"Please provide a valid session token\"}");
//            }
//        };
//    });

//builder.Services.AddAuthorization();

var app = builder.Build();

app.UseHttpsRedirection();

//app.UseAuthentication();
//app.UseAuthorization();

app.MapMagicOnionService<AuctionService>();

app.Run();
