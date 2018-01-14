﻿using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using IdentityModel.Client;
using ImageGallery.Client.Configuration;
using ImageGallery.Client.Services;
using ImageGallery.Client.ViewModels;
using ImageGallery.Model;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Newtonsoft.Json;

namespace ImageGallery.Client.Apis
{
    [Route("api/images")]
    public class GalleryApiCommandController : Controller
    {
        private const string InternalImagesRoute = "api/images";

        private readonly IImageGalleryHttpClient _imageGalleryHttpClient;

        private readonly ILogger<GalleryApiCommandController> _logger;

        public GalleryApiCommandController(IOptions<ConfigurationOptions> settings, IImageGalleryHttpClient imageGalleryHttpClient, ILogger<GalleryApiCommandController> logger)
        {
            _logger = logger;
            _imageGalleryHttpClient = imageGalleryHttpClient;
            ApplicationSettings = settings.Value;
        }

        private ConfigurationOptions ApplicationSettings { get; }

        [HttpPost]
        [Route("edit")]
        public async Task<IActionResult> EditImage([FromBody] EditImageViewModel editImageViewModel)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            // create an ImageForUpdate instance
            var imageForUpdate = new ImageForUpdate
            {
                Title = editImageViewModel.Title,
                Category = editImageViewModel.Category,
            };

            // serialize it
            var serializedImageForUpdate = JsonConvert.SerializeObject(imageForUpdate);

            // call the API
            var httpClient = await _imageGalleryHttpClient.GetClient();

            var response = await httpClient.PutAsync(
                    $"{InternalImagesRoute}/{editImageViewModel.Id}",
                    new StringContent(serializedImageForUpdate, System.Text.Encoding.Unicode, "application/json"))
                .ConfigureAwait(false);

            if (response.IsSuccessStatusCode)
                return Ok();

            throw new Exception($"A problem happened while calling the API: {response.ReasonPhrase}");
        }

        [HttpDelete]
        public async Task<IActionResult> DeleteImage(Guid id)
        {
            _logger.LogInformation($"Delete image by Id {id}");

            // call the API
            var httpClient = await _imageGalleryHttpClient.GetClient();

            var response = await httpClient.DeleteAsync($"{InternalImagesRoute}/{id}").ConfigureAwait(false);

            if (response.IsSuccessStatusCode)
                return Ok();

            switch (response.StatusCode)
            {
                case HttpStatusCode.Unauthorized:
                    return Unauthorized();

                case HttpStatusCode.Forbidden:
                    return new ForbidResult();
            }

            throw new Exception($"A problem happened while calling the API: {response.ReasonPhrase}");
        }

        [HttpPost]
        [Route("add")]
        [Authorize(Roles = "PayingUser")]
        public IActionResult AddImage()
        {
            return Ok();
        }

        [HttpPost]
        [Route("order")]
        [Authorize(Policy = "CanOrderFrame")]
        public async Task<IActionResult> AddImage(AddImageViewModel addImageViewModel)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            // create an ImageForCreation instance
            var imageForCreation = new ImageForCreation
            {
                Title = addImageViewModel.Title,
                Category = addImageViewModel.Category,
            };

            // take the first (only) file in the Files list
            var imageFile = addImageViewModel.File;

            if (imageFile.Length > 0)
            {
                using (var fileStream = imageFile.OpenReadStream())
                using (var ms = new MemoryStream())
                {
                    fileStream.CopyTo(ms);
                    imageForCreation.Bytes = ms.ToArray();
                }
            }

            // serialize it
            var serializedImageForCreation = JsonConvert.SerializeObject(imageForCreation);

            // call the API
            var httpClient = await _imageGalleryHttpClient.GetClient();

            var response = await httpClient.PostAsync(
                    InternalImagesRoute,
                    new StringContent(serializedImageForCreation, Encoding.Unicode, "application/json"))
                .ConfigureAwait(false);

            if (response.IsSuccessStatusCode)
                return Ok();

            throw new Exception($"A problem happened while calling the API: {response.ReasonPhrase}");
        }

        public async Task Logout()
        {
            #region Revocation Token on Logout

            // get the metadata
            Console.WriteLine("ApplicationSettings.Authority" + ApplicationSettings.OpenIdConnectConfiguration.Authority);

            var discoveryClient = new DiscoveryClient(ApplicationSettings.OpenIdConnectConfiguration.Authority);
            var metaDataResponse = await discoveryClient.GetAsync();

            Console.WriteLine(metaDataResponse.TokenEndpoint);
            Console.WriteLine(metaDataResponse.StatusCode);
            Console.WriteLine(metaDataResponse.Error);

            // create a TokenRevocationClient
            var revocationClient = new TokenRevocationClient(metaDataResponse.RevocationEndpoint, ApplicationSettings.OpenIdConnectConfiguration.ClientId, ApplicationSettings.OpenIdConnectConfiguration.ClientSecret);

            var x = revocationClient.ClientId;
            var x1 = revocationClient.ClientSecret;
            var x2 = revocationClient.AuthenticationStyle;

            Console.WriteLine("ClientId:" + x + "ClientSecret:" + x1 + "AuthenticationStyle:" + x2);

            // get the access token to revoke
            var accessToken = await HttpContext.GetTokenAsync(OpenIdConnectParameterNames.AccessToken);

            if (!string.IsNullOrWhiteSpace(accessToken))
            {
                Console.WriteLine("Access Token:" + accessToken);

                var revokeAccessTokenResponse =
                    await revocationClient.RevokeAccessTokenAsync(accessToken);

                if (revokeAccessTokenResponse.IsError)
                    throw new Exception("Problem encountered while revoking the access token.", revokeAccessTokenResponse.Exception);
            }

            // revoke the refresh token as well
            var refreshToken = await HttpContext.GetTokenAsync(OpenIdConnectParameterNames.RefreshToken);

            if (!string.IsNullOrWhiteSpace(refreshToken))
            {
                var revokeRefreshTokenResponse =
                    await revocationClient.RevokeRefreshTokenAsync(refreshToken);

                if (revokeRefreshTokenResponse.IsError)
                    throw new Exception("Problem encountered while revoking the refresh token.", revokeRefreshTokenResponse.Exception);
            }

            #endregion

            await HttpContext.SignOutAsync("Cookies");
            await HttpContext.SignOutAsync("OpenIdConnect");
        }
    }
}