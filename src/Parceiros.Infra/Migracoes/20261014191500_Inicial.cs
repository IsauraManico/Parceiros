using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Parceiros.Infra.Migracoes
{
    /// <inheritdoc />
    public partial class Inicial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Parceiros",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Nome = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false, collation: "SQL_Latin1_General_CP1_CI_AS"),
                    Nif = table.Column<string>(type: "char(9)", unicode: false, nullable: false),
                    Tipo = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(254)", maxLength: 254, nullable: true),
                    LimiteCredito = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Ativo = table.Column<bool>(type: "bit", nullable: false),
                    Versao = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "datetime2(7)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Parceiros", x => x.Id);
                    table.CheckConstraint("CK_Parceiros_Limite", "[LimiteCredito] >= 0 AND [LimiteCredito] <= 1000000");
                    table.CheckConstraint("CK_Parceiros_Nif", "[Nif] NOT LIKE '%[^0-9]%' AND LEN([Nif]) = 9");
                });

            migrationBuilder.CreateIndex(
                name: "IX_Parceiros_Nome_Id",
                table: "Parceiros",
                columns: new[] { "Nome", "Id" });

            migrationBuilder.CreateIndex(
                name: "UX_Parceiros_Nif",
                table: "Parceiros",
                column: "Nif",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Parceiros");
        }
    }
}
