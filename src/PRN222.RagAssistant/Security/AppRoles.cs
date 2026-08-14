namespace PRN222.RagAssistant.Security;

public static class AppRoles
{
    public const string Admin = "Admin";

    public const string SubjectLeader = "SubjectLeader";

    public const string Student = "Student";

    public static readonly string[] All = [Admin, SubjectLeader, Student];

    public static string GetDisplayName(string roleName) => roleName switch
    {
        Admin => "Admin",
        SubjectLeader => "Subject Leader",
        Student => "Student",
        _ => roleName
    };
}
