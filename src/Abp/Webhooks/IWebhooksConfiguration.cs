using System;
using System.Text.Json;
using Abp.Collections;
using Abp.Json;

namespace Abp.Webhooks
{
    public interface IWebhooksConfiguration
    {
        /// <summary>
        /// HttpClient timeout. Sender will wait <c>TimeoutDuration</c> second before throw timeout exception
        /// </summary>
        TimeSpan TimeoutDuration { get; set; }

        /// <summary>
        ///  How many times <see cref="IWebhookPublisher"/> will try resend webhook until gets HttpStatusCode.OK 
        /// </summary>
        int MaxSendAttemptCount { get; set; }

        /// <summary>
        /// Json serializer settings for converting webhook data to json, If this is null default settings will be used. <see cref="JsonExtensions.ToJsonString(object,bool,bool)"/>
        /// </summary>
        JsonSerializerOptions JsonSerializerOptions { get; set; }

        /// <summary>
        /// Webhook providers.
        /// </summary>
        ITypeList<WebhookDefinitionProvider> Providers { get; }

        /// <summary>
        /// If you enable that, subscriptions will be automatically disabled if they fails <see cref="MaxConsecutiveFailCountBeforeDeactivateSubscription"/> times consecutively.
        /// Tenants should activate it back manually.
        /// </summary>
        bool IsAutomaticSubscriptionDeactivationEnabled { get; set; }

        /// <summary>
        /// If you enable <see cref="IsAutomaticSubscriptionDeactivationEnabled"/>, subscriptions will be automatically disabled if they fails <see cref="MaxConsecutiveFailCountBeforeDeactivateSubscription"/> times consecutively.
        /// </summary>
        int MaxConsecutiveFailCountBeforeDeactivateSubscription { get; set; }

        /// <summary>
        /// If enabled, webhooks are not sent to loopback, private, link-local or other non-public addresses.
        /// This prevents subscribers from using webhooks to reach the internal network of the server (SSRF).
        /// When enabled, webhook requests do not use a proxy. Default: true.
        /// </summary>
        bool IsPrivateNetworkProtectionEnabled { get; set; }
    }
}
