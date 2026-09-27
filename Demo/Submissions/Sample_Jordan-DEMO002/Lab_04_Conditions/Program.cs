// DEMO SUBMISSION - fictitious student, no real grades.
// Section 1: Read input
Console.Write("Enter a score (0-100): ");
if (!int.TryParse(Console.ReadLine(), out var score))
{
    Console.WriteLine("Please enter a whole number.");
    return;
}

// Section 2: Validate range
if (score < 0 || score > 100)
{
    Console.WriteLine("Score must be between 0 and 100.");
    return;
}

// Section 3: Classify result
var result = score >= 70 ? "Pass" : "Needs practice";
Console.WriteLine($"Result: {result}");
