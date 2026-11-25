using Balsm.Identity.Application.FamilyLinks;
using Balsm.Identity.Domain.FamilyLinks;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Balsm.Identity.Infrastructure.Data.Configurations;

/// <summary>Stores <see cref="FamilyLinkStatus"/> as its lowercase wire name, matching the open-pair index filter.</summary>
internal sealed class FamilyLinkStatusConverter() : ValueConverter<FamilyLinkStatus, string>(
    v => FamilyLinkWire.ToWire(v),
    v => Enum.Parse<FamilyLinkStatus>(v, true))
{
    public static readonly FamilyLinkStatusConverter Instance = new();
}
