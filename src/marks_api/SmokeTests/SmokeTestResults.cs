namespace marks_api.SmokeTests;

public sealed class SmokeTestResults
{
    private readonly List<string> _failures = [];

    public int Passed { get; private set; }

    public int Failed { get; private set; }

    public void Pass(string name, string? detail = null)
    {
        Passed++;

        Console.WriteLine(
            string.IsNullOrWhiteSpace(detail)
                ? $"PASS  {name}"
                : $"PASS  {name}  [{detail}]");
    }

    public void Fail(string name, string detail)
    {
        Failed++;
        _failures.Add($"{name}: {detail}");

        Console.WriteLine($"FAIL  {name}  [{detail}]");
    }

    public void Check(string name, bool condition, string detail)
    {
        if (condition)
        {
            Pass(name, detail);
        }
        else
        {
            Fail(name, detail);
        }
    }

    public int PrintSummary()
    {
        var total = Passed + Failed;

        Console.WriteLine();
        Console.WriteLine(new string('-', 72));
        Console.WriteLine(
            $"Smoke tests: {Passed} passed, {Failed} failed, {total} total.");

        if (Failed > 0)
        {
            Console.WriteLine("Failures:");
            foreach (var failure in _failures)
            {
                Console.WriteLine($"  - {failure}");
            }
        }

        Console.WriteLine(new string('-', 72));

        return Failed == 0 ? 0 : 1;
    }
}
