// Pet-in səsi — brauzerin Web Speech API-si (SpeechSynthesis).
//
// Səs MƏCBURİ deyil: dəstək yoxdursa funksiya sadəcə `false` qaytarır və
// söhbət ekranı ağzı öz ölçüsü ilə tərpədir. Heç bir halda xəta atılmır —
// uşaq ekranında "səs işləmir" mesajı görünməməlidir.

/** Səslər Chrome-da ASENXRON yüklənir: ilk çağırışda siyahı boş ola bilər. */
function voicesReady() {
    const voices = speechSynthesis.getVoices();
    if (voices.length > 0) {
        return Promise.resolve(voices);
    }

    return new Promise(resolve => {
        // Hadisə heç vaxt gəlməyə bilər (bəzi mobil brauzerlərdə) — ona görə
        // qısa taymer də var: səssiz gözləmək danışmamaqdan pisdir.
        const done = () => resolve(speechSynthesis.getVoices());
        speechSynthesis.addEventListener('voiceschanged', done, { once: true });
        setTimeout(done, 1000);
    });
}

/**
 * Namizəd dil kodlarını ÜSTÜNLÜK SIRASI ilə yoxlayır: əvvəlcə tam uyğunluq
 * (az-AZ), sonra prefiks (az). Heç biri yoxdursa `null` qayıdır və brauzerin
 * öz standart səsi işlədilir — səs dilinin qaydası C# tərəfdədir
 * (PetSpeechLocales), burada yalnız tətbiq olunur.
 */
function pickVoice(voices, candidates) {
    for (const code of candidates) {
        const exact = voices.find(v => v.lang.toLowerCase() === code.toLowerCase());
        if (exact) return exact;

        const prefix = code.split('-')[0].toLowerCase();
        const loose = voices.find(v => v.lang.toLowerCase().startsWith(prefix));
        if (loose) return loose;
    }

    return null;
}

export async function speak(text, candidates) {
    if (typeof speechSynthesis === 'undefined' || !text) {
        return false;
    }

    // Əvvəlki replika hələ oxunursa kəsilir: iki səs eyni anda danışmamalıdır.
    speechSynthesis.cancel();

    const voice = pickVoice(await voicesReady(), candidates || []);
    const utterance = new SpeechSynthesisUtterance(text);

    if (voice) {
        utterance.voice = voice;
        utterance.lang = voice.lang;
    }

    // Uşaq üçün bir az yavaş və bir az incə: pet böyük adam kimi danışmamalıdır.
    utterance.rate = 0.95;
    utterance.pitch = 1.25;

    return new Promise(resolve => {
        let settled = false;
        const finish = ok => {
            if (!settled) {
                settled = true;
                resolve(ok);
            }
        };

        utterance.onend = () => finish(true);

        // `onerror` səs bağlı olanda da gəlir — bu, xəta deyil, sadəcə səs yoxdur.
        utterance.onerror = () => finish(false);

        speechSynthesis.speak(utterance);
    });
}

export function stop() {
    if (typeof speechSynthesis !== 'undefined') {
        speechSynthesis.cancel();
    }
}
