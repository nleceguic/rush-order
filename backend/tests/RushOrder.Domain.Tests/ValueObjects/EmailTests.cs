using FluentAssertions;
using RushOrder.Domain.ValueObjects;

namespace RushOrder.Domain.Tests.ValueObjects;

public sealed class EmailTests
{
    [Fact]
    public void Constructor_ValidEmail_DoesNotThrow()
    {
        var act = () => new Email("valid@example.com");

        act.Should().NotThrow();
    }

    [Theory]
    [InlineData("invalid")]
    [InlineData("notld@domain")]
    [InlineData("@missing.com")]
    [InlineData("missing-at-sign")]
    [InlineData("double@@at.com")]
    public void Constructor_InvalidEmail_ThrowsArgumentException(string invalidEmail)
    {
        var act = () => new Email(invalidEmail);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_EmptyEmail_ThrowsArgumentException()
    {
        var act = () => new Email("");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_WhitespaceEmail_ThrowsArgumentException()
    {
        var act = () => new Email("   ");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Equality_SameValue_AreEqual()
    {
        var a = new Email("a@b.com");
        var b = new Email("a@b.com");

        (a == b).Should().BeTrue();
        a.Should().Be(b);
    }

    [Fact]
    public void Equality_DifferentValue_AreNotEqual()
    {
        var a = new Email("a@b.com");
        var b = new Email("c@d.com");

        (a == b).Should().BeFalse();
        a.Should().NotBe(b);
    }

    [Fact]
    public void Value_IsNormalisedToLowercase()
    {
        var email = new Email("TEST@EXAMPLE.COM");

        email.Value.Should().Be("test@example.com");
    }

    [Fact]
    public void Value_LeadingAndTrailingWhitespace_IsTrimmed()
    {
        var email = new Email("  user@domain.com  ");

        email.Value.Should().Be("user@domain.com");
    }

    [Fact]
    public void ImplicitConversion_FromString_Works()
    {
        Email email = "hello@world.org";

        email.Value.Should().Be("hello@world.org");
    }

    [Fact]
    public void ImplicitConversion_ToString_ReturnsValue()
    {
        var email = new Email("abc@def.io");
        string str = email;

        str.Should().Be("abc@def.io");
    }
}
