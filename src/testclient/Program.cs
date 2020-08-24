using System;
using System.Diagnostics;
using System.Net.Http;
using System.Threading.Tasks;

namespace testclient
{
    class Program
    {
        static async Task Main(string[] args)
        {
            // let it dispose at the end
            using var http = new HttpClient
            {
                BaseAddress = new Uri("http://greeter.demo")
            };

            // in-line function makes for less code
            var watch = new Stopwatch();
            async Task Greet(string name)
            {
                var requestObj = new {name};

                watch.Restart();
                HttpResponseMessage response = await http.PostAsJsonAsync("/api/greeting", requestObj);
                watch.Stop();

                if (response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"Posted '{name}' in {watch.ElapsedMilliseconds}ms");

                    var greeting = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"Greeting = {greeting}");
                }
                else
                {
                    Console.WriteLine($"Failed to post '{name}', status = ({(int)response.StatusCode}) {response.StatusCode}");
                }
                Console.WriteLine();

                await Task.Delay(500);  // we want to see timestamps go up
            }

            // say hello once
            await Greet("John");
            await Greet("Paul");
            await Greet("George");
            await Greet("Ringo");

            // say hello to Paul again (cache hit)
            await Greet("Paul");

            // say hello to no one (input validation - bad request)
            await Greet(" ");

            // Say Hello to Mud (bad request - Service back-end)
            await Greet("Mud");
        }
    }
}
