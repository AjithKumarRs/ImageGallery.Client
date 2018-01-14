﻿namespace ImageGallery.Client.Configuration
{
    public class ConfigurationOptions
    {
        public string ApiUri { get; set; }

        public string ImagesUri { get; set; }

        public Dataprotection Dataprotection { get; set; }

        public OpenIdConnectConfiguration OpenIdConnectConfiguration { get; set; }
    }

    public class Dataprotection
    {
        public string RedisConnection { get; set; }

        public string RedisKey { get; set; }

        public bool Enabled { get; set; }
    }

    public class OpenIdConnectConfiguration
    {
        public string Authority { get; set; }

        public string ClientSecret { get; set; }

        public string ClientId { get; set; }
    }
}