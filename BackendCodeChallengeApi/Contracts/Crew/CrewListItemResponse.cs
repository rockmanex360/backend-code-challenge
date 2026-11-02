using BackendCodeChallengeApi.Data.Models;

namespace BackendCodeChallengeApi.Contracts.Crew;

public sealed record CrewListItemResponse(
    string RankName, string CrewMemberId, string FirstName, string LastName,
    int Age, string Nationality, DateOnly SignOnDate, string Status)
{
    public static CrewListItemResponse From(CrewListItem c) =>
        new(c.RankName, c.CrewMemberId, c.FirstName, c.LastName, c.Age, c.Nationality,
            DateOnly.FromDateTime(c.SignOnDate), c.Status);
}