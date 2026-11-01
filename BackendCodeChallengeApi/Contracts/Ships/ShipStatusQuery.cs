using System.ComponentModel.DataAnnotations;

namespace BackendCodeChallengeApi.Contracts.Ships;

public sealed class ShipStatusQuery
{
    [AllowedValues("active", "inactive", "all", ErrorMessage = "Status must be one of: active, inactive, all.")]
    public string Status { get; init; } = "all";

    public string? ToSpStatus() => Status switch
    {
        "active" => "Active",
        "inactive" => "Inactive",
        _ => null
    };
}