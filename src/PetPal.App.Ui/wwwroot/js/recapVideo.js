export async function attach(video, streamRef) {
    release(video);

    const bytes = await streamRef.arrayBuffer();
    const url = URL.createObjectURL(new Blob([bytes], { type: 'video/mp4' }));

    video.dataset.recapUrl = url;
    video.src = url;
}

export function release(video) {
    if (!video || !video.dataset || !video.dataset.recapUrl) {
        return;
    }

    const url = video.dataset.recapUrl;

    delete video.dataset.recapUrl;
    video.removeAttribute('src');
    video.load();
    URL.revokeObjectURL(url);
}

export function play(video) {
    if (!video) {
        return;
    }

    const started = video.play();

    if (started && typeof started.catch === 'function') {
        started.catch(() => { });
    }
}

export function enterFullscreen(element) {
    if (!element || document.fullscreenElement || typeof element.requestFullscreen !== 'function') {
        return;
    }

    element.requestFullscreen().catch(() => { });
}

export function exitFullscreen() {
    if (document.fullscreenElement && typeof document.exitFullscreen === 'function') {
        document.exitFullscreen().catch(() => { });
    }
}

export function followCaptions(video, list) {
    if (!video || !list) {
        return;
    }

    const update = () => {
        const items = Array.from(list.querySelectorAll('[data-start]'));
        const time = video.currentTime;

        items.forEach((item, index) => {
            const start = Number(item.dataset.start);
            const end = Number(item.dataset.end);
            const last = index === items.length - 1;

            item.toggleAttribute('data-active', time >= start && (time < end || last));
        });
    };

    video.addEventListener('timeupdate', update);
    video.addEventListener('seeked', update);
    update();
}
