using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;

//!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!
//!!   ТОЧКА ВХОДА ДЛЯ АВТОНОМНОЙ ОТЛАДКИ (при запуске index.exe)    !!
//!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!
string scriptName = args.Length > 0 ?
                    Path.GetFileNameWithoutExtension(args[0]) : "Test";

// Объект переменных окружения:
var debugEnv = new Dictionary<string,
                   string>(StringComparer.OrdinalIgnoreCase) {
    ["Accept-Language"] = "ru-RU,ru;q=0.9",
    ["SERVER_PROTOCOL"] = "HTTP/1.1",
    ["SCRIPT_FILENAME"] = Path.GetFullPath($"{scriptName}.cs"),
    ["POST_FILENAME"] = "",
    ["QUERY_STRING"] = "123",
    ["REMOTE_ADDR"] = "127.0.0.1",
    ["User-Agent"] = "ConsoleDebugger/1.0",
    ["Cookie"] = ""
};

// Стандартный ввод:
Stream debugInput = Console.IsInputRedirected
    ? Console.OpenStandardInput()
    : new MemoryStream(Encoding.UTF8.GetBytes("test_post_data=hello"));

// Находим класс по имени и вызываем его метод EvalAsync
var scriptType = typeof(Program).Assembly.GetTypes()
    .First(t => t.Name.Equals(scriptName, StringComparison.OrdinalIgnoreCase));
var instance = Activator.CreateInstance(scriptType);
var method = scriptType.GetMethod("EvalAsync");
var (status, body) =
    await (Task<(string, byte[])>)method.Invoke(instance, [debugEnv, debugInput]);

// Выводим резальтат на консоль
if(status!=null) Console.WriteLine(status);
if(body!=null) Console.OpenStandardOutput().Write(body);


//!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!
//!!  Тест. Вывод переменных окружения.                  24.11.2026  !!
//!!  вызывается по URL .../test?123 или .../test.cs?456             !!
//!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!
public class Test {
  public async Task<(string status, byte[] body)> EvalAsync(
               Dictionary<string, string> env, Stream input) {
    using var reader = new StreamReader(input, Encoding.UTF8, leaveOpen: true);
    string getEnv(string key) => env.TryGetValue(key, out var v) ? v : "";
    string STD_INPUT = await reader.ReadToEndAsync();

    string html= $"""
Content-Type: text/html; charset=utf-8

<h1>Привет мир из C# .NET10!</h1>
<h3>Переменные окружения:</h3>
Accept-Language={getEnv("Accept-Language")};<br>
SERVER_PROTOCOL={getEnv("SERVER_PROTOCOL")};<br>
SCRIPT_FILENAME={getEnv("SCRIPT_FILENAME")};<br>
POST_FILENAME={getEnv("POST_FILENAME")};<br>
QUERY_STRING={getEnv("QUERY_STRING")};<br>
REMOTE_ADDR={getEnv("REMOTE_ADDR")};<br>
User-Agent={getEnv("User-Agent")};<br>
Cookie={getEnv("Cookie")};<br>
STD_INPUT={STD_INPUT}.
""";
    // P.S.
    // Также могут быть доступны и многие другие переменные окружения.
    // Названия переменных соответствуют их названиям в заголовках
    // поступившего запроса.

    // Необязательный возврат статуса (null = стандартный "200 OK")
    return (null, Encoding.UTF8.GetBytes(html));
   }
}

//!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!
//!!   Проверка POST-запроса.                            26.11.2026  !!
//!!   вызывается по URL .../post или .../post.cs                    !!
//!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!
public class Post {
  public async Task<(string status, byte[] body)> EvalAsync(
               Dictionary<string, string> env, Stream input) {
    using var reader = new StreamReader(input, Encoding.UTF8, leaveOpen: true);
    string STD_INPUT = await reader.ReadToEndAsync();
    string DT = DateTime.Now.ToString("ddd MMM dd HH:mm:ss yyyy",
                                      System.Globalization.CultureInfo.InvariantCulture);

    string resp = $"Content-Type: text/plain; charset=utf-8\r\n\r\n{DT} response body: {STD_INPUT}";
    return (null, Encoding.UTF8.GetBytes(resp));
  }
}
