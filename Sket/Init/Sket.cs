﻿using Bracketcore.Sket.Entity;
using MongoDB.Driver.Linq;
using MongoDB.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Bracketcore.Sket.Repository;
using Microsoft.Extensions.DependencyInjection;

namespace Bracketcore.Sket
{
    /// <summary>
    /// This class setup the roles and context models.
    /// </summary>
    public static class Sket
    {
        public static SketConfig Cfg { get; set; }  = new SketConfig();

        /// <summary>
        /// Initiate a normal setup for your app
        /// </summary>
        /// <param name="services"></param>
        /// <param name="Config"></param>
        /// <param name="settings"></param>
        public static SketConfig Init(SketSettings settings)
        {
            Cfg.Settings = settings;

            GetModels();
            GetRoles();

            return Cfg ;

            
        }

        private static void GetRoles()
        {
            var getRoles = DB.Queryable<SketRoleModel>().Any();
            var normalRole = Enum.GetValues(typeof(SketRoleEnum)).Cast<SketRoleEnum>();

            if (getRoles)
            {
                Console.WriteLine("Roles Set");
            }
            else
                foreach (var role in normalRole)
                {
                    DB.SaveAsync(new SketRoleModel()
                    {
                        Name = role.ToString()
                    });
                }
        }

        private static void GetModels()
        {
            //Todo: work on the context data which will allow the user to access every repo with ease
            var type = typeof(SketBaseRepository<>);
            var types = AppDomain.CurrentDomain.GetAssemblies().SelectMany(s => s.GetTypes())
                .Where(p => type.IsAssignableFrom(p));

            foreach (var t in types.ToList())
            {
                Sket.Cfg.Context.Add(t);
            }

        }

        public static SketConfig Init(IServiceCollection services, SketSettings settings)
        {
            services.AddSket(settings);
            Cfg.Settings = settings;
            return Cfg;
        }

     
    }
}