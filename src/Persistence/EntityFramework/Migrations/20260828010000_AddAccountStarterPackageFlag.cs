// <copyright file="20260828010000_AddAccountStarterPackageFlag.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

#nullable disable

namespace MUnique.OpenMU.Persistence.EntityFramework.Migrations
{
    using Microsoft.EntityFrameworkCore.Infrastructure;
    using Microsoft.EntityFrameworkCore.Migrations;
    using MUnique.OpenMU.Persistence.EntityFramework;

    /// <inheritdoc />
    [DbContext(typeof(EntityDataContext))]
    [Migration("20260828010000_AddAccountStarterPackageFlag")]
    public partial class AddAccountStarterPackageFlag : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "HasReceivedStarterPackage",
                schema: "data",
                table: "Account",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HasReceivedStarterPackage",
                schema: "data",
                table: "Account");
        }
    }
}
