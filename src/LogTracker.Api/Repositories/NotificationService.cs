using System.Net.Http;
using System.Threading.Tasks;

namespace LogTrackerAPI.Repositories
{
    public class NotificationService
    {
        private readonly HttpClient _httpClient;

        // Credentials intentionally removed before publishing; they are read from configuration after the refactor.
        private readonly string _botToken = string.Empty;
        private readonly string _chatId = string.Empty;

        public NotificationService()
        {
            _httpClient = new HttpClient();
        }

        /// <summary>
        /// Kritik bir hata oluştuğunda yazılımcının Telegram hesabına anlık bildirim atar.
        /// </summary>
        public async Task SendTelegramAlertAsync(string errorMessage, string errorSource, string errorType)
        {
            try
            {
                // Telegram mesajını biçimlendiriyoruz (Bold ve emoji kullanarak şık hale getirelim)
                string message = $"🚨 *Kritik Sistem Hatası Yakalandı!* 🚨\n\n" +
                                 $"⚠️ *Hata Tipi:* `{errorType}`\n" +
                                 $"📂 *Kaynak:* `{errorSource}`\n" +
                                 $"💬 *Mesaj:* _{errorMessage}_\n\n" +
                                 $"⏰ *Zaman:* {System.DateTime.Now:HH:mm:ss}";

                // Telegram API URL'si (Markdown formatında şık göndermek için parse_mode ekliyoruz)
                string url = $"https://api.telegram.org/bot{_botToken}/sendMessage?chat_id={_chatId}&text={System.Uri.EscapeDataString(message)}&parse_mode=Markdown";

                // İstek atılıyor (Kullanıcı akışını bloklamamak için Fire-and-Forget yapıyoruz)
                await _httpClient.GetAsync(url);
            }
            catch
            {
                // Bildirim gönderilirken bir hata olursa uygulamanın ana akışı etkilenmesin diye yutuyoruz
            }
        }
    }
}