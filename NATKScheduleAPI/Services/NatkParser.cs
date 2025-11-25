using HtmlAgilityPack;

namespace NATKScheduleAPI.Services
{
    public class GroupInfo
    {
        public string Name { get; set; }
        public string Url { get; set; }
        public int CourseNumber { get; set; }

        public GroupInfo(string name, string url, int courseNumber)
        {
            Name = name;
            Url = url;
            CourseNumber = courseNumber;
        }
    }

    public class Lesson
    {
        public int Number { get; set; }
        public string Time { get; set; } = string.Empty;
        public string Subject { get; set; } = string.Empty;
        public string Teacher { get; set; } = string.Empty;
        public string TeacherUrl { get; set; } = string.Empty;
        public string Classroom { get; set; } = string.Empty;
        public string? Subgroup { get; set; }
    }

    public class DaySchedule
    {
        public DateTime Date { get; set; }
        public string DayOfWeek { get; set; } = string.Empty;
        public List<Lesson> Lessons { get; set; } = new List<Lesson>();
        public bool IsDayOff { get; set; }
    }

    public class GroupSchedule
    {
        public string GroupName { get; set; } = string.Empty;
        public string Curator { get; set; } = string.Empty;
        public string CuratorUrl { get; set; } = string.Empty;
        public string Department { get; set; } = string.Empty;
        public string Speciality { get; set; } = string.Empty;
        public string Qualification { get; set; } = string.Empty;
        public string StudyPeriod { get; set; } = string.Empty;
        public string EducationBase { get; set; } = string.Empty;
        public string StudyForm { get; set; } = string.Empty;
        public DateTime LastUpdate { get; set; }
        public List<DaySchedule> Days { get; set; } = new List<DaySchedule>();
    }

    public class NatkParser : IDisposable
    {
        private readonly HttpClient _client;

        public NatkParser()
        {
            var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true
            };

            _client = new HttpClient(handler);
            _client.DefaultRequestHeaders.Add("User-Agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
            _client.Timeout = TimeSpan.FromSeconds(30);
        }

        public async Task<List<GroupInfo>> GetGroupsAsync()
        {
            try
            {
                var html = await _client.GetStringAsync("https://natk.ru/stud-grad/schedule");
                var doc = new HtmlDocument();
                doc.LoadHtml(html);

                var groups = new List<GroupInfo>();

                var scheduleDiv = doc.DocumentNode.SelectSingleNode("//div[@class='surapage_shedule']");

                if (scheduleDiv != null)
                {
                    int currentCourse = 0;

                    foreach (var childNode in scheduleDiv.ChildNodes)
                    {
                        if (childNode.Name == "h3")
                        {
                            currentCourse = ExtractCourseNumber(childNode.InnerText.Trim());
                        }
                        else if (childNode.Name == "a" && childNode.HasClass("group") && currentCourse > 0)
                        {
                            var name = childNode.InnerText.Trim();
                            var href = childNode.GetAttributeValue("href", "");
                            var fullUrl = MakeAbsoluteUrl(href);

                            groups.Add(new GroupInfo(name, fullUrl, currentCourse));
                        }
                    }
                }

                return groups;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка при получении списка групп: {ex.Message}");
                return [];
            }
        }

        private int ExtractCourseNumber(string headerText)
        {
            if (string.IsNullOrEmpty(headerText)) return 0;

            if (headerText.Length > 0 && char.IsDigit(headerText[0]))
            {
                return int.Parse(headerText[0].ToString());
            }

            return 0;
        }

        public async Task<GroupSchedule?> GetGroupScheduleAsync(string scheduleUrl)
        {
            try
            {
                var html = await _client.GetStringAsync(Uri.UnescapeDataString(scheduleUrl));
                var doc = new HtmlDocument();
                doc.LoadHtml(html);

                return ParseScheduleFromHtml(doc, scheduleUrl);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка при получении расписания: {ex.Message}");
                return null;
            }
        }

        private GroupSchedule ParseScheduleFromHtml(HtmlDocument doc, string url)
        {
            var schedule = new GroupSchedule();

            ParseGroupInfo(doc, schedule);

            ParseScheduleTable(doc, schedule);

            return schedule;
        }

        private void ParseGroupInfo(HtmlDocument doc, GroupSchedule schedule)
        {
            var titleNode = doc.DocumentNode.SelectSingleNode("//h1");
            if (titleNode != null)
            {
                schedule.GroupName = titleNode.InnerText.Replace("Расписание занятий группы", "").Trim();
            }

            var curatorNode = doc.DocumentNode.SelectSingleNode("//h4[contains(text(), 'Куратор группы')]/following-sibling::p/a");
            if (curatorNode != null)
            {
                schedule.Curator = curatorNode.InnerText.Trim();
                schedule.CuratorUrl = MakeAbsoluteUrl(curatorNode.GetAttributeValue("href", ""));
            }

            var departmentNode = doc.DocumentNode.SelectSingleNode("//h4[contains(text(), 'Отделение')]/following-sibling::p");
            if (departmentNode != null)
            {
                schedule.Department = departmentNode.InnerText.Trim();
            }

            var infoNodes = doc.DocumentNode.SelectNodes("//h4[contains(text(), 'Сведения о группе')]/following-sibling::p");
            if (infoNodes != null)
            {
                foreach (var node in infoNodes)
                {
                    var text = node.InnerText.Trim();
                    if (text.Contains("Специальность:"))
                        schedule.Speciality = GetValueAfterColon(text);
                    else if (text.Contains("Квалификация:"))
                        schedule.Qualification = GetValueAfterColon(text);
                    else if (text.Contains("Срок обучения:"))
                        schedule.StudyPeriod = GetValueAfterColon(text);
                    else if (text.Contains("Базовое образование:"))
                        schedule.EducationBase = GetValueAfterColon(text);
                    else if (text.Contains("Форма обучения:"))
                        schedule.StudyForm = GetValueAfterColon(text);
                }
            }

            var updateNode = doc.DocumentNode.SelectSingleNode("//p[contains(text(), 'Последнее обновление')]");
            if (updateNode != null)
            {
                var updateText = updateNode.InnerText.Replace("Последнее обновление", "").Trim();
                if (DateTime.TryParse(updateText, out DateTime lastUpdate))
                {
                    schedule.LastUpdate = lastUpdate;
                }
            }
        }

        private static void ParseScheduleTable(HtmlDocument doc, GroupSchedule schedule)
        {
            var table = doc.DocumentNode.SelectSingleNode("//table[@class='sura_shedule']");
            if (table == null) return;

            DaySchedule? currentDay = null;
            var rows = table.SelectNodes(".//tr");

            foreach (var row in rows)
            {
                if (row.HasClass("date"))
                {
                    if (currentDay != null)
                    {
                        schedule.Days.Add(currentDay);
                    }

                    currentDay = ParseDayHeader(row);
                }
                else if (currentDay != null && !row.HasClass("date"))
                {
                    var lessons = ParseLessonRow(row);
                    if (lessons.Any())
                    {
                        currentDay.Lessons.AddRange(lessons);
                    }
                }
            }

            if (currentDay != null)
            {
                schedule.Days.Add(currentDay);
            }
        }

        private static DaySchedule ParseDayHeader(HtmlNode row)
        {
            var daySchedule = new DaySchedule();
            var headerText = row.InnerText.Trim();

            // Парсим обычный день
            var parts = headerText.Split(new[] { " - " }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2)
            {
                if (DateTime.TryParse(parts[0], out DateTime date))
                {
                    daySchedule.Date = date;
                    daySchedule.DayOfWeek = parts[1];
                }
            }


            return daySchedule;
        }

        private static List<Lesson> ParseLessonRow(HtmlNode row)
        {
            var lessons = new List<Lesson>();

            var cells = row.SelectNodes("./td");
            if (cells == null || cells.Count < 3) return lessons;

            if (!int.TryParse(cells[0].InnerText.Trim(), out int lessonNumber))
            {
                return lessons;
            }

            var time = cells[1].InnerText.Trim();

            var subgroupCells = cells[2].SelectNodes("./td[@class='podguppa']");
            if (subgroupCells != null && subgroupCells.Count == 2)
            {
                for (int i = 0; i < 2; i++)
                {
                    var lesson = ParseLessonFromCell(subgroupCells[i], lessonNumber, time);
                    lesson.Subgroup = i == 0 ? "1 подгруппа" : "2 подгруппа";
                    lessons.Add(lesson);
                }
            }
            else
            {
                var lesson = ParseLessonFromCell(cells[2], lessonNumber, time);
                lessons.Add(lesson);
            }

            return lessons;
        }

        private static Lesson ParseLessonFromCell(HtmlNode cell, int lessonNumber, string time)
        {
            var lesson = new Lesson
            {
                Number = lessonNumber,
                Time = time
            };

            var spans = cell.SelectNodes("./span");
            if (spans != null)
            {
                foreach (var span in spans)
                {
                    var text = span.InnerText.Trim();

                    if (string.IsNullOrEmpty(lesson.Subject) && !string.IsNullOrEmpty(text))
                    {
                        lesson.Subject = text;
                    }

                    var teacherLink = span.SelectSingleNode("./a");
                    if (teacherLink != null)
                    {
                        lesson.Teacher = teacherLink.InnerText.Trim();
                        lesson.TeacherUrl = MakeAbsoluteUrl(teacherLink.GetAttributeValue("href", ""));
                    }

                    if (text.Contains("Кабинет") || text.Contains("адресу"))
                    {
                        lesson.Classroom = text;
                    }
                }
            }

            return lesson;
        }

        private static string MakeAbsoluteUrl(string url)
        {
            if (string.IsNullOrEmpty(url)) return url;
            if (url.StartsWith("http")) return url;
            return $"https://natk.ru/{url.TrimStart('/')}";
        }

        private static string GetValueAfterColon(string text)
        {
            var colonIndex = text.IndexOf(':');
            return colonIndex >= 0 ? text.Substring(colonIndex + 1).Trim() : text;
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
            _client?.Dispose();
        }
    }
}