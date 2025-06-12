window.markdownEditorUtils = {
    exportAsTxt: function (filename, text) {
        // Create a Blob with the text content
        const blob = new Blob([text], {type: 'text/plain'});

        // Create a temporary anchor element
        const a = document.createElement('a');
        a.href = URL.createObjectURL(blob);
        a.download = filename;

        // Trigger the download
        document.body.appendChild(a);
        a.click();

        // Clean up
        setTimeout(() => {
            document.body.removeChild(a);
            URL.revokeObjectURL(a.href);
        }, 100);
    },

    insertMarkdownAtCursor: function (elementId, prefix, suffix) {
        const editor = document.getElementById(elementId);
        if (!editor) {
            console.error('Editor element not found:', elementId);
            return editor.value;
        }

        const currentValue = editor.value;
        const start = editor.selectionStart;
        const end = editor.selectionEnd;
        const selectedText = currentValue.substring(start, end);

        let newText;
        let cursorPos;

        if (selectedText) {
            newText = currentValue.substring(0, start) +
                prefix +
                selectedText +
                suffix +
                currentValue.substring(end);
            cursorPos = start + prefix.length + selectedText.length + suffix.length;
        } else {
            newText = currentValue.substring(0, start) +
                prefix +
                suffix +
                currentValue.substring(start);
            cursorPos = start + prefix.length;
        }

        editor.value = newText;
        editor.focus();
        editor.setSelectionRange(cursorPos, cursorPos);

        const event = new Event('input', {bubbles: true, cancelable: true});
        editor.dispatchEvent(event);

        return newText;
    }
};