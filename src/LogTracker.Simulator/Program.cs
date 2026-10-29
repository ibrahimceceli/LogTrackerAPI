using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace LogTracker.Simulator
{
    class Program
    {
        // LogTracker API adresimiz (Kendi localhost portuna göre güncelle!)
        private static readonly string ApiBaseUrl = "https://localhost:7292";
        private static readonly HttpClient Client = new HttpClient();
        private static readonly Random Randomizer = new Random();

        static async Task Main(string[] args)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("==================================================");
            Console.WriteLine("🚀 LogTracker Yük Testi & İstek Simülatörü Başladı");
            Console.WriteLine("==================================================");
            Console.ResetColor();
            Console.WriteLine("API'ye sürekli asenkron trafik ve hatalar gönderiliyor...\n");

            // Simülasyonu durdurmak için konsolda bir tuşa basılmasını bekleyen asenkron döngü
            bool keepRunning = true;
            _ = Task.Run(() =>
            {
                Console.ReadKey();
                keepRunning = false;
            });

            // Endpoint listemiz (Dashboard'u besleyecek gerçekçi adresler)
            string[] endpoints = {
                "/api/products",
                "/api/LogReport/analysis",
                "/api/LogReport/performance",
                "/index.html"
            };

            // Hata simülasyonu yapacak endpointler
            string[] errorEndpoints = {
                "/api/TestLog/simulate-error",
                "/api/TestLog/test-error"
            };

            while (keepRunning)
            {
                // Aynı anda 5 farklı asenkron istek fırlatalım (Eşzamanlılık / Concurrency)
                Task[] tasks = new Task[5];

                for (int i = 0; i < tasks.Length; i++)
                {
                    int chance = Randomizer.Next(100);

                    if (chance < 75) // %75 olasılıkla normal performans isteği at
                    {
                        string targetPath = endpoints[Randomizer.Next(endpoints.Length)];
                        tasks[i] = SendRequestAsync(targetPath, ConsoleColor.Green);
                    }
                    else // %25 olasılıkla hata üreten endpointleri tetikle
                    {
                        string targetPath = errorEndpoints[Randomizer.Next(errorEndpoints.Length)];
                        tasks[i] = SendRequestAsync(targetPath, ConsoleColor.Red);
                    }
                }

                // İsteklerin tamamlanmasını bekle
                await Task.WhenAll(tasks);

                // Sunucuyu çökertmemek ve izlenebilir kılmak için her döngü arası kısa bir mola
                await Task.Delay(Randomizer.Next(1000, 3000));
            }

            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("\n[!] Simülasyon sonlandırıldı.");
            Console.ResetColor();
        }

        private static async Task SendRequestAsync(string path, ConsoleColor logColor)
        {
            try
            {
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                HttpResponseMessage response = await Client.GetAsync(ApiBaseUrl + path);
                stopwatch.Stop();

                Console.ForegroundColor = logColor;
                Console.WriteLine($"[{DateTime.Now.ToLongTimeString()}] İstek: {path} | Durum: {response.StatusCode} | Süre: {stopwatch.ElapsedMilliseconds}ms");
                Console.ResetColor();
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.DarkRed;
                Console.WriteLine($"[-] Bağlantı Hatası: {ex.Message}");
                Console.ResetColor();
            }
        }
    }
}