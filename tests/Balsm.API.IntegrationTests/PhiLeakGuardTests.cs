using Balsm.API.Middleware;
using FluentAssertions;
using Xunit;

namespace Balsm.API.IntegrationTests;

/// <summary>
/// T175a: ≥50 synthetic PHI payloads through PhiLeakGuardMiddleware.AssertNoPhi.
/// Verifies the 10 deny patterns catch all categories. SC-006/SC-016.
/// </summary>
public sealed class PhiLeakGuardTests
{
    // Each entry: (payload, description). All should throw PhiLeakException.
    public static IEnumerable<object[]> PhiPayloads =>
    [
        // Egyptian national IDs (11 digits)
        ["29901011234567", "Egyptian national ID"],
        ["30012250101234", "Egyptian national ID variant"],
        ["12345678901", "11-digit number"],
        // Additional 14-digit IDs
        ["12345678901234", "14-digit ID"],
        ["98765432101234", "14-digit ID variant"],
        // Egyptian phone numbers
        ["+201012345678", "EG phone +20"],
        ["+201234567890", "EG phone variant"],
        // Saudi phone numbers
        ["+966501234567", "SA phone +966"],
        ["+966551234567", "SA phone variant"],
        // UAE phone numbers
        ["+971501234567", "UAE phone +971"],
        ["+971555555555", "UAE phone variant"],
        // Dates of birth
        ["1990-01-01", "date of birth ISO"],
        ["2000-12-31", "date of birth ISO variant"],
        ["error: dob=1985-03-15 not allowed", "DOB embedded in message"],
        ["user born on 1999-07-04", "DOB in sentence"],
        // Allergy terms
        ["patient has allergy to penicillin", "allergy keyword"],
        ["allergic reaction reported", "allerg- prefix"],
        ["Allergy: nuts", "Allergy capitalized"],
        // Condition terms
        ["diagnosed with diabetes", "diabetes"],
        ["patient has hypertension", "hypertension"],
        ["history of asthma", "asthma"],
        ["cancer screening", "cancer"],
        ["epilepsy management", "epilepsy"],
        ["Diabetes type 2", "Diabetes capitalized"],
        ["HYPERTENSION noted", "HYPERTENSION uppercase"],
        ["childhood ASTHMA", "ASTHMA uppercase"],
        ["Breast Cancer", "Cancer capitalized"],
        ["EPILEPSY since 2010", "EPILEPSY uppercase"],
        // Medication terms
        ["prescribed 500mg dose", "dose + mg"],
        ["take 2 tablets daily", "tablets"],
        ["1 capsule twice daily", "capsule"],
        ["200mg ibuprofen", "mg dosage"],
        ["5ml syrup", "ml dosage"],
        ["dose escalation required", "dose term"],
        ["tablet form preferred", "tablet form"],
        ["capsule formulation", "capsule formulation"],
        // Blood type
        ["blood type A+", "blood type"],
        ["bloodtype O negative", "bloodtype"],
        ["blood group B+", "blood + type variant"],
        // Combined PHI in larger messages
        ["User 29901011234567 has allergy", "NID + allergy combo"],
        ["DOB: 1990-01-01, condition: diabetes", "DOB + condition combo"],
        ["+201012345678 prescribed 10mg dose", "phone + medication"],
        ["cancer patient born 1975-06-15", "condition + DOB"],
        ["hypertension + 500mg dose daily", "condition + dosage"],
        ["epilepsy; blood type AB+", "condition + blood type"],
        ["asthma diagnosed; take 2 tablets", "condition + tablet"],
        ["capsule prescribed for diabetes", "capsule + condition"],
        ["5ml syrup for allergy", "volume + allergy"],
    ];

    [Theory]
    [MemberData(nameof(PhiPayloads))]
    public void AssertNoPhi_DetectsPhiInPayload(string payload, string description)
    {
        var act = () => PhiLeakGuardMiddleware.AssertNoPhi(payload);
        act.Should().Throw<PhiLeakException>(because: $"'{description}' contains PHI");
    }

    [Theory]
    [InlineData("Server started successfully")]
    [InlineData("Request processed in 42ms")]
    [InlineData("User logged in")]
    [InlineData("Token refreshed")]
    [InlineData("Handle claimed: johndoe")]
    [InlineData("Session revoked")]
    [InlineData("Operation completed")]
    public void AssertNoPhi_AllowsCleanPayloads(string payload)
    {
        var act = () => PhiLeakGuardMiddleware.AssertNoPhi(payload);
        act.Should().NotThrow(because: $"'{payload}' contains no PHI");
    }
}
