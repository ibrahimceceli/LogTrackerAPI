using System.Diagnostics;
using System.Net;
using System.Text.Json;
using LogTrackerAPI.Repositories;

namespace LogTrackerAPI.Middlewares
{
    public class ExceptionHandlingMiddleware
    {
        private readonly RequestDelegate _next;

        public ExceptionHandlingMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context, LogRepository logRepository)
        {
            // Hataya düşse bile süreyi ölçebilmek için kronometreyi en üst katmanda başlatıyoruz
            Stopwatch stopwatch = Stopwatch.StartNew();
            string endpointPath = context.Request.Path.Value;

            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                int responseTimeMS = (int)stopwatch.ElapsedMilliseconds;

                // 1. Hatayı veritabanına logla ve kullanıcıya yanıt dön
                await HandleExceptionAsync(context, ex, logRepository);

                // 2. Telegram Botu ile sana (yazılımcıya) anlık bildirim fırlat (Asenkron - Fire and Forget)
                var notificationService = new NotificationService();
                string errorSource = ex.TargetSite?.DeclaringType?.FullName ?? ex.Source ?? "Bilinmeyen Kaynak";
                _ = Task.Run(() => notificationService.SendTelegramAlertAsync(
                    ex.Message,
                    errorSource,
                    ex.GetType().Name
                ));

                // 3. Hatalı isteğin performans süresini de logla!
                if (!string.IsNullOrEmpty(endpointPath) && !endpointPath.Contains("swagger"))
                {
                    logRepository.LogPerformance(endpointPath, responseTimeMS);
                }
            }
        }

        private static Task HandleExceptionAsync(HttpContext context, Exception exception, LogRepository logRepository)
        {
            string errorMessage = exception.Message;
            string errorSource = exception.TargetSite?.DeclaringType?.FullName ?? exception.Source ?? "Bilinmeyen Kaynak";
            string errorType = exception.GetType().Name;

            logRepository.LogError(errorMessage, errorSource, errorType);

            context.Response.ContentType = "application/json";
            context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;

            var response = new
            {
                StatusCode = context.Response.StatusCode,
                Message = "Sistemde beklenmedik bir hata oluştu. Teknik ekip bilgilendirildi."
            };

            string jsonResponse = JsonSerializer.Serialize(response);
            return context.Response.WriteAsync(jsonResponse);
        }
    }
}