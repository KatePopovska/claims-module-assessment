using ClaimsModule.Application.Claims.Commands.AddClaimParty;
using ClaimsModule.Application.Claims.Commands.UpdateClaimStatus;
using ClaimsModule.Application.Claims.Queries.ListClaims;
using ClaimsModule.Application.Reserves.Commands.CreateReserve;
using ClaimsModule.Application.Reserves.Commands.RejectReserve;
using ClaimsModule.Application.Reserves.Commands.SetReserveLimitOverride;
using ClaimsModule.Domain.Enums;
using FluentValidation.TestHelper;

namespace ClaimsModule.Application.UnitTests.Validators;

public class ValidatorTests
{
    [Fact]
    public void UpdateClaimStatus_ReasonLongerThan500_IsInvalid()
    {
        var result = new UpdateClaimStatusCommandValidator().TestValidate(new UpdateClaimStatusCommand(Guid.NewGuid(), ClaimStatus.Withdrawn, new string('x', 501)));

        result.ShouldHaveValidationErrorFor(c => c.Reason);
    }

    [Fact]
    public void UpdateClaimStatus_ValidCommand_Passes()
    {
        var result = new UpdateClaimStatusCommandValidator().TestValidate(new UpdateClaimStatusCommand(Guid.NewGuid(), ClaimStatus.Open, null));

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void AddClaimParty_PersonRequiresFirstAndLastName()
    {
        var result = new AddClaimPartyCommandValidator().TestValidate(new AddClaimPartyCommand(Guid.NewGuid(), PartyRole.Claimant, PartyType.Person, null, null, null, null, null, null));

        result.ShouldHaveValidationErrorFor(c => c.FirstName);
        result.ShouldHaveValidationErrorFor(c => c.LastName);
        result.ShouldNotHaveValidationErrorFor(c => c.CompanyName);
    }

    [Fact]
    public void AddClaimParty_CompanyRequiresCompanyName()
    {
        var result = new AddClaimPartyCommandValidator().TestValidate(new AddClaimPartyCommand(Guid.NewGuid(), PartyRole.Insured, PartyType.Company, null, null, null, null, null, null));

        result.ShouldHaveValidationErrorFor(c => c.CompanyName);
        result.ShouldNotHaveValidationErrorFor(c => c.FirstName);
    }

    [Fact]
    public void AddClaimParty_FieldsLongerThanColumns_AreInvalid()
    {
        var result = new AddClaimPartyCommandValidator().TestValidate(new AddClaimPartyCommand(Guid.NewGuid(), PartyRole.Witness, PartyType.Person, "Ann", "Lee", null, new string('e', 256), new string('9', 51), null));

        result.ShouldHaveValidationErrorFor(c => c.Email);
        result.ShouldHaveValidationErrorFor(c => c.Phone);
    }

    [Theory]
    [InlineData(0, 20)]
    [InlineData(1, 0)]
    [InlineData(1, 101)]
    public void ListClaims_InvalidPaging_IsInvalid(int page, int pageSize)
    {
        var result = new ListClaimsQueryValidator().TestValidate(new ListClaimsQuery(null, null, null, null, null, null, null, page, pageSize));

        Assert.False(result.IsValid);
    }

    [Fact]
    public void ListClaims_DateToBeforeDateFrom_IsInvalid()
    {
        var result = new ListClaimsQueryValidator().TestValidate(new ListClaimsQuery(null, TestData.Now, TestData.Now.AddDays(-1), null, null, null, null));

        result.ShouldHaveValidationErrorFor(q => q.DateTo);
    }

    [Fact]
    public void CreateReserve_AmountWithMoreThanFourDecimals_IsInvalid()
    {
        var result = new CreateReserveCommandValidator().TestValidate(new CreateReserveCommand(Guid.NewGuid(), ReserveComponentType.Indemnity, 100.12345m, null));

        result.ShouldHaveValidationErrorFor(c => c.Amount);
    }

    [Fact]
    public void CreateReserve_UndefinedComponent_IsInvalid()
    {
        var result = new CreateReserveCommandValidator().TestValidate(new CreateReserveCommand(Guid.NewGuid(), (ReserveComponentType)99, 100, null));

        result.ShouldHaveValidationErrorFor(c => c.Component).WithErrorMessage("Invalid reserve component type.");
    }

    [Fact]
    public void RejectReserve_RequiresReason()
    {
        var result = new RejectReserveCommandValidator().TestValidate(new RejectReserveCommand(Guid.NewGuid(), Guid.NewGuid(), ""));

        result.ShouldHaveValidationErrorFor(c => c.RejectionReason).WithErrorMessage("A rejection reason is required.");
    }

    [Fact]
    public void SetReserveLimitOverride_RequiresReasonUpTo500Characters()
    {
        var validator = new SetReserveLimitOverrideCommandValidator();

        validator.TestValidate(new SetReserveLimitOverrideCommand(Guid.NewGuid(), "")).ShouldHaveValidationErrorFor(c => c.Reason);
        validator.TestValidate(new SetReserveLimitOverrideCommand(Guid.NewGuid(), new string('x', 501))).ShouldHaveValidationErrorFor(c => c.Reason);
        validator.TestValidate(new SetReserveLimitOverrideCommand(Guid.NewGuid(), "Catastrophic loss")).ShouldNotHaveAnyValidationErrors();
    }
}
