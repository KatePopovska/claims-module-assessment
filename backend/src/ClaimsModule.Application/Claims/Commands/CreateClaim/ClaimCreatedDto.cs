using ClaimsModule.Application.Reserves;
using ClaimsModule.Domain.Enums;

namespace ClaimsModule.Application.Claims.Commands.CreateClaim;

public record ClaimCreatedDto(Guid Id, string ClaimNumber, ClaimStatus Status, DateTimeOffset ReportedDate, ReserveSubmissionResultDto? InitialReserve);
