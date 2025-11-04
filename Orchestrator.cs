using System.Text;
using System.Text.Json;
using OnlineLearningPlatform.Models;
using OpenAI.Chat;

namespace OnlineCoursePlateform;

public class CourseOrchestrator
{
    private readonly ChatClient _client;
    private readonly TextWriter _output;
    private readonly int _maxParallel = 1;
    
    public CourseOrchestrator(ChatClient client, TextWriter output)
    {
        _client = client;
        _output = output;
    }

    public async Task<Course> BuildCourseAsync(string courseDescription, CancellationToken cancellationToken)
    {
        await _output.WriteLineAsync($"[Orchestrator] Starting course build for description: {courseDescription}");
        await _output.WriteLineAsync("[Orchestrator] Requesting modules JSON...");
        var modulesJson = await RequestModulesJsonAsync(courseDescription, cancellationToken);
        await _output.WriteLineAsync("[Orchestrator] Parsing modules JSON...");
        var modules = await ParseModulesAsync(modulesJson);

        var semaphore = new SemaphoreSlim(_maxParallel);
        var moduleTasks = new List<Task<Module>>();
        var moduleIndex = 0;
        foreach (var m in modules)
        {
            await _output.WriteLineAsync($"[Orchestrator] Starting module {moduleIndex + 1}/{modules.Count}: {m.Title}");
            await semaphore.WaitAsync();
            moduleTasks.Add(Task.Run(async () =>
            {
                try
                {
                    await _output.WriteLineAsync($"[Orchestrator] Requesting lessons for module: {m.Title}");
                    var lessonsJson = await RequestLessonsForModuleAsync(m.Title, m.Description, cancellationToken);
                    await _output.WriteLineAsync($"[Orchestrator] Parsing lessons for module: {m.Title}");
                    var lessons = await ParseLessonsAsync(lessonsJson);

                    for (int i = 0; i < lessons.Count; i++)
                    {
                        await _output.WriteLineAsync($"[Orchestrator] Requesting lesson ({i + 1}/{lessons.Count}) detail for: {lessons[i].Title}");
                        var lessonDetailJson = await RequestLessonDetailAsync(m.Title, lessons[i].Title, lessons[i].Summary, cancellationToken);
                        await _output.WriteLineAsync($"[Orchestrator] Parsing lesson ({i + 1}/{lessons.Count}) detail for: {lessons[i].Title}");
                        var detailed = await ParseLessonDetailAsync(lessonDetailJson);
                        lessons[i] = MergeLesson(lessons[i], detailed);
                    }
                    await _output.WriteLineAsync($"[Orchestrator] Finished module: {m.Title}");
                    return new Module(m.Title, m.Description, m.EstimatedHours, lessons);
                }
                finally { semaphore.Release(); }
            }));
            moduleIndex++;
        }

        var completedModules = await Task.WhenAll(moduleTasks);
        await _output.WriteLineAsync("[Orchestrator] All modules completed. Building final course object.");
        var course = new Course("Cours généré", courseDescription, completedModules.ToList());
        await _output.WriteLineAsync("[Orchestrator] Course build complete.");
        return course;
    }

    // Les méthodes ci-dessous appellent l'API OpenAI et parse les réponses JSON.
    private async Task<string> RequestModulesJsonAsync(string courseDescription, CancellationToken cancellationToken)
    {
        await _output.WriteLineAsync("[Orchestrator] Sending prompt for modules JSON...");
        string prompt = $@"
Tu es un assistant pédagogique. Voici la description du cours :
""{courseDescription}""

Propose un découpage en modules.Réponds strictement en JSON avec ce schema:
        {{ ""modules"": [ {{ ""title"":""text"",""short_description"":""text"",""estimated_hours"": number}} }} ] }}
";
        return await SendPromptAndGetJsonAsync(prompt, cancellationToken);
    }

    private async Task<string> RequestLessonsForModuleAsync(string moduleTitle, string moduleDesc, CancellationToken cancellationToken)
    {
        await _output.WriteLineAsync($"[Orchestrator] Sending prompt for lessons in module: {moduleTitle}");
        string prompt = $@"
Tu es un créateur de cours. Pour le module ""{moduleTitle}"" ({moduleDesc}) propose une liste de leçons.
Réponds strictement en JSON : {{ ""lessons"": [ {{""title"":"""",""summary"":"""",""estimated_minutes"":30}} ] }}
";
        return await SendPromptAndGetJsonAsync(prompt, cancellationToken);
    }

    private async Task<string> RequestLessonDetailAsync(string moduleTitle, string lessonTitle, string lessonSummary, CancellationToken cancellationToken)
    {
        await _output.WriteLineAsync($"[Orchestrator] Sending prompt for lesson detail: {lessonTitle} (module: {moduleTitle})");
        string prompt = $@"
Détaille la leçon ""{lessonTitle}"" (module: {moduleTitle}). Sommaire : {lessonSummary}.
Donne : learning_objectives (liste), content_sections (liste de {{title, text}}), exercises (liste de {{instruction, answer, explanation}}), estimated_minutes.
Réponds strictement en JSON.
";
        return await SendPromptAndGetJsonAsync(prompt, cancellationToken);
    }

    // Wrapper d'appel à l'API (simple)
    private async Task<string> SendPromptAndGetJsonAsync(string prompt, CancellationToken cancellationToken)
    {
        await _output.WriteLineAsync($"[Orchestrator] Sending prompt to agent (streaming)...\n{prompt}");
        var response = await _client.CompleteChatAsync([prompt], new ChatCompletionOptions(), cancellationToken);
        await _output.WriteLineAsync($"[Orchestrator] Received response from agent.\n");
        var content = response.Value.Content.Aggregate("", (acc, part) => acc + part.Text);
        await _output.WriteLineAsync($"[Orchestrator] Streaming response content: {content}");
        return content;
    }

    private async Task<List<(string Title, string Description, double EstimatedHours)>> ParseModulesAsync(string jsonText)
    {
        try
        {
            await _output.WriteLineAsync($"[Orchestrator] Parsing modules JSON... {jsonText}");
            using var doc = JsonDocument.Parse(jsonText);
            var root = doc.RootElement.GetProperty("modules");
            var list = new List<(string, string, double)>();
            foreach (var el in root.EnumerateArray())
            {
                var t = el.GetProperty("title").GetString() ?? "";
                var d = el.GetProperty("short_description").GetString() ?? "";
                var h = el.GetProperty("estimated_hours").GetDouble();
                list.Add((t, d, h));
            }
            return list;
        }
        catch
        {
            await _output.WriteLineAsync("[Orchestrator] Error parsing modules JSON. Returning fallback module.");
            // En cas d'erreur, fallback simple
            return new List<(string, string, double)> { 
                ("Module 1", "Description générée par défaut", 1.0) 
            };
        }
    }

    private async Task<List<Lesson>> ParseLessonsAsync(string jsonText)
    {
        try
        {
            await _output.WriteLineAsync("[Orchestrator] Parsing lessons JSON...");
            using var doc = JsonDocument.Parse(jsonText);
            var root = doc.RootElement.GetProperty("lessons");
            var list = new List<Lesson>();
            foreach (var el in root.EnumerateArray())
            {
                var t = el.GetProperty("title").GetString() ?? "";
                var s = el.GetProperty("summary").GetString() ?? "";
                var m = el.GetProperty("estimated_minutes").GetInt32();
                list.Add(new Lesson(t, s, m, new List<string>(), "", new List<Exercise>()));
            }
            return list;
        }
        catch
        {
            await _output.WriteLineAsync("[Orchestrator] Error parsing lessons JSON. Returning fallback lesson.");
            return new List<Lesson> { new Lesson("Leçon 1", "Résumé par défaut", 30, new List<string>(), "", new List<Exercise>()) };
        }
    }

    private async Task<(List<string> objectives, string content, List<Exercise> exercises, int minutes)> ParseLessonDetailAsync(string jsonText)
    {
        try
        {
            await _output.WriteLineAsync("[Orchestrator] Parsing lesson detail JSON...");
            using var doc = JsonDocument.Parse(jsonText);
            var root = doc.RootElement;
            var objectives = new List<string>();
            if (root.TryGetProperty("learning_objectives", out var objEl))
            {
                foreach (var v in objEl.EnumerateArray())
                    objectives.Add(v.GetString() ?? "");
            }
            var contentBuilder = new StringBuilder();
            if (root.TryGetProperty("content_sections", out var cs))
            {
                foreach (var section in cs.EnumerateArray())
                {
                    var title = section.GetProperty("title").GetString() ?? "";
                    var text = section.GetProperty("text").GetString() ?? "";
                    contentBuilder.AppendLine(title);
                    contentBuilder.AppendLine(text);
                    contentBuilder.AppendLine();
                }
            }
            string content = contentBuilder.ToString();
            var exercises = new List<Exercise>();
            if (root.TryGetProperty("exercises", out var ex))
            {
                foreach (var e in ex.EnumerateArray())
                {
                    var instr = e.GetProperty("instruction").GetString() ?? "";
                    var ans = e.GetProperty("answer").GetString() ?? "";
                    var expl = e.GetProperty("explanation").GetString() ?? "";
                    exercises.Add(new Exercise(instr, ans, expl));
                }
            }
            int minutes = root.TryGetProperty("estimated_minutes", out var mm) ? mm.GetInt32() : 30;
            return (objectives, content, exercises, minutes);
        }
        catch
        {
            await _output.WriteLineAsync("[Orchestrator] Error parsing lesson detail JSON. Returning fallback detail.");
            return (new List<string>(), "Contenu default", new List<Exercise>(), 30);
        }
    }

    private Lesson MergeLesson(Lesson baseLesson, (List<string> objectives, string content, List<Exercise> exercises, int minutes) detail)
    {
        return baseLesson with
        {
            LearningObjectives = detail.objectives,
            Content = detail.content,
            Exercises = detail.exercises,
            EstimatedMinutes = detail.minutes
        };
    }
}
