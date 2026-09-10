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
