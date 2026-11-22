using YogaClassManager.Core.Models.People;

namespace YogaClassManager.Core.Tests;

/// <summary>Identity.Validate / Student.Validate - what the Identity and Student edit dialogs rely on to
/// refuse a bad save. Validate also tidies the record it checks (trims every field, blank optional
/// fields become null), and the dialogs save that tidied record.</summary>
public class PersonValidationTests
{
    private static Identity IdentityWith(string firstName = "Ava", string? lastName = "Lee",
        string? phone = "0400000000", string? email = "ava@example.com")
    {
        return new Identity(0, firstName, lastName, phone, email, true);
    }

    private static Student StudentWith(string lastName = "Lee", string? phone = "0400000000",
        string? email = "ava@example.com")
    {
        return new Student(0, "Ava", lastName, phone, email, true);
    }

    // ---- Identity: contact details ----

    [Theory]
    [InlineData("0400000000", null)] // mobile
    [InlineData("98765432", null)] // 8-digit landline
    [InlineData(null, "ava@example.com")]
    [InlineData("0400000000", "ava@example.com")]
    public void Identity_IsValid_WithAPhoneNumberOrAnEmail(string? phone, string? email)
    {
        Assert.True(IdentityWith(phone: phone, email: email).Validate());
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("", "")]
    [InlineData("   ", "   ")]
    public void Identity_IsInvalid_WithNeitherAPhoneNumberNorAnEmail(string? phone, string? email)
    {
        Assert.False(IdentityWith(phone: phone, email: email).Validate());
    }

    /// <summary>The ways people write Australian numbers are accepted, and saved as bare national digits.</summary>
    [Theory]
    [InlineData("0412 345 678", "0412345678")]
    [InlineData("0412-345-678", "0412345678")]
    [InlineData("0412.345.678", "0412345678")]
    [InlineData("+61 412 345 678", "0412345678")]
    [InlineData("+61412345678", "0412345678")]
    [InlineData("+61 (0)412 345 678", "0412345678")]
    [InlineData("0061 412 345 678", "0412345678")]
    [InlineData("(02) 9876 5432", "0298765432")]
    [InlineData("02 9876 5432", "0298765432")]
    [InlineData("+61 2 9876 5432", "0298765432")]
    [InlineData("(03) 9876-5432", "0398765432")]
    [InlineData("07 3123 4567", "0731234567")]
    [InlineData("08 9123 4567", "0891234567")]
    [InlineData("9876 5432", "98765432")] // local landline, no area code
    [InlineData("1300 123 456", "1300123456")]
    [InlineData("1800-123-456", "1800123456")]
    [InlineData("13 12 34", "131234")]
    public void Identity_AcceptsAustralianPhoneNumberFormats_AndNormalisesThem(string typed, string saved)
    {
        var identity = IdentityWith(phone: typed, email: null);

        Assert.True(identity.Validate());
        Assert.Equal(saved, identity.PhoneNumber);
    }

    [Theory]
    [InlineData("041234567")] // 9 digits
    [InlineData("04123456789")] // 11 digits
    [InlineData("041234567a")]
    [InlineData("0112345678")] // no 01 area code
    [InlineData("0512345678")]
    [InlineData("1234567890")] // 10 digits without a leading 0, 1300 or 1800
    [InlineData("1900123456")]
    [InlineData("0876 5432")] // 8 digits starting with 0
    [InlineData("+64 21 123 4567")] // New Zealand
    [InlineData("+61 412 345 67")]
    [InlineData("0412 345 678 ext 2")]
    [InlineData("0412 + 345678")]
    public void Identity_IsInvalid_WithAMalformedPhoneNumber(string phone)
    {
        Assert.False(IdentityWith(phone: phone, email: null).Validate());
    }

    [Fact]
    public void Identity_IsInvalid_WithAMalformedPhoneNumber_EvenWithAValidEmail()
    {
        Assert.False(IdentityWith(phone: "041234567", email: "ava@example.com").Validate());
    }

    [Fact]
    public void Validate_LeavesAnInvalidPhoneNumberAsTyped()
    {
        var identity = IdentityWith(phone: " 0412 345 67 ", email: null);

        Assert.False(identity.Validate());
        Assert.Equal("0412 345 67", identity.PhoneNumber);
    }

    [Theory]
    [InlineData("ava.lee@example.com")]
    [InlineData("ava+yoga@example.com")]
    [InlineData("o'brien@example.com.au")]
    [InlineData("ava@mail-server.example.com")]
    public void Identity_IsValid_WithAWellFormedEmail(string email)
    {
        Assert.True(IdentityWith(phone: null, email: email).Validate());
    }

    [Theory]
    [InlineData("ava")]
    [InlineData("ava@")]
    [InlineData("ava@example")]
    [InlineData("@example.com")]
    [InlineData("ava lee@example.com")]
    [InlineData("ava@@example.com")]
    public void Identity_IsInvalid_WithAMalformedEmail(string email)
    {
        Assert.False(IdentityWith(phone: null, email: email).Validate());
    }

    [Fact]
    public void Identity_IsInvalid_WithAMalformedEmail_EvenWithAValidPhoneNumber()
    {
        Assert.False(IdentityWith(phone: "0400000000", email: "ava@example").Validate());
    }

    // ---- Identity: names ----

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Identity_IsInvalid_WithABlankFirstName(string firstName)
    {
        Assert.False(IdentityWith(firstName).Validate());
    }

    [Fact]
    public void Identity_IsValid_WithoutALastName()
    {
        Assert.True(IdentityWith(lastName: null).Validate());
    }

    // ---- Identity: tidying ----

    [Fact]
    public void Validate_TrimsEveryField()
    {
        var identity = IdentityWith(" Ava ", "  Lee ", " 0400000000 ", " ava@example.com  ");

        Assert.True(identity.Validate());

        Assert.Equal("Ava", identity.FirstName);
        Assert.Equal("Lee", identity.LastName);
        Assert.Equal("0400000000", identity.PhoneNumber);
        Assert.Equal("ava@example.com", identity.Email);
        Assert.Equal("Ava Lee", identity.FullName);
    }

    [Fact]
    public void Validate_TurnsBlankOptionalFieldsIntoNull()
    {
        var identity = IdentityWith(lastName: "  ", phone: "", email: "ava@example.com");

        Assert.True(identity.Validate());

        Assert.Null(identity.LastName);
        Assert.Null(identity.PhoneNumber);
        Assert.Equal("Ava", identity.FullName);
    }

    // ---- Student: everything Identity requires, plus a last name ----

    [Fact]
    public void Student_IsValid_WithALastNameAndContactDetails()
    {
        Assert.True(StudentWith().Validate());
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Student_IsInvalid_WithABlankLastName(string lastName)
    {
        Assert.False(StudentWith(lastName).Validate());
    }

    [Fact]
    public void Student_IsInvalid_WhenTheIdentityRulesFail()
    {
        Assert.False(StudentWith(phone: null, email: null).Validate());
        Assert.False(StudentWith(phone: "041234567", email: null).Validate());
    }
}
