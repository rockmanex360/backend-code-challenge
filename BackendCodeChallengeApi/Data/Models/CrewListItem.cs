namespace BackendCodeChallengeApi.Data.Models;

public sealed record CrewListItem(
    string RankName, string CrewMemberId, string FirstName, string LastName,
    int Age, string Nationality, DateTime SignOnDate, string Status);