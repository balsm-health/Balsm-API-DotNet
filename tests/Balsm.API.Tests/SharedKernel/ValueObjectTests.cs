using Balsm.SharedKernel.Domain;
using FluentAssertions;
using Xunit;

namespace Balsm.API.Tests.SharedKernel;

public sealed class ValueObjectTests
{
    private sealed class Money(decimal amount, string? currency) : ValueObject
    {
        protected override IEnumerable<object?> GetEqualityComponents()
        {
            yield return amount;
            yield return currency;
        }
    }

    private sealed class OtherMoney(decimal amount, string? currency) : ValueObject
    {
        protected override IEnumerable<object?> GetEqualityComponents()
        {
            yield return amount;
            yield return currency;
        }
    }

    [Fact]
    public void Equals_SameComponents_IsTrue()
    {
        var left = new Money(10m, "EGP");
        var right = new Money(10m, "EGP");

        left.Equals(right).Should().BeTrue();
    }

    [Fact]
    public void Equals_DifferentComponents_IsFalse()
    {
        var left = new Money(10m, "EGP");
        var right = new Money(10m, "SAR");

        left.Equals(right).Should().BeFalse();
    }

    [Fact]
    public void Equals_Null_IsFalse()
    {
        var money = new Money(10m, "EGP");

        money.Equals(null).Should().BeFalse();
    }

    [Fact]
    public void Equals_DifferentTypeWithSameComponents_IsFalse()
    {
        var money = new Money(10m, "EGP");
        var other = new OtherMoney(10m, "EGP");

        money.Equals(other).Should().BeFalse();
    }

    [Fact]
    public void GetHashCode_SameComponents_IsEqual()
    {
        var left = new Money(10m, null);
        var right = new Money(10m, null);

        left.GetHashCode().Should().Be(right.GetHashCode());
    }

    [Fact]
    public void EqualityOperator_SameComponents_IsTrue()
    {
        var left = new Money(5m, "EGP");
        var right = new Money(5m, "EGP");

        (left == right).Should().BeTrue();
    }

    [Fact]
    public void InequalityOperator_DifferentComponents_IsTrue()
    {
        var left = new Money(5m, "EGP");
        var right = new Money(6m, "EGP");

        (left != right).Should().BeTrue();
    }
}
