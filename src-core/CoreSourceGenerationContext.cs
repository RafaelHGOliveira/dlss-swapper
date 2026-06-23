using System.Text.Json.Serialization;
using DLSS_Swapper.Data.Steam.SteamAPI;

namespace DLSS_Swapper.Core;

[JsonSourceGenerationOptions(WriteIndented = false)]
[JsonSerializable(typeof(GetItemsInput))]
[JsonSerializable(typeof(SteamAPIResponse<GetItemsResponse>))]
internal partial class CoreSourceGenerationContext : JsonSerializerContext
{
}
