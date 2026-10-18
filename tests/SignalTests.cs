using System.Net;
using System.Text;
using SignalMonitor;
using Microsoft.AspNetCore.Authorization;
using Xunit;

public class SignalTests
{
    [Fact] public async Task ReadsActiveAndIdleTunersWithoutExposingOtherDeviceFields()
    {
        using var client = new HttpClient(new Handler("""[{"Resource":"tuner0","VctNumber":"32.1","VctName":"FOX","SignalStrengthPercent":77,"SignalQualityPercent":55,"SymbolQualityPercent":0,"NetworkRate":4076320,"DeviceAuth":"secret"},{"Resource":"tuner1"}]"""));
        var result = await SignalController.ReadTunerAsync("http://tuner.test", client, default);
        Assert.Null(result.Error); Assert.Equal(2, result.Tuners.Length);
        Assert.Equal("32.1", result.Tuners[0].Channel); Assert.Equal(0L, result.Tuners[0].Symbol);
        Assert.Null(result.Tuners[1].Channel); Assert.Null(result.Tuners[1].Symbol);
        Assert.DoesNotContain("secret", System.Text.Json.JsonSerializer.Serialize(result));
    }
    [Theory][InlineData("file:///etc/passwd")][InlineData("http://user:secret@tuner.test")][InlineData("http://tuner.test/path")]
    public async Task RejectsInvalidAddresses(string address)
    {
        using var client=new HttpClient(new Handler("[]"));
        Assert.NotNull((await SignalController.ReadTunerAsync(address,client,default)).Error);
    }
    [Fact] public async Task InvalidResponseIsAnErrorRatherThanIdle()
    {
        using var client=new HttpClient(new Handler("<html>Offline</html>"));
        var result=await SignalController.ReadTunerAsync("tuner.test",client,default);
        Assert.NotNull(result.Error);Assert.Empty(result.Tuners);
    }
    [Fact] public async Task CallerCancellationIsNotHidden()
    {
        using var client=new HttpClient(new Handler("[]"));using var cts=new CancellationTokenSource();cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>SignalController.ReadTunerAsync("tuner.test",client,cts.Token));
    }
    [Fact] public void EndpointRequiresAdministrator()
    {
        var authorization=Assert.Single(typeof(SignalController).GetCustomAttributes(typeof(AuthorizeAttribute),true).Cast<AuthorizeAttribute>());
        Assert.Equal("RequiresElevation",authorization.Policy);
    }
    private sealed class Handler(string json):HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token)
        {token.ThrowIfCancellationRequested();Assert.Equal("/status.json",request.RequestUri!.AbsolutePath);return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent(json,Encoding.UTF8,"application/json")});}
    }
}

