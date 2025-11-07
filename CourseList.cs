using OnlineCoursePlatform.Models;

namespace OnlineCoursePlatform;

public static class CourseList
{
    public static readonly CourseMenu[] Courses =
    {
        new CourseMenu
        {
            Title = "DNSSEC",
            Path = "https://raw.githubusercontent.com/freddycoder/OnlineCoursePlatform/refs/heads/main/wwwroot/Courses/DNSSEC.json",
            Type = "url"
        },
        new CourseMenu
        {
            Title = "Équipe TI - ISO-9001",
            Path = "https://raw.githubusercontent.com/freddycoder/OnlineCoursePlatform/refs/heads/main/wwwroot/Courses/%C3%89quipe%20TI%20-%20ISO-9001.json",
            Type = "url"
        },
    };
}