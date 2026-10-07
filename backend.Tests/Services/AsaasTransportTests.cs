using System.Net;
using System.Text;
using System.Text.Json;
using backend.Application.Services.Asaas;
using backend.Domain.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace backend.Tests.Services;

public class AsaasTransportTests
{
    private static AsaasService Service(HttpMessageHandler handler) => new(new HttpClient(handler),
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Asaas:ApiKey"] = "test-only-api-key", ["Asaas:BaseUrl"] = "https://api-sandbox.asaas.com/v3" }).Build(),
        NullLogger<AsaasService>.Instance);

    [Fact]
    public async Task ReconciliationFindsTransferOnLaterPageWithoutUndocumentedFilter()
    {
        var handler = new Handler(request =>
        {
            Assert.DoesNotContain("externalReference=", request.RequestUri!.Query);
            return request.RequestUri.Query.Contains("offset=0")
                ? "{\"hasMore\":true,\"data\":[{\"id\":\"tr_other\",\"externalReference\":\"other\",\"value\":5}]}"
                : "{\"hasMore\":false,\"data\":[{\"id\":\"tr_target\",\"externalReference\":\"TRANSFER_target\",\"value\":50}]}";
        });
        var found = await Service(handler).FindTransferAsync("TRANSFER_target");
        Assert.Equal("tr_target", found!.Id); Assert.Equal(50m, found.Value); Assert.Equal(2, handler.Calls);
    }
    [Fact]
    public async Task InvalidCustomerDocumentCannotGenerateFictitiousCustomer()
    {
        var handler = new Handler(_ => throw new Exception("No external request should occur"));
        await Assert.ThrowsAsync<ArgumentException>(() => Service(handler).GetOrCreateCustomerAsync(new User { Id = Guid.NewGuid() }));
        Assert.Equal(0, handler.Calls);
    }
    [Fact]
    public async Task FailedCustomerLookupDoesNotCreateDuplicateCustomer()
    {
        var handler = new Handler(_ => "{}") { Status = HttpStatusCode.ServiceUnavailable };
        await Assert.ThrowsAsync<HttpRequestException>(() => Service(handler).GetOrCreateCustomerAsync(new User
            { Id = Guid.NewGuid(), Profile = new UserProfile { CPF = "52998224725" } }));
        Assert.Equal(1, handler.Calls);
    }
    [Theory]
    [InlineData("52998224725", false)]
    [InlineData("real@example.com", true)]
    public async Task CustomerCreationOmitsCpfLoginAndPreservesRealEmail(string login, bool expectsEmail)
    {
        var handler = new Handler(request =>
        {
            if (request.Method == HttpMethod.Get) return "{\"data\":[]}";
            using var body = JsonDocument.Parse(request.Content!.ReadAsStringAsync().GetAwaiter().GetResult());
            Assert.Equal(expectsEmail, body.RootElement.TryGetProperty("email", out var email));
            if (expectsEmail) Assert.Equal(login, email.GetString());
            Assert.Equal("52998224725", body.RootElement.GetProperty("cpfCnpj").GetString());
            return "{\"id\":\"cus_test\"}";
        });
        var customer = await Service(handler).GetOrCreateCustomerAsync(new User { Id = Guid.NewGuid(), Email = login,
            Profile = new UserProfile { CPF = "52998224725", Nome = "Investidor" } });
        Assert.Equal("cus_test", customer); Assert.Equal(2, handler.Calls);
    }
    private class Handler(Func<HttpRequestMessage, string> reply) : HttpMessageHandler
    {
        public int Calls;
        public HttpStatusCode Status = HttpStatusCode.OK;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(new HttpResponseMessage(Status) { Content = new StringContent(reply(request), Encoding.UTF8, "application/json") });
        }
    }
}
