namespace OnlineLearningPlatform.Models;

public record Course(string Title, string Description, List<Module> Modules);
public record Module(string Title, string Description, double EstimatedHours, List<Lesson> Lessons);
public record Lesson(string Title, string Summary, int EstimatedMinutes, List<string> LearningObjectives, string Content, List<Exercise> Exercises);

public class Exercise
{
    public string Instruction { get; init; }
    public string Answer { get; init; }
    public string Explanation { get; init; }
    public bool ShowAnswer { get; set; } = false;

    public Exercise(string instruction, string answer, string explanation)
    {
        Instruction = instruction;
        Answer = answer;
        Explanation = explanation;
    }
}