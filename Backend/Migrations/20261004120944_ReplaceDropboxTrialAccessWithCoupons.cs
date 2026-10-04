using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Backend.Migrations
{
    /// <inheritdoc />
    public partial class ReplaceDropboxTrialAccessWithCoupons : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DropboxCoupons",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CodeHash = table.Column<string>(type: "text", nullable: false),
                    Type = table.Column<string>(type: "text", nullable: false),
                    IsSharedTest = table.Column<bool>(type: "boolean", nullable: false),
                    IsRedeemed = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DropboxCoupons", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DropboxCouponRedemptions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CouponId = table.Column<int>(type: "integer", nullable: false),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    RedeemedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DropboxCouponRedemptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DropboxCouponRedemptions_DropboxCoupons_CouponId",
                        column: x => x.CouponId,
                        principalTable: "DropboxCoupons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DropboxCouponRedemptions_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DropboxCouponRedemptions_CouponId_UserId",
                table: "DropboxCouponRedemptions",
                columns: new[] { "CouponId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DropboxCouponRedemptions_UserId",
                table: "DropboxCouponRedemptions",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_DropboxCoupons_CodeHash",
                table: "DropboxCoupons",
                column: "CodeHash",
                unique: true);

            migrationBuilder.Sql(
                """
                INSERT INTO "DropboxCoupons"
                    ("CodeHash", "Type", "IsSharedTest", "IsRedeemed", "CreatedAt")
                VALUES
                    ('3380e43becbae57a5db80d9217774668c0c645381b86d0c0be0a0db467888e49',
                     'Test', TRUE, FALSE, CURRENT_TIMESTAMP);

                WITH test_coupon AS (
                    SELECT "Id"
                    FROM "DropboxCoupons"
                    WHERE "CodeHash" = '3380e43becbae57a5db80d9217774668c0c645381b86d0c0be0a0db467888e49'
                )
                INSERT INTO "DropboxCouponRedemptions"
                    ("CouponId", "UserId", "RedeemedAt", "ExpiresAt")
                SELECT
                    test_coupon."Id",
                    trial."UserId",
                    trial."RedeemedAt",
                    trial."RedeemedAt" + INTERVAL '24 hours'
                FROM "DropboxTrialAccess" AS trial
                CROSS JOIN test_coupon;

                DROP TABLE "DropboxTrialAccess";
                """
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DropboxCouponRedemptions");

            migrationBuilder.DropTable(
                name: "DropboxCoupons");

            migrationBuilder.CreateTable(
                name: "DropboxTrialAccess",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    RedeemedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DropboxTrialAccess", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DropboxTrialAccess_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DropboxTrialAccess_UserId",
                table: "DropboxTrialAccess",
                column: "UserId",
                unique: true);
        }
    }
}
