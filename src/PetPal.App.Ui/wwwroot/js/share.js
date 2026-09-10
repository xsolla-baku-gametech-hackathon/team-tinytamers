// Nailiyyət kartı və brauzer paylaşımı.
//
// Kart CANVAS-da çəkilir, çünki paylaşılan şey ŞƏKİL olmalıdır: valideyn onu
// mesajlaşma proqramına atır və orada HTML deyil, yalnız şəkil görünür.
//
// Uşağın əsl adı kartda YOXDUR — yalnız pet adı. Bu, arenanın qaydası ilə
// eynidir: tanımadığı adam uşağın adını görməməlidir, paylaşılan şəkil isə
// harasa çata bilər.
window.petpalShare = (function () {
    const W = 1080;
    const H = 1080;

    // Rənglər theme.css-dəki tokenlərin eynisidir. Canvas CSS dəyişəni oxuya
    // bilmir, ona görə burada təkrarlanır — dəyişəndə hər iki yer yenilənməlidir.
    const INK = '#241E38';
    const INK_SOFT = '#5C5470';
    const CARD = '#FBF7EF';
    const PRIMARY = '#6F5AD8';
    const SPARK = '#E89A3C';
    const SPARK_DEEP = '#93540E';
    const LIFE_DEEP = '#156F4F';
    const BORDER = '#E4DCCE';

    function roundRect(ctx, x, y, w, h, r) {
        ctx.beginPath();
        ctx.moveTo(x + r, y);
        ctx.arcTo(x + w, y, x + w, y + h, r);
        ctx.arcTo(x + w, y + h, x, y + h, r);
        ctx.arcTo(x, y + h, x, y, r);
        ctx.arcTo(x, y, x + w, y, r);
        ctx.closePath();
    }

    function centre(ctx, text, y, font, colour) {
        ctx.font = font;
        ctx.fillStyle = colour;
        ctx.textAlign = 'center';
        ctx.fillText(text, W / 2, y);
    }

    /**
     * Kartı çəkir və base64 PNG qaytarır ("data:" prefiksi olmadan).
     * data: { petName, avatar, age, stars, streak, badges, caption, appName, statLabels }
     */
    function drawCard(data) {
        const canvas = document.createElement('canvas');
        canvas.width = W;
        canvas.height = H;
        const ctx = canvas.getContext('2d');

        // Fon
        ctx.fillStyle = '#F0EBE0';
        ctx.fillRect(0, 0, W, H);

        // Kart səthi
        ctx.fillStyle = CARD;
        roundRect(ctx, 60, 60, W - 120, H - 120, 56);
        ctx.fill();
        ctx.strokeStyle = BORDER;
        ctx.lineWidth = 4;
        ctx.stroke();

        // Üst zolaq — brend rəngi
        ctx.fillStyle = PRIMARY;
        roundRect(ctx, 60, 60, W - 120, 20, 10);
        ctx.fill();

        // Avatar (emoji) — app-in özündə də pet emoji ilə göstərilir
        centre(ctx, data.avatar || '🦊', 330, '190px system-ui, "Segoe UI Emoji", sans-serif', INK);

        centre(ctx, data.petName || '', 440, '700 76px system-ui, "Segoe UI", sans-serif', INK);
        centre(ctx, data.caption || '', 500, '400 34px system-ui, "Segoe UI", sans-serif', INK_SOFT);

        // Üç göstərici: yaş · ulduz · ardıcıllıq
        const labels = data.statLabels || {};
        const stats = [
            { value: String(data.age ?? 1), label: labels.age || 'yaş', colour: PRIMARY },
            { value: String(data.stars ?? 0), label: labels.stars || 'ulduz', colour: SPARK_DEEP },
            { value: String(data.streak ?? 0), label: labels.streak || 'gün', colour: LIFE_DEEP }
        ];

        const boxW = 260;
        const gap = 30;
        const totalW = stats.length * boxW + (stats.length - 1) * gap;
        let x = (W - totalW) / 2;

        stats.forEach(function (stat) {
            ctx.fillStyle = '#F6F1E6';
            roundRect(ctx, x, 580, boxW, 200, 32);
            ctx.fill();

            ctx.textAlign = 'center';
            ctx.font = '800 84px system-ui, "Segoe UI", sans-serif';
            ctx.fillStyle = stat.colour;
            ctx.fillText(stat.value, x + boxW / 2, 680);

            ctx.font = '600 30px system-ui, "Segoe UI", sans-serif';
            ctx.fillStyle = INK_SOFT;
            ctx.fillText(stat.label, x + boxW / 2, 730);

            x += boxW + gap;
        });

        if (data.badges) {
            centre(ctx, data.badges, 860, '600 38px system-ui, "Segoe UI", sans-serif', SPARK_DEEP);
        }

        // Alt yazı — app adı
        ctx.fillStyle = SPARK;
        ctx.fillRect(W / 2 - 90, 920, 180, 5);
        centre(ctx, data.appName || 'AI Pets for Kids', 985, '700 40px system-ui, "Segoe UI", sans-serif', INK);

        return canvas.toDataURL('image/png').split(',')[1];
    }

    function canShare() {
        return typeof navigator !== 'undefined' && typeof navigator.share === 'function';
    }

    async function shareText(title, text) {
        if (canShare()) {
            try {
                await navigator.share({ title: title, text: text });
                return true;
            } catch (e) {
                // İstifadəçi imtina etdi və ya vərəq açılmadı — panoya düşürük.
            }
        }

        return copyToClipboard(text);
    }

    function base64ToBlob(base64, type) {
        const binary = atob(base64);
        const bytes = new Uint8Array(binary.length);
        for (let i = 0; i < binary.length; i++) {
            bytes[i] = binary.charCodeAt(i);
        }
        return new Blob([bytes], { type: type });
    }

    async function shareImage(title, fileName, base64Png) {
        const blob = base64ToBlob(base64Png, 'image/png');
        const file = new File([blob], fileName, { type: 'image/png' });

        if (canShare() && navigator.canShare && navigator.canShare({ files: [file] })) {
            try {
                await navigator.share({ title: title, files: [file] });
                return true;
            } catch (e) {
                // İmtina — yükləməyə düşürük.
            }
        }

        // Paylaşma yoxdursa şəkil sadəcə yüklənir: valideyn onu özü göndərir.
        const url = URL.createObjectURL(blob);
        const link = document.createElement('a');
        link.href = url;
        link.download = fileName;
        document.body.appendChild(link);
        link.click();
        document.body.removeChild(link);
        URL.revokeObjectURL(url);
        return true;
    }

    async function copyToClipboard(text) {
        try {
            await navigator.clipboard.writeText(text);
            return true;
        } catch (e) {
            return false;
        }
    }

    /** Ünvandakı ?friend=KOD parametrini oxuyur və ünvanı təmizləyir. */
    function readFriendCode() {
        try {
            const params = new URLSearchParams(window.location.search);
            const code = params.get('friend');

            if (!code) {
                return null;
            }

            // Kod bir dəfə işlədilir: səhifə yenilənəndə forma təkrar dolmasın.
            params.delete('friend');
            const rest = params.toString();
            const clean = window.location.pathname + (rest ? '?' + rest : '') + window.location.hash;
            window.history.replaceState({}, '', clean);

            return code.toUpperCase().replace(/[^A-Z0-9]/g, '').slice(0, 6);
        } catch (e) {
            return null;
        }
    }

    return {
        drawCard: drawCard,
        canShare: canShare,
        shareText: shareText,
        shareImage: shareImage,
        copyToClipboard: copyToClipboard,
        readFriendCode: readFriendCode
    };
})();
