using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Claims.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateSequence(
                name: "claim_number_seq");

            migrationBuilder.CreateTable(
                name: "markets",
                columns: table => new
                {
                    code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_markets", x => x.code);
                });

            migrationBuilder.CreateTable(
                name: "outbox_messages",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false),
                    claim_id = table.Column<Guid>(type: "uuid", nullable: false),
                    correlation_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    payload = table.Column<string>(type: "jsonb", nullable: false),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    sent_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    attempts = table.Column<int>(type: "integer", nullable: false),
                    last_error = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_outbox_messages", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "processed_events",
                columns: table => new
                {
                    event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    consumer = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    processed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_processed_events", x => new { x.event_id, x.consumer });
                });

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    display_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    role = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    password_hash = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_users", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "policies",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    policy_number = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    holder_id = table.Column<Guid>(type: "uuid", nullable: false),
                    market = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    product_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    start_date = table.Column<DateOnly>(type: "date", nullable: false),
                    end_date = table.Column<DateOnly>(type: "date", nullable: false),
                    coverage_limit_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    coverage_limit_currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    deductible_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    deductible_currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_policies", x => x.id);
                    table.ForeignKey(
                        name: "fk_policies_markets_market",
                        column: x => x.market,
                        principalTable: "markets",
                        principalColumn: "code",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_policies_users_holder_id",
                        column: x => x.holder_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "claims",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    claim_number = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    policy_id = table.Column<Guid>(type: "uuid", nullable: false),
                    claimant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    market = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    product_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    incident_date = table.Column<DateOnly>(type: "date", nullable: false),
                    description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    assigned_officer_id = table.Column<Guid>(type: "uuid", nullable: true),
                    assessment_notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    submitted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    claimed_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    claimed_currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    max_payable_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    max_payable_currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    reserve_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    reserve_currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_claims", x => x.id);
                    table.ForeignKey(
                        name: "fk_claims_markets_market",
                        column: x => x.market,
                        principalTable: "markets",
                        principalColumn: "code",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_claims_policies_policy_id",
                        column: x => x.policy_id,
                        principalTable: "policies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_claims_users_assigned_officer_id",
                        column: x => x.assigned_officer_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_claims_users_claimant_id",
                        column: x => x.claimant_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "claim_decisions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    claim_id = table.Column<Guid>(type: "uuid", nullable: false),
                    outcome = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    decided_by = table.Column<Guid>(type: "uuid", nullable: false),
                    decided_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    approved_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    approved_currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_claim_decisions", x => x.id);
                    table.ForeignKey(
                        name: "fk_claim_decisions_claims_claim_id",
                        column: x => x.claim_id,
                        principalTable: "claims",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_claim_decisions_users_decided_by",
                        column: x => x.decided_by,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "claim_status_history",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    claim_id = table.Column<Guid>(type: "uuid", nullable: false),
                    from_status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    to_status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    changed_by = table.Column<Guid>(type: "uuid", nullable: false),
                    changed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_claim_status_history", x => x.id);
                    table.ForeignKey(
                        name: "fk_claim_status_history_claims_claim_id",
                        column: x => x.claim_id,
                        principalTable: "claims",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_claim_status_history_users_changed_by",
                        column: x => x.changed_by,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "information_requests",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    claim_id = table.Column<Guid>(type: "uuid", nullable: false),
                    question = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    requested_by = table.Column<Guid>(type: "uuid", nullable: false),
                    requested_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    answer = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    answered_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_information_requests", x => x.id);
                    table.ForeignKey(
                        name: "fk_information_requests_claims_claim_id",
                        column: x => x.claim_id,
                        principalTable: "claims",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_information_requests_users_requested_by",
                        column: x => x.requested_by,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "notifications",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    recipient_id = table.Column<Guid>(type: "uuid", nullable: false),
                    claim_id = table.Column<Guid>(type: "uuid", nullable: false),
                    message = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    read_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_notifications", x => x.id);
                    table.ForeignKey(
                        name: "fk_notifications_claims_claim_id",
                        column: x => x.claim_id,
                        principalTable: "claims",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_notifications_users_recipient_id",
                        column: x => x.recipient_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_claim_decisions_claim_id",
                table: "claim_decisions",
                column: "claim_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_claim_decisions_decided_by",
                table: "claim_decisions",
                column: "decided_by");

            migrationBuilder.CreateIndex(
                name: "ix_claim_status_history_changed_by",
                table: "claim_status_history",
                column: "changed_by");

            migrationBuilder.CreateIndex(
                name: "ix_claim_status_history_claim_id",
                table: "claim_status_history",
                column: "claim_id");

            migrationBuilder.CreateIndex(
                name: "ix_claims_assigned_officer_id",
                table: "claims",
                column: "assigned_officer_id");

            migrationBuilder.CreateIndex(
                name: "ix_claims_claim_number",
                table: "claims",
                column: "claim_number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_claims_claimant_id",
                table: "claims",
                column: "claimant_id");

            migrationBuilder.CreateIndex(
                name: "ix_claims_market_status",
                table: "claims",
                columns: new[] { "market", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_claims_policy_id",
                table: "claims",
                column: "policy_id");

            migrationBuilder.CreateIndex(
                name: "ix_claims_status_assigned_officer_id",
                table: "claims",
                columns: new[] { "status", "assigned_officer_id" });

            migrationBuilder.CreateIndex(
                name: "ix_information_requests_claim_id",
                table: "information_requests",
                column: "claim_id");

            migrationBuilder.CreateIndex(
                name: "ix_information_requests_requested_by",
                table: "information_requests",
                column: "requested_by");

            migrationBuilder.CreateIndex(
                name: "ix_notifications_claim_id",
                table: "notifications",
                column: "claim_id");

            migrationBuilder.CreateIndex(
                name: "ix_notifications_recipient_id_read_at",
                table: "notifications",
                columns: new[] { "recipient_id", "read_at" });

            migrationBuilder.CreateIndex(
                name: "ix_outbox_messages_occurred_at",
                table: "outbox_messages",
                column: "occurred_at",
                filter: "sent_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_policies_holder_id",
                table: "policies",
                column: "holder_id");

            migrationBuilder.CreateIndex(
                name: "ix_policies_market",
                table: "policies",
                column: "market");

            migrationBuilder.CreateIndex(
                name: "ix_policies_policy_number",
                table: "policies",
                column: "policy_number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_users_email",
                table: "users",
                column: "email",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "claim_decisions");

            migrationBuilder.DropTable(
                name: "claim_status_history");

            migrationBuilder.DropTable(
                name: "information_requests");

            migrationBuilder.DropTable(
                name: "notifications");

            migrationBuilder.DropTable(
                name: "outbox_messages");

            migrationBuilder.DropTable(
                name: "processed_events");

            migrationBuilder.DropTable(
                name: "claims");

            migrationBuilder.DropTable(
                name: "policies");

            migrationBuilder.DropTable(
                name: "markets");

            migrationBuilder.DropTable(
                name: "users");

            migrationBuilder.DropSequence(
                name: "claim_number_seq");
        }
    }
}
