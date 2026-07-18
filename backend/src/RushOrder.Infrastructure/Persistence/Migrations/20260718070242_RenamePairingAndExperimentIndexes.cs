using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RushOrder.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RenamePairingAndExperimentIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameIndex(
                name: "IX_product_pairing_rules_source_target",
                table: "product_pairing_rules",
                newName: "IX_product_pairing_rules_SourceProductId_TargetProductId");

            migrationBuilder.RenameIndex(
                name: "IX_product_pairing_rules_lookup",
                table: "product_pairing_rules",
                newName: "IX_product_pairing_rules_TenantId_RestaurantId_SourceProductId~");

            migrationBuilder.RenameIndex(
                name: "IX_experiments_restaurant_key",
                table: "experiments",
                newName: "IX_experiments_RestaurantId_Key");

            migrationBuilder.RenameIndex(
                name: "IX_experiment_results_lookup",
                table: "experiment_results",
                newName: "IX_experiment_results_TenantId_RestaurantId_ExperimentKey_Even~");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameIndex(
                name: "IX_product_pairing_rules_TenantId_RestaurantId_SourceProductId~",
                table: "product_pairing_rules",
                newName: "IX_product_pairing_rules_lookup");

            migrationBuilder.RenameIndex(
                name: "IX_product_pairing_rules_SourceProductId_TargetProductId",
                table: "product_pairing_rules",
                newName: "IX_product_pairing_rules_source_target");

            migrationBuilder.RenameIndex(
                name: "IX_experiments_RestaurantId_Key",
                table: "experiments",
                newName: "IX_experiments_restaurant_key");

            migrationBuilder.RenameIndex(
                name: "IX_experiment_results_TenantId_RestaurantId_ExperimentKey_Even~",
                table: "experiment_results",
                newName: "IX_experiment_results_lookup");
        }
    }
}
