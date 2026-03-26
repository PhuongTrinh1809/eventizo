using Eventizo.Models;
using Eventizo.Data;
using Eventizo.Helper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;

namespace Eventizo.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class WebhookController : ControllerBase
    {
        private readonly IConfiguration _configuration;
        private readonly string _geminiKey;
        private readonly ApplicationDbContext _context;
        private static readonly Dictionary<string, string> _userChoice = new();
        private static readonly Dictionary<string, string> _eventMode = new();

        public WebhookController(IConfiguration configuration, ApplicationDbContext context)
        {
            _configuration = configuration;
            _geminiKey = _configuration["Gemini:ApiKey"];
            _context = context;
        }

        [HttpPost]
        public async Task<IActionResult> Post()
        {
            try
            {
                using var reader = new StreamReader(HttpContext.Request.Body);
                string json = await reader.ReadToEndAsync();
                string text = "";

                JObject data = JObject.Parse(json);
                string userInput = data["queryResult"]?["queryText"]?.ToString()?.Trim() ?? "";
                string sessionId = data["session"]?.ToString() ?? "default";

                // ✅ Chuẩn hóa chính tả + lỗi nhập của người dùng (dùng AI)
                string fixedInput = await FixSpellingWithAI(userInput, "General");
                string lowerInput = fixedInput.ToLower();

                // ✅ Khởi tạo session nếu mới
                if (!_userChoice.ContainsKey(sessionId)) _userChoice[sessionId] = null;
                if (!_eventMode.ContainsKey(sessionId)) _eventMode[sessionId] = null;

                string currentIntent = _userChoice[sessionId];
                string currentMode = _eventMode[sessionId];

                // ✅ Xác định intent từ sessionId nếu chưa có
                if (currentIntent == null)
                {
                    if (sessionId.StartsWith("Event", StringComparison.OrdinalIgnoreCase)) currentIntent = "Event";
                    else if (sessionId.StartsWith("Product", StringComparison.OrdinalIgnoreCase)) currentIntent = "Product";
                    _userChoice[sessionId] = currentIntent;
                }

                // ✅ Xác định mode nếu là Event
                if (currentIntent == "Event" && currentMode == null)
                {
                    if (sessionId.ToLower().Contains("type")) currentMode = "type";
                    else if (sessionId.ToLower().Contains("time")) currentMode = "time";
                    else currentMode = "choose_mode";
                    _eventMode[sessionId] = currentMode;
                }

                Console.WriteLine($"🧠 Session {sessionId}: Intent={currentIntent}, Mode={currentMode}, Input='{fixedInput}'");

                // ✅ Phản hồi cảm ơn (sửa lỗi gõ sai và tiếng Anh)
                if (lowerInput.Contains("cảm ơn") || lowerInput.Contains("cam on") ||
                    lowerInput.Contains("thank") || lowerInput.Contains("tks"))
                {
                    text = "Không có gì, rất vui được giúp bạn! 🙌";
                    await SaveChatHistory(sessionId, userInput, text);
                    return Ok(new { fulfillmentText = text });
                }

                // ================= Product Intent =================
                if (currentIntent == "Product" || lowerInput.Contains("sản phẩm"))
                {
                    string category = lowerInput.Contains("lightstick") ? "Lightstick" :
                                      lowerInput.Contains("album") ? "Album" : fixedInput.Trim();

                    _userChoice[sessionId] = "Product";
                    _eventMode[sessionId] = category;

                    text = $"Các sản phẩm thuộc loại {category}:";
                    var responseObj = GetProductsByCategoryResponse(category);

                    await SaveChatHistory(sessionId, userInput, text);
                    return Ok(responseObj);
                }

                // ================= Event Intent =================
                if (currentIntent == "Event")
                {
                    if (currentMode == "choose_mode")
                    {
                        if (lowerInput.Contains("loại"))
                        {
                            _eventMode[sessionId] = "type";
                            text = "Hãy chọn loại sự kiện bạn muốn xem:";
                            await SaveChatHistory(sessionId, userInput, text);
                            return Ok(ChipsResponse(text, new[] { "🎤 Concert", "🗺️ Tour", "🎬 Liveshow" }));
                        }

                        if (lowerInput.Contains("thời gian") || lowerInput.Contains("tháng") ||
                            lowerInput.Contains("năm") || lowerInput.Contains("ngày"))
                        {
                            _eventMode[sessionId] = "time";
                            text = "📅 Vui lòng nhập ngày, tháng hoặc năm bạn muốn xem sự kiện 🎟️\nVí dụ: 'tháng 10', 'năm 2025' hoặc 'ngày 20 tháng 11'.";
                            await SaveChatHistory(sessionId, userInput, text);
                            return Ok(new { fulfillmentText = text });
                        }

                        text = "Bạn muốn xem sự kiện theo loại hay theo thời gian?";
                        await SaveChatHistory(sessionId, userInput, text);
                        return Ok(new { fulfillmentText = text });
                    }

                    if (currentMode == "type")
                    {
                        string correctedType = await NormalizeEventTypeWithAI(fixedInput);
                        text = GetEventsByType(correctedType);

                        await SaveChatHistory(sessionId, userInput, text);
                        return Ok(ChipsResponse(text, new[] { "🎭 Theo loại sự kiện", "📅 Theo thời gian" }));
                    }

                    if (currentMode == "time")
                    {
                        string fixedTimeInput = (await FixSpellingWithAI(userInput, "Event")).ToLower();
                        Console.WriteLine($"🪄 Input after FixSpelling: '{fixedTimeInput}'");

                        var (day, month, year) = ExtractDateParts(fixedTimeInput);
                        DateTime now = DateTime.Now;

                        if (day.HasValue && month.HasValue)
                            text = GetEventsByDate(day.Value, month.Value, year ?? now.Year);
                        else if (month.HasValue)
                            text = GetEventsByMonth(month.Value, year ?? now.Year);
                        else if (year.HasValue)
                            text = GetEventsByYear(year.Value);
                        else
                            text = "Không nhận diện được thời gian. Hãy nhập lại ngày/tháng/năm nhé!";

                        await SaveChatHistory(sessionId, userInput, text);
                        return Ok(ChipsResponse(text, new[] { "🎭 Theo loại sự kiện", "📅 Theo thời gian" }));
                    }
                }

                // ================= Nhập trực tiếp Concert/Tour/Liveshow =================
                if (lowerInput.Contains("concert") || lowerInput.Contains("tour") || lowerInput.Contains("liveshow"))
                {
                    _userChoice[sessionId] = "Event";
                    _eventMode[sessionId] = "type";

                    string eventType = lowerInput.Contains("concert") ? "Concert"
                                         : lowerInput.Contains("tour") ? "Tour"
                                         : "Liveshow";

                    text = GetEventsByType(eventType);
                    await SaveChatHistory(sessionId, userInput, text);
                    return Ok(ChipsResponse(text, new[] { "🎭 Theo loại sự kiện", "📅 Theo thời gian" }));
                }

                // ================= Fallback =================
                text = "Tôi chưa hiểu bạn muốn tìm gì. Bạn có thể nói rõ hơn không?";
                await SaveChatHistory(sessionId, userInput, text);
                return Ok(ChipsResponse(text, new[] { "🎫 Sự kiện", "🛍️ Sản phẩm" }));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Lỗi webhook: {ex.Message}");
                return Ok(new { fulfillmentText = "Lỗi xử lý yêu cầu. Vui lòng thử lại sau." });
            }
        }

        // ================= Helper =================
        private object ChipsResponse(string text, string[] options)
        {
            return new
            {
                fulfillmentText = text,
                fulfillmentMessages = new object[]
                {
            new {
                payload = new {
                    richContent = new object[] {
                        new object[] {
                            new {
                                type = "chips",
                                options = options.Select(o => new { text = o }).ToArray()
                            }
                        }
                    }
                }
            }
                }
            };
        }

        private ProductResponse GetProductsByKeyword(string keyword)
        {
            try
            {
                using var conn = new SqlConnection(_configuration.GetConnectionString("DefaultConnection"));
                conn.Open();
                var cmd = new SqlCommand(@"
            SELECT p.Id, p.Name, p.Price, p.ImageUrl, c.Name AS CategoryName
            FROM Products p
            JOIN Categories c ON p.CategoryId = c.Id
            WHERE p.Name LIKE @kw OR c.Name LIKE @kw", conn);
                cmd.Parameters.AddWithValue("@kw", $"%{keyword}%");

                using var reader = cmd.ExecuteReader();
                if (!reader.HasRows)
                {
                    return new ProductResponse
                    {
                        FulfillmentText = $"Không tìm thấy sản phẩm nào phù hợp với từ khóa “{keyword}”.",
                        FulfillmentMessages = Array.Empty<object>()
                    };
                }

                var products = new List<object>();
                while (reader.Read())
                {
                    int id = Convert.ToInt32(reader["Id"]);
                    string name = reader["Name"].ToString();
                    string price = string.Format("{0:N0}", reader["Price"]);
                    string category = reader["CategoryName"].ToString();
                    string imageUrl = reader["ImageUrl"]?.ToString() ?? "";

                    products.Add(new
                    {
                        type = "info",
                        title = name,
                        subtitle = $"💰 {price} VNĐ | 📦 {category}",
                        image = new
                        {
                            src = new { rawUrl = imageUrl },
                            accessibilityText = name
                        },
                        actionLink = $"/Home/ProductDisplay/{id}"
                    });
                }

                return new ProductResponse
                {
                    FulfillmentText = "🔍 Kết quả tìm kiếm sản phẩm:",
                    FulfillmentMessages = new object[]
                    {
                new { payload = new { richContent = new object[] { products } } }
                    }
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Lỗi GetProductsByKeyword: {ex.Message}");
                return new ProductResponse
                {
                    FulfillmentText = "Không thể tìm kiếm sản phẩm lúc này.",
                    FulfillmentMessages = Array.Empty<object>()
                };
            }
        }

        private string GetEventsByKeyword(string query)
        {
            var results = new List<string>();
            using var conn = new SqlConnection(_configuration.GetConnectionString("DefaultConnection"));
            conn.Open();

            string sql = @"
        SELECT e.Id, e.Name, e.EventStartingDate, e.Place, et.Name AS EventType
        FROM Events e
        LEFT JOIN EventTypes et ON e.EventTypeId = et.Id
        WHERE LOWER(e.Name) LIKE @kw 
           OR LOWER(et.Name) LIKE @kw 
           OR LOWER(e.Place) LIKE @kw";

            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@kw", $"%{query.ToLower()}%");

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                int id = Convert.ToInt32(reader["Id"]);
                string eventType = reader["EventType"]?.ToString() ?? "";

                string link = eventType switch
                {
                    "Tour" => $"/Home/DetailsTour/{id}",
                    "Concert" => $"/Home/DetailsConcert/{id}",
                    "Liveshow" => $"/Home/DetailsLiveshow/{id}",
                    _ => $"/Home/DetailsEvent/{id}"
                };

                results.Add($"""
            🎫 <b><a href="{link}">{reader["Name"]}</a></b> ({eventType})<br>
            📅 {Convert.ToDateTime(reader["EventStartingDate"]):dd/MM/yyyy}<br>
            📍 {reader["Place"]}
        """);
            }

            return results.Count == 0
                ? "Không tìm thấy sự kiện nào phù hợp."
                : "Các sự kiện bạn có thể quan tâm:<br><br>" +
                  string.Join("<br><br>━━━━━━━━━━━━━━━<br><br>", results);
        }

        private string GetEventsByDate(int day, int month, int year)
        {
            var results = new List<string>();
            using var conn = new SqlConnection(_configuration.GetConnectionString("DefaultConnection"));
            conn.Open();

            string sql = @"
        SELECT e.Id, e.Name, e.EventStartingDate, e.Place, et.Name AS EventType
        FROM Events e
        LEFT JOIN EventTypes et ON e.EventTypeId = et.Id
        WHERE DAY(e.EventStartingDate) = @day
          AND MONTH(e.EventStartingDate) = @month
          AND YEAR(e.EventStartingDate) = @year";

            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@day", day);
            cmd.Parameters.AddWithValue("@month", month);
            cmd.Parameters.AddWithValue("@year", year);

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                int id = Convert.ToInt32(reader["Id"]);
                string eventType = reader["EventType"]?.ToString() ?? "";

                string link = eventType switch
                {
                    "Tour" => $"/Home/DetailsTour/{id}",
                    "Concert" => $"/Home/DetailsConcert/{id}",
                    "Liveshow" => $"/Home/DetailsLiveshow/{id}",
                    _ => $"/Home/DetailsEvent/{id}"
                };

                results.Add($"""
            🎫 <b><a href="{link}">{reader["Name"]}</a></b> ({eventType})<br>
            📅 {Convert.ToDateTime(reader["EventStartingDate"]):dd/MM/yyyy}<br>
            📍 {reader["Place"]}
        """);
            }

            return results.Count == 0
                ? $"Không tìm thấy sự kiện nào vào ngày {day}/{month}/{year}."
                : $"Các sự kiện ngày {day}/{month}/{year}:<br><br>" +
                  string.Join("<br><br>━━━━━━━━━━━━━━━<br><br>", results);
        }

        private string GetEventsByMonth(int month, int year)
        {
            var results = new List<string>();
            using var conn = new SqlConnection(_configuration.GetConnectionString("DefaultConnection"));
            conn.Open();

            string sql = @"
        SELECT e.Id, e.Name, e.EventStartingDate, e.Place, et.Name AS EventType
        FROM Events e
        LEFT JOIN EventTypes et ON e.EventTypeId = et.Id
        WHERE MONTH(e.EventStartingDate) = @month
          AND YEAR(e.EventStartingDate) = @year";

            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@month", month);
            cmd.Parameters.AddWithValue("@year", year);

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                int id = Convert.ToInt32(reader["Id"]);
                string eventType = reader["EventType"]?.ToString() ?? "";

                string link = eventType switch
                {
                    "Tour" => $"/Home/DetailsTour/{id}",
                    "Concert" => $"/Home/DetailsConcert/{id}",
                    "Liveshow" => $"/Home/DetailsLiveshow/{id}",
                    _ => $"/Home/DetailsEvent/{id}"
                };

                results.Add($"""
            🎤 <b><a href="{link}">{reader["Name"]}</a></b> ({eventType})<br>
            📅 {Convert.ToDateTime(reader["EventStartingDate"]):dd/MM/yyyy}<br>
            📍 {reader["Place"]}
        """);
            }

            return results.Count == 0
                ? $"Không có sự kiện nào trong tháng {month}/{year}."
                : $"Tháng {month}/{year} có các sự kiện:<br><br>" +
                  string.Join("<br><br>━━━━━━━━━━━━━━━<br><br>", results);
        }

        private string GetEventsByYear(int year)
        {
            var results = new List<string>();
            using var conn = new SqlConnection(_configuration.GetConnectionString("DefaultConnection"));
            conn.Open();

            string sql = @"
        SELECT e.Id, e.Name, e.EventStartingDate, e.Place, et.Name AS EventType
        FROM Events e
        LEFT JOIN EventTypes et ON e.EventTypeId = et.Id
        WHERE YEAR(e.EventStartingDate) = @year";

            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@year", year);

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                int id = Convert.ToInt32(reader["Id"]);
                string eventType = reader["EventType"]?.ToString() ?? "";

                string link = eventType switch
                {
                    "Tour" => $"/Home/DetailsTour/{id}",
                    "Concert" => $"/Home/DetailsConcert/{id}",
                    "Liveshow" => $"/Home/DetailsLiveshow/{id}",
                    _ => $"/Home/DetailsEvent/{id}"
                };

                results.Add($"""
            🎫 <b><a href="{link}">{reader["Name"]}</a></b> ({eventType})<br>
            📅 {Convert.ToDateTime(reader["EventStartingDate"]):dd/MM/yyyy}<br>
            📍 {reader["Place"]}
        """);
            }

            return results.Count == 0
                ? $"Không có sự kiện nào trong năm {year}."
                : $"Các sự kiện năm {year}:<br><br>" +
                  string.Join("<br><br>━━━━━━━━━━━━━━━<br><br>", results);
        }

        
        // ===================================================
        // 🔹 Lấy sản phẩm theo loại (Lightstick, Album)
        // ===================================================
        private string GetProductsByCategory(string category)
        {
            try
            {
                // Map category từ user → DB
                string dbCategory = category.ToLower() switch
                {
                    "lightstick" => "Lightstick",
                    "album" => "Album",
                    _ => category
                };

                using (var conn = new SqlConnection(_configuration.GetConnectionString("DefaultConnection")))
                {
                    conn.Open();
                    var cmd = new SqlCommand(@"
                SELECT p.Id, p.Name, p.Price, p.ImageUrl, c.Name AS CategoryName
                FROM Products p
                JOIN Categories c ON p.CategoryId = c.Id
                WHERE c.Name LIKE '%' + @category + '%'", conn);
                    cmd.Parameters.AddWithValue("@category", dbCategory);

                    using (var reader = cmd.ExecuteReader())
                    {
                        if (!reader.HasRows)
                            return $"Không tìm thấy sản phẩm loại {category}.";

                        var result = $"🛍️ Các sản phẩm loại {category}:\n\n";
                        while (reader.Read())
                        {
                            int id = Convert.ToInt32(reader["Id"]);
                            string name = reader["Name"].ToString();
                            decimal price = Convert.ToDecimal(reader["Price"]);
                            string img = reader["ImageUrl"].ToString();
                            string link = $"/Home/ProductDisplay/{id}";

                            result += $@"
                                <br><b>📦 {name}</b>
                                <br>💰 <b>{price:N0} VNĐ</b><br>
                                <a href='{link}'>🔍 Xem chi tiết</a><br>
                                <img src='{img}' style='width:100px;border-radius:8px;margin-top:4px;'><br><br>";
                        }

                        return result.Trim();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Lỗi GetProductsByCategory: {ex.Message}");
                return "Không thể tải danh sách sản phẩm lúc này.";
            }
        }

        private object GetProductsByCategoryResponse(string category)
        {
            string text = GetProductsByCategory(category);

            return new
            {
                fulfillmentText = text,
                fulfillmentMessages = new object[]
                {
            new
            {
                payload = new
                {
                    richContent = new object[]
                    {
                        new object[]
                        {
                            new
                            {
                                type = "info",
                                title = $"Sản phẩm loại {category}",
                                subtitle = text.Replace("\n","<br>")
                            }
                        },
                        new object[]
                        {
                            new
                            {
                                type = "chips",
                                options = new[]
                                {
                                    new { text = "Lightstick" },
                                    new { text = "Album" }
                                }
                            }
                        }
                    }
                }
            }
                }
            };
        }

        // ===================================================
        // 🔹 Lấy sự kiện theo loại (Concert, Tour, Liveshow)
        // ===================================================
        private string GetEventsByType(string type)
        {
            var results = new List<string>();
            using var conn = new SqlConnection(_configuration.GetConnectionString("DefaultConnection"));
            conn.Open();

            string sql = @"
        SELECT e.Id, e.Name, e.EventStartingDate, e.Place, et.Name AS EventType
        FROM Events e
        LEFT JOIN EventTypes et ON e.EventTypeId = et.Id
        WHERE LOWER(et.Name) LIKE @type";

            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@type", $"%{type.ToLower()}%");

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                int id = Convert.ToInt32(reader["Id"]);
                string eventType = reader["EventType"]?.ToString() ?? "";

                string link = eventType switch
                {
                    "Tour" => $"/Home/DetailsTour/{id}",
                    "Concert" => $"/Home/DetailsConcert/{id}",
                    "Liveshow" => $"/Home/DetailsLiveshow/{id}",
                    _ => $"/Home/DetailsEvent/{id}"
                };

                results.Add($"""
            🎫 <b><a href="{link}">{reader["Name"]}</a></b> ({eventType})<br>
            📅 {Convert.ToDateTime(reader["EventStartingDate"]):dd/MM/yyyy}<br>
            📍 {reader["Place"]}
        """);
            }

            return results.Count == 0
                ? $"Không tìm thấy sự kiện loại {type}."
                : $"Các sự kiện loại {type}:<br><br>" +
                  string.Join("<br><br>━━━━━━━━━━━━━━━<br><br>", results);
        }

        private List<string> GetAllProductNames()
        {
            var list = new List<string>();
            string connStr = _configuration.GetConnectionString("DefaultConnection");

            using var conn = new SqlConnection(connStr);
            conn.Open();

            string sql = "SELECT Name FROM Products";
            using var cmd = new SqlCommand(sql, conn);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                list.Add(reader["Name"].ToString());
            }

            return list;
        }

        private List<string> GetAllEventNames()
        {
            var list = new List<string>();
            string connStr = _configuration.GetConnectionString("DefaultConnection");

            using var conn = new SqlConnection(connStr);
            conn.Open();

            string sql = "SELECT Name FROM Events";
            using var cmd = new SqlCommand(sql, conn);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                list.Add(reader["Name"].ToString());
            }

            return list;
        }

        // ===================================================
        // 🔹 Các phần còn lại (AI + Regex)
        // ===================================================
        private string RemoveDiacritics(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            var normalized = text.Normalize(NormalizationForm.FormD);
            var chars = normalized.Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark);
            return new string(chars.ToArray()).Normalize(NormalizationForm.FormC);
        }

        private async Task<string> FixSpellingWithAI(string userInput, string currentIntent)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(userInput))
                    return userInput;

                using var client = new HttpClient();
                string apiUrl = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-2.0-flash:generateContent?key={_geminiKey}";

                // 🧠 Bước 1: Sửa chính tả tiếng Việt (ngày, tháng, năm)
                string prompt = $@"
Bạn là công cụ **chỉ sửa lỗi chính tả tiếng Việt** cho chatbot.

🎯 Mục tiêu:
- Chỉ sửa lỗi gõ sai trong các từ liên quan đến thời gian (ngày, tháng, năm).
- KHÔNG được tự tạo ra sự kiện, danh sách sự kiện, mô tả hoặc tên mới.
- KHÔNG được dịch, thêm nội dung, hoặc diễn giải.

Ví dụ:
- 'thnags' -> 'tháng'
- 'thangs 5' -> 'tháng 5'
- 'ngay 2 thnag 4' -> 'ngày 2 tháng 4'
- 'nam 2025' -> 'năm 2025'

Người dùng nhập: ""{userInput}""

❗Chỉ trả về duy nhất phiên bản đã sửa (không giải thích, không thêm dấu câu, không liệt kê gì khác).
";

                var payload = new
                {
                    contents = new[] { new { parts = new[] { new { text = prompt } } } }
                };

                var content = new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json");
                var res = await client.PostAsync(apiUrl, content);
                if (res.IsSuccessStatusCode)
                {
                    dynamic json = JsonConvert.DeserializeObject(await res.Content.ReadAsStringAsync());
                    string corrected = json?.candidates?[0]?.content?.parts?[0]?.text?.ToString()?.Trim() ?? userInput;

                    // Lọc kết quả không hợp lệ
                    if (!string.IsNullOrWhiteSpace(corrected) &&
                        corrected.Length <= userInput.Length * 2 &&
                        !Regex.IsMatch(corrected, @"^[a-zA-Z\s]+$"))
                    {
                        userInput = corrected;
                    }
                }

                // 🧩 Bỏ bước 2 nếu intent là Event (đang ở mode thời gian)
                if (currentIntent == "Event")
                    return userInput;

                // 🧩 Chỉ Product mới cần đối chiếu tên DB
                List<string> namesFromDb = currentIntent switch
                {
                    "Product" => GetAllProductNames(),
                    _ => new List<string>()
                };

                if (namesFromDb.Count == 0)
                    return userInput;

                string prompt2 = $@"
Người dùng nhập: '{userInput}'.
Danh sách tên hợp lệ: {string.Join(", ", namesFromDb)}.
Hãy chọn tên trong danh sách giống nhất, chỉ trả về 1 tên duy nhất, không thêm giải thích.
";

                payload = new
                {
                    contents = new[] { new { parts = new[] { new { text = prompt2 } } } }
                };

                content = new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json");
                res = await client.PostAsync(apiUrl, content);

                if (!res.IsSuccessStatusCode)
                    return userInput;

                dynamic json2 = JsonConvert.DeserializeObject(await res.Content.ReadAsStringAsync());
                string matched = json2?.candidates?[0]?.content?.parts?[0]?.text?.ToString()?.Trim() ?? userInput;

                return namesFromDb.Contains(matched) ? matched : userInput;
            }
            catch
            {
                return userInput;
            }
        }

        private async Task<string> NormalizeEventTypeWithAI(string query)
        {
            try
            {
                var client = new HttpClient();
                string apiUrl = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-2.0-flash:generateContent?key={_geminiKey}";
                string prompt = $"'{query}' là loại sự kiện nào trong ['Tour', 'Concert', 'Liveshow']? Chỉ trả về 1 từ.";
                var payload = new { contents = new[] { new { parts = new[] { new { text = prompt } } } } };
                var res = await client.PostAsync(apiUrl, new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json"));
                if (!res.IsSuccessStatusCode) return query;
                dynamic json = JsonConvert.DeserializeObject(await res.Content.ReadAsStringAsync());
                return json?.candidates?[0]?.content?.parts?[0]?.text?.Trim() ?? query;
            }
            catch { return query; }
        }

        private (int? day, int? month, int? year) ExtractDateParts(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
                return (null, null, null);

            int? day = null, month = null, year = null;

            query = query.ToLower().Trim();

            // 0️⃣ CHỈ NHẬP SỐ: "1" → hiểu là tháng 1
            if (Regex.IsMatch(query, @"^\d{1,2}$"))
            {
                int val = int.Parse(query);
                if (val >= 1 && val <= 12)
                    return (null, val, null);
            }

            // 1️⃣ ngày xx tháng yy năm zzzz
            var m1 = Regex.Match(query, @"ngày\s*(\d{1,2})\s*th[aá]ng\s*(\d{1,2})(\s*n[aă]m\s*(\d{4}))?");
            if (m1.Success)
            {
                day = int.Parse(m1.Groups[1].Value);
                month = int.Parse(m1.Groups[2].Value);
                if (m1.Groups[4].Success) year = int.Parse(m1.Groups[4].Value);
                return (day, month, year);
            }

            // 2️⃣ dd/mm hoặc dd-mm
            var m2 = Regex.Match(query, @"\b(\d{1,2})[/-](\d{1,2})([/-](\d{4}))?\b");
            if (m2.Success)
            {
                day = int.Parse(m2.Groups[1].Value);
                month = int.Parse(m2.Groups[2].Value);
                if (m2.Groups[4].Success) year = int.Parse(m2.Groups[4].Value);
                return (day, month, year);
            }

            // 3️⃣ "ngày <dd>"
            var m3 = Regex.Match(query, @"ngày\s*(\d{1,2})");
            if (m3.Success) day = int.Parse(m3.Groups[1].Value);

            // 4️⃣ "tháng <mm>" hoặc "thang <mm>"
            var m4 = Regex.Match(query, @"th[aá]ng\s*(\d{1,2})");
            if (m4.Success) month = int.Parse(m4.Groups[1].Value);

            // 5️⃣ "năm <yyyy>" hoặc "nam <yyyy>"
            var m5 = Regex.Match(query, @"n[aă]m\s*(\d{4})");
            if (m5.Success) year = int.Parse(m5.Groups[1].Value);

            return (day, month, year);
        }

        private async Task SaveChatHistory(string sessionId, string userInput, string botReply)
        {
            try
            {
                string? userId = null;
                if (User?.Identity?.IsAuthenticated == true)
                {
                    userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                }

                var chat = new ChatHistory
                {
                    SessionId = sessionId,
                    EncryptedUserMessage = EncryptionHelper.Encrypt(userInput),
                    EncryptedBotReply = EncryptionHelper.Encrypt(botReply),
                    Timestamp = DateTime.Now,
                    UserId = userId
                };

                _context.ChatHistories.Add(chat);
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ Không thể lưu lịch sử chat: {ex.Message}");
            }
        }
    }
}

public class ProductResponse
{
    public string FulfillmentText { get; set; } = "";
    public object[] FulfillmentMessages { get; set; } = Array.Empty<object>();
}
