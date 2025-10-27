namespace OnlineLearningPlatform.Models;

public record Course(string Title, string Description, List<Module> Modules);
public record Module(string Title, string Description, double EstimatedHours, List<Lesson> Lessons);
public record Lesson(string Title, string Summary, int EstimatedMinutes, List<string> LearningObjectives, string Content, List<Exercise> Exercises);
public record Exercise(string Instruction, string Answer, string Explanation);