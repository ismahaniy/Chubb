using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Claims.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddClaimSlaDueAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_claims_assigned_officer_id",
                table: "claims");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "sla_due_at",
                table: "claims",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            // Backfill existing rows with the same rule as Claim.Submit (submitted_at + Claim.SlaTarget, 7 days).
            // Hours, not days, so the result does not depend on the session time zone.
            migrationBuilder.Sql("UPDATE claims SET sla_due_at = submitted_at + interval '168 hours';");

            migrationBuilder.CreateIndex(
                name: "ix_claims_assigned_officer_id_sla_due_at",
                table: "claims",
                columns: new[] { "assigned_officer_id", "sla_due_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_claims_assigned_officer_id_sla_due_at",
                table: "claims");

            migrationBuilder.DropColumn(
                name: "sla_due_at",
                table: "claims");

            migrationBuilder.CreateIndex(
                name: "ix_claims_assigned_officer_id",
                table: "claims",
                column: "assigned_officer_id");
        }
    }
}
