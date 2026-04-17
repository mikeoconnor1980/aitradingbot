using TradePilot.Domain.Entities;

namespace TradePilot.Domain.Tests.Entities;

[TestClass]
public sealed class UserTests
{
    [TestMethod]
    public void GivenValidInputs_WhenCreate_ThenPropertiesSet()
    {
        var user = User.Create("test@example.com", "Test User", "hashed-pw");

        user.Id.Should().NotBeEmpty();
        user.Email.Should().Be("test@example.com");
        user.DisplayName.Should().Be("Test User");
        user.PasswordHash.Should().Be("hashed-pw");
        user.IsActive.Should().BeTrue();
        user.CreatedAtUtc.Should().BeGreaterThan(0);
    }

    [TestMethod]
    public void GivenEmailWithMixedCase_WhenCreate_ThenNormalized()
    {
        var user = User.Create("Test@EXAMPLE.com", "Test", "hash");

        user.Email.Should().Be("test@example.com");
    }

    [TestMethod]
    public void GivenEmailWithWhitespace_WhenCreate_ThenTrimmed()
    {
        var user = User.Create("  test@example.com  ", "Test", "hash");

        user.Email.Should().Be("test@example.com");
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("  ")]
    public void GivenInvalidEmail_WhenCreate_ThenThrowsArgumentException(string? email)
    {
        var act = () => User.Create(email!, "name", "hash");

        act.Should().Throw<ArgumentException>();
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("  ")]
    public void GivenInvalidDisplayName_WhenCreate_ThenThrowsArgumentException(string? displayName)
    {
        var act = () => User.Create("test@example.com", displayName!, "hash");

        act.Should().Throw<ArgumentException>();
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("  ")]
    public void GivenInvalidPasswordHash_WhenCreate_ThenThrowsArgumentException(string? passwordHash)
    {
        var act = () => User.Create("test@example.com", "name", passwordHash!);

        act.Should().Throw<ArgumentException>();
    }

    [TestMethod]
    public void GivenUser_WhenUpdateDisplayName_ThenNameUpdated()
    {
        var user = User.Create("test@example.com", "Old Name", "hash");

        user.UpdateDisplayName("New Name");

        user.DisplayName.Should().Be("New Name");
    }

    [TestMethod]
    public void GivenUser_WhenDeactivate_ThenIsActiveFalse()
    {
        var user = User.Create("test@example.com", "Test", "hash");

        user.Deactivate();

        user.IsActive.Should().BeFalse();
    }
}
