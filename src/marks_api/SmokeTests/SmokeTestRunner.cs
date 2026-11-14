using System.Text.Json;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;

namespace marks_api.SmokeTests;

/// <summary>
/// Runs end-to-end smoke tests against a real Kestrel instance and the real
/// PostgreSQL database. Invoked only with --smoke-test or SMOKE_TEST=true.
/// </summary>
public static class SmokeTestRunner
{
    public static async Task<int> RunAsync(
        WebApplication app,
        IConfiguration configuration)
    {
        var teacherUsername =
            configuration["SmokeTest:TeacherUsername"] ?? "teacher1";
        var teacherPassword =
            configuration["SmokeTest:TeacherPassword"];
        var studentUsername =
            configuration["SmokeTest:StudentUsername"] ?? "ali.hassan";
        var studentPassword =
            configuration["SmokeTest:StudentPassword"];

        if (string.IsNullOrWhiteSpace(teacherPassword) ||
            string.IsNullOrWhiteSpace(studentPassword))
        {
            Console.Error.WriteLine(
                "Smoke tests require credentials in configuration or " +
                "User Secrets:");
            Console.Error.WriteLine("  SmokeTest:TeacherPassword");
            Console.Error.WriteLine("  SmokeTest:StudentPassword");
            Console.Error.WriteLine(
                "No requests were sent and no test data was created.");

            return 1;
        }

        // Port 0 lets Kestrel choose a free port, which keeps concurrent runs
        // from colliding.
        app.Urls.Add("http://127.0.0.1:0");

        try
        {
            await app.StartAsync();
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(
                "Could not start the application for smoke tests: " +
                exception.Message);

            return 1;
        }

        var addresses = app.Services
            .GetRequiredService<IServer>()
            .Features
            .Get<IServerAddressesFeature>()
            ?.Addresses;

        var baseUrl = addresses?.FirstOrDefault();

        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            Console.Error.WriteLine(
                "Could not determine the smoke-test base URL.");

            await app.StopAsync();
            return 1;
        }

        Console.WriteLine($"Smoke tests against {baseUrl}");
        Console.WriteLine();

        var results = new SmokeTestResults();
        var data = new SmokeTestData();

        using var client = new SmokeTestClient(baseUrl);

        try
        {
            await RunScenariosAsync(
                client,
                results,
                data,
                configuration,
                teacherUsername,
                teacherPassword,
                studentUsername,
                studentPassword);
        }
        catch (Exception exception)
        {
            results.Fail(
                "Smoke-test run",
                $"{exception.GetType().Name}: {exception.Message}");
        }
        finally
        {
            await CleanupAsync(client, results, data);
            await app.StopAsync();
        }

        return results.PrintSummary();
    }

    private static async Task RunScenariosAsync(
        SmokeTestClient client,
        SmokeTestResults results,
        SmokeTestData data,
        IConfiguration configuration,
        string teacherUsername,
        string teacherPassword,
        string studentUsername,
        string studentPassword)
    {
        async Task<SmokeTestResponse> Send(
            HttpMethod method,
            string path,
            string? token = null,
            object? body = null)
            => await client.SendAsync(
                method,
                path,
                token,
                body,
                CancellationToken.None);

        // ---------------------------------------------------------------
        // Health
        // ---------------------------------------------------------------
        var health = await Send(HttpMethod.Get, "/health");
        results.Check(
            "GET /health returns 200",
            health.StatusCode == 200,
            $"status={health.StatusCode}");
        results.Check(
            "Health status is Healthy",
            health.GetString("status") == "Healthy",
            $"status={health.GetString("status")}");
        results.Check(
            "Health includes the PostgreSQL check",
            health.Body.Contains("\"postgresql\"", StringComparison.Ordinal),
            "check name present");

        // ---------------------------------------------------------------
        // Authentication
        // ---------------------------------------------------------------
        var teacherLogin = await Send(
            HttpMethod.Post,
            "/api/auth/login",
            body: new { username = teacherUsername, password = teacherPassword });

        results.Check(
            "Teacher login returns 200",
            teacherLogin.StatusCode == 200,
            $"status={teacherLogin.StatusCode}");

        data.TeacherToken = teacherLogin.GetString("token") ?? string.Empty;

        results.Check(
            "Teacher login returns a token",
            data.TeacherToken.Length > 0,
            "token present");

        if (data.TeacherToken.Length == 0)
        {
            results.Fail(
                "Smoke-test abort",
                "Teacher login failed, so dependent scenarios were not run.");
            return;
        }

        var studentLogin = await Send(
            HttpMethod.Post,
            "/api/auth/login",
            body: new { username = studentUsername, password = studentPassword });

        results.Check(
            "Student login returns 200",
            studentLogin.StatusCode == 200,
            $"status={studentLogin.StatusCode}");

        data.StudentToken = studentLogin.GetString("token") ?? string.Empty;

        results.Check(
            "Student login returns a token",
            data.StudentToken.Length > 0,
            "token present");

        if (data.StudentToken.Length == 0)
        {
            results.Fail(
                "Smoke-test abort",
                "Student login failed, so dependent scenarios were not run.");
            return;
        }

        var badLogin = await Send(
            HttpMethod.Post,
            "/api/auth/login",
            body: new
            {
                username = teacherUsername,
                password = "Definitely-Wrong-Password!"
            });

        results.Check(
            "Invalid credentials return 401 ProblemDetails",
            badLogin.StatusCode == 401 && badLogin.HasProperty("status"),
            $"status={badLogin.StatusCode}");

        var me = await Send(
            HttpMethod.Get,
            "/api/auth/me",
            data.TeacherToken);

        results.Check(
            "GET /api/auth/me returns the Teacher role",
            me.StatusCode == 200 && me.GetString("role") == "Teacher",
            $"status={me.StatusCode}, role={me.GetString("role")}");

        var meWithoutToken = await Send(HttpMethod.Get, "/api/auth/me");

        results.Check(
            "GET /api/auth/me without a token returns 401 ProblemDetails",
            meWithoutToken.StatusCode == 401 &&
            meWithoutToken.HasProperty("status"),
            $"status={meWithoutToken.StatusCode}");

        // ---------------------------------------------------------------
        // Students
        // ---------------------------------------------------------------
        var studentMe = await Send(
            HttpMethod.Get,
            "/api/auth/me",
            data.StudentToken);

        data.StudentId = int.TryParse(
            studentMe.GetString("studentId"),
            out var studentId)
            ? studentId
            : 0;

        results.Check(
            "Student token contains a studentId claim",
            data.StudentId > 0,
            $"studentId={data.StudentId}");

        var students = await Send(
            HttpMethod.Get,
            "/api/students?page=1&pageSize=100",
            data.TeacherToken);

        results.Check(
            "Teacher lists students",
            students.StatusCode == 200 && students.HasProperty("items"),
            $"status={students.StatusCode}");

        var studentsForbidden = await Send(
            HttpMethod.Get,
            "/api/students",
            data.StudentToken);

        results.Check(
            "Student listing all students returns 403",
            studentsForbidden.StatusCode == 403 &&
            studentsForbidden.HasProperty("status"),
            $"status={studentsForbidden.StatusCode}");

        if (data.StudentId > 0)
        {
            var ownRecord = await Send(
                HttpMethod.Get,
                $"/api/students/{data.StudentId}",
                data.StudentToken);

            results.Check(
                "Student reads their own record",
                ownRecord.StatusCode == 200 &&
                ownRecord.GetInt("id") == data.StudentId,
                $"status={ownRecord.StatusCode}");

            if (students.Json is JsonElement studentsJson &&
                studentsJson.TryGetProperty("items", out var items) &&
                items.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in items.EnumerateArray())
                {
                    if (item.TryGetProperty("id", out var idProperty) &&
                        idProperty.TryGetInt32(out var otherId) &&
                        otherId != data.StudentId)
                    {
                        data.OtherStudentId = otherId;
                        break;
                    }
                }
            }

            if (data.OtherStudentId is int otherStudentId)
            {
                var otherRecord = await Send(
                    HttpMethod.Get,
                    $"/api/students/{otherStudentId}",
                    data.StudentToken);

                results.Check(
                    "Student reading another student's record returns 403",
                    otherRecord.StatusCode == 403,
                    $"status={otherRecord.StatusCode}");
            }
        }

        var missingStudent = await Send(
            HttpMethod.Get,
            "/api/students/999999",
            data.TeacherToken);

        results.Check(
            "Reading a nonexistent student returns 404",
            missingStudent.StatusCode == 404,
            $"status={missingStudent.StatusCode}");

        var unique = DateTime.UtcNow.Ticks % 10_000_000_000;

        data.CreatedStudentRegistrationNo = $"SMK{unique:D10}";

        var createStudentBody = new
        {
            registrationNo = data.CreatedStudentRegistrationNo,
            fullName = "Smoke Test Student",
            email = $"smoke{unique:D10}@example.com"
        };

        var createdStudent = await Send(
            HttpMethod.Post,
            "/api/students",
            data.TeacherToken,
            createStudentBody);

        results.Check(
            "Create student returns 201",
            createdStudent.StatusCode == 201,
            $"status={createdStudent.StatusCode}");

        data.CreatedStudentId = createdStudent.GetInt("id");

        results.Check(
            "Created student has an id",
            data.CreatedStudentId is > 0,
            $"id={data.CreatedStudentId}");

        var duplicateStudent = await Send(
            HttpMethod.Post,
            "/api/students",
            data.TeacherToken,
            createStudentBody);

        results.Check(
            "Duplicate student registration returns 409",
            duplicateStudent.StatusCode == 409,
            $"status={duplicateStudent.StatusCode}");

        if (data.CreatedStudentId is int createdStudentId)
        {
            var updatedStudent = await Send(
                HttpMethod.Put,
                $"/api/students/{createdStudentId}",
                data.TeacherToken,
                new
                {
                    fullName = "Smoke Test Student Updated",
                    email = $"smoke{unique:D10}@example.com"
                });

            results.Check(
                "Update student returns 200",
                updatedStudent.StatusCode == 200 &&
                updatedStudent.GetString("fullName") ==
                "Smoke Test Student Updated",
                $"status={updatedStudent.StatusCode}");

            var deletedStudent = await Send(
                HttpMethod.Delete,
                $"/api/students/{createdStudentId}",
                data.TeacherToken);

            results.Check(
                "Delete student returns 204",
                deletedStudent.StatusCode == 204,
                $"status={deletedStudent.StatusCode}");

            data.CreatedStudentId = null;
        }

        var missingStudentDelete = await Send(
            HttpMethod.Delete,
            "/api/students/999999",
            data.TeacherToken);

        results.Check(
            "Deleting a nonexistent student returns 404",
            missingStudentDelete.StatusCode == 404,
            $"status={missingStudentDelete.StatusCode}");

        // ---------------------------------------------------------------
        // Courses
        // ---------------------------------------------------------------
        var courses = await Send(
            HttpMethod.Get,
            "/api/courses",
            data.TeacherToken);

        results.Check(
            "Teacher lists courses",
            courses.StatusCode == 200 &&
            courses.ArrayLength() is > 0,
            $"status={courses.StatusCode}");

        if (courses.Json is JsonElement coursesJson &&
            coursesJson.ValueKind == JsonValueKind.Array &&
            coursesJson.GetArrayLength() > 0 &&
            coursesJson[0].TryGetProperty("id", out var firstCourseId) &&
            firstCourseId.TryGetInt32(out var seededCourseId))
        {
            data.SeededCourseId = seededCourseId;
        }

        if (data.SeededCourseId > 0)
        {
            var courseById = await Send(
                HttpMethod.Get,
                $"/api/courses/{data.SeededCourseId}",
                data.TeacherToken);

            results.Check(
                "Get course by valid id returns 200",
                courseById.StatusCode == 200 &&
                courseById.GetInt("id") == data.SeededCourseId,
                $"status={courseById.StatusCode}");
        }

        var missingCourse = await Send(
            HttpMethod.Get,
            "/api/courses/999999",
            data.TeacherToken);

        results.Check(
            "Get nonexistent course returns 404",
            missingCourse.StatusCode == 404,
            $"status={missingCourse.StatusCode}");

        var courseSuffix = DateTime.UtcNow.Ticks % 10_000_000;

        data.CourseCode = $"SMK{courseSuffix:D7}";
        data.CourseTitle = "Smoke Test Course";

        var createCourseBody = new
        {
            code = data.CourseCode,
            title = data.CourseTitle,
            creditHours = 3
        };

        var createdCourse = await Send(
            HttpMethod.Post,
            "/api/courses",
            data.TeacherToken,
            createCourseBody);

        results.Check(
            "Create course returns 201",
            createdCourse.StatusCode == 201,
            $"status={createdCourse.StatusCode}");

        data.CreatedCourseId = createdCourse.GetInt("id");

        results.Check(
            "Created course has an id",
            data.CreatedCourseId is > 0,
            $"id={data.CreatedCourseId}");

        var duplicateCourse = await Send(
            HttpMethod.Post,
            "/api/courses",
            data.TeacherToken,
            createCourseBody);

        results.Check(
            "Duplicate course code returns 409",
            duplicateCourse.StatusCode == 409,
            $"status={duplicateCourse.StatusCode}");

        var invalidCourse = await Send(
            HttpMethod.Post,
            "/api/courses",
            data.TeacherToken,
            new
            {
                code = $"BAD{courseSuffix:D7}",
                title = "Invalid Credits",
                creditHours = 5
            });

        results.Check(
            "Credit hours above 4 return 400",
            invalidCourse.StatusCode == 400,
            $"status={invalidCourse.StatusCode}");

        var studentCreateCourse = await Send(
            HttpMethod.Post,
            "/api/courses",
            data.StudentToken,
            new
            {
                code = $"STU{courseSuffix:D7}",
                title = "Forbidden Course",
                creditHours = 3
            });

        results.Check(
            "Student creating a course returns 403",
            studentCreateCourse.StatusCode == 403,
            $"status={studentCreateCourse.StatusCode}");

        if (data.CreatedCourseId is int createdCourseId)
        {
            var updatedCourse = await Send(
                HttpMethod.Put,
                $"/api/courses/{createdCourseId}",
                data.TeacherToken,
                new
                {
                    title = "Smoke Test Course Updated",
                    creditHours = 4
                });

            results.Check(
                "Update course returns 200",
                updatedCourse.StatusCode == 200 &&
                updatedCourse.GetInt("creditHours") == 4,
                $"status={updatedCourse.StatusCode}");
        }

        // ---------------------------------------------------------------
        // Enrollments
        // ---------------------------------------------------------------
        if (data.CreatedCourseId is not int courseId ||
            data.StudentId == 0)
        {
            results.Fail(
                "Enrollment scenarios",
                "Course creation or student identity was unavailable.");
        }
        else
        {
            var createdEnrollment = await Send(
                HttpMethod.Post,
                $"/api/courses/{courseId}/enrollments",
                data.TeacherToken,
                new { studentId = data.StudentId, mark = 77 });

            results.Check(
                "Create enrollment returns 201",
                createdEnrollment.StatusCode == 201,
                $"status={createdEnrollment.StatusCode}");

            data.CreatedEnrollmentId = createdEnrollment.GetInt("id");

            results.Check(
                "Created enrollment has an id",
                data.CreatedEnrollmentId is > 0,
                $"id={data.CreatedEnrollmentId}");

            var duplicateEnrollment = await Send(
                HttpMethod.Post,
                $"/api/courses/{courseId}/enrollments",
                data.TeacherToken,
                new { studentId = data.StudentId, mark = 77 });

            results.Check(
                "Duplicate enrollment returns 409",
                duplicateEnrollment.StatusCode == 409,
                $"status={duplicateEnrollment.StatusCode}");

            var missingStudentEnrollment = await Send(
                HttpMethod.Post,
                $"/api/courses/{courseId}/enrollments",
                data.TeacherToken,
                new { studentId = 999999, mark = 60 });

            results.Check(
                "Enrolling a nonexistent student returns 404",
                missingStudentEnrollment.StatusCode == 404,
                $"status={missingStudentEnrollment.StatusCode}");

            var invalidMark = await Send(
                HttpMethod.Post,
                $"/api/courses/{courseId}/enrollments",
                data.TeacherToken,
                new { studentId = data.StudentId, mark = 101 });

            results.Check(
                "Enrollment mark above 100 returns 400",
                invalidMark.StatusCode == 400,
                $"status={invalidMark.StatusCode}");

            var courseEnrollments = await Send(
                HttpMethod.Get,
                $"/api/courses/{courseId}/enrollments",
                data.TeacherToken);

            results.Check(
                "Teacher lists course enrollments",
                courseEnrollments.StatusCode == 200 &&
                courseEnrollments.ArrayLength() == 1,
                $"status={courseEnrollments.StatusCode}, " +
                $"count={courseEnrollments.ArrayLength()}");

            var enrollmentsForbidden = await Send(
                HttpMethod.Get,
                $"/api/courses/{courseId}/enrollments",
                data.StudentToken);

            results.Check(
                "Student listing course enrollments returns 403",
                enrollmentsForbidden.StatusCode == 403,
                $"status={enrollmentsForbidden.StatusCode}");

            if (data.CreatedEnrollmentId is int enrollmentId)
            {
                var updatedMark = await Send(
                    HttpMethod.Put,
                    $"/api/enrollments/{enrollmentId}/mark",
                    data.TeacherToken,
                    new { mark = 88 });

                results.Check(
                    "Update enrollment mark returns 200",
                    updatedMark.StatusCode == 200 &&
                    updatedMark.GetInt("mark") == 88,
                    $"status={updatedMark.StatusCode}, " +
                    $"mark={updatedMark.GetInt("mark")}");

                var clearedMark = await Send(
                    HttpMethod.Put,
                    $"/api/enrollments/{enrollmentId}/mark",
                    data.TeacherToken,
                    new { mark = (int?)null });

                results.Check(
                    "Set enrollment mark to null returns 200",
                    clearedMark.StatusCode == 200 &&
                    clearedMark.HasProperty("mark") &&
                    clearedMark.GetInt("mark") is null,
                    $"status={clearedMark.StatusCode}");

                var studentUpdateMark = await Send(
                    HttpMethod.Put,
                    $"/api/enrollments/{enrollmentId}/mark",
                    data.StudentToken,
                    new { mark = 99 });

                results.Check(
                    "Student updating a mark returns 403",
                    studentUpdateMark.StatusCode == 403,
                    $"status={studentUpdateMark.StatusCode}");
            }

            var missingEnrollmentUpdate = await Send(
                HttpMethod.Put,
                "/api/enrollments/999999/mark",
                data.TeacherToken,
                new { mark = 50 });

            results.Check(
                "Updating a nonexistent enrollment returns 404",
                missingEnrollmentUpdate.StatusCode == 404,
                $"status={missingEnrollmentUpdate.StatusCode}");

            var courseWithEnrollmentDelete = await Send(
                HttpMethod.Delete,
                $"/api/courses/{courseId}",
                data.TeacherToken);

            results.Check(
                "Deleting a course with enrollments returns 409",
                courseWithEnrollmentDelete.StatusCode == 409,
                $"status={courseWithEnrollmentDelete.StatusCode}");

            if (data.CreatedEnrollmentId is int enrollmentIdToDelete)
            {
                var deletedEnrollment = await Send(
                    HttpMethod.Delete,
                    $"/api/enrollments/{enrollmentIdToDelete}",
                    data.TeacherToken);

                results.Check(
                    "Delete enrollment returns 204",
                    deletedEnrollment.StatusCode == 204,
                    $"status={deletedEnrollment.StatusCode}");

                data.CreatedEnrollmentId = null;
            }

            var deletedCourse = await Send(
                HttpMethod.Delete,
                $"/api/courses/{courseId}",
                data.TeacherToken);

            results.Check(
                "Delete course without enrollments returns 204",
                deletedCourse.StatusCode == 204,
                $"status={deletedCourse.StatusCode}");

            data.CreatedCourseId = null;
        }

        var missingEnrollmentDelete = await Send(
            HttpMethod.Delete,
            "/api/enrollments/999999",
            data.TeacherToken);

        results.Check(
            "Deleting a nonexistent enrollment returns 404",
            missingEnrollmentDelete.StatusCode == 404,
            $"status={missingEnrollmentDelete.StatusCode}");

        // ---------------------------------------------------------------
        // Statistics
        // ---------------------------------------------------------------
        if (data.SeededCourseId > 0)
        {
            var stats = await Send(
                HttpMethod.Get,
                $"/api/courses/{data.SeededCourseId}/stats",
                data.TeacherToken);

            results.Check(
                "Course statistics returns 200",
                stats.StatusCode == 200,
                $"status={stats.StatusCode}");

            results.Check(
                "Course statistics exposes all required fields",
                stats.HasProperty("totalEnrolled") &&
                stats.HasProperty("gradedCount") &&
                stats.HasProperty("ungradedCount") &&
                stats.HasProperty("averageMark") &&
                stats.HasProperty("highestMark") &&
                stats.HasProperty("lowestMark"),
                "fields present");

            results.Check(
                "Statistics counts add up",
                stats.GetInt("totalEnrolled") ==
                stats.GetInt("gradedCount") + stats.GetInt("ungradedCount"),
                $"total={stats.GetInt("totalEnrolled")}, " +
                $"graded={stats.GetInt("gradedCount")}, " +
                $"ungraded={stats.GetInt("ungradedCount")}");
        }

        var missingStats = await Send(
            HttpMethod.Get,
            "/api/courses/999999/stats",
            data.TeacherToken);

        results.Check(
            "Statistics for a nonexistent course returns 404",
            missingStats.StatusCode == 404,
            $"status={missingStats.StatusCode}");

        var statsWithoutToken = await Send(
            HttpMethod.Get,
            $"/api/courses/{data.SeededCourseId}/stats");

        results.Check(
            "Statistics without a token returns 401",
            statsWithoutToken.StatusCode == 401,
            $"status={statsWithoutToken.StatusCode}");

        // ---------------------------------------------------------------
        // Transcripts
        // ---------------------------------------------------------------
        var expectedPassMark = configuration
            .GetValue<int?>("TranscriptSettings:PassMark") ?? 50;

        if (data.StudentId > 0)
        {
            var transcript = await Send(
                HttpMethod.Get,
                $"/api/students/{data.StudentId}/transcript",
                data.TeacherToken);

            results.Check(
                "Teacher reads any transcript",
                transcript.StatusCode == 200,
                $"status={transcript.StatusCode}");

            results.Check(
                "Transcript exposes the configured pass mark",
                transcript.GetInt("passMark") == expectedPassMark,
                $"passMark={transcript.GetInt("passMark")}, " +
                $"expected={expectedPassMark}");

            results.Check(
                "Transcript exposes a weighted average",
                transcript.HasProperty("weightedAverage"),
                "weightedAverage present");

            results.Check(
                "Transcript exposes isPassed",
                transcript.HasProperty("isPassed"),
                "isPassed present");

            var ownTranscript = await Send(
                HttpMethod.Get,
                $"/api/students/{data.StudentId}/transcript",
                data.StudentToken);

            results.Check(
                "Student reads their own transcript",
                ownTranscript.StatusCode == 200,
                $"status={ownTranscript.StatusCode}");

            if (data.OtherStudentId is int otherStudentId)
            {
                var otherTranscript = await Send(
                    HttpMethod.Get,
                    $"/api/students/{otherStudentId}/transcript",
                    data.StudentToken);

                results.Check(
                    "Student reading another transcript returns 403",
                    otherTranscript.StatusCode == 403,
                    $"status={otherTranscript.StatusCode}");
            }
        }

        var missingTranscript = await Send(
            HttpMethod.Get,
            "/api/students/999999/transcript",
            data.TeacherToken);

        results.Check(
            "Transcript for a nonexistent student returns 404",
            missingTranscript.StatusCode == 404,
            $"status={missingTranscript.StatusCode}");

        var transcriptWithoutToken = await Send(
            HttpMethod.Get,
            $"/api/students/{data.StudentId}/transcript");

        results.Check(
            "Transcript without a token returns 401",
            transcriptWithoutToken.StatusCode == 401,
            $"status={transcriptWithoutToken.StatusCode}");
    }

    private static async Task CleanupAsync(
        SmokeTestClient client,
        SmokeTestResults results,
        SmokeTestData data)
    {
        if (string.IsNullOrWhiteSpace(data.TeacherToken))
        {
            Console.WriteLine(
                "Cleanup skipped: no teacher token was obtained.");
            return;
        }

        Console.WriteLine();
        Console.WriteLine("Cleanup:");

        if (data.CreatedEnrollmentId is int enrollmentId)
        {
            await CleanupResourceAsync(
                client,
                results,
                "enrollment",
                $"/api/enrollments/{enrollmentId}",
                data.TeacherToken);
        }

        if (data.CreatedCourseId is int courseId)
        {
            await CleanupResourceAsync(
                client,
                results,
                "course",
                $"/api/courses/{courseId}",
                data.TeacherToken);
        }

        if (data.CreatedStudentId is int studentId)
        {
            await CleanupResourceAsync(
                client,
                results,
                "student",
                $"/api/students/{studentId}",
                data.TeacherToken);
        }
    }

    private static async Task CleanupResourceAsync(
        SmokeTestClient client,
        SmokeTestResults results,
        string resourceName,
        string path,
        string teacherToken)
    {
        try
        {
            var response = await client.SendAsync(
                HttpMethod.Delete,
                path,
                teacherToken,
                cancellationToken: CancellationToken.None);

            // 404 means a scenario already removed it, which is still clean.
            results.Check(
                $"Cleanup {resourceName}",
                response.StatusCode is 204 or 404,
                $"status={response.StatusCode}");
        }
        catch (Exception exception)
        {
            results.Fail(
                $"Cleanup {resourceName}",
                $"{exception.GetType().Name}: {exception.Message}");
        }
    }
}
