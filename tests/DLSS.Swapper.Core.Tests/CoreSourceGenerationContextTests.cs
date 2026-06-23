using System.Text.Json;
using DLSS_Swapper.Core;
using DLSS_Swapper.Data.Steam.SteamAPI;
using Xunit;

public class CoreSourceGenerationContextTests
{
    [Fact]
    public void GetItemsInput_RoundTrips_ViaCoreContext()
    {
        var input = new GetItemsInput();
        input.Ids.Add(new StoreItemId { AppId = 570 });

        var json = JsonSerializer.Serialize(input, CoreSourceGenerationContext.Default.GetItemsInput);

        Assert.Contains("570", json);
    }
}
