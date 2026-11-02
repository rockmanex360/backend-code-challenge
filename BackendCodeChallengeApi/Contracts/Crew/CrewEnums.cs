using System.Text.Json.Serialization;

namespace BackendCodeChallengeApi.Contracts.Crew;

[JsonConverter(typeof(JsonStringEnumConverter<CrewSortColumn>))]
public enum CrewSortColumn
{
    RankName,
    CrewMemberId,
    FirstName,
    LastName,
    Age,
    Nationality,
    SignOnDate,
    Status
}