﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Blazored.LocalStorage;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using MongoDB.Driver;
using MongoDB.Entities;
using Newtonsoft.Json;
using Swashbuckle.AspNetCore.SwaggerUI;
using UnoRoute.Sket.Core.Entity;
using UnoRoute.Sket.Core.Init;
using UnoRoute.Sket.Core.Manager;
using UnoRoute.Sket.Core.Middleware;
using UnoRoute.Sket.Core.Repository;
using UnoRoute.Sket.Core.Repository.Interfaces;

namespace UnoRoute.Sket.Core
{
    /// <summary>
    ///     This extension class is used for Dependency injections
    /// </summary>
    public static class Extension
    {
        private static SketSettings _settings;

        /// <summary>
        ///     Initial setup for Sket for dependency injection.
        /// </summary>
        /// <param name="services"></param>
        /// <param name="settings"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public static IServiceCollection AddSket(
            this IServiceCollection services, SketSettings settings)
        {
            _settings = settings;
            Init.Sket.SketServices = services;
     
            var sketFile = Path.Join(Environment.CurrentDirectory, "Sket.Config.json");

            switch (settings)
            {
                case null:
                {
                    if (File.Exists(sketFile))
                    {
                        settings = JsonConvert.DeserializeObject<SketSettings>(File.ReadAllText(sketFile));
                        Sket.Core.Init.Sket.Cfg.Settings = settings;
                        SetupDb();
                      SketServices(services, settings);
                    }
                    else
                    {
                        // goto setup page
                        Sket.Core.Init.Sket.Cfg.Settings = null;
                    }

                    break;
                }
                default:
                    DefaultCheck();
                    Sket.Core.Init.Sket.Cfg.Settings = settings;
                    SetupDb();
                    SketServices(services, settings);
                    
                    break;
            }

            #region Check Setup Section

            void DefaultCheck()
            {
                if (string.IsNullOrEmpty(settings.JwtKey)) throw new Exception("JwtKey is required");
                // if (string.IsNullOrEmpty(settings.DomainUrl)) throw new Exception("DomainUrl is required");
            }

            #endregion

            #region Db Section

            void SetupDb()
            {
                DB.InitAsync(settings.DatabaseName, string.IsNullOrEmpty(settings.ConnectionString)
                    ? new MongoClientSettings {Server = new MongoServerAddress("localhost", 27017)}
                    : MongoClientSettings.FromConnectionString(settings.ConnectionString));
            }

            #endregion
            
            return services;
        }

        private static void SketServices(IServiceCollection services, SketSettings settings)
        {
            services.AddHttpClient();
            services.AddHttpContextAccessor();
            services.TryAddScoped(typeof(ISketAccessTokenRepository<>), typeof(SketAccessTokenRepository<>));
            services.TryAddScoped(typeof(ISketRoleRepository<>), typeof(SketRoleRepository<>));
            services.TryAddScoped(typeof(ISketUserRepository<>), typeof(SketUserRepository<>));
            services.TryAddScoped(typeof(ISketAuthenticationManager<>), typeof(SketAuthenticationManager<>));

       
            services.Add(new ServiceDescriptor(typeof(SketConfig), Init.Sket.Init(settings)));

            services.AddBlazoredLocalStorage(config =>
                config.JsonSerializerOptions.WriteIndented = true);

            services.AddAuthorizationCore(option =>
            {
                var normalRole = Enum.GetValues(typeof(SketRoleEnum)).Cast<SketRoleEnum>();
                foreach (var sketRoleEnum in normalRole)
                    option.AddPolicy(sketRoleEnum.ToString(),
                        policy => policy.RequireRole(sketRoleEnum.ToString()));
            });

            //Swagger section
            services.AddSwaggerGen(c =>
            {
                c.UseOneOfForPolymorphism();
                c.SelectDiscriminatorNameUsing(baseType => "TypeName");
                c.SelectDiscriminatorValueUsing(subType => subType.Name);
                // c.UseOneOfForPolymorphism();
                c.SwaggerDoc("v1", new OpenApiInfo
                {
                    Title = "API Explorer",
                    Version = "v1"
                });

                c.AddSecurityDefinition("Auth", new OpenApiSecurityScheme
                {
                    BearerFormat = JwtBearerDefaults.AuthenticationScheme,
                    In = ParameterLocation.Header,
                    Scheme = JwtBearerDefaults.AuthenticationScheme,
                    Type = SecuritySchemeType.ApiKey
                });
            });


            #region Middlewares Section

            services.TryAddTransient<SketTokenHeaderHandler>();

            #endregion


            #region Security Section

            services.AddDataProtection();

            void AddCookies()
            {
                services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
                    .AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, c =>
                    {
                        c.Cookie.Name = Init.Sket.Cfg.Settings.AuthType.ToString();
                        c.LoginPath = "/login";
                        c.LogoutPath = "/login";
                    });
            }

            void AddJwt()
            {
                services.AddAuthentication(option =>
                    {
                        option.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                        option.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
                    })
                    .AddJwtBearer(options =>
                    {
                        options.TokenValidationParameters = new TokenValidationParameters
                        {
                            ValidateIssuer = true,
                            ValidateAudience = true,
                            ValidateLifetime = false,
                            ValidateIssuerSigningKey = true,
                            ValidIssuer = settings.DomainUrl,
                            ValidAudience = settings.DomainUrl,
                            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.JwtKey))
                        };
                        options.SaveToken = true;

                        options.Events = new JwtBearerEvents
                        {
                            OnAuthenticationFailed = context =>
                            {
                                if (context.Exception.GetType() == typeof(SecurityTokenExpiredException))
                                    context.Response.Headers.Add("Token-Expired", "true");
                                return Task.CompletedTask;
                            }
                        };
                    })
                    .AddCookie(Init.Sket.Cfg.Settings.AuthType.ToString(), c =>
                    {
                        c.Cookie.Name = Init.Sket.Cfg.Settings.AuthType.ToString();
                        c.LoginPath = "/login";
                        c.LogoutPath = "/login";
                    });
            }

            void AddBoth()
            {
                AddJwt();
                AddCookies();
            }


            switch (settings.AuthType)
            {
                case AuthType.Jwt:
                    AddJwt();
                    break;
                case AuthType.Both:
                    AddBoth();
                    break;
                default:
                    AddCookies();
                    break;
            }

            #endregion


            #region CORS security Section

            if (settings.CorsDomains is not null)
                services.AddCors(options =>
                {
                    options.AddPolicy("Custom", builder =>
                    {
                        foreach (var domains in settings.CorsDomains)
                            builder.WithOrigins(domains).AllowAnyHeader().AllowAnyMethod();
                    });
                });

            #endregion
        }

        public static IServiceCollection AddSket(this IServiceCollection services)
        {
            AddSket(services, null);

            return services;
        }

        /// <summary>
        ///     Initial setup for Sket middleware
        /// </summary>
        /// <param name="app"></param>
        /// <returns></returns>
        public static IApplicationBuilder UseSket(this IApplicationBuilder app)
        {
            app.UseReDoc(c =>
            {
                c.RoutePrefix = "docs";
                c.DocumentTitle = "My API Docs";
                c.SpecUrl("/v1/swagger.json");
                c.EnableUntrustedSpec();
                c.ScrollYOffset(10);
                c.HideHostname();
                c.HideDownloadButton();
                c.ExpandResponses("200,201");
                c.RequiredPropsFirst();
                c.NoAutoAuth();
                c.PathInMiddlePanel();
                c.HideLoading();
                c.NativeScrollbars();
                c.DisableSearch();
                c.OnlyRequiredInSamples();
                c.SortPropsAlphabetically();
            });

            if (_settings is not null)
                app.UseSwagger(d =>
                {
                    d.RouteTemplate = "explorer/{documentName}/swagger.json";
                    d.SerializeAsV2 = true;
                });

            if (_settings is not null)
                app.UseSwaggerUI(c =>
                {
                    c.RoutePrefix = "explorer";
                    c.DocumentTitle = "API Explorer";
                    c.SwaggerEndpoint("/explorer/v1/swagger.json", "API Explorer");
                    c.DocumentTitle = "Api Explorer";
                    c.EnableValidator("localhost");
                    c.EnableFilter();
                    c.EnableDeepLinking();

                    c.DocExpansion(DocExpansion.None);

                    c.OAuthClientId("test-id");
                    c.OAuthClientSecret("test-secret");
                    c.OAuthRealm("test-realm");
                    c.OAuthAppName("test-app");
                    c.OAuthScopeSeparator(" ");
                    c.OAuthAdditionalQueryStringParams(new Dictionary<string, string> {{"foo", "bar"}});
                    c.OAuthUseBasicAuthenticationWithAccessCodeGrant();
                });
            app.UseAuthentication();

            return app;
        }
    }
}