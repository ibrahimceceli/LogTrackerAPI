using LogTrackerAPI.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace LogTrackerAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TestLogController : ControllerBase
    {
        private readonly LogRepository _logRepository;

        public TestLogController(LogRepository logRepository)
        {
            _logRepository = logRepository;
        }

        // Simülatörün GET isteği atabilmesi için HttpGet yaptık ve gerçek hata fırlatıyoruz
        [HttpGet("test-error")]
        public IActionResult TestErrorLog()
        {
            // Kasıtlı olarak InvalidOperationException fırlatıyoruz
            throw new InvalidOperationException("API üzerinde geçersiz bir operasyon algılandı!");
        }

        // Kasıtlı olarak sıfıra bölme hatası fırlatan yeni bir test ucu
        [HttpGet("simulate-error")]
        public IActionResult SimulateError()
        {
            // Gerçek hayatta sıkça yaşanan Null Reference hatasını fırlatıyoruz
            throw new NullReferenceException("Sistemde kritik bir nesne yüklenirken null referans hatası alındı!");
        }

        // İstersen test amaçlı veritabanına doğrudan manuel log yazan bir uç (Hata fırlatmaz)
        [HttpPost("manual-log")]
        public IActionResult ManualLog()
        {
            _logRepository.LogError(
                errorMessage: "C# Web API üzerinden gelen manuel test hatası!",
                errorSource: "TestLogController.cs",
                errorType: "ManualTestException"
            );

            return Ok("Manuel hata logu SQL'e başarıyla gönderildi!");
        }
    }
}