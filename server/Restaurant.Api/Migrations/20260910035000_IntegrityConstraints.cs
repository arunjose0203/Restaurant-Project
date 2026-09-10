using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Restaurant.Api.Migrations
{
    /// <inheritdoc />
    public partial class IntegrityConstraints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Notifications_UserId",
                table: "Notifications");

            migrationBuilder.AddCheckConstraint(
                name: "CK_User_Role",
                table: "Users",
                sql: "\"Role\" IN ('Waiter','Kitchen','Cashier','Admin')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Table_Seats",
                table: "Tables",
                sql: "\"Seats\" BETWEEN 1 AND 50");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_TableId",
                table: "Orders",
                column: "TableId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Order_Status",
                table: "Orders",
                sql: "\"Status\" IN ('New','Preparing','Ready','Served','Paid')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Item_Price",
                table: "OrderItems",
                sql: "\"UnitPrice\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Item_Quantity",
                table: "OrderItems",
                sql: "\"Quantity\" BETWEEN 1 AND 99");

            migrationBuilder.CreateIndex(
                name: "IX_OrderEvents_UserId",
                table: "OrderEvents",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_UserId_Read_CreatedAt",
                table: "Notifications",
                columns: new[] { "UserId", "Read", "CreatedAt" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_Menu_Price",
                table: "MenuItems",
                sql: "\"Price\" >= 0");

            migrationBuilder.CreateIndex(
                name: "IX_Bills_CashierId",
                table: "Bills",
                column: "CashierId");

            migrationBuilder.AddForeignKey(
                name: "FK_Bills_Users_CashierId",
                table: "Bills",
                column: "CashierId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_OrderEvents_Users_UserId",
                table: "OrderEvents",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Orders_Tables_TableId",
                table: "Orders",
                column: "TableId",
                principalTable: "Tables",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Bills_Users_CashierId",
                table: "Bills");

            migrationBuilder.DropForeignKey(
                name: "FK_OrderEvents_Users_UserId",
                table: "OrderEvents");

            migrationBuilder.DropForeignKey(
                name: "FK_Orders_Tables_TableId",
                table: "Orders");

            migrationBuilder.DropCheckConstraint(
                name: "CK_User_Role",
                table: "Users");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Table_Seats",
                table: "Tables");

            migrationBuilder.DropIndex(
                name: "IX_Orders_TableId",
                table: "Orders");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Order_Status",
                table: "Orders");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Item_Price",
                table: "OrderItems");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Item_Quantity",
                table: "OrderItems");

            migrationBuilder.DropIndex(
                name: "IX_OrderEvents_UserId",
                table: "OrderEvents");

            migrationBuilder.DropIndex(
                name: "IX_Notifications_UserId_Read_CreatedAt",
                table: "Notifications");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Menu_Price",
                table: "MenuItems");

            migrationBuilder.DropIndex(
                name: "IX_Bills_CashierId",
                table: "Bills");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_UserId",
                table: "Notifications",
                column: "UserId");
        }
    }
}
