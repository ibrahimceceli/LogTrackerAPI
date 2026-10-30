// All user-facing text lives in this one dictionary (English + Turkish).
// Static markup uses data-i18n="key" (text) and data-i18n-attr="attribute:key" (attributes);
// JavaScript calls t("key", { placeholder: value }).
import { readSetting, writeSetting } from './storage.js';

const STORAGE_KEY = 'logtracker.language';
const SUPPORTED_LANGUAGES = ['en', 'tr'];

const messages = {
    en: {
        'app.documentTitle': 'LogTracker · Dashboard',
        'app.tagline': 'API request & error monitoring',
        'a11y.skipToContent': 'Skip to content',
        'language.label': 'Language',

        'status.connecting': 'Connecting…',
        'status.live': 'Live',
        'status.paused': 'Paused',
        'status.degraded': 'Partly unavailable',
        'status.offline': 'API unreachable',
        'status.locked': 'API key required',
        'status.lastUpdated': 'Updated',

        'actions.pause': 'Pause',
        'actions.resume': 'Resume',
        'actions.pauseHint': 'Pause automatic refresh (every {seconds} s)',
        'actions.resumeHint': 'Resume automatic refresh',
        'actions.forgetKey': 'Forget API key',
        'actions.enterKey': 'Enter API key',
        'actions.retry': 'Retry',
        'actions.close': 'Close',
        'actions.cancel': 'Cancel',

        'overview.title': 'Overview',
        'range.label': 'Time range',
        'range.24h': '24h',
        'range.7d': '7d',
        'range.last24h': 'Last 24 hours',
        'range.last7d': 'Last 7 days',

        'summary.title': 'Summary',
        'summary.totalRequests': 'Total requests',
        'summary.perHour': '≈ {value} per hour',
        'summary.serverErrors': 'Server errors (5xx)',
        'summary.clientErrors': '{value} client errors (4xx)',
        'summary.errorRate': 'Error rate',
        'summary.errorRateHint': 'share of 5xx responses',
        'summary.healthGood': 'Healthy',
        'summary.healthWarning': 'Elevated',
        'summary.healthCritical': 'Critical',
        'summary.avgLatency': 'Average latency',
        'summary.avgLatencyHint': 'mean response time',
        'summary.p95Latency': 'p95 latency',
        'summary.p95LatencyHint': '95% of requests were faster',
        'summary.noTraffic': 'No requests recorded in this time range yet. Send some traffic to the API (for example with the simulator) and it will show up here.',

        'hourly.title': 'Errors per hour',
        'hourly.subtitle': 'stacked by exception type, local time',
        'hourly.total': '{value} errors',
        'hourly.empty': 'No errors in this time range.',
        'hourly.chartLabel': 'Stacked bar chart of errors per hour. {total} errors in total; the busiest hour was {peakTime} with {peak} errors.',
        'hourly.tooltipTotal': 'Total: {value}',
        'hourly.showTable': 'Show data as a table',
        'hourly.tableCaption': 'Errors per hour and exception type (hours with errors only)',
        'hourly.hour': 'Hour',
        'hourly.type': 'Exception type',
        'hourly.count': 'Errors',
        'hourly.other': 'Other',
        'hourly.chartUnavailable': 'The chart library could not be loaded.',

        'endpoints.title': 'Endpoints',
        'endpoints.subtitle': 'sorted by request count',
        'endpoints.caption': 'Requests and latency per endpoint',
        'endpoints.endpoint': 'Endpoint',
        'endpoints.requests': 'Requests',
        'endpoints.serverErrors': '5xx',
        'endpoints.avg': 'Avg',
        'endpoints.p95': 'p95',
        'endpoints.max': 'Max',
        'endpoints.share': 'Share of all requests: {value}',
        'endpoints.empty': 'No endpoint traffic in this time range.',
        'endpoint.unmatched': '(no matching route)',

        'weekly.title': 'Weekly latency trend',
        'weekly.subtitle': 'Average response time per weekday over the last 7 days (UTC). Darker cells are slower (logarithmic scale).',
        'weekly.caption': 'Average response time per endpoint and weekday',
        'weekly.legendFast': 'Faster',
        'weekly.legendSlow': 'Slower',
        'weekly.noData': 'No data',
        'weekly.today': 'today (UTC)',
        'weekly.empty': 'No requests in the last 7 days.',

        'requests.title': 'Recent requests',
        'requests.subtitle': 'Newest first',
        'requests.caption': 'Most recent requests',
        'requests.time': 'Time',
        'requests.method': 'Method',
        'requests.path': 'Path',
        'requests.status': 'Status',
        'requests.duration': 'Duration',
        'requests.empty': 'No requests recorded yet.',

        'errors.title': 'Recent errors',
        'errors.subtitle': 'Newest first · select an error to see its stack trace',
        'errors.caption': 'Most recent exceptions',
        'errors.time': 'Time',
        'errors.type': 'Type',
        'errors.message': 'Message',
        'errors.status': 'Status',
        'errors.empty': 'No errors recorded. Nice!',
        'errors.noMessage': '(no message)',

        'pager.label': 'Pagination',
        'pager.previous': 'Previous',
        'pager.next': 'Next',
        'pager.summary': 'Page {page} of {pages} · {total} total',

        'details.title': 'Error details',
        'details.titleWithId': 'Error #{id}',
        'details.occurredAt': 'Occurred at',
        'details.request': 'Request',
        'details.endpoint': 'Endpoint',
        'details.status': 'Status code',
        'details.source': 'Source',
        'details.traceId': 'Trace ID',
        'details.stackTrace': 'Stack trace',
        'details.noStackTrace': 'No stack trace was recorded for this error.',
        'details.notFound': 'This error no longer exists. It may have been removed by the retention policy.',

        'apiKey.title': 'API key required',
        'apiKey.description': 'This LogTracker instance protects its reports with an API key. The key is stored only in this browser.',
        'apiKey.label': 'API key',
        'apiKey.submit': 'Save and retry',
        'apiKey.rejected': 'The API key was rejected. Check it and try again.',

        'state.loading': 'Loading…',
        'state.errorTitle': 'Could not load this section',
        'error.network': 'The API could not be reached. Is it running?',
        'error.timeout': 'The API did not respond in time.',
        'error.unauthorized': 'An API key is required to view the reports.',
        'error.notFound': 'The requested data was not found.',
        'error.http': 'The API returned an error (HTTP {status}).',
        'error.badResponse': 'The API returned an unexpected response.',
        'error.unexpected': 'Something went wrong while showing this data.',
        'error.traceId': 'Trace ID: {traceId}',
        'error.stale': 'Could not refresh — showing data from {time}.',

        'footer.text': 'LogTracker · open-source request and error monitoring for ASP.NET Core',
    },
    tr: {
        'app.documentTitle': 'LogTracker · İzleme Paneli',
        'app.tagline': 'API istek ve hata izleme',
        'a11y.skipToContent': 'İçeriğe geç',
        'language.label': 'Dil',

        'status.connecting': 'Bağlanıyor…',
        'status.live': 'Canlı',
        'status.paused': 'Duraklatıldı',
        'status.degraded': 'Kısmen erişilemiyor',
        'status.offline': 'API\'ye ulaşılamıyor',
        'status.locked': 'API anahtarı gerekli',
        'status.lastUpdated': 'Son güncelleme',

        'actions.pause': 'Duraklat',
        'actions.resume': 'Sürdür',
        'actions.pauseHint': 'Otomatik yenilemeyi duraklat ({seconds} sn\'de bir)',
        'actions.resumeHint': 'Otomatik yenilemeyi sürdür',
        'actions.forgetKey': 'API anahtarını unut',
        'actions.enterKey': 'API anahtarı gir',
        'actions.retry': 'Tekrar dene',
        'actions.close': 'Kapat',
        'actions.cancel': 'Vazgeç',

        'overview.title': 'Genel bakış',
        'range.label': 'Zaman aralığı',
        'range.24h': '24 sa',
        'range.7d': '7 gün',
        'range.last24h': 'Son 24 saat',
        'range.last7d': 'Son 7 gün',

        'summary.title': 'Özet',
        'summary.totalRequests': 'Toplam istek',
        'summary.perHour': 'saatte ≈ {value}',
        'summary.serverErrors': 'Sunucu hataları (5xx)',
        'summary.clientErrors': '{value} istemci hatası (4xx)',
        'summary.errorRate': 'Hata oranı',
        'summary.errorRateHint': '5xx yanıtların oranı',
        'summary.healthGood': 'Sağlıklı',
        'summary.healthWarning': 'Yüksek',
        'summary.healthCritical': 'Kritik',
        'summary.avgLatency': 'Ortalama süre',
        'summary.avgLatencyHint': 'ortalama yanıt süresi',
        'summary.p95Latency': 'p95 süresi',
        'summary.p95LatencyHint': 'isteklerin %95\'i daha hızlı',
        'summary.noTraffic': 'Bu zaman aralığında henüz istek kaydedilmedi. API\'ye trafik gönderin (örneğin simülatörle), veriler burada görünecek.',

        'hourly.title': 'Saatlik hatalar',
        'hourly.subtitle': 'istisna türüne göre, yerel saat',
        'hourly.total': '{value} hata',
        'hourly.empty': 'Bu zaman aralığında hata yok.',
        'hourly.chartLabel': 'Saatlik hataları gösteren yığılmış çubuk grafik. Toplam {total} hata; en yoğun saat {peakTime}, {peak} hata.',
        'hourly.tooltipTotal': 'Toplam: {value}',
        'hourly.showTable': 'Verileri tablo olarak göster',
        'hourly.tableCaption': 'Saat ve istisna türüne göre hatalar (yalnızca hata olan saatler)',
        'hourly.hour': 'Saat',
        'hourly.type': 'İstisna türü',
        'hourly.count': 'Hata',
        'hourly.other': 'Diğer',
        'hourly.chartUnavailable': 'Grafik kütüphanesi yüklenemedi.',

        'endpoints.title': 'Uç noktalar',
        'endpoints.subtitle': 'istek sayısına göre sıralı',
        'endpoints.caption': 'Uç nokta bazında istekler ve yanıt süreleri',
        'endpoints.endpoint': 'Uç nokta',
        'endpoints.requests': 'İstek',
        'endpoints.serverErrors': '5xx',
        'endpoints.avg': 'Ort.',
        'endpoints.p95': 'p95',
        'endpoints.max': 'Maks.',
        'endpoints.share': 'Tüm istekler içindeki payı: {value}',
        'endpoints.empty': 'Bu zaman aralığında uç nokta trafiği yok.',
        'endpoint.unmatched': '(eşleşen rota yok)',

        'weekly.title': 'Haftalık yanıt süresi trendi',
        'weekly.subtitle': 'Son 7 günde hafta günlerine göre ortalama yanıt süresi (UTC). Koyu hücreler daha yavaş (logaritmik ölçek).',
        'weekly.caption': 'Uç nokta ve hafta gününe göre ortalama yanıt süresi',
        'weekly.legendFast': 'Hızlı',
        'weekly.legendSlow': 'Yavaş',
        'weekly.noData': 'Veri yok',
        'weekly.today': 'bugün (UTC)',
        'weekly.empty': 'Son 7 günde istek yok.',

        'requests.title': 'Son istekler',
        'requests.subtitle': 'En yeniler önce',
        'requests.caption': 'En son istekler',
        'requests.time': 'Zaman',
        'requests.method': 'Metot',
        'requests.path': 'Yol',
        'requests.status': 'Durum',
        'requests.duration': 'Süre',
        'requests.empty': 'Henüz kayıtlı istek yok.',

        'errors.title': 'Son hatalar',
        'errors.subtitle': 'En yeniler önce · yığın izini görmek için bir hata seçin',
        'errors.caption': 'En son istisnalar',
        'errors.time': 'Zaman',
        'errors.type': 'Tür',
        'errors.message': 'Mesaj',
        'errors.status': 'Durum',
        'errors.empty': 'Kayıtlı hata yok. Harika!',
        'errors.noMessage': '(mesaj yok)',

        'pager.label': 'Sayfalama',
        'pager.previous': 'Önceki',
        'pager.next': 'Sonraki',
        'pager.summary': 'Sayfa {page}/{pages} · toplam {total}',

        'details.title': 'Hata ayrıntıları',
        'details.titleWithId': 'Hata #{id}',
        'details.occurredAt': 'Oluşma zamanı',
        'details.request': 'İstek',
        'details.endpoint': 'Uç nokta',
        'details.status': 'Durum kodu',
        'details.source': 'Kaynak',
        'details.traceId': 'Trace ID',
        'details.stackTrace': 'Yığın izi (stack trace)',
        'details.noStackTrace': 'Bu hata için yığın izi kaydedilmemiş.',
        'details.notFound': 'Bu hata artık mevcut değil. Saklama süresi dolduğu için silinmiş olabilir.',

        'apiKey.title': 'API anahtarı gerekli',
        'apiKey.description': 'Bu LogTracker kurulumu raporlarını bir API anahtarıyla koruyor. Anahtar yalnızca bu tarayıcıda saklanır.',
        'apiKey.label': 'API anahtarı',
        'apiKey.submit': 'Kaydet ve tekrar dene',
        'apiKey.rejected': 'API anahtarı reddedildi. Kontrol edip tekrar deneyin.',

        'state.loading': 'Yükleniyor…',
        'state.errorTitle': 'Bu bölüm yüklenemedi',
        'error.network': 'API\'ye ulaşılamadı. Çalıştığından emin misiniz?',
        'error.timeout': 'API zamanında yanıt vermedi.',
        'error.unauthorized': 'Raporları görmek için API anahtarı gerekiyor.',
        'error.notFound': 'İstenen veri bulunamadı.',
        'error.http': 'API bir hata döndürdü (HTTP {status}).',
        'error.badResponse': 'API beklenmeyen bir yanıt döndürdü.',
        'error.unexpected': 'Bu veriler gösterilirken bir sorun oluştu.',
        'error.traceId': 'Trace ID: {traceId}',
        'error.stale': 'Yenilenemedi — {time} itibarıyla veriler gösteriliyor.',

        'footer.text': 'LogTracker · ASP.NET Core için açık kaynak istek ve hata izleme',
    },
};

let currentLanguage = detectLanguage();
const listeners = new Set();

function detectLanguage() {
    const saved = readSetting(STORAGE_KEY);
    if (SUPPORTED_LANGUAGES.includes(saved)) {
        return saved;
    }
    const browserLanguage = (navigator.languages?.[0] ?? navigator.language ?? 'en').toLowerCase();
    return browserLanguage.startsWith('tr') ? 'tr' : 'en';
}

export function getLanguage() {
    return currentLanguage;
}

export function setLanguage(language) {
    if (!SUPPORTED_LANGUAGES.includes(language) || language === currentLanguage) {
        return;
    }
    currentLanguage = language;
    writeSetting(STORAGE_KEY, language);
    applyStaticTranslations();
    listeners.forEach((listener) => listener(language));
}

export function onLanguageChange(listener) {
    listeners.add(listener);
}

/** Looks up a message and fills {placeholders}. Falls back to English, then to the key itself. */
export function t(key, values = {}) {
    const template = messages[currentLanguage][key] ?? messages.en[key] ?? key;
    return template.replace(/\{(\w+)\}/g, (match, name) => (name in values ? String(values[name]) : match));
}

/** Translates the static markup in index.html. Only textContent/setAttribute are used, never innerHTML. */
export function applyStaticTranslations(root = document) {
    document.documentElement.lang = currentLanguage;
    document.title = t('app.documentTitle');

    for (const element of root.querySelectorAll('[data-i18n]')) {
        element.textContent = t(element.dataset.i18n);
    }
    for (const element of root.querySelectorAll('[data-i18n-attr]')) {
        for (const pair of element.dataset.i18nAttr.split(',')) {
            const [attribute, key] = pair.split(':').map((part) => part.trim());
            element.setAttribute(attribute, t(key));
        }
    }
}
