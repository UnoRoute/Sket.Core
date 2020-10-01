﻿using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Blazored.LocalStorage;
using Bracketcore.Sket.Entity;
using Bracketcore.Sket.Repository.Interfaces;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;

namespace Bracketcore.Sket.Manager
{
    /// <summary>
    ///     This is an abstract of the Authentication state provider.
    ///     used for blazor based application
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public abstract class SketAuthenticationStateProvider<T> : AuthenticationStateProvider,
        ISketAuthenticationStateProvider<T>
        where T : SketUserModel
    {
        private readonly ISketAccessTokenRepository<SketAccessTokenModel> _accessToken;
        private readonly ILocalStorageService _localstorage;
        private readonly ISketUserRepository<T> _userRepository;

        public SketAuthenticationStateProvider(ILocalStorageService localstorage,
            ISketAccessTokenRepository<SketAccessTokenModel> accessToken, ISketUserRepository<T> userRepository)
        {
            _localstorage = localstorage;
            _accessToken = accessToken;
            _userRepository = userRepository;
        }

        public CancellationToken CancellationToken { get; set; }

        public async Task LoginUser(T loginData, string Token, HttpContext httpContext)
        {
            try
            {
                var u = new ClaimsPrincipal();

                var GetToken = await _accessToken.FindByToken(Token);

                var LoggedUser = await _userRepository.FindById(GetToken.OwnerID.ID);

                LoggedUser.Password = string.Empty;

                var verifyUser = JsonConvert.SerializeObject(LoggedUser);

                var RoleValue = string.Join(",", LoggedUser.Role);

                var identity = new ClaimsIdentity(new[]
                {
                    new Claim(ClaimTypes.Email, LoggedUser.Email),
                    new Claim(ClaimTypes.NameIdentifier, LoggedUser.ID),
                    new Claim("Profile", verifyUser),
                    new Claim("Token", Token),
                    new Claim(ClaimTypes.Role, RoleValue)
                }, Init.Sket.Cfg.Settings.AuthType.ToString());

                var user = new ClaimsPrincipal(identity);

                if (httpContext != null)
                    await httpContext.SignInAsync(Init.Sket.Cfg.Settings.AuthType.ToString(), user);


                NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(user)));
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }

        public async Task LogOutUser(HttpContext httpContext)
        {
            await httpContext.SignOutAsync(Init.Sket.Cfg.Settings.AuthType.ToString(),
                new AuthenticationProperties
                {
                    AllowRefresh = true,
                    RedirectUri = "/login"
                });

            var user = new ClaimsPrincipal();
            NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(user)));
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        public override async Task<AuthenticationState> GetAuthenticationStateAsync()
        {
            // Todo work on the authentication to give user access to the platform
            var user = new ClaimsPrincipal();

            var getToken = await _localstorage.GetItemAsync<string>("Token");

            if (getToken == null) return await Task.FromResult(new AuthenticationState(user));

            var tokenExist = await _accessToken.FindByToken(getToken);

            if (tokenExist is null) return await Task.FromResult(new AuthenticationState(user));

            var getUser = await _userRepository.FindById(tokenExist.OwnerID.ID);

            getUser.Password = string.Empty;

            var RoleValue = string.Join(",", getUser.Role);

            var identity = new ClaimsIdentity(new[]
            {
                new Claim("Profile", JsonConvert.SerializeObject(getUser)),
                new Claim(ClaimTypes.Email, getUser.Email),
                new Claim(ClaimTypes.NameIdentifier, getUser.ID),
                new Claim("Token", getToken),
                new Claim(ClaimTypes.Role, RoleValue)
            }, "SketAuth");

            user = new ClaimsPrincipal(identity);

            return await Task.FromResult(new AuthenticationState(user));
        }

        public async Task LoginUser(T loginData, string Token)
        {
            await LoginUser(loginData, Token, null);
        }

        // protected virtual void Dispose(bool disposing)
        // {
        //     if (disposing)
        //     {
        //         _accessToken?.Dispose();
        //     }
        // }
        //
        // public void Dispose()
        // {
        //     Dispose(true);
        //     GC.SuppressFinalize(this);
        // }
        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
            }
        }
    }
}