using System;
using System.ComponentModel.DataAnnotations;

namespace BackendCodeChallengeApi.Contracts.Crew;

public sealed class CrewListQuery
{
    public CrewSortColumn? SortBy { get; init; }
    
    [AllowedValues("asc", "desc", ErrorMessage = "SortDir must be either 'asc' or 'desc'.")]
    public string SortDir { get; init; } = "asc";

    [StringLength(100, ErrorMessage = "Search term cannot exceed 100 characters.")]
    public string? Search { get; init; }
}
