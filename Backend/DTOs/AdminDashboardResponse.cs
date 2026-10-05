namespace Backend.DTOs;

/// <summary>Database overview available to the superadmin.</summary>
public sealed record AdminDashboardResponse(
    IReadOnlyList<AdminUserResponse> Users,
    IReadOnlyList<AdminPendingRegistrationResponse> PendingRegistrations,
    IReadOnlyList<AdminCourseResponse> Courses,
    IReadOnlyList<AdminAccessResponse> AccessGrants,
    IReadOnlyList<AdminProgressResponse> LessonProgress
);

/// <summary>A registered user and account status without credential data.</summary>
public sealed record AdminUserResponse(
    int Id,
    string Email,
    string Name,
    bool EmailConfirmed,
    DateTimeOffset CreatedAt
);

/// <summary>A registration waiting for email confirmation.</summary>
public sealed record AdminPendingRegistrationResponse(
    string Email,
    string Name,
    DateTimeOffset CreatedAt,
    DateTimeOffset ExpiresAt
);

/// <summary>A local course and its lesson count.</summary>
public sealed record AdminCourseResponse(
    int Id,
    string Title,
    string Category,
    int LessonCount
);

/// <summary>A coupon redemption and its access expiry.</summary>
public sealed record AdminAccessResponse(
    int Id,
    int UserId,
    string Email,
    string? CoursePath,
    string Type,
    DateTimeOffset RedeemedAt,
    DateTimeOffset? ExpiresAt
);

/// <summary>A completed or in-progress lesson record.</summary>
public sealed record AdminProgressResponse(
    int UserId,
    string Email,
    string Course,
    string Lesson,
    bool IsCompleted,
    DateTimeOffset? CompletedAt
);
