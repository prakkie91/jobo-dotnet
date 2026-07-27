namespace Jobo.Enterprise.Client;

// Closed value sets for the Jobo Enterprise Jobs API.
//
// These are not C# enums: the API exchanges plain strings, and the client
// methods take `string` parameters so any raw value remains valid. Each class
// below simply exposes the known values as `const string` fields for
// discoverability and autocomplete — e.g. `WorkModel.Remote` instead of
// remembering the literal "remote".
//
// Filter values are lowercase and hyphenated ("full-time", not "full_time");
// the API matches them exactly and an unrecognised value simply matches
// nothing.

/// <summary>Where the job is performed (<c>work_model</c> / <c>workplace_type</c>).</summary>
public static class WorkModel
{
    public const string Remote = "remote";
    public const string Hybrid = "hybrid";
    public const string Onsite = "onsite";
}

/// <summary>Nature of the engagement (<c>employment_type</c>).</summary>
public static class EmploymentType
{
    public const string FullTime = "full-time";
    public const string PartTime = "part-time";
    public const string Contract = "contract";
    public const string Internship = "internship";
    public const string Freelance = "freelance";
    public const string Temporary = "temporary";
}

/// <summary>Seniority of the role (<c>experience_level</c>).</summary>
public static class ExperienceLevel
{
    public const string Intern = "intern";
    public const string Entry = "entry";
    public const string Mid = "mid";
    public const string Senior = "senior";
    public const string Lead = "lead";
    public const string Executive = "executive";
}

/// <summary>Period a compensation range refers to (<c>compensation.period</c>).</summary>
public static class CompensationPeriod
{
    public const string Hourly = "hourly";
    public const string Daily = "daily";
    public const string Weekly = "weekly";
    public const string Monthly = "monthly";
    public const string Yearly = "yearly";
    public const string PerDiem = "per-diem";
}

/// <summary>Classification of a qualification skill (<c>skills[].type</c>).</summary>
public static class SkillType
{
    public const string Hard = "hard";
    public const string Soft = "soft";
}
