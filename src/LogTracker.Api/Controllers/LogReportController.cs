using LogTrackerAPI.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace LogTrackerAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class LogReportController : ControllerBase
    {
        private readonly LogRepository _logRepository;

        // Dependency Injection ile LogRepository servisimizi içeri alıyoruz
        public LogReportController(LogRepository logRepository)
        {
            _logRepository = logRepository;
        }

        /// <summary>
        /// Sistemde oluşan tüm hataları en yeniden en eskiye doğru listeler.
        /// </summary>
        [HttpGet("errors")]
        public IActionResult GetErrors()
        {
            var errors = _logRepository.GetErrorLogs();
            return Ok(errors);
        }

        /// <summary>
        /// API'ye yapılan tüm isteklerin sürelerini listeler.
        /// </summary>
        [HttpGet("performance")]
        public IActionResult GetPerformance()
        {
            var performance = _logRepository.GetPerformanceMetrics();
            return Ok(performance);
        }

        /// <summary>
        /// Endpoint bazında toplam istek sayısı ve ortalama yanıt süresi raporunu döner.
        /// </summary>
        [HttpGet("analysis")]
        public IActionResult GetAnalysis()
        {
            var analysis = _logRepository.GetPerformanceAnalysis();
            return Ok(analysis);
        }

        /// <summary>
        /// Saatlik bazda hangi hata tipinden kaç adet alındığını matris formatında döner (PIVOT).
        /// </summary>
        [HttpGet("hourly-error-matrix")]
        public IActionResult GetHourlyErrorMatrix()
        {
            var matrix = _logRepository.GetHourlyErrorMatrix();
            return Ok(matrix);
        }

        /// <summary>
        /// Son 7 günde endpointlerin ortalama yanıt sürelerinin gün bazlı dağılımını döner (PIVOT).
        /// </summary>
        [HttpGet("weekly-performance-trend")]
        public IActionResult GetWeeklyPerformanceTrend()
        {
            var trend = _logRepository.GetWeeklyPerformanceTrend();
            return Ok(trend);
        }
    }
}