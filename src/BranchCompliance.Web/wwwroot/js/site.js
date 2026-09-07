document.querySelectorAll('.demo-account').forEach(button => {
    button.addEventListener('click', () => {
        document.getElementById('Email').value = button.dataset.email;
        document.getElementById('Password').value = 'PortfolioDemo123!';
        document.getElementById('Password').focus();
    });
});
document.querySelectorAll('form[data-confirm]').forEach(form => {
    form.addEventListener('submit', event => {
        if (!window.confirm(form.dataset.confirm)) event.preventDefault();
    });
});

const bandRows = document.getElementById('rating-band-rows');
function numberBandRows() {
    bandRows?.querySelectorAll('.band-row').forEach((row, index) => {
        row.querySelectorAll('input').forEach(input => {
            const field = input.name.endsWith('.Label') ? 'Label' : 'Minimum';
            input.name = `Bands[${index}].${field}`;
            input.id = `Bands_${index}__${field}`;
            input.parentElement.querySelector('label').htmlFor = input.id;
        });
    });
}
bandRows?.addEventListener('click', event => {
    if (event.target.closest('.remove-band') && bandRows.children.length > 1) {
        event.target.closest('.band-row').remove();
        numberBandRows();
    }
});
document.getElementById('add-rating-band')?.addEventListener('click', () => {
    if (bandRows.children.length >= 10) return;
    const row = bandRows.firstElementChild.cloneNode(true);
    row.querySelectorAll('input').forEach(input => { input.value = input.name.endsWith('.Label') ? '' : '0.00'; });
    bandRows.appendChild(row);
    numberBandRows();
    row.querySelector('input').focus();
});
