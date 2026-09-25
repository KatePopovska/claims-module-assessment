using ClaimsModule.Domain.Claims;
using ClaimsModule.Domain.Enums;

namespace ClaimsModule.Domain.UnitTests.Claims;

public class ClaimPartyTests
{
    [Fact]
    public void GetDisplayName_ForPerson_JoinsFirstAndLastName()
    {
        var party = new ClaimParty { PartyType = PartyType.Person, FirstName = "Ann", LastName = "Lee" };

        Assert.Equal("Ann Lee", party.GetDisplayName());
    }

    [Fact]
    public void GetDisplayName_ForCompany_UsesCompanyName()
    {
        var party = new ClaimParty { PartyType = PartyType.Company, CompanyName = "Acme Towing", FirstName = "Ignored" };

        Assert.Equal("Acme Towing", party.GetDisplayName());
    }
}
