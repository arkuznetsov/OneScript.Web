﻿using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Hangfire;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;
using OneScript.WebHost.Application;
using OneScript.WebHost.Identity;
using OneScript.WebHost.Infrastructure;
using OneScript.WebHost.Infrastructure.Implementations;
using OneScript.WebHost.Database;
using OneScript.WebHost.BackgroundJobs;

namespace OneScript.WebHost
{
    public class Startup
    {
        public Startup(IConfiguration conf)
        {
            Configuration = conf;
        }
        
        private IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddSingleton<IConfiguration>(Configuration);

            services.Configure<RazorViewEngineOptions>(options =>
            {
                options.ViewLocationExpanders.Add(new OscriptViewsOverride());
            });
            
            services.AddMemoryCache();
            services.AddSession();
            services.AddDatabaseByConfiguration(Configuration);
            services.AddIdentityByConfiguration(Configuration);
            services.AddBackgroundJobsByConfiguration(Configuration);
            
            services.AddMvc()
                .ConfigureApplicationPartManager(pm=>pm.FeatureProviders.Add(new ScriptedViewComponentFeatureProvider()));

            services.AddOneScript();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env, IServiceProvider services)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseStatusCodePages();
            }

            PrepareEnvironment(services);

            var oscriptApp = services.GetService<ApplicationInstance>();
            oscriptApp.OnStartup(app);
            
            // анализ имеющихся компонентов представлений
            var manager = services.GetService<ApplicationPartManager>();
            var provider = manager.FeatureProviders.OfType<ScriptedViewComponentFeatureProvider>().FirstOrDefault();
            if (provider != null)
            {
                provider.Application = oscriptApp;
                provider.Framework = services.GetService<IApplicationRuntime>();
                provider.ScriptsProvider = services.GetService<IFileProvider>();
            }
        }

        private void PrepareEnvironment(IServiceProvider services)
        {
            if (Configuration.GetSection("Database:DbType").Value != null)
            {
                var dbctx = services.GetService<ApplicationDbContext>();
                dbctx.Database.EnsureCreated();

                var environment = services.GetRequiredService<IApplicationRuntime>().Environment;
                var userManager = new InfobaseUsersManagerContext(services);

                environment.InjectGlobalProperty(userManager, "ПользователиИнформационнойБазы", true);
                environment.InjectGlobalProperty(userManager, "InfoBaseUsers", true);
            }

            if (Configuration.GetSection("BackgroundJobs:StorageType").Value != null)
            {
                var environment = services.GetRequiredService<IApplicationRuntime>().Environment;
                var jobsManager = new BackgroundJobsManagerContext(environment);
                
                environment.InjectGlobalProperty(jobsManager, "ФоновыеЗадания", true);
                environment.InjectGlobalProperty(jobsManager, "BackgroundJobs", true);
            }
        }
    }
}
