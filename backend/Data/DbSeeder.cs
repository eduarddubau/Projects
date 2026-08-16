using Backend.Config;
using Backend.Models;
using Backend.Services.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Backend.Data;

public static partial class DbSeeder
{
    private const string DefaultPassword = "Password123!";
    private const int UserCount = 3;

    public static async Task SeedAsync(
        UserManager<User> userManager,
        RoleManager<IdentityRole<Guid>> roleManager,
        AppDbContext context,
        ILogger logger,
        ProjectRetentionOptions retentionOptions,
        AdminSeedOptions adminOptions,
        ILookupNormalizer normalizer,
        IWorkspaceService workspaceService,
        bool isDevelopment
    )
    {
        LogSeedingStarted(logger);

        await SeedRolesAsync(roleManager, logger);
        await SeedAdminAsync(userManager, logger, adminOptions);

        // Users, then workspaces, then anything that belongs to one. Project.WorkspaceId
        // is required, so a project cannot be written before its workspace exists.
        var devUsers = isDevelopment
            ? await SeedDevelopmentUsersAsync(userManager, context, logger, normalizer)
            : [];

        await SeedPersonalWorkspacesAsync(context, workspaceService, logger);

        if (isDevelopment)
            await SeedDevelopmentContentAsync(context, logger, retentionOptions, devUsers);

        LogSeedingCompleted(logger);
    }

    private static async Task SeedRolesAsync(
        RoleManager<IdentityRole<Guid>> roleManager,
        ILogger logger
    )
    {
        foreach (var roleName in new[] { AppRoles.Admin, AppRoles.User })
        {
            if (await roleManager.RoleExistsAsync(roleName))
                continue;

            LogCreatingRole(logger, roleName);
            await roleManager.CreateAsync(new IdentityRole<Guid> { Name = roleName });
        }
    }

    private static async Task SeedAdminAsync(
        UserManager<User> userManager,
        ILogger logger,
        AdminSeedOptions options
    )
    {
        if (string.IsNullOrWhiteSpace(options.Email) || string.IsNullOrWhiteSpace(options.Password))
            throw new InvalidOperationException(
                $"No admin credentials configured. Set {AdminSeedOptions.SectionName}:Email and "
                    + $"{AdminSeedOptions.SectionName}:Password via configuration/secrets "
                    + $"(e.g. {AdminSeedOptions.SectionName}__Email / {AdminSeedOptions.SectionName}__Password)."
            );

        var existing = await userManager.FindByEmailAsync(options.Email);
        if (existing is not null)
        {
            LogAdminExists(logger, options.Email);
            return;
        }

        LogSeedingAdmin(logger, options.Email);
        var id = Guid.CreateVersion7();
        var admin = new User
        {
            Id = id,
            UserName = id.ToString("N"),
            Email = options.Email,
            FirstName = options.FirstName,
            LastName = options.LastName,
            EmailConfirmed = true,
        };

        var result = await userManager.CreateAsync(admin, options.Password);
        if (!result.Succeeded)
        {
            LogAdminSeedFailed(
                logger,
                options.Email,
                string.Join(", ", result.Errors.Select(e => e.Description))
            );
            return;
        }

        await userManager.AddToRoleAsync(admin, AppRoles.Admin);
    }

    private static async Task<List<User>> SeedDevelopmentUsersAsync(
        UserManager<User> userManager,
        AppDbContext context,
        ILogger logger,
        ILookupNormalizer normalizer
    )
    {
        var devUsers = new List<User>();

        for (var index = 1; index <= UserCount; index++)
        {
            var user = await SeedUserAsync(userManager, context, logger, normalizer, index);
            if (user is not null)
                devUsers.Add(user);
        }

        return devUsers;
    }

    private static async Task SeedDevelopmentContentAsync(
        AppDbContext context,
        ILogger logger,
        ProjectRetentionOptions retentionOptions,
        List<User> devUsers
    )
    {
        var sharedWorkspaceId = await SeedSharedWorkspaceAsync(context, logger, devUsers);

        for (var index = 1; index <= devUsers.Count; index++)
        {
            var user = devUsers[index - 1];

            // (Guid?), not Guid: a projected value type comes back as Guid.Empty when
            // there is no row, which reads as a real id.
            var personalWorkspaceId = await context
                .Workspaces.Where(w => w.IsPersonal && w.Members.Any(m => m.UserId == user.Id))
                .Select(w => (Guid?)w.Id)
                .FirstOrDefaultAsync();

            if (personalWorkspaceId is null)
            {
                LogNoPersonalWorkspace(logger, user.Email);
                continue;
            }

            await SeedProjectsForUserAsync(
                context,
                logger,
                user,
                index,
                retentionOptions.TrashWindowDays,
                personalWorkspaceId.Value
            );
        }

        if (sharedWorkspaceId is not null)
        {
            await SeedSharedProjectsAsync(context, logger, devUsers, sharedWorkspaceId.Value);
            await SeedSharedTasksAsync(context, logger, devUsers, sharedWorkspaceId.Value);
        }
    }

    /// <summary>Delegates to the service rather than reimplementing it: this used to be a
    /// near-verbatim copy, which meant the derived-name overflow had to be fixed twice.</summary>
    private static async Task SeedPersonalWorkspacesAsync(
        AppDbContext context,
        IWorkspaceService workspaceService,
        ILogger logger
    )
    {
        var adminRoleId = await context
            .Roles.Where(r => r.Name == AppRoles.Admin)
            .Select(r => (Guid?)r.Id)
            .FirstOrDefaultAsync();

        // Administrators hold no projects, so a personal workspace would be a row
        // nothing ever shows and every query has to remember to exclude.
        var users = await context
            .Users.Where(u => !u.IsAnonymized)
            .Where(u => !context.UserRoles.Any(ur => ur.UserId == u.Id && ur.RoleId == adminRoleId))
            .Where(u =>
                !context.Workspaces.Any(w => w.IsPersonal && w.Members.Any(m => m.UserId == u.Id))
            )
            .ToListAsync();

        if (users.Count == 0)
        {
            LogAllWorkspacesPresent(logger);
            return;
        }

        foreach (var user in users)
        {
            LogSeedingPersonalWorkspace(logger, user.Id);
            await workspaceService.EnsurePersonalWorkspaceAsync(user);
        }

        LogSeededPersonalWorkspaces(logger, users.Count);
    }

    private static async Task<User?> SeedUserAsync(
        UserManager<User> userManager,
        AppDbContext context,
        ILogger logger,
        ILookupNormalizer normalizer,
        int index
    )
    {
        var email = $"dev{index}@example.com";

        // Looked up past the query filter, not via FindByEmailAsync, which reads through it:
        // a soft-deleted user is invisible to !IsDeleted, so the seeder would decide the
        // account is missing. Since AddPartialUniqueUserIndexes that no longer fails on a
        // duplicate — the insert would succeed and silently resurrect the account — which
        // makes the skip below a policy choice rather than crash-avoidance.
        // Hoisted out of the predicate: a method call inside an expression tree relies on
        // EF's parameter extraction, which works here but is not something to depend on.
        var normalizedEmail = normalizer.NormalizeEmail(email);

        var existingUser = await context
            .Users.IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.NormalizedEmail == normalizedEmail);

        if (existingUser is not null)
        {
            // Deleting a dev user is a deliberate act; resurrecting it on the next boot
            // would undo that silently.
            if (existingUser.IsDeleted)
            {
                LogDevUserDeleted(logger, email);
                return null;
            }

            return existingUser;
        }

        LogSeedingUser(logger, email);
        var id = Guid.CreateVersion7();
        var user = new User
        {
            Id = id,
            UserName = id.ToString("N"),
            Email = email,
            FirstName = "Dev",
            LastName = $"User{index}",
            Nickname = $"dev{index}",
            EmailConfirmed = true,
        };

        var result = await userManager.CreateAsync(user, DefaultPassword);
        if (!result.Succeeded)
        {
            LogUserSeedFailed(
                logger,
                email,
                string.Join(", ", result.Errors.Select(e => e.Description))
            );
            return null;
        }

        await userManager.AddToRoleAsync(user, AppRoles.User);

        return user;
    }

    private static async Task SeedProjectsForUserAsync(
        AppDbContext context,
        ILogger logger,
        User user,
        int index,
        int trashWindowDays,
        Guid workspaceId
    )
    {
        if (await context.Projects.AnyAsync(p => p.WorkspaceId == workspaceId))
        {
            LogUserHasProjects(logger, user.Email);
            return;
        }

        LogSeedingProjects(logger, user.Email);

        var activeProjects = new[]
        {
            new Project
            {
                Name = $"{user.FirstName}'s First Project",
                Description = "Automatically generated starter project.",
                CreatedBy = user.Id,
                WorkspaceId = workspaceId,
            },
            new Project
            {
                Name = $"Ongoing Research Project no {index}",
                Description = "A project for tracking long-term goals.",
                CreatedBy = user.Id,
                WorkspaceId = workspaceId,
            },
        };

        // Tiered ages relative to the retention window, so the admin purge filters
        // (>30/>60/>90 days) each have something to show: one project still within
        // the window, and three past it by increasing margins.
        var deletedAges = new[]
        {
            5,
            trashWindowDays + 5,
            trashWindowDays + 35,
            trashWindowDays + 65,
        };

        var deletedProjects = deletedAges
            .Select(ageDays => new Project
            {
                Name = $"{user.FirstName}'s Project Deleted {ageDays} Days Ago",
                Description =
                    ageDays <= trashWindowDays
                        ? "Soft-deleted recently; still within the trash retention window."
                        : $"Soft-deleted {ageDays} days ago; past the {trashWindowDays}-day retention window.",
                CreatedBy = user.Id,
                WorkspaceId = workspaceId,
            })
            .ToArray();

        context.Projects.AddRange(activeProjects);
        context.Projects.AddRange(deletedProjects);
        await context.SaveChangesAsync();

        // SaveChangesAsync forces IsDeleted = false for newly Added entities, so these
        // are seeded active first, then soft-deleted with backdated timestamps here.
        for (var i = 0; i < deletedProjects.Length; i++)
        {
            deletedProjects[i].IsDeleted = true;
            deletedProjects[i].DeletedAt = DateTime.UtcNow.AddDays(-deletedAges[i]);
        }

        await context.SaveChangesAsync();
    }

    /// <summary>One shared workspace so the switcher, the members page and the multi-owner
    /// and last-owner guards have something real to run against.</summary>
    private static async Task<Guid?> SeedSharedWorkspaceAsync(
        AppDbContext context,
        ILogger logger,
        List<User> devUsers
    )
    {
        const string sharedName = "Acme Team";

        if (devUsers.Count == 0)
        {
            LogNoDevUsersForSharedWorkspace(logger);
            return null;
        }

        var existingId = await context
            .Workspaces.Where(w => !w.IsPersonal && w.Name == sharedName)
            .Select(w => (Guid?)w.Id)
            .FirstOrDefaultAsync();

        if (existingId is not null)
        {
            LogSharedWorkspaceExists(logger);
            return existingId;
        }

        LogSeedingSharedWorkspace(logger, sharedName);

        var workspace = new Workspace
        {
            Name = sharedName,
            Description = "Shared demo workspace.",
            IsPersonal = false,
            // No HTTP context at startup, so SaveChangesAsync can't infer the creator.
            // devUsers is filled dev1..devN in order, so the first is dev1.
            CreatedBy = devUsers[0].Id,
        };

        for (var i = 0; i < devUsers.Count; i++)
        {
            workspace.Members.Add(
                new WorkspaceMember
                {
                    UserId = devUsers[i].Id,
                    Role = i == 0 ? WorkspaceRole.Owner : WorkspaceRole.Member,
                    JoinedAt = DateTime.UtcNow,
                }
            );
        }

        context.Workspaces.Add(workspace);
        await context.SaveChangesAsync();

        return workspace.Id;
    }

    /// <summary>Projects owned by the shared workspace rather than by one person, so the
    /// members-see-each-other's-work behaviour has something to run against.</summary>
    private static async Task SeedSharedProjectsAsync(
        AppDbContext context,
        ILogger logger,
        List<User> devUsers,
        Guid workspaceId
    )
    {
        if (await context.Projects.AnyAsync(p => p.WorkspaceId == workspaceId))
            return;

        LogSeedingSharedProjects(logger);

        context.Projects.AddRange(
            new Project
            {
                Name = RedesignProjectName,
                Description = "Shared workspace project, created by the owner.",
                CreatedBy = devUsers[0].Id,
                WorkspaceId = workspaceId,
            },
            new Project
            {
                Name = "Acme Q3 Roadmap",
                Description = "Shared workspace project, created by another member.",
                CreatedBy = devUsers[^1].Id,
                WorkspaceId = workspaceId,
            }
        );

        await context.SaveChangesAsync();
    }

    /// <summary>Shared between the project seed and the task seed, so the lookup can't drift.</summary>
    private const string RedesignProjectName = "Acme Website Redesign";

    /// <summary>Gives the Acme board something to look like a board with.</summary>
    private static async Task SeedSharedTasksAsync(
        AppDbContext context,
        ILogger logger,
        List<User> devUsers,
        Guid workspaceId
    )
    {
        // By name, not "the first one": these fixtures describe a redesign, and picking
        // positionally silently filed them under the roadmap project instead.
        var found = await context
            .Projects.Where(p => p.WorkspaceId == workspaceId && p.Name == RedesignProjectName)
            .Select(p => (Guid?)p.Id)
            .FirstOrDefaultAsync();

        if (found is not Guid projectId)
            return;

        // IgnoreQueryFilters: without it, deleting every seeded card and restarting seeds a
        // second set on top of the soft-deleted first one.
        if (await context.Tasks.IgnoreQueryFilters().AnyAsync(t => t.ProjectId == projectId))
            return;

        LogSeedingSharedTasks(logger);

        var owner = devUsers[0].Id;
        var member = devUsers[^1].Id;
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // Titles, statuses and dates chosen to exercise every state the card renders:
        // overdue, due soon, undated, unassigned, and completed.
        (
            string Title,
            string? Description,
            TaskItemStatus Status,
            Guid? Assignee,
            DateOnly? Start,
            DateOnly? Due
        )[] fixtures =
        [
            (
                "Audit the current sitemap",
                "Catalogue every page before anything moves.",
                TaskItemStatus.Done,
                owner,
                today.AddDays(-21),
                today.AddDays(-14)
            ),
            (
                "Agree the new information architecture",
                null,
                TaskItemStatus.Done,
                member,
                today.AddDays(-14),
                today.AddDays(-7)
            ),
            (
                "Rebuild the marketing homepage",
                "Hero, testimonials, pricing table.",
                TaskItemStatus.InProgress,
                owner,
                today.AddDays(-3),
                today.AddDays(4)
            ),
            (
                "Migrate blog posts to the new CMS",
                null,
                TaskItemStatus.InProgress,
                member,
                today.AddDays(-10),
                today.AddDays(-2)
            ),
            (
                "Write copy for the pricing page",
                "Waiting on final numbers from finance.",
                TaskItemStatus.Todo,
                null,
                null,
                today.AddDays(11)
            ),
            (
                "Set up redirects for retired URLs",
                "Every removed page needs a 301.",
                TaskItemStatus.Todo,
                owner,
                null,
                null
            ),
            (
                "Accessibility pass on the new templates",
                "Colour contrast, focus order, alt text.",
                TaskItemStatus.Todo,
                member,
                today.AddDays(7),
                today.AddDays(18)
            ),
            ("Decide on a webfont licence", null, TaskItemStatus.Todo, null, null, null),
        ];

        // Position is assigned per column by the loop, never hardcoded: a hardcoded list
        // silently stops sorting the moment someone reorders the fixtures above.
        var nextPosition = new Dictionary<TaskItemStatus, int>();

        foreach (var (title, description, status, assignee, start, due) in fixtures)
        {
            nextPosition.TryGetValue(status, out var position);
            nextPosition[status] = position + 1;

            context.Tasks.Add(
                new TaskItem
                {
                    Title = title,
                    Description = description,
                    Status = status,
                    Position = position,
                    ProjectId = projectId,
                    AssigneeId = assignee,
                    StartDate = start,
                    DueDate = due,
                    CompletedAt =
                        status == TaskItemStatus.Done ? DateTime.UtcNow.AddDays(-5) : null,
                    CreatedBy = owner,
                }
            );
        }

        await context.SaveChangesAsync();
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Starting database seeding...")]
    private static partial void LogSeedingStarted(ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Seeding shared workspace tasks...")]
    private static partial void LogSeedingSharedTasks(ILogger logger);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Database seeding completed successfully."
    )]
    private static partial void LogSeedingCompleted(ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Creating role: {roleName}")]
    private static partial void LogCreatingRole(ILogger logger, string roleName);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Admin account {email} already exists. Skipping admin seed."
    )]
    private static partial void LogAdminExists(ILogger logger, string email);

    [LoggerMessage(Level = LogLevel.Information, Message = "Seeding admin account: {email}")]
    private static partial void LogSeedingAdmin(ILogger logger, string email);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to seed admin {email}: {errors}")]
    private static partial void LogAdminSeedFailed(ILogger logger, string email, string errors);

    [LoggerMessage(
        Level = LogLevel.Debug,
        Message = "Every user already has a personal workspace."
    )]
    private static partial void LogAllWorkspacesPresent(ILogger logger);

    [LoggerMessage(
        Level = LogLevel.Debug,
        Message = "Seeding personal workspace for user: {userId}"
    )]
    private static partial void LogSeedingPersonalWorkspace(ILogger logger, Guid userId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Seeded {count} personal workspace(s).")]
    private static partial void LogSeededPersonalWorkspaces(ILogger logger, int count);

    [LoggerMessage(
        Level = LogLevel.Debug,
        Message = "Dev user {email} is deleted. Leaving it alone."
    )]
    private static partial void LogDevUserDeleted(ILogger logger, string email);

    [LoggerMessage(Level = LogLevel.Information, Message = "Seeding user: {email}")]
    private static partial void LogSeedingUser(ILogger logger, string email);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to seed user {email}: {errors}")]
    private static partial void LogUserSeedFailed(ILogger logger, string email, string errors);

    [LoggerMessage(
        Level = LogLevel.Debug,
        Message = "User {email} already has projects. Skipping project seed."
    )]
    private static partial void LogUserHasProjects(ILogger logger, string? email);

    [LoggerMessage(Level = LogLevel.Information, Message = "Seeding projects for user: {email}")]
    private static partial void LogSeedingProjects(ILogger logger, string? email);

    [LoggerMessage(
        Level = LogLevel.Debug,
        Message = "No dev users to place in the shared workspace. Skipping."
    )]
    private static partial void LogNoDevUsersForSharedWorkspace(ILogger logger);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Shared workspace already seeded. Skipping.")]
    private static partial void LogSharedWorkspaceExists(ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Seeding shared workspace: {name}")]
    private static partial void LogSeedingSharedWorkspace(ILogger logger, string name);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Seeding projects for the shared workspace."
    )]
    private static partial void LogSeedingSharedProjects(ILogger logger);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "No personal workspace for {email}. Skipping their projects."
    )]
    private static partial void LogNoPersonalWorkspace(ILogger logger, string? email);
}
