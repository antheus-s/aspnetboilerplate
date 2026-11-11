using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading.Tasks;
using Abp.Webhooks;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Abp.Tests.Webhooks
{
    public class WebhookHttpMessageHandlerFactory_Tests
    {
        [Theory]
        [InlineData("0.0.0.0")]
        [InlineData("10.0.0.1")]
        [InlineData("100.64.0.1")]
        [InlineData("127.0.0.1")]
        [InlineData("169.254.169.254")]
        [InlineData("172.16.0.1")]
        [InlineData("172.31.255.255")]
        [InlineData("192.0.0.1")]
        [InlineData("192.168.1.1")]
        [InlineData("198.18.0.1")]
        [InlineData("224.0.0.1")]
        [InlineData("255.255.255.255")]
        [InlineData("::")]
        [InlineData("::1")]
        [InlineData("::ffff:10.0.0.1")]
        [InlineData("::ffff:127.0.0.1")]
        [InlineData("fc00::1")]
        [InlineData("fd12:3456::1")]
        [InlineData("fe80::1")]
        [InlineData("ff02::1")]
        [InlineData("64:ff9b::a9fe:a9fe")]
        [InlineData("2002:0a00:0001::1")]
        public void Should_Not_Treat_Non_Public_Addresses_As_Public(string address)
        {
            WebhookHttpMessageHandlerFactory.IsPublicAddress(IPAddress.Parse(address)).ShouldBeFalse();
        }

        [Theory]
        [InlineData("1.1.1.1")]
        [InlineData("8.8.8.8")]
        [InlineData("100.128.0.1")]
        [InlineData("172.32.0.1")]
        [InlineData("192.169.0.1")]
        [InlineData("::ffff:8.8.8.8")]
        [InlineData("2606:4700:4700::1111")]
        [InlineData("64:ff9b::808:808")]
        public void Should_Treat_Public_Addresses_As_Public(string address)
        {
            WebhookHttpMessageHandlerFactory.IsPublicAddress(IPAddress.Parse(address)).ShouldBeTrue();
        }

        [Fact]
        public async Task Should_Not_Connect_To_Private_Network_Address()
        {
            using var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;

            using var client = new HttpClient(WebhookHttpMessageHandlerFactory.Create(CreateConfiguration(true)));

            var exception = await Should.ThrowAsync<HttpRequestException>(() => client.PostAsync($"http://localhost:{port}/", new StringContent("")));

            exception.Message.ShouldContain(WebhookHttpMessageHandlerFactory.PrivateNetworkAddressNotAllowedMessage);
            listener.Pending().ShouldBeFalse();
        }

        [Fact]
        public void Should_Use_Default_Handler_When_Protection_Is_Disabled()
        {
            using var handler = WebhookHttpMessageHandlerFactory.Create(CreateConfiguration(false));

            handler.ShouldBeOfType<HttpClientHandler>();
        }

        private static IWebhooksConfiguration CreateConfiguration(bool isPrivateNetworkProtectionEnabled)
        {
            var configuration = Substitute.For<IWebhooksConfiguration>();
            configuration.IsPrivateNetworkProtectionEnabled.Returns(isPrivateNetworkProtectionEnabled);
            return configuration;
        }
    }
}
