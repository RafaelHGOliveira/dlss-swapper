using System.Text.Json.Serialization;
using DLSS_Swapper.Data;
using DLSS_Swapper.Data.Steam.SteamAPI;

namespace DLSS_Swapper.Core;

[JsonSourceGenerationOptions(WriteIndented = false)]
[JsonSerializable(typeof(GetItemsInput))]
[JsonSerializable(typeof(SteamAPIResponse<GetItemsResponse>))]
[JsonSerializable(typeof(Manifest))]
[JsonSerializable(typeof(KnownDLLs))]
[JsonSerializable(typeof(HashedKnownDLL))]
internal partial class CoreSourceGenerationContext : JsonSerializerContext
{
}
