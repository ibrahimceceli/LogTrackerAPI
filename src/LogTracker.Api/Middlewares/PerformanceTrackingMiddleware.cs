using System.Diagnostics;
using LogTrackerAPI.Repositories;

namespace LogTrackerAPI.Middlewares
{
    public class PerformanceTrackingMiddleware
    {
        private readonly RequestDelegate _next;

        // RequestDelegate, boru hattındaki bir sonraki aşamayı (örneğin controller'ı) temsil eder
        public PerformanceTrackingMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        // Her HTTP isteğinde bu metot otomatik olarak tetiklenir
        public async Task InvokeAsync(HttpContext context, LogRepository logRepository)
        {
            // 1. Kronometreyi başlatıyoruz
            Stopwatch stopwatch = Stopwatch.StartNew();

            // 2. İsteğin hangi adrese (Path) atıldığını alıyoruz (örn: /api/products)
            string endpointPath = context.Request.Path.Value;

            // 3. İsteğin bir sonraki adıma geçmesine izin veriyoruz ve tamamlanmasını bekliyoruz
            await _next(context);

            // 4. İşlem bittiğinde kronometreyi durduruyoruz
            stopwatch.Stop();
            int responseTimeMS = (int)stopwatch.ElapsedMilliseconds;

            // Swagger isteklerini veya boş path'leri loglayıp veritabanını şişirmemek için ufak bir filtre koyuyoruz
            if (!string.IsNullOrEmpty(endpointPath) && !endpointPath.Contains("swagger"))
            {
                // 5. Süreyi ve adresi asenkron akışın içinde veritabanına kaydediyoruz
                logRepository.LogPerformance(endpointPath, responseTimeMS);
            }
        }
    }
}