// <copyright file="ClaimStarterPackageChatCommandPlugInTest.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests;

using System.Reflection;
using Moq;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.DataModel.Configuration.Items;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.PlugIns.ChatCommands;
using CharacterClass = MUnique.OpenMU.DataModel.Configuration.CharacterClass;

/// <summary>
/// Tests for the <see cref="ClaimStarterPackageChatCommandPlugIn"/>.
/// </summary>
[TestFixture]
public class ClaimStarterPackageChatCommandPlugInTest
{
    /// <summary>
    /// Verifies that the command adds the configured package to the current character and marks the account as claimed.
    /// </summary>
    [Test]
    public async ValueTask ClaimAddsConfiguredPackageToCurrentCharacterAsync()
    {
        var player = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        player.GameContext.Configuration.MaximumInventoryMoney = int.MaxValue;
        player.SelectedCharacter!.CharacterClass!.Number = 4;
        AddDefinitions(player.GameContext.Configuration, player.SelectedCharacter.CharacterClass);

        var plugIn = CreatePlugIn();

        await plugIn.HandleCommandAsync(player, "/starter").ConfigureAwait(false);

        Assert.That(player.Account!.HasReceivedStarterPackage, Is.True);
        Assert.That(player.SelectedCharacter.Inventory!.Money, Is.EqualTo(10_000));
        Assert.That(player.SelectedCharacter.Inventory.Items.Any(item => item.Definition!.Group == 14 && item.Definition.Number == 1 && item.Level == 0 && item.Durability == 5), Is.True);
        Assert.That(player.SelectedCharacter.Inventory.Items.Any(item => item.Definition!.Group == 1 && item.Definition.Number == 0 && item.Level == 3 && item.Durability == 18), Is.True);
        Assert.That(player.SelectedCharacter.LearnedSkills.Select(skill => skill.Skill?.Number), Does.Contain((short)17));
    }

    /// <summary>
    /// Verifies that the command accepts configuration references selected by the admin panel lookup fields.
    /// </summary>
    [Test]
    public async ValueTask ClaimAddsPackageConfiguredWithReferencedObjectsAsync()
    {
        var player = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        player.GameContext.Configuration.MaximumInventoryMoney = int.MaxValue;
        player.SelectedCharacter!.CharacterClass!.Number = 4;
        player.SelectedCharacter.CharacterClass.Name = "Dark Knight";
        AddDefinitions(player.GameContext.Configuration, player.SelectedCharacter.CharacterClass);
        var commonItem = player.GameContext.Configuration.Items.First(item => item.Group == 14 && item.Number == 1);
        var classItem = player.GameContext.Configuration.Items.First(item => item.Group == 1 && item.Number == 0);
        var skill = player.GameContext.Configuration.Skills.First(item => item.Number == 17);
        var otherClass = new CharacterClass { Number = 0, Name = "Dark Wizard" };
        var plugIn = new ClaimStarterPackageChatCommandPlugIn
        {
            Configuration = new ClaimStarterPackageChatCommandPlugIn.StarterPackageConfiguration
            {
                Packages =
                [
                    new()
                    {
                        Money = 10_000,
                        Items =
                        [
                            new() { ItemDefinition = commonItem, Durability = 5 },
                        ],
                    },
                    new()
                    {
                        CharacterClass = player.SelectedCharacter.CharacterClass,
                        Items =
                        [
                            new() { ItemDefinition = classItem, Level = 3 },
                        ],
                        Skills =
                        [
                            new() { Skill = skill },
                        ],
                    },
                    new()
                    {
                        CharacterClass = otherClass,
                        Items =
                        [
                            new() { ItemDefinition = commonItem, Level = 7 },
                        ],
                    },
                ],
            },
        };

        await plugIn.HandleCommandAsync(player, "/starter").ConfigureAwait(false);

        Assert.That(player.Account!.HasReceivedStarterPackage, Is.True);
        Assert.That(player.SelectedCharacter.Inventory!.Money, Is.EqualTo(10_000));
        Assert.That(player.SelectedCharacter.Inventory.Items.Any(item => item.Definition == commonItem && item.Level == 0 && item.Durability == 5), Is.True);
        Assert.That(player.SelectedCharacter.Inventory.Items.Any(item => item.Definition == classItem && item.Level == 3 && item.Durability == 18), Is.True);
        Assert.That(player.SelectedCharacter.Inventory.Items.Any(item => item.Level == 7), Is.False);
        Assert.That(player.SelectedCharacter.LearnedSkills.Select(entry => entry.Skill), Does.Contain(skill));
    }

    /// <summary>
    /// Verifies that configured item options are applied when a starter item is created.
    /// </summary>
    [Test]
    public async ValueTask ClaimAddsConfiguredItemOptionsAsync()
    {
        var player = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        player.GameContext.Configuration.MaximumInventoryMoney = int.MaxValue;
        player.SelectedCharacter!.CharacterClass!.Number = 4;
        AddDefinitions(player.GameContext.Configuration, player.SelectedCharacter.CharacterClass);
        var itemDefinition = player.GameContext.Configuration.Items.First(item => item.Group == 1 && item.Number == 0);
        itemDefinition.Skill = player.GameContext.Configuration.Skills.First(skill => skill.Number == 17);
        AddOptionDefinitions(itemDefinition);
        var plugIn = new ClaimStarterPackageChatCommandPlugIn
        {
            Configuration = new ClaimStarterPackageChatCommandPlugIn.StarterPackageConfiguration
            {
                Packages =
                [
                    new()
                    {
                        CharacterClassNumber = 4,
                        Items =
                        [
                            new()
                            {
                                ItemDefinition = itemDefinition,
                                Level = 9,
                                Skill = true,
                                Luck = true,
                                Opt = 4,
                                ExcellentNumber = 3,
                            },
                        ],
                    },
                ],
            },
        };

        await plugIn.HandleCommandAsync(player, "/starter").ConfigureAwait(false);

        var item = player.SelectedCharacter.Inventory!.Items.Single();
        Assert.That(item.Definition, Is.SameAs(itemDefinition));
        Assert.That(item.Level, Is.EqualTo(9));
        Assert.That(item.HasSkill, Is.True);
        Assert.That(item.ItemOptions.Single(option => option.ItemOption?.OptionType == ItemOptionTypes.Option).Level, Is.EqualTo(4));
        Assert.That(item.ItemOptions.Any(option => option.ItemOption?.OptionType == ItemOptionTypes.Luck), Is.True);
        Assert.That(item.ItemOptions.Count(option => option.ItemOption?.OptionType == ItemOptionTypes.Excellent), Is.EqualTo(2));
    }

    /// <summary>
    /// Verifies that the package can only be claimed once per account.
    /// </summary>
    [Test]
    public async ValueTask ClaimCanOnlyBeUsedOncePerAccountAsync()
    {
        var player = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        player.GameContext.Configuration.MaximumInventoryMoney = int.MaxValue;
        player.SelectedCharacter!.CharacterClass!.Number = 4;
        AddDefinitions(player.GameContext.Configuration, player.SelectedCharacter.CharacterClass);

        var plugIn = CreatePlugIn();

        await plugIn.HandleCommandAsync(player, "/starter").ConfigureAwait(false);
        var itemCountAfterFirstClaim = player.SelectedCharacter.Inventory!.Items.Count;
        var moneyAfterFirstClaim = player.SelectedCharacter.Inventory.Money;
        await plugIn.HandleCommandAsync(player, "/starter").ConfigureAwait(false);

        Assert.That(player.Account!.HasReceivedStarterPackage, Is.True);
        Assert.That(player.SelectedCharacter.Inventory.Items, Has.Count.EqualTo(itemCountAfterFirstClaim));
        Assert.That(player.SelectedCharacter.Inventory.Money, Is.EqualTo(moneyAfterFirstClaim));
    }

    /// <summary>
    /// Verifies that class-specific items are selected by the current character class.
    /// </summary>
    [Test]
    public async ValueTask ClaimOnlyAddsItemsForCurrentCharacterClassAsync()
    {
        var player = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        player.GameContext.Configuration.MaximumInventoryMoney = int.MaxValue;
        player.SelectedCharacter!.CharacterClass!.Number = 0;
        AddDefinitions(player.GameContext.Configuration, player.SelectedCharacter.CharacterClass);

        var plugIn = CreatePlugIn();

        await plugIn.HandleCommandAsync(player, "/starter").ConfigureAwait(false);

        Assert.That(player.Account!.HasReceivedStarterPackage, Is.True);
        Assert.That(player.SelectedCharacter.Inventory!.Items.Any(item => item.Definition!.Group == 14 && item.Definition.Number == 1), Is.True);
        Assert.That(player.SelectedCharacter.Inventory.Items.Any(item => item.Definition!.Group == 1 && item.Definition.Number == 0), Is.False);
        Assert.That(player.SelectedCharacter.LearnedSkills.Select(skill => skill.Skill?.Number), Does.Not.Contain((short)17));
    }

    /// <summary>
    /// Verifies that a failed claim doesn't mark the account as claimed.
    /// </summary>
    [Test]
    public async ValueTask FailedClaimDoesNotMarkAccountAsClaimedAsync()
    {
        var player = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        player.GameContext.Configuration.MaximumInventoryMoney = int.MaxValue;
        player.SelectedCharacter!.CharacterClass!.Number = 4;
        AddDefinitions(player.GameContext.Configuration, player.SelectedCharacter.CharacterClass, includeClassItem: false);

        var plugIn = CreatePlugIn();

        await plugIn.HandleCommandAsync(player, "/starter").ConfigureAwait(false);

        Assert.That(player.Account!.HasReceivedStarterPackage, Is.False);
        Assert.That(player.SelectedCharacter.Inventory!.Items, Is.Empty);
        Assert.That(player.SelectedCharacter.Inventory.Money, Is.Zero);
    }

    /// <summary>
    /// Verifies that the previous flat configuration is migrated to grouped packages.
    /// </summary>
    [Test]
    public async ValueTask ClaimMigratesLegacyFlatConfigurationAsync()
    {
        var player = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        player.GameContext.Configuration.MaximumInventoryMoney = int.MaxValue;
        player.SelectedCharacter!.CharacterClass!.Number = 4;
        AddDefinitions(player.GameContext.Configuration, player.SelectedCharacter.CharacterClass);
        var configuration = new ClaimStarterPackageChatCommandPlugIn.StarterPackageConfiguration
        {
            Money = 10_000,
            Items =
            [
                new() { Group = 14, Number = 1, Durability = 5 },
                new() { CharacterClassNumber = 4, Group = 1, Number = 0, Level = 3 },
            ],
            Skills =
            [
                new() { CharacterClassNumber = 4, Number = 17 },
            ],
        };
        var plugIn = new ClaimStarterPackageChatCommandPlugIn
        {
            Configuration = configuration,
        };

        await plugIn.HandleCommandAsync(player, "/starter").ConfigureAwait(false);

        Assert.That(player.Account!.HasReceivedStarterPackage, Is.True);
        Assert.That(configuration.Packages, Has.Count.EqualTo(2));
        Assert.That(configuration.Packages.Any(package => package.CharacterClassNumber is null && package.Money == 10_000 && package.Items.Count == 1), Is.True);
        Assert.That(configuration.Packages.Any(package => package.CharacterClassNumber == 4 && package.Items.Count == 1 && package.Skills.Count == 1), Is.True);
        Assert.That(configuration.Items, Is.Null);
        Assert.That(configuration.Skills, Is.Null);
    }

    /// <summary>
    /// Verifies that predefined fallback values are resolved to object references for the admin panel.
    /// </summary>
    [Test]
    public async ValueTask ResolveReferencesFillsPredefinedFallbackValues()
    {
        var player = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        var configuration = player.GameContext.Configuration;
        var characterClass = player.SelectedCharacter!.CharacterClass!;
        characterClass.Number = 4;
        characterClass.Name = "Dark Knight";
        if (!configuration.CharacterClasses.Contains(characterClass))
        {
            configuration.CharacterClasses.Add(characterClass);
        }

        AddDefinitions(configuration, characterClass);
        var starterConfiguration = new ClaimStarterPackageChatCommandPlugIn.StarterPackageConfiguration
        {
            Packages =
            [
                new()
                {
                    CharacterClassNumber = 4,
                    Items =
                    [
                        new() { Group = 1, Number = 0, Level = 3 },
                    ],
                    Skills =
                    [
                        new() { Number = 17 },
                    ],
                },
            ],
        };

        starterConfiguration.ResolveReferences(configuration);

        var package = starterConfiguration.Packages.Single();
        Assert.That(package.CharacterClass, Is.SameAs(characterClass));
        Assert.That(package.Items.Single().ItemDefinition, Is.SameAs(configuration.Items.Single(item => item.Group == 1 && item.Number == 0)));
        Assert.That(package.Skills.Single().Skill, Is.SameAs(configuration.Skills.Single(skill => skill.Number == 17)));
    }

    private static ClaimStarterPackageChatCommandPlugIn CreatePlugIn()
    {
        return new ClaimStarterPackageChatCommandPlugIn
        {
            Configuration = new ClaimStarterPackageChatCommandPlugIn.StarterPackageConfiguration
            {
                Packages =
                [
                    new()
                    {
                        Money = 10_000,
                        Items =
                        [
                            new() { Group = 14, Number = 1, Durability = 5 },
                        ],
                    },
                    new()
                    {
                        CharacterClassNumber = 4,
                        Items =
                        [
                            new() { Group = 1, Number = 0, Level = 3 },
                        ],
                        Skills =
                        [
                            new() { Number = 17 },
                        ],
                    },
                ],
            },
        };
    }

    private static void AddDefinitions(GameConfiguration configuration, CharacterClass characterClass, bool includeClassItem = true)
    {
        configuration.Items.Add(CreateItemDefinition(14, 1, "Small Healing Potion", durability: 10));
        if (includeClassItem)
        {
            configuration.Items.Add(CreateItemDefinition(1, 0, "Small Axe", durability: 18));
        }

        var skillMock = new Mock<Skill>();
        skillMock.SetupAllProperties();
        skillMock.Setup(s => s.QualifiedCharacters).Returns(new List<CharacterClass> { characterClass });
        var skill = skillMock.Object;
        skill.Number = 17;
        skill.Name = "Energy Ball";
        configuration.Skills.Add(skill);
    }

    private static ItemDefinition CreateItemDefinition(byte group, short number, string name, byte durability)
    {
        var itemDefinition = new ItemDefinition
        {
            Group = group,
            Number = number,
            Name = name,
            Width = 1,
            Height = 1,
            Durability = durability,
            MaximumItemLevel = 15,
        };
        SetProtectedCollection(itemDefinition, nameof(ItemDefinition.PossibleItemOptions), new List<ItemOptionDefinition>());
        SetProtectedCollection(itemDefinition, nameof(ItemDefinition.PossibleItemSetGroups), new List<ItemSetGroup>());
        return itemDefinition;
    }

    private static void AddOptionDefinitions(ItemDefinition itemDefinition)
    {
        var normalOption = new IncreasableItemOption { OptionType = ItemOptionTypes.Option, Number = 1 };
        var luckOption = new IncreasableItemOption { OptionType = ItemOptionTypes.Luck, Number = 1 };
        var firstExcellentOption = new IncreasableItemOption { OptionType = ItemOptionTypes.Excellent, Number = 1 };
        var secondExcellentOption = new IncreasableItemOption { OptionType = ItemOptionTypes.Excellent, Number = 2 };
        itemDefinition.PossibleItemOptions.Add(CreateItemOptionDefinition(normalOption));
        itemDefinition.PossibleItemOptions.Add(CreateItemOptionDefinition(luckOption));
        itemDefinition.PossibleItemOptions.Add(CreateItemOptionDefinition(firstExcellentOption, secondExcellentOption));
    }

    private static ItemOptionDefinition CreateItemOptionDefinition(params IncreasableItemOption[] options)
    {
        var definition = new ItemOptionDefinition { Name = "Option" };
        SetProtectedCollection(definition, nameof(ItemOptionDefinition.PossibleOptions), options);
        return definition;
    }

    private static void SetProtectedCollection<TItem, TCollectionItem>(TItem owner, string propertyName, ICollection<TCollectionItem> value)
    {
        typeof(TItem)
            .GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public)!
            .SetValue(owner, value);
    }
}
