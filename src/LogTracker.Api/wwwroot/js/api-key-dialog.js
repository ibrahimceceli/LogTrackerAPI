// Dialog that asks for the API key after a 401 response.
const dialog = document.getElementById('api-key-dialog');
const form = document.getElementById('api-key-form');
const input = document.getElementById('api-key-input');
const rejectedMessage = document.getElementById('api-key-error');
const cancelButton = dialog.querySelector('[data-cancel]');

let pending = null;

/**
 * Opens the dialog and resolves to the entered key, or to null when the user cancels.
 * All sections refresh in parallel and may all get a 401 at once; they share one dialog.
 */
export function promptForApiKey({ rejected = false } = {}) {
    if (pending) {
        return pending;
    }

    pending = new Promise((resolve) => {
        const finish = (value) => {
            form.removeEventListener('submit', onSubmit);
            dialog.removeEventListener('close', onClose);
            cancelButton.removeEventListener('click', onCancel);
            pending = null;
            resolve(value);
        };
        const onSubmit = (event) => {
            event.preventDefault();
            const key = input.value.trim();
            if (!key) {
                input.setAttribute('aria-invalid', 'true');
                input.focus();
                return;
            }
            finish(key);
            dialog.close();
        };
        // Covers the Cancel button and the Escape key. The close event is delivered asynchronously, so the
        // close event of the previous prompt can arrive after a 401 retry has already re-opened the dialog;
        // it must not cancel the new prompt.
        const onClose = () => {
            if (!dialog.open) finish(null);
        };
        const onCancel = () => dialog.close();

        form.addEventListener('submit', onSubmit);
        dialog.addEventListener('close', onClose);
        cancelButton.addEventListener('click', onCancel);
    });

    rejectedMessage.hidden = !rejected;
    input.value = '';
    input.removeAttribute('aria-invalid');
    if (rejected) {
        input.setAttribute('aria-describedby', 'api-key-error');
    } else {
        input.removeAttribute('aria-describedby');
    }
    dialog.showModal();
    input.focus();
    return pending;
}
