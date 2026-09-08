window.vfExportDownload = {
    trigger: function (url) {
        const a = document.createElement('a');
        a.href = url;
        a.style.display = 'none';
        document.body.appendChild(a);
        a.click();
        a.remove();
    }
};
