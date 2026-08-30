// <copyright file="IncreasePotionStackSizeUpdatePlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.Updates;

using System.Runtime.InteropServices;
using MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix.Items;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// This update increases the maximum stack size of classic potion-style consumables.
/// </summary>
[PlugIn]
[Display(Name = PlugInName, Description = PlugInDescription)]
[Guid("7E3DF4A6-89C2-4F91-8C62-2C2ED13740CF")]
public class IncreasePotionStackSizeUpdatePlugIn : UpdatePlugInBase
{
    /// <summary>
    /// The plug in name.
    /// </summary>
    internal const string PlugInName = "Increase potion stack size";

    /// <summary>
    /// The plug in description.
    /// </summary>
    internal const string PlugInDescription = "Increases classic potion-style consumable stack sizes from 3 to 255.";

    /// <summary>
    /// The item group of potion-style consumables.
    /// </summary>
    private const byte PotionGroup = 14;

    /// <summary>
    /// The item numbers of classic potion-style consumables.
    /// </summary>
    private static readonly short[] PotionNumbers =
    [
        0,  // Apple
        1,  // Small Healing Potion
        2,  // Medium Healing Potion
        3,  // Large Healing Potion
        4,  // Small Mana Potion
        5,  // Medium Mana Potion
        6,  // Large Mana Potion
        8,  // Antidote
        35, // Small Shield Potion
        36, // Medium Shield Potion
        37, // Large Shield Potion
        38, // Small Complex Potion
        39, // Medium Complex Potion
        40, // Large Complex Potion
    ];

    /// <inheritdoc />
    public override UpdateVersion Version => UpdateVersion.IncreasePotionStackSize;

    /// <inheritdoc />
    public override string DataInitializationKey => VersionSeasonSix.DataInitialization.Id;

    /// <inheritdoc />
    public override string Name => PlugInName;

    /// <inheritdoc />
    public override string Description => PlugInDescription;

    /// <inheritdoc />
    public override bool IsMandatory => true;

    /// <inheritdoc />
    public override DateTime CreatedAt => new(2026, 08, 29, 0, 0, 0, DateTimeKind.Utc);

    /// <inheritdoc />
    protected override ValueTask ApplyAsync(IContext context, GameConfiguration gameConfiguration)
    {
        foreach (var item in gameConfiguration.Items.Where(item => item.Group == PotionGroup && PotionNumbers.Contains(item.Number)))
        {
            item.Durability = Math.Max(item.Durability, Potions.MaximumPotionStackSize);
        }

        return default;
    }
}
