using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProjectVital.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddStudentCancelledAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CancelledAt",
                table: "Students",
                type: "datetime(6)",
                nullable: true);

            // Bases existentes: alunos inativos sem data de cancelamento recebem uma
            // estimativa (dia seguinte ao último check-in; sem check-ins, a matrícula).
            migrationBuilder.Sql(@"
                UPDATE Students s
                SET s.CancelledAt = COALESCE(
                    (SELECT DATE_ADD(MAX(c.Timestamp), INTERVAL 1 DAY) FROM CheckIns c WHERE c.StudentId = s.Id),
                    s.EnrollmentDate)
                WHERE s.Active = 0 AND s.CancelledAt IS NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CancelledAt",
                table: "Students");
        }
    }
}
