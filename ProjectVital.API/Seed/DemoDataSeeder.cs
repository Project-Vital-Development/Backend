using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using ProjectVital.Application.Interfaces;
using ProjectVital.Domain.Entities;
using ProjectVital.Domain.Enums;
using ProjectVital.Infrastructure.Persistence;

namespace ProjectVital.API.Seed;

/// <summary>
/// Base de DEMONSTRAÇÃO (dados 100% sintéticos — nenhum dado real de pessoas).
///
/// Três academias com perfis distintos, cada uma com gestor, planos e alunos próprios,
/// para demonstrar o isolamento entre tenants e treinar um modelo por academia.
/// O histórico cobre ~18 meses e simula, com sementes fixas (reprodutível):
///   - matrículas sazonais (pico em janeiro/fevereiro, baixa em novembro/dezembro);
///   - engajamento latente por aluno → frequência, permanência e regularidade;
///   - perfis de horário (manhã, almoço, fim de tarde, noite) e preferência de fim de semana;
///   - cancelamento por risco diário (maior para baixo engajamento, planos mensais e
///     nos primeiros 90 dias). Antes de cancelar: 55% deixam de frequentar 2–8 semanas
///     antes ("evasão silenciosa"), 30% reduzem gradualmente e 15% saem de forma abrupta;
///   - pausas de alunos ativos (férias/lesão) e queda geral no fim de ano;
///   - ~12% dos check-ins sem saída registrada (sem tempo de permanência);
///   - alunos recém-matriculados (histórico insuficiente) e exclusões LGPD.
/// Horários são gerados no fuso de Brasília e gravados em UTC (+3 h).
///
/// E-mails dos alunos usam subdomínios do domínio reservado example.com (RFC 2606), um por academia. Alunos recebem
/// um hash de senha inutilizável — não fazem login. Senha dos gestores: DEMO_MANAGER_PASSWORD.
/// Idempotente: uma academia só é criada se o e-mail do gestor ainda não existir.
/// </summary>
public sealed class DemoDataSeeder(ProjectVitalDbContext db, IAuthService auth, ILogger logger)
{
    private const int HistoryDays = 540;
    private const double BaseDailyHazard = 0.0017;
    private static readonly TimeSpan BrasiliaOffset = TimeSpan.FromHours(3);

    private sealed record PlanSpec(string Name, decimal Price, int Days, double ChurnMultiplier, double Weight);
    private sealed record HourProfile(double MeanHour, double StdHours, double Weight);
    private sealed record AcademySpec(
        string Name, string Address, string ManagerName, string ManagerEmail, string EmailDomain,
        int Students, int Seed, double EngagementShift, PlanSpec[] Plans, HourProfile[] Hours);

    private static readonly AcademySpec[] Academies =
    [
        new("Academia Corpo em Movimento", "Av. Brasil, 1450 — Centro, Americana/SP",
            "Gestor Corpo em Movimento", "gestor@vital.dev", "movimento.example.com", 900, 2026, 0.0,
            [
                new("Mensal", 129.90m, 30, 1.3, 0.38),
                new("Mensal Estudante", 99.90m, 30, 1.25, 0.12),
                new("Trimestral", 349.90m, 90, 1.0, 0.22),
                new("Semestral", 649.90m, 180, 0.8, 0.14),
                new("Anual", 1099.00m, 365, 0.5, 0.14),
            ],
            [new(6.7, 0.7, 0.25), new(12.3, 0.5, 0.15), new(18.6, 1.0, 0.45), new(20.6, 0.6, 0.15)]),
        new("Studio Equilíbrio Pilates & Funcional", "Rua Dom Pedro II, 820 — Santa Bárbara d'Oeste/SP",
            "Gestora Studio Equilíbrio", "gestora.equilibrio@vital.dev", "equilibrio.example.com", 450, 3031, 0.06,
            [
                new("Mensal 2x/semana", 189.00m, 30, 1.2, 0.45),
                new("Trimestral", 519.00m, 90, 0.9, 0.35),
                new("Anual", 1890.00m, 365, 0.5, 0.20),
            ],
            [new(7.5, 0.8, 0.35), new(10.0, 1.0, 0.25), new(17.8, 0.9, 0.40)]),
        new("Arena Treino Funcional", "Rua Carlos Botelho, 77 — Nova Odessa/SP",
            "Gestor Arena Funcional", "gestor.arena@vital.dev", "arena.example.com", 380, 4049, -0.04,
            [
                new("Mensal", 219.00m, 30, 1.35, 0.55),
                new("Semestral", 1140.00m, 180, 0.8, 0.25),
                new("Anual", 2040.00m, 365, 0.55, 0.20),
            ],
            [new(6.0, 0.5, 0.40), new(7.2, 0.5, 0.20), new(19.0, 0.8, 0.40)]),
    ];

    private static readonly string[] FirstNames =
    [
        "Ana", "Beatriz", "Camila", "Daniela", "Eduarda", "Fernanda", "Gabriela", "Helena", "Isabela", "Júlia",
        "Larissa", "Letícia", "Mariana", "Natália", "Olívia", "Patrícia", "Rafaela", "Sofia", "Tatiane", "Vitória",
        "Aline", "Bruna", "Carolina", "Débora", "Elisa", "Giovana", "Lívia", "Manuela", "Priscila", "Renata",
        "André", "Bruno", "Carlos", "Diego", "Eduardo", "Felipe", "Gabriel", "Henrique", "Igor", "João",
        "Lucas", "Matheus", "Nicolas", "Otávio", "Paulo", "Rafael", "Samuel", "Thiago", "Vinícius", "Wagner",
        "Arthur", "Bernardo", "Caio", "Davi", "Enzo", "Fábio", "Gustavo", "Leonardo", "Marcelo", "Rodrigo",
    ];

    private static readonly string[] LastNames =
    [
        "Silva", "Santos", "Oliveira", "Souza", "Rodrigues", "Ferreira", "Alves", "Pereira", "Lima", "Gomes",
        "Costa", "Ribeiro", "Martins", "Carvalho", "Almeida", "Lopes", "Soares", "Fernandes", "Vieira", "Barbosa",
        "Rocha", "Dias", "Nascimento", "Andrade", "Moreira", "Nunes", "Marques", "Machado", "Mendes", "Freitas",
        "Cardoso", "Ramos", "Gonçalves", "Santana", "Teixeira", "Moura", "Cavalcanti", "Pinto", "Araújo", "Campos",
        "Correia", "Monteiro", "Batista", "Bezerra", "Fonseca", "Prado", "Siqueira", "Tavares", "Queiroz", "Brandão",
    ];

    // Peso de matrículas por mês (jan..dez)
    private static readonly double[] EnrollmentSeasonality = [1.8, 1.5, 1.3, 1.0, 0.9, 0.8, 1.1, 1.2, 1.0, 0.9, 0.7, 0.5];

    public async Task SeedAsync(string managerPassword)
    {
        var managerHash = auth.HashPassword(managerPassword);
        // Hash de valor aleatório descartado: contas de aluno não têm senha utilizável.
        var unusableHash = auth.HashPassword(Convert.ToBase64String(Guid.NewGuid().ToByteArray()) + Guid.NewGuid());

        foreach (var spec in Academies)
        {
            if (await db.Users.IgnoreQueryFilters().AnyAsync(u => u.Email == spec.ManagerEmail))
                continue;
            var sw = System.Diagnostics.Stopwatch.StartNew();
            // Uma transação por academia: falha no meio não deixa academia "pela metade".
            var strategy = db.Database.CreateExecutionStrategy();
            var (students, cancelled, checkins) = await strategy.ExecuteAsync(async () =>
            {
                db.ChangeTracker.Clear();
                await using var tx = await db.Database.BeginTransactionAsync();
                var result = await SeedAcademyAsync(spec, managerHash, unusableHash);
                await tx.CommitAsync();
                return result;
            });
            logger.LogInformation(
                "Base de demonstração: {Academy} — {Students} alunos ({Cancelled} cancelados), {CheckIns} check-ins em {Seconds:F1}s.",
                spec.Name, students, cancelled, checkins, sw.Elapsed.TotalSeconds);
        }
    }

    private async Task<(int students, int cancelled, int checkins)> SeedAcademyAsync(
        AcademySpec spec, string managerHash, string unusableHash)
    {
        var rng = new Random(spec.Seed);
        var now = DateTime.UtcNow;
        var todayLocal = (now - BrasiliaOffset).Date;

        var manager = new User
        {
            Name = spec.ManagerName, Email = spec.ManagerEmail, PasswordHash = managerHash,
            Role = UserRole.Manager, CreatedAt = now.AddDays(-HistoryDays - 30),
        };
        db.Users.Add(manager);
        await db.SaveChangesAsync();

        var academy = new Academy { Name = spec.Name, Address = spec.Address, ManagerId = manager.Id, CreatedAt = manager.CreatedAt };
        db.Academies.Add(academy);
        await db.SaveChangesAsync();

        var plans = spec.Plans.Select(p => new Plan { AcademyId = academy.Id, Name = p.Name, Price = p.Price, DurationDays = p.Days }).ToList();
        db.Plans.AddRange(plans);
        await db.SaveChangesAsync();
        var academyId = academy.Id;
        var planIds = plans.Select(p => p.Id).ToArray();
        db.ChangeTracker.Clear();

        var prevDetect = db.ChangeTracker.AutoDetectChangesEnabled;
        db.ChangeTracker.AutoDetectChangesEnabled = false;
        var usedEmails = new HashSet<string>();
        int totalCheckins = 0, totalCancelled = 0;
        var lgpdCandidates = new List<Student>();

        for (var i = 0; i < spec.Students; i++)
        {
            double Normal(double mean, double sd)
            {
                var u1 = 1.0 - rng.NextDouble();
                return mean + sd * Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * rng.NextDouble());
            }
            T Pick<T>(IReadOnlyList<T> items, Func<T, double> weight)
            {
                var r = rng.NextDouble() * items.Sum(weight);
                foreach (var item in items) { r -= weight(item); if (r <= 0) return item; }
                return items[^1];
            }

            // Matrícula sazonal nos últimos 18 meses
            int enrollDaysAgo;
            while (true)
            {
                enrollDaysAgo = rng.Next(0, HistoryDays);
                var month = todayLocal.AddDays(-enrollDaysAgo).Month;
                if (rng.NextDouble() < EnrollmentSeasonality[month - 1] / 1.8) break;
            }

            var engagement = Math.Clamp((rng.NextDouble() + rng.NextDouble() + rng.NextDouble()) / 3 + 0.05 + spec.EngagementShift, 0.03, 0.98);
            var planIndex = Array.IndexOf(spec.Plans, Pick(spec.Plans, p => p.Weight));
            var plan = spec.Plans[planIndex];
            var hours = Pick(spec.Hours, h => h.Weight);
            var weekendFactor = rng.NextDouble() < 0.2 ? 1.4 : 0.45;
            var hourJitter = hours.StdHours * (1.0 + (1.0 - engagement));        // pouco engajado = rotina irregular
            var baseWeekly = 0.6 + 4.2 * engagement;
            var baseDuration = 35 + 55 * engagement;

            // Cancelamento: risco diário
            int? cancelDaysAgo = null;
            for (var d = enrollDaysAgo - 7; d >= 0; d--)
            {
                var tenure = enrollDaysAgo - d;
                var month = todayLocal.AddDays(-d).Month;
                var h = BaseDailyHazard * Math.Exp(2.5 * (0.45 - engagement)) * plan.ChurnMultiplier
                        * (tenure < 90 ? 1.5 : 1.0) * (month is 12 or 1 ? 1.3 : 1.0);
                if (rng.NextDouble() < h) { cancelDaysAgo = d; break; }
            }

            // Como o aluno sai: 55% param de frequentar 2–8 semanas ANTES de cancelar
            // ("evasão silenciosa"), 30% reduzem gradualmente e 15% cancelam de forma abrupta.
            var exitStyle = rng.NextDouble();
            var silentGap = exitStyle < 0.55 ? rng.Next(14, 57) : 0;
            var gradual = exitStyle < 0.85;
            var declineWindow = rng.Next(14, 50);
            var declineFloor = 0.1 + 0.4 * rng.NextDouble();
            var pauses = Enumerable.Range(0, rng.Next(0, 3))
                .Select(_ => { var start = rng.Next(0, Math.Max(1, enrollDaysAgo)); return (start, end: start - rng.Next(7, 26)); })
                .ToList();

            var (first, last) = (FirstNames[rng.Next(FirstNames.Length)], LastNames[rng.Next(LastNames.Length)]);
            var second = LastNames[rng.Next(LastNames.Length)];
            var email = MakeEmail(first, last, spec.EmailDomain, usedEmails);

            var student = new Student
            {
                User = new User
                {
                    Name = $"{first} {second} {last}", Email = email, PasswordHash = unusableHash,
                    Role = UserRole.Student, CreatedAt = todayLocal.AddDays(-enrollDaysAgo).Add(BrasiliaOffset).AddHours(10),
                },
                AcademyId = academyId,
                PlanId = planIds[planIndex],
                EnrollmentDate = todayLocal.AddDays(-enrollDaysAgo).Add(BrasiliaOffset).AddHours(10),
                Active = cancelDaysAgo is null,
                CancelledAt = cancelDaysAgo is int c ? todayLocal.AddDays(-c).Add(BrasiliaOffset).AddHours(rng.Next(9, 19)) : null,
            };

            // Último dia com treino: para quem "some", a frequência acaba antes do cancelamento
            var endDaysAgo = cancelDaysAgo is int cd0 ? Math.Min(enrollDaysAgo, cd0 + silentGap) : 0;
            var declineEnd = endDaysAgo;
            for (var d = enrollDaysAgo; d >= endDaysAgo; d--)
            {
                if (pauses.Any(p => d <= p.start && d > p.end)) continue;
                var date = todayLocal.AddDays(-d);
                var rate = baseWeekly / 7.0;
                if (date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) rate *= weekendFactor;
                if ((date.Month == 12 && date.Day >= 20) || (date.Month == 1 && date.Day <= 6)) rate *= 0.5;
                if (cancelDaysAgo is not null && gradual && d - declineEnd <= declineWindow)
                    rate *= declineFloor + (1 - declineFloor) * (d - declineEnd) / (double)declineWindow;
                if (rng.NextDouble() >= Math.Min(rate, 0.95)) continue;

                var localHour = Math.Clamp(Normal(hours.MeanHour, hourJitter), 5.5, 22.5);
                var timestamp = date.AddHours(localHour).Add(BrasiliaOffset);    // grava em UTC
                if (timestamp > now) continue;
                student.CheckIns.Add(new CheckIn
                {
                    Timestamp = timestamp,
                    DurationMinutes = rng.NextDouble() < 0.12 ? null : (int)Math.Clamp(Normal(baseDuration, 15), 15, 150),
                });
            }

            totalCheckins += student.CheckIns.Count;
            if (!student.Active)
            {
                totalCancelled++;
                if (lgpdCandidates.Count < 3 && cancelDaysAgo > 120) lgpdCandidates.Add(student);
            }
            db.Students.Add(student);

            if ((i + 1) % 40 == 0)
            {
                db.ChangeTracker.DetectChanges();
                await db.SaveChangesAsync();
                db.ChangeTracker.Clear();
            }
        }

        db.ChangeTracker.DetectChanges();
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        db.ChangeTracker.AutoDetectChangesEnabled = prevDetect;

        // Exclusões LGPD (Art. 18): alunos antigos que pediram eliminação — somem de
        // todas as consultas e do treino (filtro DeletedAt IS NULL).
        if (lgpdCandidates.Count > 0)
        {
            var ids = lgpdCandidates.Select(s => s.Id).ToList();
            var toDelete = await db.Students.IgnoreQueryFilters().Include(s => s.User)
                .Where(s => ids.Contains(s.Id)).ToListAsync();
            foreach (var s in toDelete) { s.SoftDelete(); s.User.SoftDelete(); }
            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();
        }

        return (spec.Students, totalCancelled, totalCheckins);
    }

    private static string MakeEmail(string first, string last, string domain, HashSet<string> used)
    {
        static string Ascii(string s)
        {
            var normalized = s.Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder();
            foreach (var ch in normalized)
                if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark && char.IsLetter(ch))
                    sb.Append(char.ToLowerInvariant(ch));
            return sb.ToString();
        }
        var baseName = $"{Ascii(first)}.{Ascii(last)}";
        var email = $"{baseName}@{domain}";
        for (var n = 2; !used.Add(email); n++) email = $"{baseName}{n}@{domain}";
        return email;
    }
}
