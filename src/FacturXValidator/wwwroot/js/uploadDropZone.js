const registrations = new Map();

export function initializeUploadDropZone(dropZoneId, inputFileId) {
    const dropZone = document.getElementById(dropZoneId);
    const input = document.getElementById(inputFileId);

    if (!dropZone || !input) {
        return;
    }

    disposeUploadDropZone(dropZoneId);

    const preventDefault = (event) => {
        event.preventDefault();
        event.stopPropagation();
    };

    const showActiveState = (event) => {
        preventDefault(event);
        dropZone.classList.add("is-drag-over");
    };

    const clearActiveState = (event) => {
        preventDefault(event);
        dropZone.classList.remove("is-drag-over");
    };

    const handleDrop = (event) => {
        preventDefault(event);
        dropZone.classList.remove("is-drag-over");

        const files = event.dataTransfer?.files;
        if (!files || files.length === 0) {
            return;
        }

        const transfer = new DataTransfer();
        for (const file of files) {
            transfer.items.add(file);
        }

        input.files = transfer.files;
        input.dispatchEvent(new Event("change", { bubbles: true }));
    };

    dropZone.addEventListener("dragenter", showActiveState);
    dropZone.addEventListener("dragover", showActiveState);
    dropZone.addEventListener("dragleave", clearActiveState);
    dropZone.addEventListener("drop", handleDrop);

    registrations.set(dropZoneId, {
        dropZone,
        listeners: [
            ["dragenter", showActiveState],
            ["dragover", showActiveState],
            ["dragleave", clearActiveState],
            ["drop", handleDrop]
        ]
    });
}

export function disposeUploadDropZone(dropZoneId) {
    const registration = registrations.get(dropZoneId);
    if (!registration) {
        return;
    }

    for (const [eventName, listener] of registration.listeners) {
        registration.dropZone.removeEventListener(eventName, listener);
    }

    registration.dropZone.classList.remove("is-drag-over");
    registrations.delete(dropZoneId);
}
