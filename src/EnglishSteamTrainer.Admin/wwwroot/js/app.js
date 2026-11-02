// Warnung beim Schließen/Neuladen, solange Änderungen nicht gespeichert sind.
window.karteikarten = {
    unsaved: false,
    setUnsaved(value) { this.unsaved = value; }
};

window.addEventListener("beforeunload", event => {
    if (window.karteikarten.unsaved) {
        event.preventDefault();
        event.returnValue = "";
    }
});

// Firefox startet Drag & Drop nur, wenn beim dragstart Daten gesetzt werden.
document.addEventListener("dragstart", event => {
    if (event.dataTransfer && event.dataTransfer.types.length === 0) {
        event.dataTransfer.setData("text/plain", "");
    }
});
