namespace PhungDKH.Identity.Api
{
    using System.Collections.Generic;
    using Microsoft.AspNetCore.Builder;
    using Microsoft.AspNetCore.Hosting;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using PhungDKH.Core;
    using PhungDKH.Core.Constants;
    using PhungDKH.Core.Extensions;
    using PhungDKH.Identity.Api.Domain.Entities;
    using PhungDKH.Identity.Api.Domain.Entities.Contexts;
    using Serilog;
    using Microsoft.EntityFrameworkCore;
    using PhungDKH.Identity.Api.Infrastructure.Filters;
    using Microsoft.OpenApi.Models;
    using Microsoft.AspNetCore.Identity;
    using System.IdentityModel.Tokens.Jwt;
    using Microsoft.AspNetCore.Authentication.JwtBearer;
    using Microsoft.IdentityModel.Tokens;
    using System.Text;
    using System;
    using PhungDKH.Identity.Api.Domain;

    public class Startup
    {
        public Startup(IWebHostEnvironment env)
        {
            var builder = new ConfigurationBuilder()
              .SetBasePath(env.ContentRootPath)
              .AddJsonFile($"appsettings.{env.EnvironmentName}.json", optional: true, reloadOnChange: true);

            builder.AddEnvironmentVariables();
            Configuration = builder.Build();

            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Information()
                .WriteTo.Logger(log => log.Filter.ByIncludingOnly("@Level = 'Information'")
                    .WriteTo.RollingFile(
                        pathFormat: "Logs//Informations//log-information-{Date}.txt",
                        outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}"
                    ))
                .WriteTo.Logger(log => log.Filter.ByIncludingOnly("@Level = 'Warning'")
                    .WriteTo.RollingFile(
                        pathFormat: "Logs//Warnings//log-warning-{Date}.txt",
                        outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}"
                    ))
                .WriteTo.Logger(log => log.Filter.ByIncludingOnly("@Level = 'Error'")
                    .WriteTo.RollingFile(
                        pathFormat: "Logs//Errors//log-error-{Date}.txt",
                        outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}"
                    ))
                .CreateLogger();
        }

        /// <summary>
        /// 
        /// </summary>
        public static IConfigurationRoot Configuration;

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddControllers(
                options =>
                {
                    options.Filters.Add(typeof(HttpGlobalExceptionFilter));
                })
                .AddNewtonsoftJson(options =>
                    options.SerializerSettings.ReferenceLoopHandling = Newtonsoft.Json.ReferenceLoopHandling.Ignore
                );

            // Register the Swagger generator, defining one or more Swagger documents
            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo
                {
                    Version = "v1",
                    Title = "Microservice API",
                    Description = "Microservice API",
                    TermsOfService = null,
                    Contact = new OpenApiContact { Name = "DINH KHAC HOAI PHUNG", Email = "phungdkh@gmail.com", Url = null },
                });

                c.DescribeAllParametersInCamelCase();
            });

            services.AddSingleton(Configuration);

            // Entity framework
            string msSqlConnectionString = Configuration.GetValue<string>("database:msSql:connectionString");
            services.AddDbContext<AppIdentityDbContext>(opt =>
                opt.UseSqlServer(
                    msSqlConnectionString,
                    options =>
                    {
                        options.EnableRetryOnFailure();
                    }));

            services.AddIdentity<IdentityUser, IdentityRole>()
                .AddEntityFrameworkStores<AppIdentityDbContext>()
                .AddDefaultTokenProviders();

            // ===== Add Jwt Authentication ========
            JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Clear(); // => remove default claims
            services
                .AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                    options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
                    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;

                })
                .AddJwtBearer(cfg =>
                {
                    cfg.RequireHttpsMetadata = false;
                    cfg.SaveToken = true;
                    cfg.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidIssuer = Configuration["JwtIssuer"],
                        ValidAudience = Configuration["JwtIssuer"],
                        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Configuration["JwtKey"])),
                        ClockSkew = TimeSpan.Zero // remove delay of token when expire
                    };
                });

            services.AddCors(options =>
            {
                options.AddPolicy("CorsPolicy",
                    builder => builder
                    .SetIsOriginAllowed((host) => true)
                    .AllowAnyMethod()
                    .AllowAnyHeader()
                    .AllowCredentials());
            });
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env, 
            AppIdentityDbContext context,
            RoleManager<IdentityRole> roleManager,
            UserManager<IdentityUser> userManager)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseHttpsRedirection();

            app.UseRouting();

            app.UseCors("CorsPolicy");

            app.UseAuthentication();
            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });

            // auto run migration
            RunMigration(app);

            DummyData.Initialize(context, userManager, roleManager).Wait();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="app"></param>
        private void RunMigration(IApplicationBuilder app)
        {
            using (var scope = app.ApplicationServices.GetService<IServiceScopeFactory>().CreateScope())
            {
                scope.ServiceProvider.GetRequiredService<AppIdentityDbContext>().Database.Migrate();
            }
        }
    }
}
