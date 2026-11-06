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
    [Fact] public async Task ScanMeasurementsWithoutChannelArePreserved()
    {
        using var client=new HttpClient(new Handler("""[{"Resource":"tuner0","Frequency":533000000,"SignalStrengthPercent":0,"SignalQualityPercent":0,"SymbolQualityPercent":0}]""", """{"ScanInProgress":1,"Progress":42,"Found":12}"""));
        var result=await SignalController.ReadTunerAsync("tuner.test",client,default);
        Assert.Null(result.Error);Assert.True(result.Scan!.InProgress);Assert.Equal(42L,result.Scan.Progress);Assert.Equal(12L,result.Scan.Found);
        var tuner=Assert.Single(result.Tuners);Assert.Null(tuner.Channel);Assert.Equal(533000000L,tuner.Frequency);Assert.Equal(0L,tuner.Strength);
    }
    [Theory][InlineData("not json")][InlineData("{}")]
    public async Task UnavailableScanMetadataDoesNotHideTunerReadings(string scan)
    {
        using var client=new HttpClient(new Handler("""[{"Resource":"tuner0","VctNumber":"32.1","SignalStrengthPercent":80}]""",scan));
        var result=await SignalController.ReadTunerAsync("tuner.test",client,default);
        Assert.Null(result.Error);Assert.Null(result.Scan);Assert.Equal(80L,Assert.Single(result.Tuners).Strength);
    }    private sealed class Handler(string json, string scan = null):HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token)
        {token.ThrowIfCancellationRequested();var status=request.RequestUri!.AbsolutePath=="/status.json";return Task.FromResult(new HttpResponseMessage(status || scan is not null ? HttpStatusCode.OK : HttpStatusCode.NotFound){Content=new StringContent(status ? json : scan ?? "",Encoding.UTF8,"application/json")});}
    }
}
