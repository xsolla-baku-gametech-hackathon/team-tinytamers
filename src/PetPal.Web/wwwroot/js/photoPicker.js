// Kəşf (Real Life Connect) ekranı üçün brauzer şəkil seçimi.
// Fayl HƏMİŞƏ data URL kimi qaytarılır — API prefiksi özü kəsir, ekran isə
// önizləməni birbaşa <img src> ilə göstərir.

// Şəkil serverə göndərilməmişdən əvvəl kiçildilir. Telefon kamerası 10–20 MB
// çıxarır; belə fayl həm yavaş gedir, həm də kolleksiyada geri yüklənəndə
// eyni yavaşlığı təkrarlayır. 1600 px kəşf şəkli üçün artıqlaması ilə kifayətdir.
const MAX_EDGE = 1600;
const JPEG_QUALITY = 0.85;

export function capture() {
    return new Promise((resolve) => {
        const input = document.createElement('input');
        input.type = 'file';
        input.accept = 'image/*';
        // `capture` atributu QƏSDƏN qoyulmur: o, seçimi yalnız kameraya
        // məhdudlaşdırır və qalereyadakı hazır şəkli göndərmək mümkün olmur.
        // Atributsuz mobil brauzer onsuz da "Kamera / Qalereya" seçimi verir.
        input.style.display = 'none';

        let settled = false;
        const finish = (value) => {
            if (settled) return;
            settled = true;
            input.remove();
            resolve(value);
        };

        input.addEventListener('cancel', () => finish(null));
        input.addEventListener('change', () => {
            const file = input.files && input.files[0];
            if (!file) {
                finish(null);
                return;
            }

            const reader = new FileReader();
            reader.onload = () => {
                const dataUrl = typeof reader.result === 'string' ? reader.result : null;
                if (!dataUrl) {
                    finish(null);
                    return;
                }

                // Kiçiltmə alınmasa (brauzer formatı tanımırsa — məsələn HEIC)
                // orijinal göndərilir: şəkilsiz qalmaqdansa böyük şəkil yaxşıdır.
                shrink(dataUrl).then((smaller) => finish(smaller || dataUrl), () => finish(dataUrl));
            };
            reader.onerror = () => finish(null);
            reader.readAsDataURL(file);
        });

        document.body.appendChild(input);
        input.click();
    });
}

function shrink(dataUrl) {
    return new Promise((resolve) => {
        const image = new Image();

        image.onerror = () => resolve(null);
        image.onload = () => {
            try {
                const scale = Math.min(1, MAX_EDGE / Math.max(image.width, image.height));

                // Onsuz da kiçik şəkli yenidən kodlamaq mənasızdır — həm vaxt
                // aparır, həm keyfiyyət itirir.
                if (scale >= 1) {
                    resolve(null);
                    return;
                }

                const canvas = document.createElement('canvas');
                canvas.width = Math.round(image.width * scale);
                canvas.height = Math.round(image.height * scale);

                const context = canvas.getContext('2d');
                if (!context) {
                    resolve(null);
                    return;
                }

                context.drawImage(image, 0, 0, canvas.width, canvas.height);
                resolve(canvas.toDataURL('image/jpeg', JPEG_QUALITY));
            } catch (e) {
                resolve(null);
            }
        };

        image.src = dataUrl;
    });
}
